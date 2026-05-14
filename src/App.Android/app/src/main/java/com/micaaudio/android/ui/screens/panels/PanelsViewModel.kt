package com.micaaudio.android.ui.screens.panels

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.api.AppCatalogItem
import com.micaaudio.android.data.api.CatalogPanelResponse
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.data.api.PanelDefinition
import com.micaaudio.android.data.api.PanelWidgetDefinition
import com.micaaudio.android.data.api.ServerPanelResponse
import com.micaaudio.android.data.repository.AppCatalogRepository
import com.micaaudio.android.data.repository.DeviceRepository
import com.micaaudio.android.data.repository.PanelRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.util.UUID
import javax.inject.Inject

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
    val availableApps: List<AppCatalogItem> = emptyList(),

    // Kept for editor: per-device panels fetched on demand
    val devicePanels: Map<String, ServerPanelResponse?> = emptyMap(),
)

@HiltViewModel
class PanelsViewModel @Inject constructor(
    private val deviceRepository: DeviceRepository,
    private val panelRepository: PanelRepository,
    private val catalogRepository: AppCatalogRepository,
) : ViewModel() {

    private val _uiState = MutableStateFlow(PanelsUiState())
    val uiState: StateFlow<PanelsUiState> = _uiState.asStateFlow()

    init {
        refresh()
        loadCatalog()
    }

    private fun loadCatalog() {
        viewModelScope.launch {
            catalogRepository.getCatalog().onSuccess { apps ->
                _uiState.update { it.copy(availableApps = apps) }
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
                    // Update catalog entry to reflect new activeOnDeviceId.
                    _uiState.update { state ->
                        val updated = state.catalogPanels.map { entry ->
                            if (entry.panel.panelId == panel.panelId)
                                entry.copy(activeOnDeviceId = deviceId)
                            else if (entry.activeOnDeviceId == deviceId)
                                entry.copy(activeOnDeviceId = null)
                            else entry
                        }
                        state.copy(successMessage = "Painel '${panel.name}' ativado!", catalogPanels = updated)
                    }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(error = "Erro ao ativar: ${e.message}") }
                }
        }
    }

    fun dismissMessage() {
        _uiState.update { it.copy(successMessage = null, error = null) }
    }

    // ── Editor navigation ────────────────────────────────────────────────────

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
            val newWidget = PanelWidgetDefinition(
                widgetId = UUID.randomUUID().toString(),
                appId = appId,
                x = 0, y = 0, width = 32, height = 32,
                zIndex = (panel.widgets.maxOfOrNull { it.zIndex } ?: 0) + 1,
            )
            state.copy(
                selectedWidgetId = newWidget.widgetId,
                panelResponse = state.panelResponse.copy(panel = panel.copy(widgets = panel.widgets + newWidget)),
            )
        }
    }

    /**
     * Save panel to the server — uploads to device store AND upserts to catalog.
     * The catalog upsert is a side-effect inside HandleAdminUploadPanelAsync on the server.
     */
    fun savePanel() {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        val panel = _uiState.value.panelResponse?.panel ?: return
        viewModelScope.launch {
            _uiState.update { it.copy(isPanelLoading = true) }
            panelRepository.uploadPanel(deviceId, panel)
                .onSuccess {
                    _uiState.update { state ->
                        val updatedDevice = state.devicePanels + (deviceId to
                            ServerPanelResponse(deviceId = deviceId, panel = panel))
                        // Also reflect the update in catalog list (upsert or add).
                        val panelInCatalog = state.catalogPanels.any { it.panel.panelId == panel.panelId }
                        val updatedCatalog = if (panelInCatalog) {
                            state.catalogPanels.map { entry ->
                                if (entry.panel.panelId == panel.panelId)
                                    entry.copy(panel = panel, activeOnDeviceId = deviceId)
                                else entry
                            }
                        } else {
                            state.catalogPanels + CatalogPanelResponse(panel = panel, activeOnDeviceId = deviceId)
                        }
                        state.copy(
                            isPanelLoading = false,
                            successMessage = "Painel salvo!",
                            devicePanels = updatedDevice,
                            catalogPanels = updatedCatalog,
                        )
                    }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(isPanelLoading = false, error = "Erro ao salvar: ${e.message}") }
                }
        }
    }
}
