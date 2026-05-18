package com.micaaudio.android.data.repository

import com.micaaudio.android.data.api.CommandDispatchResult
import com.micaaudio.android.data.api.GiphySearchResponse
import okhttp3.RequestBody
import com.micaaudio.android.data.api.CreatePairingCodeRequest
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import com.micaaudio.android.data.api.DeviceCommandProgressMessage
import com.micaaudio.android.data.api.DeviceLogMessage
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.data.api.FirmwareManifestResponse
import com.micaaudio.android.data.api.MicaServerApi
import com.micaaudio.android.data.api.PairingCodeInfo
import com.micaaudio.android.data.api.TrackedCommandRequest
import com.micaaudio.android.data.websocket.AdminEventsSocket
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.filterNotNull
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class DeviceRepository @Inject constructor(
    private val api: MicaServerApi,
    private val eventsSocket: AdminEventsSocket,
) {
    val devicesChangedSignal: Flow<Unit> = eventsSocket.events.map { }

    val commandProgress: Flow<DeviceCommandProgressMessage> = eventsSocket.commandProgress

    val deviceLogs: Flow<DeviceLogMessage> = eventsSocket.events
        .map { it.deviceLog }
        .filterNotNull()

    suspend fun getDevices(): Result<List<DeviceSnapshot>> = runCatching {
        api.getDevices().devices
    }

    suspend fun removeDevice(deviceId: String): Result<Boolean> = runCatching {
        val response = api.removeDevice(deviceId)
        response.isSuccessful
    }

    suspend fun createPairingCode(ttlSeconds: Int): Result<PairingCodeInfo> = runCatching {
        val response = api.createPairingCode(CreatePairingCodeRequest(ttlSeconds))
        if (!response.isSuccessful) throw Exception("Erro ao criar código: ${response.code()}")
        response.body() ?: throw Exception("Resposta vazia do servidor")
    }

    suspend fun sendCommand(
        deviceId: String,
        commandType: Int,
        parameters: Map<String, String>? = null,
        timeoutMs: Int = 10_000,
    ): Result<CommandDispatchResult> = runCatching {
        api.sendTrackedCommand(
            deviceId = deviceId,
            request = TrackedCommandRequest(
                commandType = commandType,
                parameters = parameters,
                timeoutMs = timeoutMs,
            ),
        )
    }

    suspend fun setBrightness(deviceId: String, brightness: Int): Result<CommandDispatchResult> {
        return sendCommand(
            deviceId = deviceId,
            commandType = 8, // SetBrightness
            parameters = mapOf("brightness" to brightness.toString()),
        )
    }

    suspend fun testLed(deviceId: String): Result<CommandDispatchResult> {
        return sendCommand(
            deviceId = deviceId,
            commandType = 3, // TestLed
        )
    }

    suspend fun setAppConfig(
        deviceId: String,
        parameters: Map<String, String>,
    ): Result<CommandDispatchResult> {
        return sendCommand(
            deviceId = deviceId,
            commandType = 7, // SetAppConfig
            parameters = parameters,
        )
    }

    suspend fun updateFirmware(
        deviceId: String,
        targetVersion: String = "",
    ): Result<CommandDispatchResult> {
        return sendCommand(
            deviceId = deviceId,
            commandType = 9, // UpdateFirmware
            parameters = if (targetVersion.isNotBlank()) mapOf("version" to targetVersion) else null,
            timeoutMs = 120_000,
        )
    }

    suspend fun installApp(
        deviceId: String,
        appId: String,
        configJson: String,
    ): Result<CommandDispatchResult> {
        return sendCommand(
            deviceId = deviceId,
            commandType = 5, // InstallApp
            parameters = mapOf("appId" to appId, "config" to configJson),
            timeoutMs = 30_000,
        )
    }

    suspend fun activateApp(
        deviceId: String,
        appId: String,
    ): Result<CommandDispatchResult> {
        return sendCommand(
            deviceId = deviceId,
            commandType = 6, // ActivateApp
            parameters = mapOf("appId" to appId),
        )
    }

    suspend fun getFirmwareManifest(
        boardModel: String,
        panelType: String,
        profile: String,
    ): FirmwareManifestResponse? = runCatching {
        val response = api.getFirmwareManifest(boardModel, panelType, profile)
        if (response.isSuccessful) response.body() else null
    }.getOrNull()

    suspend fun uploadMedia(
        deviceId: String,
        mediaId: String,
        body: RequestBody,
    ): Result<Boolean> = runCatching {
        api.uploadMedia(deviceId, mediaId, body).isSuccessful
    }

    // ── GIPHY ────────────────────────────────────────────────────────────────

    suspend fun searchGiphy(q: String): Result<GiphySearchResponse> = runCatching {
        val response = api.searchGiphy(q)
        if (!response.isSuccessful) throw Exception("Erro na busca GIPHY: ${response.code()}")
        response.body() ?: throw Exception("Resposta vazia do servidor")
    }

    /**
     * Downloads GIF bytes directly from the GIPHY CDN.
     * CDN URLs do not require authentication — called on IO dispatcher.
     */
    suspend fun downloadGifBytes(url: String): Result<ByteArray> = runCatching {
        withContext(Dispatchers.IO) {
            java.net.URL(url).readBytes()
        }
    }
}
