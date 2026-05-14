package com.micaaudio.android.data.repository

import android.content.Context
import com.micaaudio.android.data.api.CatalogPanelResponse
import com.micaaudio.android.data.api.MicaServerApi
import com.micaaudio.android.data.api.PanelDefinition
import com.micaaudio.android.data.api.PanelsSeedDocument
import com.micaaudio.android.data.api.ServerPanelResponse
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class PanelRepository @Inject constructor(
    private val api: MicaServerApi,
    @ApplicationContext private val context: Context,
) {
    private val json = Json { ignoreUnknownKeys = true; isLenient = true }

    /**
     * Fetches the panel stored on a single device.
     *
     * The server returns: { "deviceId":"...", "panel":{...PanelDefinition...}, "capability":"..." }
     * Returns null when the device has no panel (HTTP 404).
     */
    suspend fun getPanel(deviceId: String): Result<ServerPanelResponse?> = runCatching {
        val response = api.getPanel(deviceId)
        if (response.code() == 404) return@runCatching null
        if (!response.isSuccessful) {
            throw Exception("Server returned ${response.code()}: ${response.errorBody()?.string()}")
        }
        val body = response.body()?.string() ?: return@runCatching null
        json.decodeFromString<ServerPanelResponse>(body)
    }

    /**
     * Returns a default PanelDefinition from the bundled seed asset.
     * Used as a starting point when creating a brand-new panel for a device.
     */
    fun defaultPanelFromAssets(): PanelDefinition? = try {
        val text = context.assets.open("panels.seed.json").bufferedReader().readText()
        json.decodeFromString<PanelsSeedDocument>(text).panels.firstOrNull()
    } catch (_: Exception) {
        null
    }

    suspend fun uploadPanel(deviceId: String, panel: PanelDefinition): Result<Unit> = runCatching {
        val panelJson = json.encodeToString(PanelDefinition.serializer(), panel)
        val body = panelJson.toRequestBody("application/json".toMediaType())
        val response = api.uploadPanel(deviceId, body)
        if (!response.isSuccessful) {
            throw Exception("Upload failed: ${response.code()}")
        }
    }

    suspend fun deletePanel(deviceId: String): Result<Boolean> = runCatching {
        val response = api.deletePanel(deviceId)
        response.isSuccessful
    }

    suspend fun uploadMedia(
        deviceId: String,
        mediaId: String,
        bytes: ByteArray,
    ): Result<Unit> = runCatching {
        val body = bytes.toRequestBody("application/octet-stream".toMediaType())
        val response = api.uploadMedia(deviceId, mediaId, body)
        if (!response.isSuccessful) {
            throw Exception("Media upload failed: ${response.code()}")
        }
    }

    suspend fun deleteMedia(deviceId: String, mediaId: String): Result<Boolean> = runCatching {
        val response = api.deleteMedia(deviceId, mediaId)
        response.isSuccessful
    }

    // ── Panel catalog ─────────────────────────────────────────────────────────

    /**
     * Returns all panels in the server catalog.
     * Each entry has [CatalogPanelResponse.panel] (full definition) and
     * [CatalogPanelResponse.activeOnDeviceId] (null when not active on any device).
     */
    suspend fun getCatalogPanels(): Result<List<CatalogPanelResponse>> = runCatching {
        val response = api.getCatalogPanels()
        if (!response.isSuccessful) {
            throw Exception("Server returned ${response.code()}: ${response.errorBody()?.string()}")
        }
        val body = response.body()?.string() ?: return@runCatching emptyList()
        json.decodeFromString<List<CatalogPanelResponse>>(body)
    }

    /**
     * Gets a single panel from the catalog by id.
     * Returns null when not found (HTTP 404).
     */
    suspend fun getCatalogPanel(panelId: String): Result<PanelDefinition?> = runCatching {
        val response = api.getCatalogPanel(panelId)
        if (response.code() == 404) return@runCatching null
        if (!response.isSuccessful) {
            throw Exception("Server returned ${response.code()}: ${response.errorBody()?.string()}")
        }
        val body = response.body()?.string() ?: return@runCatching null
        json.decodeFromString<PanelDefinition>(body)
    }

    /**
     * Creates or updates a panel in the catalog (panelId from the panel itself).
     */
    suspend fun upsertCatalogPanel(panel: PanelDefinition): Result<Unit> = runCatching {
        val panelJson = json.encodeToString(PanelDefinition.serializer(), panel)
        val body = panelJson.toRequestBody("application/json".toMediaType())
        val response = api.upsertCatalogPanel(panel.panelId, body)
        if (!response.isSuccessful) {
            throw Exception("Catalog upsert failed: ${response.code()}")
        }
    }

    /**
     * Removes a panel from the server catalog permanently.
     */
    suspend fun deleteCatalogPanel(panelId: String): Result<Boolean> = runCatching {
        val response = api.deleteCatalogPanel(panelId)
        response.isSuccessful
    }
}
