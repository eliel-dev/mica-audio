package com.micaaudio.android.ui.screens.monitoring

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

data class MonitoringUiState(
    val devices: List<DeviceSnapshot> = emptyList(),
    val logs: List<DeviceLogMessage> = emptyList(),
    val isLoading: Boolean = true,
    val isConnected: Boolean = false,
)

@HiltViewModel
class MonitoringViewModel @Inject constructor(
    private val deviceRepository: DeviceRepository,
    private val eventsSocket: AdminEventsSocket,
) : ViewModel() {

    private val _uiState = MutableStateFlow(MonitoringUiState())
    val uiState: StateFlow<MonitoringUiState> = _uiState.asStateFlow()

    companion object {
        private const val MAX_LOGS = 200
    }

    init {
        loadDevices()
        observeEvents()
    }

    private fun loadDevices() {
        viewModelScope.launch {
            deviceRepository.getDevices()
                .onSuccess { devices ->
                    _uiState.update { it.copy(devices = devices, isLoading = false) }
                }
                .onFailure { _uiState.update { it.copy(isLoading = false) } }
        }
    }

    private fun observeEvents() {
        eventsSocket.start()
        viewModelScope.launch {
            eventsSocket.connected.collect { connected ->
                _uiState.update { it.copy(isConnected = connected) }
            }
        }
        viewModelScope.launch {
            eventsSocket.events.collect { event ->
                when (event.type) {
                    "device_log" -> event.deviceLog?.let { log ->
                        _uiState.update { state ->
                            val newLogs = (state.logs + log).takeLast(MAX_LOGS)
                            state.copy(logs = newLogs)
                        }
                    }
                    "devices_changed" -> loadDevices()
                }
            }
        }
    }

    fun clearLogs() {
        _uiState.update { it.copy(logs = emptyList()) }
    }

    fun refresh() {
        loadDevices()
    }
}
