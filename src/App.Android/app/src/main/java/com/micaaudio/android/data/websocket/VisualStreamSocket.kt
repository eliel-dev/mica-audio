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
import okio.ByteString
import okio.ByteString.Companion.toByteString
import java.nio.ByteBuffer
import java.nio.ByteOrder
import javax.inject.Inject
import javax.inject.Singleton

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

        val deviceIdBytes = deviceId.toByteArray()
        if (deviceIdBytes.size > 0xFFFF) return

        // 1. Build StreamFrameV2 (MessageTypeBins128 = 1)
        // [Version:1][Type:1][Seq:4][TS:8][Level:1][Bins:128][Bright:1][Flags:1] = 145 bytes
        val payload = ByteBuffer.allocate(145).order(ByteOrder.LITTLE_ENDIAN).apply {
            put(2.toByte()) // Version
            put(1.toByte()) // MessageTypeBins128
            putInt(sequence++) // Sequence
            putLong(System.currentTimeMillis()) // Timestamp (fake QPC)
            put(level.coerceIn(0, 255).toByte())
            put(bins)
            put(brightness.coerceIn(0, 255).toByte())
            put(0.toByte()) // Flags
        }.array()

        // 2. Build Admin Frame Envelope
        // [Targeted:1][IdLen:2][Id:N][Payload:M]
        val envelope = ByteBuffer.allocate(1 + 2 + deviceIdBytes.size + payload.size).order(ByteOrder.LITTLE_ENDIAN).apply {
            put(1.toByte()) // Targeted = true
            putShort(deviceIdBytes.size.toShort())
            put(deviceIdBytes)
            put(payload)
        }.array()

        socket.send(envelope.toByteString())
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
