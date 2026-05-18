package com.micaaudio.android.ui.screens.visualizer

import android.content.Context
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.data.audio.AudioCaptureService
import com.micaaudio.android.data.audio.FrequencyScale
import com.micaaudio.android.data.audio.SpectrumState
import com.micaaudio.android.data.audio.WeightingFilter
import com.micaaudio.android.data.repository.DeviceRepository
import com.micaaudio.android.data.settings.AppSettings
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
    /** True only when HUB75 streaming is active (not just local visualisation). */
    val isVisualizerActive: Boolean = false,
    val brightness: Int = 128,
    // FFT processing — defaults match Windows "Centered Lines" preset
    // Gain/LinearBoost/Rise/Fall are hardcoded in AudioCaptureService (not user-configurable,
    // matching Windows where LinearBoost is fixed and there is no sensitivity UI).
    val fftSmoothing: Float = 0.09f,
    val minFrequencyHz: Float = 20f,
    val maxFrequencyHz: Float = 1000f,
    val barCount: Int = 71,
    val frequencyScale: FrequencyScale = FrequencyScale.Bark,
    val weightingFilter: WeightingFilter = WeightingFilter.B,
    val showFftConfig: Boolean = false,
    val error: String? = null,
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
        checkServiceStatus()
        collectSettings()
    }

    private fun collectSettings() {
        // Gain/Boost/Rise/Fall are collected directly by AudioCaptureService from AppSettings.
        // The ViewModel only surfaces the FFT analysis parameters that appear in the UI.
        viewModelScope.launch { appSettings.visualizerFftSmoothing.collect { v -> _uiState.update { it.copy(fftSmoothing = v) } } }
        viewModelScope.launch { appSettings.visualizerMaxFreq.collect      { v -> _uiState.update { it.copy(maxFrequencyHz = v) } } }
        viewModelScope.launch { appSettings.visualizerMinFreq.collect      { v -> _uiState.update { it.copy(minFrequencyHz = v) } } }
        viewModelScope.launch { appSettings.visualizerBarCount.collect     { v -> _uiState.update { it.copy(barCount = v) } } }
        viewModelScope.launch { appSettings.visualizerFreqScale.collect    { v -> _uiState.update { it.copy(frequencyScale = v) } } }
        viewModelScope.launch { appSettings.visualizerWeighting.collect    { v -> _uiState.update { it.copy(weightingFilter = v) } } }
    }

    private fun checkServiceStatus() {
        _uiState.update { it.copy(isVisualizerActive = AudioCaptureService.isStreaming) }
    }

    /**
     * Start local-only audio visualisation (no HUB75 streaming).
     * Safe to call even if the service is already running — it just does nothing.
     */
    fun startLocalVisualization() {
        if (!AudioCaptureService.isRunning) {
            AudioCaptureService.start(context, deviceId = null)
        }
    }

    private fun loadDevices() {
        viewModelScope.launch {
            deviceRepository.getDevices()
                .onSuccess { devices ->
                    val connected = devices.filter { it.isConnected }
                    val selectedDevice = connected.find { it.deviceId == _uiState.value.selectedDeviceId }
                        ?: connected.firstOrNull()

                    _uiState.update {
                        it.copy(
                            devices = connected,
                            selectedDeviceId = selectedDevice?.deviceId,
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

    /**
     * Toggle HUB75 streaming to the selected device.
     * The [AudioCaptureService] stays alive in local-only mode when streaming is turned off,
     * so the on-screen canvas continues to update. Only RECORD_AUDIO is required.
     */
    fun toggleVisualizer() {
        val deviceId = _uiState.value.selectedDeviceId ?: return

        if (AudioCaptureService.isStreaming) {
            // Turn off streaming, keep local visualisation running
            AudioCaptureService.setStreamingDevice(null)
            _uiState.update { it.copy(isVisualizerActive = false) }
            viewModelScope.launch {
                deviceRepository.sendCommand(
                    deviceId = deviceId,
                    commandType = 6,
                    parameters = mapOf("appId" to CLOCK_APP_ID),
                )
            }
        } else {
            // Start streaming (start service if not already running in local mode)
            if (!AudioCaptureService.isRunning) {
                AudioCaptureService.start(context, deviceId)
            } else {
                AudioCaptureService.setStreamingDevice(deviceId)
            }
            _uiState.update { it.copy(isVisualizerActive = true) }
            viewModelScope.launch {
                deviceRepository.sendCommand(
                    deviceId = deviceId,
                    commandType = 6,
                    parameters = mapOf("appId" to VISUALIZER_APP_ID),
                )
            }
        }
    }

    fun setBrightness(brightness: Int) {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        _uiState.update { it.copy(brightness = brightness) }
        viewModelScope.launch {
            deviceRepository.setBrightness(deviceId, brightness)
        }
    }

    fun setFftSmoothing(value: Float) {
        val sanitized = value.coerceIn(0f, 0.98f)
        _uiState.update { it.copy(fftSmoothing = sanitized) }
        viewModelScope.launch { appSettings.setVisualizerFftSmoothing(sanitized) }
    }

    fun setMaxFrequency(value: Float) {
        val sanitized = value.coerceIn(500f, 20_000f)
        _uiState.update { it.copy(maxFrequencyHz = sanitized) }
        viewModelScope.launch { appSettings.setVisualizerMaxFreq(sanitized) }
    }

    fun setMinFrequency(value: Float) {
        val sanitized = value.coerceIn(10f, 500f)
        _uiState.update { it.copy(minFrequencyHz = sanitized) }
        viewModelScope.launch { appSettings.setVisualizerMinFreq(sanitized) }
    }

    fun setBarCount(value: Int) {
        val clamped = value.coerceIn(10, 256)
        _uiState.update { it.copy(barCount = clamped) }
        viewModelScope.launch { appSettings.setVisualizerBarCount(clamped) }
    }

    fun setFrequencyScale(scale: FrequencyScale) {
        _uiState.update { it.copy(frequencyScale = scale) }
        viewModelScope.launch { appSettings.setVisualizerFreqScale(scale) }
    }

    fun setWeightingFilter(filter: WeightingFilter) {
        _uiState.update { it.copy(weightingFilter = filter) }
        viewModelScope.launch { appSettings.setVisualizerWeighting(filter) }
    }

    fun toggleFftConfig() {
        _uiState.update { it.copy(showFftConfig = !it.showFftConfig) }
    }

    /**
     * Reset all FFT analysis settings to the Windows VisualizerRuntimeDefaults values.
     * Mirrors the "Restaurar padrão" button in the Windows FFT settings dialog.
     */
    fun resetFftDefaults() {
        // Windows VisualizerRuntimeDefaults values
        val defaultSmoothing = 0.75f
        val defaultBarCount = 38
        val defaultMinHz = 20f
        val defaultMaxHz = 1000f
        val defaultScale = FrequencyScale.Bark
        val defaultWeighting = WeightingFilter.B

        _uiState.update {
            it.copy(
                fftSmoothing = defaultSmoothing,
                barCount = defaultBarCount,
                minFrequencyHz = defaultMinHz,
                maxFrequencyHz = defaultMaxHz,
                frequencyScale = defaultScale,
                weightingFilter = defaultWeighting,
            )
        }
        viewModelScope.launch {
            appSettings.setVisualizerFftSmoothing(defaultSmoothing)
            appSettings.setVisualizerBarCount(defaultBarCount)
            appSettings.setVisualizerMinFreq(defaultMinHz)
            appSettings.setVisualizerMaxFreq(defaultMaxHz)
            appSettings.setVisualizerFreqScale(defaultScale)
            appSettings.setVisualizerWeighting(defaultWeighting)
        }
    }

    companion object {
        private const val VISUALIZER_APP_ID = "visualizer"
        private const val CLOCK_APP_ID = "clock"
    }
}
