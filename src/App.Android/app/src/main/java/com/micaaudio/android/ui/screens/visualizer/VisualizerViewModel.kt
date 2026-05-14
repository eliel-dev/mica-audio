package com.micaaudio.android.ui.screens.visualizer

import android.content.Context
import android.content.Intent
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.data.audio.AudioCaptureService
import com.micaaudio.android.data.audio.SpectrumState
import com.micaaudio.android.data.repository.DeviceRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

// DOCS: docs/wiki/modules/visual-win2d.md#audiomotion-clone
// DOCS: docs/wiki/reference/ws-protocol-v2.md#mensagem-tipo-1---bins128
data class VisualizerUiState(
    val devices: List<DeviceSnapshot> = emptyList(),
    val selectedDeviceId: String? = null,
    val isLoading: Boolean = true,
    val isVisualizerActive: Boolean = false,
    val brightness: Int = 128,
    val error: String? = null,
    val needsMediaProjection: Boolean = false,
)

@HiltViewModel
class VisualizerViewModel @Inject constructor(
    private val deviceRepository: DeviceRepository,
    private val spectrumState: SpectrumState,
    @ApplicationContext private val context: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow(VisualizerUiState())
    val uiState: StateFlow<VisualizerUiState> = _uiState.asStateFlow()

    val spectrumBins: StateFlow<FloatArray> = spectrumState.bins

    init {
        loadDevices()
        checkServiceStatus()
    }

    private fun checkServiceStatus() {
        _uiState.update { it.copy(isVisualizerActive = AudioCaptureService.isRunning) }
    }

    private fun loadDevices() {
        viewModelScope.launch {
            deviceRepository.getDevices()
                .onSuccess { devices ->
                    val connected = devices.filter { it.isConnected }
                    val selectedDevice = connected.find { it.deviceId == _uiState.value.selectedDeviceId }
                        ?: connected.firstOrNull()
                    val selectedId = selectedDevice?.deviceId

                    _uiState.update {
                        it.copy(
                            devices = connected,
                            selectedDeviceId = selectedId,
                            isLoading = false,
                            brightness = selectedDevice?.brightnessApplied ?: 128,
                        )
                    }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(isLoading = false, error = e.message) }
                }
        }
    }

    fun selectDevice(deviceId: String) {
        val device = _uiState.value.devices.find { it.deviceId == deviceId }
        _uiState.update {
            it.copy(
                selectedDeviceId = deviceId,
                brightness = device?.brightnessApplied ?: 128,
            )
        }
    }

    fun toggleVisualizer() {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        val currentlyActive = AudioCaptureService.isRunning

        if (currentlyActive) {
            AudioCaptureService.stop(context)
            _uiState.update { it.copy(isVisualizerActive = false) }
            viewModelScope.launch {
                deviceRepository.sendCommand(
                    deviceId = deviceId,
                    commandType = 6,
                    parameters = mapOf("appId" to CLOCK_APP_ID),
                )
            }
        } else {
            _uiState.update { it.copy(needsMediaProjection = true) }
        }
    }

    fun onMediaProjectionGranted(resultCode: Int, data: Intent) {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        _uiState.update { it.copy(needsMediaProjection = false, isVisualizerActive = true) }

        viewModelScope.launch {
            deviceRepository.sendCommand(
                deviceId = deviceId,
                commandType = 6,
                parameters = mapOf("appId" to VISUALIZER_APP_ID),
            )

            AudioCaptureService.start(context, resultCode, data, deviceId)
        }
    }

    fun onMediaProjectionRejected() {
        _uiState.update { it.copy(needsMediaProjection = false) }
    }

    fun setBrightness(brightness: Int) {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        _uiState.update { it.copy(brightness = brightness) }
        viewModelScope.launch {
            deviceRepository.setBrightness(deviceId, brightness)
        }
    }

    companion object {
        private const val VISUALIZER_APP_ID = "visualizer"
        private const val CLOCK_APP_ID = "clock"
    }
}
