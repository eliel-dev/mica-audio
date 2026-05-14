package com.micaaudio.android.ui.screens.devices

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.data.api.PairingCodeInfo
import com.micaaudio.android.data.repository.DeviceRepository
import com.micaaudio.android.data.websocket.AdminEventsSocket
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class DevicesUiState(
    val devices: List<DeviceSnapshot> = emptyList(),
    val isLoading: Boolean = true,
    val error: String? = null,
    val isRefreshing: Boolean = false,
    val pairingCode: PairingCodeInfo? = null,
    val isPairingLoading: Boolean = false,
    val isServerConnected: Boolean = false,
    val serverUrl: String = "",
)

@HiltViewModel
class DevicesViewModel @Inject constructor(
    private val deviceRepository: DeviceRepository,
    private val eventsSocket: AdminEventsSocket,
) : ViewModel() {

    private val _uiState = MutableStateFlow(DevicesUiState())
    val uiState: StateFlow<DevicesUiState> = _uiState.asStateFlow()

    init {
        loadDevices()
        observeEvents()
    }

    private fun observeEvents() {
        eventsSocket.start()

        viewModelScope.launch {
            eventsSocket.connected.collect { connected ->
                _uiState.update { it.copy(isServerConnected = connected) }
            }
        }

        viewModelScope.launch {
            eventsSocket.events.collect { event ->
                if (event.type == "devices_changed") {
                    loadDevices(isRefresh = true)
                }
            }
        }
    }

    fun loadDevices(isRefresh: Boolean = false) {
        viewModelScope.launch {
            _uiState.update {
                if (isRefresh) it.copy(isRefreshing = true)
                else it.copy(isLoading = true, error = null)
            }

            deviceRepository.getDevices()
                .onSuccess { devices ->
                    _uiState.update {
                        it.copy(
                            devices = devices.sortedByDescending { d -> d.isConnected },
                            isLoading = false,
                            isRefreshing = false,
                            error = null,
                        )
                    }
                }
                .onFailure { e ->
                    _uiState.update {
                        it.copy(
                            isLoading = false,
                            isRefreshing = false,
                            error = e.message ?: "Erro ao carregar dispositivos",
                        )
                    }
                }
        }
    }

    fun createPairingCode() {
        viewModelScope.launch {
            _uiState.update { it.copy(isPairingLoading = true) }
            deviceRepository.createPairingCode(ttlSeconds = 120)
                .onSuccess { code ->
                    _uiState.update { it.copy(pairingCode = code, isPairingLoading = false) }
                }
                .onFailure {
                    _uiState.update { it.copy(isPairingLoading = false) }
                }
        }
    }

    fun dismissPairingCode() {
        _uiState.update { it.copy(pairingCode = null) }
    }

    fun removeDevice(deviceId: String) {
        viewModelScope.launch {
            deviceRepository.removeDevice(deviceId)
            loadDevices(isRefresh = true)
        }
    }

    override fun onCleared() {
        super.onCleared()
        eventsSocket.stop()
    }
}
