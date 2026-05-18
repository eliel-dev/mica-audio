package com.micaaudio.android.data.audio

import android.app.*
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.media.audiofx.Visualizer
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import com.micaaudio.android.data.settings.AppSettings
import com.micaaudio.android.data.websocket.VisualStreamSocket
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.*
import javax.inject.Inject
import kotlin.math.*

// DOCS: docs/wiki/modules/visual-win2d.md#audiomotion-clone
// DOCS: docs/wiki/reference/ws-protocol-v2.md#mensagem-tipo-1---bins128
/**
 * Foreground service that captures the system audio output mix via [Visualizer] (session 0)
 * and streams the processed spectrum bins to the HUB75 device.
 *
 * Unlike the previous MediaProjection approach, this requires only RECORD_AUDIO — no
 * screen-capture permission dialog is shown to the user.  Session ID 0 captures the
 * global output mix, so any app playing audio (Spotify, YouTube, etc.) is included.
 *
 * Processing chain:
 *   Android Visualizer (FFT) → magnitude extraction → FFT temporal smoothing
 *   → [LogBandMapper] with Bark/Mel/Log scale + A/B/C weighting → [EnvelopeSmoother]
 *   → [SpectrumState] (UI) and optionally → HUB75 via [VisualStreamSocket]
 */
@AndroidEntryPoint
class AudioCaptureService : Service() {

    @Inject lateinit var visualStreamSocket: VisualStreamSocket
    @Inject lateinit var spectrumState: SpectrumState
    @Inject lateinit var appSettings: AppSettings

    private var visualizer: Visualizer? = null
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)

    // ── Settings live-collected from DataStore ────────────────────────────────
    // Gain/Boost are no longer used (replaced by Windows-style dB normalization).
    // LinearBoost is fixed at 1.3f to match Windows VisualizerRuntimeDefaults.DefaultLinearBoost.
    @Volatile private var currentRise = 0.8f
    @Volatile private var currentFall = 0.3f
    @Volatile private var currentFftSmoothing = 0.09f
    @Volatile private var currentMaxFreq = 1000f
    @Volatile private var currentMinFreq = 20f
    @Volatile private var currentBarCount = 71
    @Volatile private var currentFreqScale = FrequencyScale.Bark
    @Volatile private var currentWeightingFilter = WeightingFilter.B


    /**
     * Per-FFT-bin amplitude multipliers for the active weighting filter.
     * Null = must be rebuilt on next FFT frame. Invalidated when filter or fftSize changes.
     */
    @Volatile private var weightingMultipliers: FloatArray? = null

    private var smoother: EnvelopeSmoother? = null
    private var frameLoopJob: Job? = null
    private val binsLock = Any()

    // Display bins — resized to currentBarCount on demand (protected by binsLock)
    private var targetBins = FloatArray(71)

    // Frame loop working buffers — resized to match targetBins on demand (frame loop thread only)
    private var frameInputBins = FloatArray(71)
    private var smoothedBins = FloatArray(71)

    // HUB75 streaming — always exactly 128 bytes (protocol requirement)
    private val byteBins = ByteArray(128)

    // Raw FFT magnitude buffers (sized by Visualizer capture size)
    private var magnitudes = FloatArray(0)
    private var smoothedMagnitudes = FloatArray(0)

    companion object {
        private const val NOTIFICATION_ID = 101
        private const val CHANNEL_ID = "audio_capture"

        // Windows VisualizerRuntimeDefaults.DefaultLinearBoost — hardcoded, never user-tunable.
        private const val WINDOWS_LINEAR_BOOST = 1.3f
        // Calibrated dB thresholds for Android Visualizer FFT output range.
        // (Windows uses -85..-25 dB for floating-point PCM; Android Visualizer scales
        // its FFT output to roughly [0..1] amplitude, so a shallower dB range works better.)
        private const val ANDROID_DB_FLOOR = -60f
        private const val ANDROID_DB_CEILING = -10f

        var isRunning = false
            private set

        /** Non-null → stream bins to that device. Null → local visualisation only. */
        @Volatile var streamingDeviceId: String? = null
            private set

        val isStreaming: Boolean get() = !streamingDeviceId.isNullOrEmpty()

        /**
         * Start the service. Pass [deviceId] to enable HUB75 streaming; pass null
         * for local-only audio visualisation (no streaming).
         */
        fun start(context: Context, deviceId: String? = null) {
            streamingDeviceId = deviceId
            val intent = Intent(context, AudioCaptureService::class.java)
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }

        /** Hot-swap the streaming target without restarting the service. */
        fun setStreamingDevice(deviceId: String?) {
            streamingDeviceId = deviceId
        }

        fun stop(context: Context) {
            streamingDeviceId = null
            context.stopService(Intent(context, AudioCaptureService::class.java))
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        createNotificationChannel()
        val contentText = if (isStreaming)
            "Transmitindo espectro para a matriz…"
        else
            "Visualizando áudio localmente…"
        val notification = NotificationCompat.Builder(this, CHANNEL_ID)
            .setContentTitle("Mica Audio")
            .setContentText(contentText)
            .setSmallIcon(android.R.drawable.ic_media_play)
            .setPriority(NotificationCompat.PRIORITY_LOW)
            .build()

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(
                NOTIFICATION_ID, notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE,
            )
        } else {
            startForeground(NOTIFICATION_ID, notification)
        }

        startCapture()
        return START_NOT_STICKY
    }

    private fun startCapture() {
        // Live-collect all settings changes while the service runs.
        // Gain/Boost from AppSettings are intentionally ignored — the new pipeline uses
        // a hardcoded LinearBoost (matching Windows) + dB normalization.
        scope.launch { appSettings.visualizerRise.collect    { currentRise    = it } }
        scope.launch { appSettings.visualizerFall.collect    { currentFall    = it } }
        scope.launch { appSettings.visualizerFftSmoothing.collect { currentFftSmoothing = it } }
        scope.launch { appSettings.visualizerMaxFreq.collect { currentMaxFreq = it } }
        scope.launch { appSettings.visualizerMinFreq.collect { currentMinFreq = it } }

        scope.launch {
            appSettings.visualizerBarCount.collect { count ->
                val clamped = count.coerceIn(10, 256)
                currentBarCount = clamped
                synchronized(binsLock) {
                    if (targetBins.size != clamped) targetBins = FloatArray(clamped)
                }
            }
        }

        scope.launch {
            appSettings.visualizerFreqScale.collect { scale ->
                currentFreqScale = scale
            }
        }

        scope.launch {
            appSettings.visualizerWeighting.collect { filter ->
                currentWeightingFilter = filter
                weightingMultipliers = null   // force rebuild on next FFT frame
            }
        }

        smoother = EnvelopeSmoother(rise = currentRise, fall = currentFall)
        visualStreamSocket.start()
        frameLoopJob = scope.launch { runFrameLoop() }
        isRunning = true

        try {
            val captureSize = Visualizer.getCaptureSizeRange()[1]  // max size, typically 1024 bytes

            @Suppress("DEPRECATION")
            visualizer = Visualizer(0 /* global output mix */).apply {
                this.captureSize = captureSize
                setDataCaptureListener(
                    object : Visualizer.OnDataCaptureListener {
                        override fun onWaveFormDataCapture(v: Visualizer, waveform: ByteArray, samplingRate: Int) = Unit
                        override fun onFftDataCapture(v: Visualizer, fft: ByteArray, samplingRate: Int) {
                            processVisualizerFft(fft, samplingRate)
                        }
                    },
                    Visualizer.getMaxCaptureRate(),  // max rate (≤ 20 000 mHz = 20 Hz)
                    /* waveform = */ false,
                    /* fft     = */ true,
                )
                enabled = true
            }
        } catch (e: Exception) {
            stopSelf()
        }
    }

    /**
     * Called on the Visualizer binder thread; must be fast and non-blocking.
     *
     * FFT layout (Android Visualizer):
     *   fft[0]          = DC  component (real)
     *   fft[1]          = Nyquist component (real)
     *   fft[2k], fft[2k+1] = real / imaginary of bin k  (k = 1 … n/2-1)
     *
     * Bytes are signed (-128..127); divide by 128f to normalise to ±1.
     */
    private fun processVisualizerFft(fft: ByteArray, samplingRateMilliHz: Int) {
        val n = fft.size                       // = captureSize (e.g. 1024)
        if (magnitudes.size != n / 2) {
            magnitudes = FloatArray(n / 2)
            smoothedMagnitudes = FloatArray(n / 2)
            weightingMultipliers = null  // fftSize changed → rebuild multipliers
        }

        // DC bin
        magnitudes[0] = abs(fft[0].toFloat() / 128f)
        // Frequency bins 1 … n/2 - 1
        for (i in 1 until n / 2) {
            val re = fft[2 * i].toFloat() / 128f
            val im = fft[2 * i + 1].toFloat() / 128f
            magnitudes[i] = sqrt(re * re + im * im)
        }

        // FFT temporal smoothing (IIR low-pass)
        val fftSmoothing = currentFftSmoothing.coerceIn(0f, 0.98f)
        val liveMix = 1f - fftSmoothing
        for (i in magnitudes.indices) {
            smoothedMagnitudes[i] = smoothedMagnitudes[i] * fftSmoothing + magnitudes[i] * liveMix
        }

        // samplingRate comes in mHz (millihertz); convert to Hz
        val sampleRate = (samplingRateMilliHz / 1000).coerceAtLeast(44100)

        // Lazily build weighting multipliers for the current filter + fftSize
        val curFilter = currentWeightingFilter
        if (weightingMultipliers == null) {
            weightingMultipliers = LogBandMapper.buildWeightingMultipliers(n, sampleRate, curFilter)
        }

        // Map FFT magnitudes → display bands using the configured scale + weighting
        synchronized(binsLock) {
            val count = currentBarCount
            if (targetBins.size != count) targetBins = FloatArray(count)

            LogBandMapper.calculateBinsInto(
                magnitudes = smoothedMagnitudes,
                destination = targetBins,
                sampleRate = sampleRate,
                fftSize = n,
                minHz = currentMinFreq,
                maxHz = currentMaxFreq,
                linearBoost = WINDOWS_LINEAR_BOOST,
                dbFloor = ANDROID_DB_FLOOR,
                dbCeiling = ANDROID_DB_CEILING,
                frequencyScale = currentFreqScale,
                weightingMultipliers = weightingMultipliers,
            )
        }
    }

    private suspend fun runFrameLoop() {
        val frameDurationMs = 1_000L / VisualFrameRateLimiter.TargetFramesPerSecond

        while (scope.isActive) {
            val frameStarted = System.nanoTime()

            // Copy display bins (may resize working buffers if barCount changed)
            synchronized(binsLock) {
                val size = targetBins.size
                if (frameInputBins.size != size) {
                    frameInputBins = FloatArray(size)
                    smoothedBins = FloatArray(size)
                    smoother = null  // reset smoother state when bin count changes
                }
                targetBins.copyInto(frameInputBins)
            }

            // Apply envelope smoother (rise/fall)
            val s = smoother ?: EnvelopeSmoother(currentRise, currentFall).also { smoother = it }
            s.rise = currentRise
            s.fall = currentFall
            s.motionDamping = 0.9f
            s.process(frameInputBins, smoothedBins)

            // Always update SpectrumState so the local UI canvas refreshes
            spectrumState.updateBins(smoothedBins.copyOf())

            // Stream to HUB75 device if a target is configured.
            // byteBins is always 128 bytes (protocol requirement); zero-pad if barCount < 128.
            val targetDevice = streamingDeviceId
            if (!targetDevice.isNullOrEmpty()) {
                byteBins.fill(0)
                var sum = 0f
                val displayCount = smoothedBins.size
                val streamCount = displayCount.coerceAtMost(128)
                for (i in 0 until streamCount) {
                    val value = smoothedBins[i].coerceIn(0f, 1f)
                    sum += value
                    byteBins[i] = (value * 255).roundToInt().coerceIn(0, 255).toByte()
                }
                visualStreamSocket.sendBins128(
                    deviceId = targetDevice,
                    bins = byteBins,
                    level = if (streamCount > 0) ((sum / streamCount) * 255).roundToInt().coerceIn(0, 255) else 0,
                    brightness = 255,
                )
            }

            val elapsedMs = ((System.nanoTime() - frameStarted) / 1_000_000L).coerceAtLeast(0L)
            delay((frameDurationMs - elapsedMs).coerceAtLeast(1L))
        }
    }

    override fun onDestroy() {
        // Stop Visualizer first so no more callbacks fire
        visualizer?.setDataCaptureListener(null, 0, false, false)
        visualizer?.enabled = false
        visualizer?.release()
        visualizer = null
        // Then cancel coroutines and stop the socket
        scope.cancel()
        visualStreamSocket.stop()
        isRunning = false
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(
                CHANNEL_ID,
                "Mica Audio Capture",
                NotificationManager.IMPORTANCE_LOW,
            )
            getSystemService(NotificationManager::class.java)?.createNotificationChannel(channel)
        }
    }
}
