package com.micaaudio.android.data.websocket

import com.micaaudio.android.data.api.AdminEventMessage
import com.micaaudio.android.data.settings.AppSettings
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.serialization.json.Json
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import javax.inject.Inject
import javax.inject.Singleton

/**
 * WebSocket client for /ws/v1/admin/events.
 * Emits typed events via SharedFlow and auto-reconnects with exponential backoff.
 */
@Singleton
class AdminEventsSocket @Inject constructor(
    private val okHttpClient: OkHttpClient,
    private val appSettings: AppSettings,
) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    private val _events = MutableSharedFlow<AdminEventMessage>(extraBufferCapacity = 64)
    val events: SharedFlow<AdminEventMessage> = _events.asSharedFlow()

    private val _commandProgress = MutableSharedFlow<com.micaaudio.android.data.api.DeviceCommandProgressMessage>(extraBufferCapacity = 32)
    val commandProgress: SharedFlow<com.micaaudio.android.data.api.DeviceCommandProgressMessage> = _commandProgress.asSharedFlow()

    private val _connected = MutableStateFlow(false)
    val connected: StateFlow<Boolean> = _connected.asStateFlow()

    private var connectJob: Job? = null
    private var currentSocket: WebSocket? = null

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
                    .trimEnd('/') + "/ws/v1/admin/events"

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

                    override fun onMessage(webSocket: WebSocket, text: String) {
                        try {
                            val event = json.decodeFromString<AdminEventMessage>(text)
                            _events.tryEmit(event)
                            event.commandProgress?.let { _commandProgress.tryEmit(it) }
                        } catch (_: Exception) {
                            // Skip malformed messages
                        }
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
