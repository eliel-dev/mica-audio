package com.micaaudio.android.data.api

import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable

// ── Device models ───────────────────────────────────────────────────────────

@Serializable
data class AdminDevicesResponse(
    val devices: List<DeviceSnapshot> = emptyList(),
)

@Serializable
data class DeviceSnapshot(
    val deviceId: String = "",
    val name: String = "",
    val profile: String = "dma_exp",
    val status: Int = 0,
    val controlPlaneState: Int = 0,
    val isRegistered: Boolean = false,
    val lastSeenUtc: String = "",
    val firstSeenUtc: String? = null,
    val lastTelemetryUtc: String? = null,
    val lastAuthUtc: String? = null,
    val configState: Int = 0,
    val lastKnownIp: String? = null,
    val lastKnownRssi: Int? = null,
    val uptimeSeconds: Int? = null,
    val loopHealthyPercent: Int? = null,
    val loopLoadPercent: Int? = null,
    val chipTemperatureCelsius: Double? = null,
    val freeHeapBytes: Long? = null,
    val largestHeapBlockBytes: Long? = null,
    val psramAvailable: Boolean? = null,
    val freePsramBytes: Long? = null,
    val largestPsramBlockBytes: Long? = null,
    val wifiConnected: Boolean? = null,
    val wifiState: String? = null,
    val provisioningPortalActive: Boolean? = null,
    val auxLedAvailable: Boolean? = null,
    val testLedAvailable: Boolean? = null,
    val lastWifiEvent: String? = null,
    val streamLastSequence: Long? = null,
    val streamFramesReceived: Long? = null,
    val streamFramesApplied: Long? = null,
    val hub75PresentFrames: Long? = null,
    val streamSequenceGapCount: Long? = null,
    val streamInvalidFrameCount: Long? = null,
    val resetReason: String? = null,
    val controlQueueDepth: Long? = null,
    val controlWorkerState: String? = null,
    val panelsWorkerState: String? = null,
    val lastSlowCommand: String? = null,
    val lastSlowCommandDurationMs: Long? = null,
    val firmwareVersion: String? = null,
    val telemetrySequence: Long? = null,
    val brightnessCap: Int? = null,
    val brightnessRequested: Int? = null,
    val brightnessApplied: Int? = null,
    val testLedEnabled: Boolean? = null,
    val testLedDuty: Int? = null,
    val activeAppId: String? = null,
    val activeAppName: String? = null,
    val boardModel: String? = null,
    val panelType: String? = null,
    val animatedWebpBatchSupported: Boolean? = null,
    val visualUdpSupported: Boolean? = null,
    val visualUdpPort: Int? = null,
    val visualUdpMode: String? = null,
    val chipModel: String? = null,
    val chipRevision: Int? = null,
    val chipCores: Int? = null,
    val cpuFreqMHz: Int? = null,
    val sdkVersion: String? = null,
    val heapTotalBytes: Long? = null,
    val psramTotalBytes: Long? = null,
    val flashTotalBytes: Long? = null,
    val sketchSizeBytes: Long? = null,
    val freeSketchBytes: Long? = null,
) {
    /** Mirrors the C# property: IsConnected => ControlPlaneState == MqttOnline (2). */
    val isConnected: Boolean get() = controlPlaneState == 2
}

// ── Pairing ─────────────────────────────────────────────────────────────────

@Serializable
data class CreatePairingCodeRequest(
    val ttlSeconds: Int,
)

@Serializable
data class PairingCodeInfo(
    val code: String = "",
    val expiresAtUtc: String = "",
)

// ── Commands ────────────────────────────────────────────────────────────────

@Serializable
enum class DeviceCommandType(val value: Int) {
    @SerialName("1") EnterProvisioning(1),
    @SerialName("2") RevokeAndRestart(2),
    @SerialName("3") TestLed(3),
    @SerialName("5") InstallApp(5),
    @SerialName("6") ActivateApp(6),
    @SerialName("7") SetAppConfig(7),
    @SerialName("8") SetBrightness(8),
    @SerialName("9") UpdateFirmware(9),
    @SerialName("10") QueuePanelsBatch(10),
}

@Serializable
data class TrackedCommandRequest(
    val commandType: Int,
    val parameters: Map<String, String>? = null,
    val timeoutMs: Int = 10_000,
)

@Serializable
data class CommandDispatchResult(
    val commandId: String = "",
    val status: String = "",
    val deviceId: String = "",
    val detail: String? = null,
)

// ── Panels ──────────────────────────────────────────────────────────────────

@Serializable
data class PanelDefinition(
    val panelId: String = "",
    val name: String = "Painel",
    val width: Int = 128,
    val height: Int = 64,
    val updatedAtUtc: String = "",
    val widgets: List<PanelWidgetDefinition> = emptyList(),
)

@Serializable
data class PanelWidgetDefinition(
    val widgetId: String = "",
    val appId: String = "",
    val x: Int = 0,
    val y: Int = 0,
    val width: Int = 64,
    val height: Int = 32,
    val zIndex: Int = 0,
    val configValues: Map<String, String> = emptyMap(),
    val runtimeState: Map<String, String> = emptyMap(),
)

@Serializable
data class ServerPanelSnapshot(
    val deviceId: String = "",
    val panelJson: String = "",
    val panelId: String = "",
    val panelName: String = "",
    val widgetCount: Int = 0,
    val capability: String = "",
)

@Serializable
data class ServerPanelResponse(
    val deviceId: String = "",
    val panel: PanelDefinition? = null,
    val capability: String = "",
)

/**
 * One entry in the server-side panel catalog.
 * Returned by GET /api/v1/admin/panels — includes all panels ever created
 * by any client, with an optional activeOnDeviceId annotation.
 */
@Serializable
data class CatalogPanelResponse(
    val panel: PanelDefinition = PanelDefinition(),
    val activeOnDeviceId: String? = null,
    val capability: String = "",
)

// ── WebSocket events ────────────────────────────────────────────────────────

@Serializable
data class AdminEventMessage(
    val type: String = "",
    val deviceLog: DeviceLogMessage? = null,
    val commandProgress: DeviceCommandProgressMessage? = null,
)

@Serializable
data class DeviceLogMessage(
    val deviceId: String = "",
    val level: String = "",
    val message: String = "",
    val timestampUtc: String = "",
)

@Serializable
data class DeviceCommandProgressMessage(
    val commandId: String = "",
    val deviceId: String = "",
    val status: String = "",
    val detail: String? = null,
)

// ── Firmware ────────────────────────────────────────────────────────────────

@Serializable
data class FirmwareManifestResponse(
    val firmwareVersion: String = "",
    val boardModel: String = "",
    val panelType: String = "",
    val profile: String = "",
    val builtAtUtc: String = "",
    val otaFileSizeBytes: Long = 0,
)

// ── App Catalog ──────────────────────────────────────────────────────────────

@Serializable
data class AppsResponse(
    val apps: List<AppCatalogItem> = emptyList(),
)

@Serializable
data class PanelsSeedDocument(
    val panels: List<PanelDefinition> = emptyList(),
)

@Serializable
data class AppCatalogDocument(
    val schemaVersion: Int = 0,
    val apps: List<AppCatalogItem> = emptyList(),
)

@Serializable
data class AppCatalogItem(
    val id: String = "",
    val name: String = "",
    val summary: String = "",
    val description: String = "",
    val category: String = "",
    val packageName: String = "",
    val recommendedIntervalMinutes: Int = 0,
    val modifiers: List<AppModifierDefinition> = emptyList(),
    val runtime: AppRuntimeDefinition? = null,
)

@Serializable
data class AppModifierDefinition(
    val key: String = "",
    val label: String = "",
    val type: ModifierFieldType = ModifierFieldType.Text,
    val description: String? = null,
    val required: Boolean = false,
    val placeholder: String? = null,
    val defaultValue: String? = null,
    val defaultToggle: Boolean? = null,
    val options: List<AppModifierOption> = emptyList(),
)

@Serializable
data class AppModifierOption(
    val label: String = "",
    val value: String = "",
)

@Serializable
enum class ModifierFieldType {
    @SerialName("Text") Text,
    @SerialName("Toggle") Toggle,
    @SerialName("Select") Select,
    @SerialName("Number") Number,
    @SerialName("CityAutocomplete") CityAutocomplete,
}

@Serializable
data class AppRuntimeDefinition(
    val kind: String = "none",
)

// ── Panels Batches ──────────────────────────────────────────────────────────

@Serializable
data class PanelsBatchRegistration(
    val panelsSessionId: String = "",
    val batchSequence: Long = 0,
    val fileSizeBytes: Long = 0,
    val sha256: String = "",
    val contentType: String = "",
    val frameCount: Int = 0,
    val durationMs: Int = 0,
    val downloadUrl: String = "",
)
