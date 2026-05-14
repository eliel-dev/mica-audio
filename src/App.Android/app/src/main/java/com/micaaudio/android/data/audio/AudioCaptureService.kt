package com.micaaudio.android.data.audio

import android.app.*
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.media.*
import android.media.projection.MediaProjection
import android.media.projection.MediaProjectionManager
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import com.micaaudio.android.data.websocket.VisualStreamSocket
import com.micaaudio.android.data.settings.AppSettings
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.*
import javax.inject.Inject
import kotlin.math.*

@AndroidEntryPoint
class AudioCaptureService : Service() {

    @Inject
    lateinit var visualStreamSocket: VisualStreamSocket

    @Inject
    lateinit var spectrumState: SpectrumState

    @Inject
    lateinit var appSettings: AppSettings

    private var mediaProjection: MediaProjection? = null
    private var audioRecord: AudioRecord? = null
    private var captureJob: Job? = null
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    private var targetDeviceId: String? = null

    companion object {
        private const val NOTIFICATION_ID = 101
        private const val CHANNEL_ID = "audio_capture"
        private const val SAMPLE_RATE = 44100
        private const val BUFFER_SIZE = 1024 * 4

        var isRunning = false
            private set

        fun start(context: Context, resultCode: Int, data: Intent, deviceId: String) {
            val intent = Intent(context, AudioCaptureService::class.java).apply {
                putExtra("resultCode", resultCode)
                putExtra("data", data)
                putExtra("deviceId", deviceId)
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                context.startForegroundService(intent)
            } else {
                context.startService(intent)
            }
        }

        fun stop(context: Context) {
            context.stopService(Intent(context, AudioCaptureService::class.java))
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        val resultCode = intent?.getIntExtra("resultCode", Activity.RESULT_CANCELED) ?: Activity.RESULT_CANCELED
        val data = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
            intent?.getParcelableExtra("data", Intent::class.java)
        } else {
            @Suppress("DEPRECATION")
            intent?.getParcelableExtra("data")
        }
        targetDeviceId = intent?.getStringExtra("deviceId")

        if (resultCode == Activity.RESULT_OK && data != null && targetDeviceId != null) {
            createNotificationChannel()
            val notification = NotificationCompat.Builder(this, CHANNEL_ID)
                .setContentTitle("Mica Audio")
                .setContentText("Transmitindo áudio para a matriz...")
                .setSmallIcon(android.R.drawable.ic_media_play)
                .setPriority(NotificationCompat.PRIORITY_LOW)
                .build()

            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
                startForeground(NOTIFICATION_ID, notification, ServiceInfo.FOREGROUND_SERVICE_TYPE_MEDIA_PROJECTION)
            } else {
                startForeground(NOTIFICATION_ID, notification)
            }

            startCapture(resultCode, data)
        } else {
            stopSelf()
        }

        return START_NOT_STICKY
    }

    private fun startCapture(resultCode: Int, data: Intent) {
        val mpManager = getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
        mediaProjection = mpManager.getMediaProjection(resultCode, data)

        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            val config = AudioPlaybackCaptureConfiguration.Builder(mediaProjection!!)
                .addMatchingUsage(AudioAttributes.USAGE_MEDIA)
                .addMatchingUsage(AudioAttributes.USAGE_GAME)
                .addMatchingUsage(AudioAttributes.USAGE_UNKNOWN)
                .build()

            val format = AudioFormat.Builder()
                .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                .setSampleRate(SAMPLE_RATE)
                .setChannelMask(AudioFormat.CHANNEL_IN_MONO)
                .build()

            try {
                audioRecord = AudioRecord.Builder()
                    .setAudioPlaybackCaptureConfig(config)
                    .setAudioFormat(format)
                    .setBufferSizeInBytes(BUFFER_SIZE)
                    .build()

                if (audioRecord?.state != AudioRecord.STATE_INITIALIZED) {
                    stopSelf()
                    return
                }

                audioRecord?.startRecording()
                isRunning = true
                visualStreamSocket.start()

                captureJob = scope.launch {
                    val buffer = ShortArray(1024)
                    val fft = FloatArray(1024 * 2)

                    var currentRise = 0.8f
                    var currentFall = 0.3f
                    var currentGain = 2.0f
                    var currentBoost = 1.5f

                    launch { appSettings.visualizerRise.collect { currentRise = it } }
                    launch { appSettings.visualizerFall.collect { currentFall = it } }
                    launch { appSettings.visualizerGain.collect { currentGain = it } }
                    launch { appSettings.visualizerBoost.collect { currentBoost = it } }

                    val smoother = EnvelopeSmoother(rise = currentRise, fall = currentFall)
                    val smoothedBins = FloatArray(128)

                    while (isActive) {
                        val read = audioRecord?.read(buffer, 0, buffer.size) ?: -1
                        if (read > 0) {
                            val fftSize = 1024
                            // 1. Hann Window & Padding
                            for (i in 0 until fftSize) {
                                if (i < read) {
                                    val window = 0.5f * (1f - cos(2f * PI.toFloat() * i / (fftSize - 1)))
                                    fft[i * 2] = buffer[i].toFloat() / 32768f * window
                                } else {
                                    fft[i * 2] = 0f
                                }
                                fft[i * 2 + 1] = 0f
                            }

                            // 2. Perform FFT
                            performFft(fft, fftSize)

                            // 3. Magnitudes
                            val magnitudes = FloatArray(fftSize / 2)
                            for (i in 0 until fftSize / 2) {
                                magnitudes[i] = sqrt(fft[i * 2] * fft[i * 2] + fft[i * 2 + 1] * fft[i * 2 + 1])
                            }

                            // 4. Log Mapping (Functional Parity)
                            val rawBins = LogBandMapper.calculateBins(
                                magnitudes = magnitudes,
                                sampleRate = SAMPLE_RATE,
                                fftSize = fftSize,
                                gain = currentGain,
                                linearBoost = currentBoost
                            )

                            // 5. Smoothing
                            smoother.rise = currentRise
                            smoother.fall = currentFall
                            smoother.process(rawBins, smoothedBins)

                            // 6. UI Update
                            spectrumState.updateBins(smoothedBins.copyOf())

                            // 7. Send to server
                            val avgLevel = (smoothedBins.average() * 255).toInt().coerceIn(0, 255)
                            val byteBins = ByteArray(128) { (smoothedBins[it] * 255).toInt().toByte() }

                            targetDeviceId?.let { deviceId ->
                                visualStreamSocket.sendBins128(
                                    deviceId = deviceId,
                                    bins = byteBins,
                                    level = avgLevel,
                                    brightness = 255
                                )
                            }
                        } else if (read == AudioRecord.ERROR_INVALID_OPERATION || read == AudioRecord.ERROR_BAD_VALUE) {
                            break
                        }
                    }
                }
            } catch (e: Exception) {
                stopSelf()
            }
        }
    }

    private fun performFft(data: FloatArray, n: Int) {
        var j = 0
        for (i in 0 until n) {
            if (i < j) {
                val tempR = data[i * 2]
                val tempI = data[i * 2 + 1]
                data[i * 2] = data[j * 2]
                data[i * 2 + 1] = data[j * 2 + 1]
                data[j * 2] = tempR
                data[j * 2 + 1] = tempI
            }
            var m = n shr 1
            while (m >= 1 && j >= m) {
                j -= m
                m = m shr 1
            }
            j += m
        }

        var m = 2
        while (m <= n) {
            val theta = -2f * PI.toFloat() / m
            val wR = cos(theta)
            val wI = sin(theta)
            var i = 0
            while (i < n) {
                var wr = 1f
                var wi = 0f
                for (k in 0 until m / 2) {
                    val r = data[(i + k + m / 2) * 2]
                    val im = data[(i + k + m / 2) * 2 + 1]
                    val tr = wr * r - wi * im
                    val ti = wr * im + wi * r
                    data[(i + k + m / 2) * 2] = data[(i + k) * 2] - tr
                    data[(i + k + m / 2) * 2 + 1] = data[(i + k) * 2 + 1] - ti
                    data[(i + k) * 2] += tr
                    data[(i + k) * 2 + 1] += ti
                    val nextWr = wr * wR - wi * wI
                    wi = wr * wI + wi * wR
                    wr = nextWr
                }
                i += m
            }
            m = m shl 1
        }
    }

    override fun onDestroy() {
        captureJob?.cancel()
        audioRecord?.stop()
        audioRecord?.release()
        mediaProjection?.stop()
        visualStreamSocket.stop()
        isRunning = false
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val serviceChannel = NotificationChannel(
                CHANNEL_ID,
                "Mica Audio Capture",
                NotificationManager.IMPORTANCE_LOW
            )
            val manager = getSystemService(NotificationManager::class.java)
            manager.createNotificationChannel(serviceChannel)
        }
    }
}
