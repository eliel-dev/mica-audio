package com.micaaudio.android.ui.screens.visualizer

import android.content.Context
import android.content.Intent
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.data.audio.AudioCaptureService
import com.micaaudio.android.data.audio.SpectrumState
import com.micaaudio.android.data.repository.DeviceRepository
import com.micaaudio.android.data.settings.AppSettings
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class VisualizerUiState(
    val devices: List<DeviceSnapshot> = emptyList(),
    val selectedDeviceId: String? = null,
    val isLoading: Boolean = true,
    val isVisualizerActive: Boolean = false,
    val brightness: Int = 128,
    val fftSmoothing: Float = 0.8f,
    val gain: Float = 2.0f,
    val linearBoost: Float = 1.5f,
    val riseSpeed: Float = 0.8f,
    val fallSpeed: Float = 0.3f,
    val error: String? = null,
    val needsMediaProjection: Boolean = false,
    val showSensitivityDialog: Boolean = false,
    val showFftConfig: Boolean = false,
)

@HiltViewModel
class VisualizerViewModel @Inject constructor(
    private val deviceRepository: DeviceRepository,
    private val spectrumState: SpectrumState,
    private val appSettings: AppSettings,
    @ApplicationContext private val context: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow(VisualizerUiState())
    val uiState: StateFlow<VisualizerUiState> = _uiState.asStateFlow()

    val spectrumBins: StateFlow<FloatArray> = spectrumState.bins

    init {
        loadDevices()
        loadVisualizerSettings()
        checkServiceStatus()
    }

    private fun loadVisualizerSettings() {
        viewModelScope.launch {
            _uiState.update { it.copy(
                gain = appSettings.visualizerGain.first(),
                linearBoost = appSettings.visualizerBoost.first(),
                riseSpeed = appSettings.visualizerRise.first(),
                fallSpeed = appSettings.visualizerFall.first(),
            ) }
        }
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

                    _uiState.update { it.copy(
                        devices = connected,
                        selectedDeviceId = selectedId,
                        isLoading = false,
                        brightness = selectedDevice?.brightnessApplied ?: 128
                    ) }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(isLoading = false, error = e.message) }
                }
        }
    }

    fun selectDevice(deviceId: String) {
        val device = _uiState.value.devices.find { it.deviceId == deviceId }
        _uiState.update { it.copy(
            selectedDeviceId = deviceId,
            brightness = device?.brightnessApplied ?: 128
        ) }
    }

    fun toggleVisualizer() {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        val currentlyActive = AudioCaptureService.isRunning

        if (currentlyActive) {
            AudioCaptureService.stop(context)
            _uiState.update { it.copy(isVisualizerActive = false) }
            viewModelScope.launch {
                deviceRepository.sendCommand(deviceId, commandType = 6, parameters = mapOf("appId" to "clock"))
            }
        } else {
            _uiState.update { it.copy(needsMediaProjection = true) }
        }
    }

    fun onMediaProjectionGranted(resultCode: Int, data: Intent) {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        _uiState.update { it.copy(needsMediaProjection = false, isVisualizerActive = true) }

        viewModelScope.launch {
            // 1. Tell server to switch device to visualizer mode
            deviceRepository.sendCommand(deviceId, commandType = 6, parameters = mapOf("appId" to "visualizer"))

            // 2. Start local capture
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

    fun setFftSmoothing(smoothing: Float) {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        _uiState.update { it.copy(fftSmoothing = smoothing) }
        viewModelScope.launch {
            deviceRepository.setAppConfig(
                deviceId = deviceId,
                parameters = mapOf("fftSmoothing" to smoothing.toString())
            )
        }
    }

    fun setGain(gain: Float) {
        _uiState.update { it.copy(gain = gain) }
        viewModelScope.launch { appSettings.setVisualizerGain(gain) }
    }

    fun setLinearBoost(boost: Float) {
        _uiState.update { it.copy(linearBoost = boost) }
        viewModelScope.launch { appSettings.setVisualizerBoost(boost) }
    }

    fun setRiseSpeed(rise: Float) {
        _uiState.update { it.copy(riseSpeed = rise) }
        viewModelScope.launch { appSettings.setVisualizerRise(rise) }
    }

    fun setFallSpeed(fall: Float) {
        _uiState.update { it.copy(fallSpeed = fall) }
        viewModelScope.launch { appSettings.setVisualizerFall(fall) }
    }

    fun toggleSensitivityDialog(show: Boolean) {
        _uiState.update { it.copy(showSensitivityDialog = show) }
    }

    fun toggleFftConfig(show: Boolean) {
        _uiState.update { it.copy(showFftConfig = show) }
    }

    fun resetSensitivity() {
        viewModelScope.launch {
            appSettings.setVisualizerGain(2.0f)
            appSettings.setVisualizerBoost(1.5f)
            appSettings.setVisualizerRise(0.8f)
            appSettings.setVisualizerFall(0.3f)
            loadVisualizerSettings()
        }
    }

    fun nextPreset() {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        viewModelScope.launch {
            deviceRepository.sendCommand(deviceId, commandType = 7, parameters = mapOf("action" to "next_preset"))
        }
    }

    fun previousPreset() {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        viewModelScope.launch {
            deviceRepository.sendCommand(deviceId, commandType = 7, parameters = mapOf("action" to "prev_preset"))
        }
    }
}
