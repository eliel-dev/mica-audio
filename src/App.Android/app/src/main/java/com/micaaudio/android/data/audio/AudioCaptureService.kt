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
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.*
import javax.inject.Inject
import kotlin.math.roundToInt

// DOCS: docs/wiki/modules/visual-win2d.md#audiomotion-clone
// DOCS: docs/wiki/reference/ws-protocol-v2.md#mensagem-tipo-1---bins128

@AndroidEntryPoint
class AudioCaptureService : Service() {

    @Inject
    lateinit var visualStreamSocket: VisualStreamSocket

    @Inject
    lateinit var spectrumState: SpectrumState

    private var mediaProjection: MediaProjection? = null
    private var audioRecord: AudioRecord? = null
    private var captureJob: Job? = null
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    private var targetDeviceId: String? = null

    companion object {
        private const val NOTIFICATION_ID = 101
        private const val CHANNEL_ID = "audio_capture"
        private const val SAMPLE_RATE = AudioMotionCloneAnalyzer.SampleRate
        private const val BUFFER_SIZE = AudioMotionCloneAnalyzer.FftSize * 4
        private const val READ_SAMPLE_COUNT = AudioMotionCloneAnalyzer.HopSize

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
                val minimumBufferSize = AudioRecord.getMinBufferSize(
                    SAMPLE_RATE,
                    AudioFormat.CHANNEL_IN_MONO,
                    AudioFormat.ENCODING_PCM_16BIT,
                ).coerceAtLeast(BUFFER_SIZE)

                audioRecord = AudioRecord.Builder()
                    .setAudioPlaybackCaptureConfig(config)
                    .setAudioFormat(format)
                    .setBufferSizeInBytes(minimumBufferSize)
                    .build()

                if (audioRecord?.state != AudioRecord.STATE_INITIALIZED) {
                    stopSelf()
                    return
                }

                audioRecord?.startRecording()
                isRunning = true
                visualStreamSocket.start()

                captureJob = scope.launch {
                    val analyzer = AudioMotionCloneAnalyzer()
                    val frameLimiter = VisualFrameRateLimiter()
                    val buffer = ShortArray(READ_SAMPLE_COUNT)
                    val byteBins = ByteArray(128)

                    while (isActive) {
                        val read = audioRecord?.read(buffer, 0, buffer.size) ?: -1
                        if (read > 0) {
                            analyzer.process(buffer, read)?.let { frame ->
                                if (!frameLimiter.shouldEmit(System.nanoTime())) {
                                    return@let
                                }

                                spectrumState.updateBins(frame.bins128)

                                val level = (frame.level * 255f)
                                    .roundToInt()
                                    .coerceIn(0, 255)

                                for (index in byteBins.indices) {
                                    byteBins[index] = (frame.bins128[index].coerceIn(0f, 1f) * 255f)
                                        .roundToInt()
                                        .coerceIn(0, 255)
                                        .toByte()
                                }

                                targetDeviceId?.let { deviceId ->
                                    visualStreamSocket.sendBins128(
                                        deviceId = deviceId,
                                        bins = byteBins,
                                        level = level,
                                        brightness = 255,
                                    )
                                }
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
