package com.micaaudio.android.ui.screens.panels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.api.CatalogPanelResponse
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.data.api.PanelDefinition
import com.micaaudio.android.data.api.PanelWidgetDefinition
import com.micaaudio.android.data.api.ServerPanelResponse
import com.micaaudio.android.data.api.WidgetDefinition
import com.micaaudio.android.data.repository.DeviceRepository
import com.micaaudio.android.data.repository.PanelRepository
import com.micaaudio.android.data.repository.WidgetCatalogRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.time.Instant
import java.util.UUID
import javax.inject.Inject

// DOCS: docs/wiki/modules/paineis.md#editor-hub75
// DOCS: docs/wiki/modules/device-server-protocol.md#atualizacao-2026-04-admin-api-e-winui-remote
data class PanelsUiState(
    /** All panels in the server catalog (from GET /api/v1/admin/panels). */
    val catalogPanels: List<CatalogPanelResponse> = emptyList(),
    /** All registered devices — online AND offline. */
    val devices: List<DeviceSnapshot> = emptyList(),
    /** DeviceIds currently connected (isConnected == true). */
    val connectedDeviceIds: Set<String> = emptySet(),
    val isLoading: Boolean = true,
    val error: String? = null,
    val successMessage: String? = null,

    /** Panel waiting for a device to be picked (multi-device activate dialog). */
    val pendingActivatePanel: PanelDefinition? = null,

    // ── Editor state (used by PanelEditorScreen) ─────────────────────────────
    val selectedDeviceId: String? = null,
    val panelResponse: ServerPanelResponse? = null,
    val isPanelLoading: Boolean = false,
    val selectedWidgetId: String? = null,
    val availableWidgets: List<WidgetDefinition> = emptyList(),

    // Kept for editor: per-device panels fetched on demand
    val devicePanels: Map<String, ServerPanelResponse?> = emptyMap(),

    val deviceMedia: List<String> = emptyList(),
    val deviceMediaCache: Map<String, String> = emptyMap(),
    /** Per-file sizes from the server (mediaId → bytes). */
    val mediaFileSizes: Map<String, Long> = emptyMap(),
    /** Sum of all media file sizes on the server for the selected device (bytes). */
    val mediaTotalBytes: Long = 0L,
    val isMediaLoading: Boolean = false,
    val serverUrl: String = "",
    val authToken: String = "",
    val giphyApiKey: String = "",
    val giphyResults: List<com.micaaudio.android.data.api.GiphyItem> = emptyList(),
    val isGiphyLoading: Boolean = false,
)

@HiltViewModel
class PanelsViewModel @Inject constructor(
    private val deviceRepository: DeviceRepository,
    private val panelRepository: PanelRepository,
    private val catalogRepository: WidgetCatalogRepository,
    private val appSettings: com.micaaudio.android.data.settings.AppSettings,
) : ViewModel() {

    private val _uiState = MutableStateFlow(PanelsUiState())
    val uiState: StateFlow<PanelsUiState> = _uiState.asStateFlow()

    init {
        viewModelScope.launch {
            appSettings.serverUrl.collect { url ->
                _uiState.update { it.copy(serverUrl = url.trimEnd('/')) }
            }
        }
        viewModelScope.launch {
            appSettings.adminToken.collect { token ->
                _uiState.update { it.copy(authToken = token) }
            }
        }
        viewModelScope.launch {
            appSettings.giphyApiKey.collect { key ->
                _uiState.update { it.copy(giphyApiKey = key) }
            }
        }
        refresh()
        loadCatalog()
    }

    private fun loadCatalog() {
        viewModelScope.launch {
            catalogRepository.getWidgets().onSuccess { widgets ->
                _uiState.update { it.copy(availableWidgets = widgets) }
            }
        }
    }

    // ── Gallery ──────────────────────────────────────────────────────────────

    /**
     * Server is the source of truth.
     *
     * 1. GET /api/v1/admin/devices         → all registered devices (online + offline)
     * 2. GET /api/v1/admin/devices/{id}/panel → panel stored for each device (parallel)
     *
     * Any device that has a panel stored on the server will appear in the list,
     * regardless of whether it is currently connected.
     */
    fun refresh() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }

            // Fetch devices (for connected status) and catalog in parallel.
            val devicesResult = deviceRepository.getDevices()
            val allDevices = devicesResult.getOrElse { emptyList() }
            val connectedIds = allDevices.filter { it.isConnected }.map { it.deviceId }.toSet()

            val catalogResult = panelRepository.getCatalogPanels()
            val catalog = catalogResult.getOrElse { emptyList() }

            _uiState.update {
                it.copy(
                    devices = allDevices,
                    connectedDeviceIds = connectedIds,
                    catalogPanels = catalog,
                    isLoading = false,
                    error = devicesResult.exceptionOrNull()?.message
                        ?: catalogResult.exceptionOrNull()?.message,
                )
            }
        }
    }

    // ── Gallery actions ───────────────────────────────────────────────────────

    fun createPanel(onCreated: (String) -> Unit) {
        val newPanel = PanelDefinition(
            panelId = UUID.randomUUID().toString(),
            name = "Novo Painel",
            width = 128,
            height = 64,
            updatedAtUtc = Instant.now().toString(),
        )
        viewModelScope.launch {
            panelRepository.upsertCatalogPanel(newPanel)
                .onSuccess {
                    _uiState.update { state ->
                        state.copy(catalogPanels = state.catalogPanels + CatalogPanelResponse(panel = newPanel))
                    }
                    onCreated(newPanel.panelId)
                }
                .onFailure { e ->
                    _uiState.update { it.copy(error = "Erro ao criar painel: ${e.message}") }
                }
        }
    }

    fun deletePanel(panelId: String, activeOnDeviceId: String?) {
        viewModelScope.launch {
            if (!activeOnDeviceId.isNullOrBlank()) {
                panelRepository.deletePanel(activeOnDeviceId)
            }
            panelRepository.deleteCatalogPanel(panelId)
                .onSuccess {
                    _uiState.update { state ->
                        state.copy(catalogPanels = state.catalogPanels.filter { it.panel.panelId != panelId })
                    }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(error = "Erro ao excluir painel: ${e.message}") }
                }
        }
    }

    /**
     * Request activation of a panel.
     * If exactly one device is connected, activates immediately.
     * If multiple, opens a device-picker dialog.
     */
    fun requestActivate(panel: PanelDefinition) {
        val connected = _uiState.value.devices.filter {
            it.deviceId in _uiState.value.connectedDeviceIds
        }
        when {
            connected.isEmpty() ->
                _uiState.update { it.copy(error = "Nenhum dispositivo conectado.") }
            connected.size == 1 ->
                activatePanel(panel, connected.first().deviceId)
            else ->
                _uiState.update { it.copy(pendingActivatePanel = panel) }
        }
    }

    fun dismissActivateDialog() {
        _uiState.update { it.copy(pendingActivatePanel = null) }
    }

    /**
     * Upload the panel to the device and switch to the panels app.
     * This also upserts the panel to the server catalog (server-side upsert
     * happens automatically inside HandleAdminUploadPanelAsync on the server).
     *
     * Panels whose widgets are not server-capable (e.g. visualizer, weather) need
     * the WinUI client to be connected and streaming frames; without it the device
     * will show a blank screen. A warning is appended to the success message so
     * the user knows why the panel may not render without Windows.
     */
    fun activatePanel(panel: PanelDefinition, deviceId: String) {
        _uiState.update { it.copy(pendingActivatePanel = null) }
        viewModelScope.launch {
            panelRepository.uploadPanel(deviceId, panel)
                .onSuccess {
                    deviceRepository.sendCommand(
                        deviceId = deviceId,
                        commandType = 6,
                        parameters = mapOf("appId" to "panels"),
                    )
                    val capability = classifyPanelCapability(panel)
                    val note = when (capability) {
                        PanelCapability.RequiresClient ->
                            "\n⚠️ Este painel contém widgets que precisam do cliente Windows para renderizar."
                        PanelCapability.GifWithoutMedia ->
                            "\n⚠️ Widget GIF sem mídia carregada no servidor — ative primeiro pelo Windows."
                        PanelCapability.ServerCapable -> ""
                    }
                    // Update catalog entry to reflect new activeOnDeviceId.
                    _uiState.update { state ->
                        val updated = state.catalogPanels.map { entry ->
                            if (entry.panel.panelId == panel.panelId)
                                entry.copy(activeOnDeviceId = deviceId)
                            else if (entry.activeOnDeviceId == deviceId)
                                entry.copy(activeOnDeviceId = null)
                            else entry
                        }
                        state.copy(successMessage = "Painel '${panel.name}' ativado!$note", catalogPanels = updated)
                    }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(error = "Erro ao ativar: ${e.message}") }
                }
        }
    }

    // ── Capability classification (mirrors PanelServerCapabilityClassifier.cs) ─

    private enum class PanelCapability { ServerCapable, GifWithoutMedia, RequiresClient }

    private fun classifyPanelCapability(panel: PanelDefinition): PanelCapability {
        if (panel.widgets.isEmpty()) return PanelCapability.ServerCapable
        for (widget in panel.widgets) {
            when (widget.appId.trim().lowercase()) {
                "analogclock" -> continue
                "gifhub75" -> {
                    val hasMediaId = !widget.runtimeState["mediaId"].isNullOrBlank()
                    val hasMediaIds = !widget.runtimeState["mediaIds"].isNullOrBlank()
                    if (!hasMediaId && !hasMediaIds) return PanelCapability.GifWithoutMedia
                }
                else -> return PanelCapability.RequiresClient
            }
        }
        return PanelCapability.ServerCapable
    }

    fun dismissMessage() {
        _uiState.update { it.copy(successMessage = null, error = null) }
    }

    // ── Editor navigation ────────────────────────────────────────────────────

    /** Called by PanelEditorScreen via LaunchedEffect(panelId). Loads panel from catalog. */
    fun selectPanel(panelId: String) {
        val entry = _uiState.value.catalogPanels.firstOrNull { it.panel.panelId == panelId }
        if (entry != null) {
            _uiState.update {
                it.copy(
                    selectedDeviceId = entry.activeOnDeviceId,
                    panelResponse = ServerPanelResponse(
                        deviceId = entry.activeOnDeviceId ?: "",
                        panel = entry.panel,
                    ),
                    isPanelLoading = false,
                )
            }
        } else {
            // Catalog not yet loaded — try server
            viewModelScope.launch {
                _uiState.update { it.copy(isPanelLoading = true) }
                panelRepository.getCatalogPanel(panelId)
                    .onSuccess { panel ->
                        if (panel != null) {
                            _uiState.update {
                                it.copy(
                                    panelResponse = ServerPanelResponse(panel = panel),
                                    isPanelLoading = false,
                                )
                            }
                        } else {
                            _uiState.update { it.copy(isPanelLoading = false, error = "Painel não encontrado.") }
                        }
                    }
                    .onFailure { e ->
                        _uiState.update { it.copy(isPanelLoading = false, error = "Falha ao carregar painel: ${e.message}") }
                    }
            }
        }
    }

    /** Called by PanelEditorScreen via LaunchedEffect(deviceId). */
    fun selectDevice(deviceId: String) {
        _uiState.update { it.copy(selectedDeviceId = deviceId) }

        // Use cached response if already fetched this session
        if (_uiState.value.devicePanels.containsKey(deviceId)) {
            val cached = _uiState.value.devicePanels[deviceId]
            val response = cached ?: ServerPanelResponse(
                deviceId = deviceId,
                panel = panelRepository.defaultPanelFromAssets()
                    ?: PanelDefinition(panelId = UUID.randomUUID().toString(), name = "Novo Painel"),
            )
            _uiState.update { it.copy(panelResponse = response, isPanelLoading = false) }
        } else {
            loadPanelForDevice(deviceId)
        }
    }

    private fun loadPanelForDevice(deviceId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isPanelLoading = true, error = null) }
            panelRepository.getPanel(deviceId)
                .onSuccess { response ->
                    val actual = response ?: ServerPanelResponse(
                        deviceId = deviceId,
                        panel = panelRepository.defaultPanelFromAssets()
                            ?: PanelDefinition(
                                panelId = UUID.randomUUID().toString(),
                                name = "Novo Painel",
                                width = 128,
                                height = 64,
                            ),
                    )
                    _uiState.update {
                        it.copy(
                            panelResponse = actual,
                            isPanelLoading = false,
                            devicePanels = it.devicePanels + (deviceId to actual),
                        )
                    }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(isPanelLoading = false, error = "Falha ao carregar painel: ${e.message}") }
                }
        }
    }

    // ── Editor widget operations ─────────────────────────────────────────────

    fun updatePanelName(name: String) {
        _uiState.update { state ->
            val panel = state.panelResponse?.panel ?: return@update state
            state.copy(panelResponse = state.panelResponse.copy(panel = panel.copy(name = name)))
        }
    }

    fun selectWidget(widgetId: String?) {
        _uiState.update { it.copy(selectedWidgetId = widgetId) }
    }

    fun moveWidget(widgetId: String, dx: Float, dy: Float) {
        _uiState.update { state ->
            val panel = state.panelResponse?.panel ?: return@update state
            val updated = panel.widgets.map { w ->
                if (w.widgetId == widgetId) w.copy(
                    x = (w.x + dx.toInt()).coerceIn(0, panel.width - w.width),
                    y = (w.y + dy.toInt()).coerceIn(0, panel.height - w.height),
                ) else w
            }
            state.copy(panelResponse = state.panelResponse.copy(panel = panel.copy(widgets = updated)))
        }
    }

    fun resizeWidget(
        widgetId: String,
        leftDelta: Int,
        topDelta: Int,
        rightDelta: Int,
        bottomDelta: Int,
    ) {
        _uiState.update { state ->
            val panel = state.panelResponse?.panel ?: return@update state
            val updated = panel.widgets.map { w ->
                if (w.widgetId != widgetId) return@map w

                val minSize = 15
                val desiredLeft = (w.x + leftDelta).coerceIn(0, w.x + w.width - minSize)
                val desiredTop = (w.y + topDelta).coerceIn(0, w.y + w.height - minSize)
                val leftShift = desiredLeft - w.x
                val topShift = desiredTop - w.y

                val desiredWidth = (w.width - leftShift + rightDelta).coerceIn(minSize, panel.width - desiredLeft)
                val desiredHeight = (w.height - topShift + bottomDelta).coerceIn(minSize, panel.height - desiredTop)

                w.copy(
                    x = desiredLeft,
                    y = desiredTop,
                    width = desiredWidth,
                    height = desiredHeight,
                )
            }
            state.copy(panelResponse = state.panelResponse.copy(panel = panel.copy(widgets = updated)))
        }
    }

    fun moveWidgetLayer(widgetId: String, up: Boolean) {
        _uiState.update { state ->
            val panel = state.panelResponse?.panel ?: return@update state
            val widgets = panel.widgets.toMutableList()
            val index = widgets.indexOfFirst { it.widgetId == widgetId }
            if (index == -1) return@update state

            val newIndex = if (up) index + 1 else index - 1
            if (newIndex !in widgets.indices) return@update state

            // Swap positions
            val item = widgets.removeAt(index)
            widgets.add(newIndex, item)

            // Re-assign z-indices based on list position (higher index = front)
            val updatedWidgets = widgets.mapIndexed { i, w ->
                w.copy(zIndex = i)
            }

            state.copy(
                panelResponse = state.panelResponse.copy(
                    panel = panel.copy(widgets = updatedWidgets)
                )
            )
        }
    }

    fun updateWidget(updated: PanelWidgetDefinition) {
        _uiState.update { state ->
            val panel = state.panelResponse?.panel ?: return@update state
            val widgets = panel.widgets.map { if (it.widgetId == updated.widgetId) updated else it }
            state.copy(panelResponse = state.panelResponse.copy(panel = panel.copy(widgets = widgets)))
        }
    }

    fun removeWidget(widgetId: String) {
        _uiState.update { state ->
            val panel = state.panelResponse?.panel ?: return@update state
            state.copy(
                selectedWidgetId = if (state.selectedWidgetId == widgetId) null else state.selectedWidgetId,
                panelResponse = state.panelResponse.copy(
                    panel = panel.copy(widgets = panel.widgets.filter { it.widgetId != widgetId }),
                ),
            )
        }
    }

    fun addWidget(appId: String) {
        _uiState.update { state ->
            val panel = state.panelResponse?.panel ?: return@update state
            val app = state.availableWidgets.firstOrNull { it.id == appId }
            val defaultConfig = app?.modifiers?.associate { m ->
                m.key to (m.defaultValue ?: if (m.defaultToggle == true) "true" else "")
            }?.filterValues { it.isNotEmpty() } ?: emptyMap()
            val newWidget = PanelWidgetDefinition(
                widgetId = UUID.randomUUID().toString(),
                appId = appId,
                x = 0, y = 0, width = 64, height = 32,
                zIndex = (panel.widgets.maxOfOrNull { it.zIndex } ?: 0) + 1,
                configValues = defaultConfig,
            )
            state.copy(
                selectedWidgetId = newWidget.widgetId,
                panelResponse = state.panelResponse.copy(panel = panel.copy(widgets = panel.widgets + newWidget)),
            )
        }
    }

    fun loadMediaForDevice(deviceId: String) {
        if (deviceId.isBlank()) return
        viewModelScope.launch {
            _uiState.update { it.copy(isMediaLoading = true) }
            panelRepository.listMedia(deviceId)
                .onSuccess { response ->
                    val ids = response.mediaIds
                    val cached = panelRepository.syncMediaCache(deviceId, ids).getOrElse { emptyMap() }
                    _uiState.update {
                        it.copy(
                            deviceMedia = ids,
                            deviceMediaCache = cached.mapValues { entry -> entry.value.absolutePath },
                            mediaFileSizes = response.fileSizes,
                            mediaTotalBytes = response.totalBytes,
                            isMediaLoading = false,
                        )
                    }
                }
                .onFailure { _uiState.update { it.copy(isMediaLoading = false) } }
        }
    }

    fun deleteMedia(deviceId: String, mediaId: String) {
        if (deviceId.isBlank() || mediaId.isBlank()) return
        viewModelScope.launch {
            panelRepository.deleteMedia(deviceId, mediaId)
                .onSuccess {
                    // Optimistically remove from local list; reload fetches authoritative sizes.
                    _uiState.update { state ->
                        state.copy(
                            deviceMedia = state.deviceMedia.filter { it != mediaId },
                            deviceMediaCache = state.deviceMediaCache - mediaId,
                            mediaFileSizes = state.mediaFileSizes - mediaId,
                        )
                    }
                    // Reload authoritative sizes from server
                    loadMediaForDevice(deviceId)
                }
                .onFailure { e ->
                    _uiState.update { it.copy(error = "Erro ao excluir mídia: ${e.message}") }
                }
        }
    }

    fun uploadMedia(deviceId: String, mediaId: String, bytes: ByteArray) {
        if (deviceId.isBlank()) {
            _uiState.update { it.copy(error = "Selecione ou ative um dispositivo antes de enviar mídia.") }
            return
        }
        viewModelScope.launch {
            panelRepository.uploadMedia(deviceId, mediaId, bytes)
                .onSuccess { loadMediaForDevice(deviceId) }
                .onFailure { e -> _uiState.update { it.copy(error = "Erro ao enviar mídia: ${e.message}") } }
        }
    }

    fun setGiphyApiKey(key: String) {
        viewModelScope.launch {
            appSettings.setGiphyApiKey(key)
        }
    }

    fun searchGiphy(query: String) {
        if (query.isBlank()) {
            _uiState.update { it.copy(giphyResults = emptyList()) }
            return
        }
        viewModelScope.launch {
            _uiState.update { it.copy(isGiphyLoading = true) }
            val result = deviceRepository.searchGiphy(query)
            result.onSuccess { response ->
                _uiState.update { it.copy(giphyResults = response.items, isGiphyLoading = false) }
            }.onFailure { e ->
                _uiState.update { it.copy(isGiphyLoading = false, error = "GIPHY Error: ${e.message}") }
            }
        }
    }

    fun importGiphyToDevice(deviceId: String, item: com.micaaudio.android.data.api.GiphyItem) {
        if (deviceId.isBlank()) return
        viewModelScope.launch {
            _uiState.update { it.copy(isMediaLoading = true) }
            // 1. Download bytes from GIPHY
            val bytesResult = deviceRepository.downloadGifBytes(item.gifUrl)
            val bytes = bytesResult.getOrElse {
                _uiState.update { it.copy(isMediaLoading = false, error = "Falha ao baixar GIF do GIPHY") }
                return@launch
            }
            // 2. Upload to Mica Device
            val mediaId = "giphy-${item.id}.gif"
            uploadMedia(deviceId, mediaId, bytes)
        }
    }

    /**
     * Save panel to the server catalog, and if it's active on a device, push there too.
     *
     * [onSaved] is invoked on the main thread after the catalog write succeeds.
     * Pass [onNavigateBack] here so navigation only fires after the HTTP call
     * completes — calling it synchronously after this function would cancel the
     * coroutine when the back-stack entry is removed.
     */
    fun savePanel(onSaved: (() -> Unit)? = null) {
        val panel = _uiState.value.panelResponse?.panel?.copy(updatedAtUtc = Instant.now().toString()) ?: return
        val deviceId = _uiState.value.selectedDeviceId
        viewModelScope.launch {
            _uiState.update { it.copy(isPanelLoading = true) }
            panelRepository.upsertCatalogPanel(panel)
                .onSuccess {
                    // Also push to device if panel is active on one
                    if (!deviceId.isNullOrBlank()) {
                        panelRepository.uploadPanel(deviceId, panel)
                    }
                    _uiState.update { state ->
                        val panelInCatalog = state.catalogPanels.any { it.panel.panelId == panel.panelId }
                        val updatedCatalog = if (panelInCatalog) {
                            state.catalogPanels.map { entry ->
                                if (entry.panel.panelId == panel.panelId) entry.copy(panel = panel) else entry
                            }
                        } else {
                            state.catalogPanels + CatalogPanelResponse(panel = panel)
                        }
                        state.copy(
                            isPanelLoading = false,
                            panelResponse = state.panelResponse?.copy(panel = panel),
                            catalogPanels = updatedCatalog,
                        )
                    }
                    onSaved?.invoke()
                }
                .onFailure { e ->
                    _uiState.update { it.copy(isPanelLoading = false, error = "Erro ao salvar: ${e.message}") }
                }
        }
    }
}
