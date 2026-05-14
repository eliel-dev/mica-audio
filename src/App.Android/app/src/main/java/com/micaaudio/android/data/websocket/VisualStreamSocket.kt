package com.micaaudio.android.data.websocket

import com.micaaudio.android.data.settings.AppSettings
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import okio.ByteString.Companion.toByteString
import javax.inject.Inject
import javax.inject.Singleton

// DOCS: docs/wiki/reference/ws-protocol-v2.md#mensagem-tipo-1---bins128
/**
 * WebSocket client for /ws/v1/admin/frames.
 * Sends binary visual frames to the server using the Admin Frame Envelope.
 */
@Singleton
class VisualStreamSocket @Inject constructor(
    private val okHttpClient: OkHttpClient,
    private val appSettings: AppSettings,
) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    private val _connected = MutableStateFlow(false)
    val connected: StateFlow<Boolean> = _connected.asStateFlow()

    private var connectJob: Job? = null
    private var currentSocket: WebSocket? = null
    private var sequence: Int = 0

    fun start() {
        if (connectJob?.isActive == true) return
        connectJob = scope.launch { connectLoop() }
    }

    fun stop() {
        connectJob?.cancel()
        connectJob = null
        currentSocket?.close(1000, "Client closing")
        currentSocket = null
        _connected.value = false
    }

    fun sendBins128(deviceId: String, bins: ByteArray, level: Int, brightness: Int) {
        val socket = currentSocket ?: return
        if (!_connected.value) return
        if (bins.size != 128) return
        if (socket.queueSize() > MAX_VISUAL_QUEUE_BYTES) return

        val envelope = try {
            VisualStreamFrameEncoder.createTargetedBins128Envelope(
                deviceId = deviceId,
                bins = bins,
                level = level,
                brightness = brightness,
                sequence = sequence++,
            )
        } catch (_: IllegalArgumentException) {
            return
        }

        socket.send(envelope.toByteString())
    }

    private companion object {
        private const val MAX_VISUAL_QUEUE_BYTES = 145L * 4L
    }

    private suspend fun connectLoop() {
        var backoffMs = 1_000L
        val maxBackoff = 16_000L

        while (scope.isActive) {
            try {
                val serverUrl = appSettings.serverUrl.first()
                val adminToken = appSettings.adminToken.first()
                val wsUrl = serverUrl
                    .replace("http://", "ws://")
                    .replace("https://", "wss://")
                    .trimEnd('/') + "/ws/v1/admin/frames"

                val requestBuilder = Request.Builder().url(wsUrl)
                if (adminToken.isNotBlank()) {
                    requestBuilder.header("Authorization", "Bearer $adminToken")
                }

                val latch = Job()
                currentSocket = okHttpClient.newWebSocket(requestBuilder.build(), object : WebSocketListener() {
                    override fun onOpen(webSocket: WebSocket, response: Response) {
                        _connected.value = true
                        backoffMs = 1_000L
                    }

                    override fun onClosing(webSocket: WebSocket, code: Int, reason: String) {
                        webSocket.close(1000, null)
                        _connected.value = false
                        latch.complete()
                    }

                    override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                        _connected.value = false
                        latch.complete()
                    }
                })

                latch.join()
            } catch (_: Exception) {
                _connected.value = false
            }

            delay(backoffMs)
            backoffMs = (backoffMs * 2).coerceAtMost(maxBackoff)
        }
    }
}
