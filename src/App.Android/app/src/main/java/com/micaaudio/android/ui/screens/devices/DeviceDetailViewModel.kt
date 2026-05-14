package com.micaaudio.android.ui.screens.devices

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.api.DeviceLogMessage
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.data.repository.DeviceRepository
import com.micaaudio.android.data.websocket.AdminEventsSocket
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

enum class DeviceDetailTab { Info, Logs, Control }

data class DeviceDetailUiState(
    val device: DeviceSnapshot? = null,
    val isLoading: Boolean = true,
    val commandResult: String? = null,
    val activeCommandId: String? = null,
    val commandStatus: String? = null,
    val commandPercent: Int = 0,
    val commandInProgress: Boolean = false,
    val logs: List<DeviceLogMessage> = emptyList(),
    val selectedTab: DeviceDetailTab = DeviceDetailTab.Info,
    val availableVersion: String? = null,
    val showUpdateBanner: Boolean = false,
)

@HiltViewModel
class DeviceDetailViewModel @Inject constructor(
    private val deviceRepository: DeviceRepository,
    private val eventsSocket: AdminEventsSocket,
) : ViewModel() {

    private val _uiState = MutableStateFlow(DeviceDetailUiState())
    val uiState: StateFlow<DeviceDetailUiState> = _uiState.asStateFlow()

    private var currentDeviceId: String = ""

    init {
        viewModelScope.launch {
            eventsSocket.events.collect { event ->
                val log = event.deviceLog ?: return@collect
                if (log.deviceId != currentDeviceId) return@collect
                _uiState.update { state ->
                    val updated = (state.logs + log).takeLast(200)
                    state.copy(logs = updated)
                }
            }
        }
        viewModelScope.launch {
            eventsSocket.commandProgress.collect { progress ->
                val current = _uiState.value
                if (current.activeCommandId != null && progress.commandId != current.activeCommandId) return@collect
                if (progress.deviceId != currentDeviceId) return@collect
                val percent = when (progress.status) {
                    "success" -> 100
                    "failed", "timeout" -> current.commandPercent
                    else -> current.commandPercent.coerceAtLeast(5)
                }
                _uiState.update {
                    it.copy(
                        commandStatus = progress.detail ?: progress.status,
                        commandPercent = percent,
                        commandInProgress = progress.status != "success" && progress.status != "failed" && progress.status != "timeout",
                    )
                }
            }
        }
    }

    fun loadDevice(deviceId: String) {
        currentDeviceId = deviceId
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }
            deviceRepository.getDevices()
                .onSuccess { devices ->
                    val device = devices.find { it.deviceId == deviceId }
                    _uiState.update { it.copy(device = device, isLoading = false) }
                }
                .onFailure {
                    _uiState.update { it.copy(isLoading = false) }
                }
        }
    }

    fun selectTab(tab: DeviceDetailTab) {
        _uiState.update { it.copy(selectedTab = tab) }
    }

    fun setBrightness(deviceId: String, brightness: Int) {
        viewModelScope.launch {
            deviceRepository.setBrightness(deviceId, brightness)
                .onSuccess { _uiState.update { it.copy(commandResult = "Brilho atualizado") } }
                .onFailure { e -> _uiState.update { it.copy(commandResult = "Erro: ${e.message}") } }
        }
    }

    fun testLed(deviceId: String) {
        viewModelScope.launch {
            deviceRepository.testLed(deviceId)
                .onSuccess { _uiState.update { it.copy(commandResult = "Test LED enviado") } }
                .onFailure { e -> _uiState.update { it.copy(commandResult = "Erro: ${e.message}") } }
        }
    }

    fun removeDevice(deviceId: String) {
        viewModelScope.launch {
            deviceRepository.removeDevice(deviceId)
        }
    }

    fun checkFirmwareUpdate(deviceId: String) {
        val device = _uiState.value.device ?: return
        val board = device.boardModel ?: return
        val panelType = device.panelType ?: return
        val profile = device.profile
        viewModelScope.launch {
            runCatching {
                val response = deviceRepository.getFirmwareManifest(board, panelType, profile)
                response?.let { manifest ->
                    val current = device.firmwareVersion
                    val available = manifest.firmwareVersion
                    val isNewer = available.isNotBlank() && available != current
                    _uiState.update { it.copy(availableVersion = if (isNewer) available else null, showUpdateBanner = isNewer) }
                }
            }
        }
    }

    fun triggerOtaUpdate(deviceId: String) {
        val target = _uiState.value.availableVersion ?: ""
        _uiState.update { it.copy(commandInProgress = true, commandStatus = "Iniciando OTA...", commandPercent = 2) }
        viewModelScope.launch {
            deviceRepository.updateFirmware(deviceId, target)
                .onSuccess { result ->
                    _uiState.update { it.copy(activeCommandId = result.commandId, commandStatus = "Aguardando progresso...") }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(commandInProgress = false, commandStatus = "Erro: ${e.message}") }
                }
        }
    }

    fun clearCommandStatus() {
        _uiState.update { it.copy(commandStatus = null, commandPercent = 0, commandInProgress = false, activeCommandId = null) }
    }
}
