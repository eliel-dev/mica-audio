package com.micaaudio.android.data.api

import okhttp3.RequestBody
import okhttp3.ResponseBody
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.PUT
import retrofit2.http.Path
import retrofit2.http.Query
import retrofit2.http.Streaming

// DOCS: docs/wiki/modules/device-server-protocol.md#atualizacao-2026-04-admin-api-e-winui-remote
// DOCS: docs/wiki/modules/paineis.md#editor-hub75
/**
 * Retrofit interface mapping 1:1 to the MicaAudio.Server Admin REST API.
 * Mirrors the surface of RemoteDeviceServerClient from Device.Client.Remote.
 */
interface MicaServerApi {

    // ── Devices ─────────────────────────────────────────────────────────────

    @GET("/api/v1/admin/devices")
    suspend fun getDevices(): AdminDevicesResponse

    @DELETE("/api/v1/admin/devices/{deviceId}")
    suspend fun removeDevice(
        @Path("deviceId") deviceId: String,
    ): Response<Unit>

    // ── Commands ────────────────────────────────────────────────────────────

    @POST("/api/v1/admin/devices/{deviceId}/commands/tracked")
    suspend fun sendTrackedCommand(
        @Path("deviceId") deviceId: String,
        @Body request: TrackedCommandRequest,
    ): CommandDispatchResult

    // ── Panels (per-device) ──────────────────────────────────────────────────

    @PUT("/api/v1/admin/devices/{deviceId}/panel")
    suspend fun uploadPanel(
        @Path("deviceId") deviceId: String,
        @Body panelJson: RequestBody,
    ): Response<Unit>

    @GET("/api/v1/admin/devices/{deviceId}/panel")
    suspend fun getPanel(
        @Path("deviceId") deviceId: String,
    ): Response<ResponseBody>

    @DELETE("/api/v1/admin/devices/{deviceId}/panel")
    suspend fun deletePanel(
        @Path("deviceId") deviceId: String,
    ): Response<Unit>

    // ── Panel catalog ────────────────────────────────────────────────────────

    /** Returns all panels in the server catalog with activeOnDeviceId annotations. */
    @GET("/api/v1/admin/panels")
    suspend fun getCatalogPanels(): Response<ResponseBody>

    /** Returns the full PanelDefinition for a single catalog entry. */
    @GET("/api/v1/admin/panels/{panelId}")
    suspend fun getCatalogPanel(
        @Path("panelId") panelId: String,
    ): Response<ResponseBody>

    /** Creates or updates a panel in the catalog (route panelId wins). */
    @PUT("/api/v1/admin/panels/{panelId}")
    suspend fun upsertCatalogPanel(
        @Path("panelId") panelId: String,
        @Body panelJson: RequestBody,
    ): Response<Unit>

    /** Removes a panel from the catalog permanently. */
    @DELETE("/api/v1/admin/panels/{panelId}")
    suspend fun deleteCatalogPanel(
        @Path("panelId") panelId: String,
    ): Response<Unit>

    // ── Media ───────────────────────────────────────────────────────────────

    @GET("/api/v1/admin/devices/{deviceId}/media")
    suspend fun listMedia(
        @Path("deviceId") deviceId: String,
    ): Response<ResponseBody>

    @Streaming
    @GET("/api/v1/admin/devices/{deviceId}/media/{mediaId}")
    suspend fun downloadMedia(
        @Path("deviceId") deviceId: String,
        @Path("mediaId") mediaId: String,
    ): Response<ResponseBody>

    @PUT("/api/v1/admin/devices/{deviceId}/media/{mediaId}")
    suspend fun uploadMedia(
        @Path("deviceId") deviceId: String,
        @Path("mediaId") mediaId: String,
        @Body bytes: RequestBody,
    ): Response<Unit>

    @DELETE("/api/v1/admin/devices/{deviceId}/media/{mediaId}")
    suspend fun deleteMedia(
        @Path("deviceId") deviceId: String,
        @Path("mediaId") mediaId: String,
    ): Response<Unit>

    // ── Panels Batches ──────────────────────────────────────────────────────

    @POST("/api/v1/admin/panels/batches/{deviceId}/{sessionId}/{batchSequence}")
    suspend fun registerPanelsBatch(
        @Path("deviceId") deviceId: String,
        @Path("sessionId") sessionId: String,
        @Path("batchSequence") batchSequence: Long,
        @Query("frameCount") frameCount: Int,
        @Query("durationMs") durationMs: Int,
        @Body payload: RequestBody,
    ): PanelsBatchRegistration

    @DELETE("/api/v1/admin/panels/batches/{deviceId}")
    suspend fun clearPanelsBatches(
        @Path("deviceId") deviceId: String,
        @Query("panelsSessionId") panelsSessionId: String? = null,
    ): Response<Unit>

    // ── Pairing ─────────────────────────────────────────────────────────────

    @POST("/api/v1/admin/pairing-codes")
    suspend fun createPairingCode(@Body request: CreatePairingCodeRequest): Response<PairingCodeInfo>

    // ── Firmware ────────────────────────────────────────────────────────────

    @GET("/api/v1/device/firmware/latest")
    suspend fun getFirmwareManifest(
        @Query("boardModel") boardModel: String,
        @Query("panelType") panelType: String,
        @Query("profile") profile: String,
    ): Response<FirmwareManifestResponse>

    // ── Widget catalog ───────────────────────────────────────────────────────

    @GET("/api/v1/admin/widgets")
    suspend fun getWidgets(): Response<WidgetsResponse>

    // ── GIPHY search proxy ───────────────────────────────────────────────────

    @GET("/api/v1/admin/giphy/search")
    suspend fun searchGiphy(
        @Query("q") q: String,
        @Query("limit") limit: Int = 20,
        @Query("offset") offset: Int = 0,
    ): Response<GiphySearchResponse>
}
