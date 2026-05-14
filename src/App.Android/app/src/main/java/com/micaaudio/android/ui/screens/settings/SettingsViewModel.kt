package com.micaaudio.android.ui.screens.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.repository.DeviceRepository
import com.micaaudio.android.data.settings.AppSettings
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class SettingsUiState(
    val serverUrl: String = AppSettings.DEFAULT_SERVER_URL,
    val adminToken: String = "",
    val darkMode: String = "system",
    val dynamicColor: Boolean = true,
    val isSaving: Boolean = false,
    val isSuccess: Boolean = false,
    val testResult: String? = null,
)

@HiltViewModel
class SettingsViewModel @Inject constructor(
    private val appSettings: AppSettings,
    private val deviceRepository: DeviceRepository,
) : ViewModel() {

    private val _uiState = MutableStateFlow(SettingsUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState.asStateFlow()

    init {
        observeSettings()
    }

    private fun observeSettings() {
        viewModelScope.launch {
            appSettings.serverUrl.collect { url ->
                _uiState.update { it.copy(serverUrl = url) }
            }
        }
        viewModelScope.launch {
            appSettings.adminToken.collect { token ->
                _uiState.update { it.copy(adminToken = token) }
            }
        }
        viewModelScope.launch {
            appSettings.darkMode.collect { mode ->
                _uiState.update { it.copy(darkMode = mode) }
            }
        }
        viewModelScope.launch {
            appSettings.dynamicColor.collect { enabled ->
                _uiState.update { it.copy(dynamicColor = enabled) }
            }
        }
    }

    fun updateServerUrl(url: String) {
        // We don't update state here, we let the collector do it after saving or just keep it local until save
        _uiState.update { it.copy(serverUrl = url) }
    }

    fun updateAdminToken(token: String) {
        _uiState.update { it.copy(adminToken = token) }
    }

    fun updateDarkMode(mode: String) {
        viewModelScope.launch { appSettings.setDarkMode(mode) }
    }

    fun updateDynamicColor(enabled: Boolean) {
        viewModelScope.launch { appSettings.setDynamicColor(enabled) }
    }

    fun saveConnection() {
        viewModelScope.launch {
            _uiState.update { it.copy(isSaving = true, isSuccess = false) }
            appSettings.setServerUrl(_uiState.value.serverUrl)
            appSettings.setAdminToken(_uiState.value.adminToken)
            _uiState.update { it.copy(isSaving = false, isSuccess = true, testResult = "Conexão salva com sucesso!") }

            // Reset success icon after 2 seconds
            kotlinx.coroutines.delay(2000)
            _uiState.update { it.copy(isSuccess = false) }
        }
    }

    fun testConnection() {
        viewModelScope.launch {
            _uiState.update { it.copy(isSaving = true) }
            deviceRepository.getDevices()
                .onSuccess { devices ->
                    _uiState.update { it.copy(isSaving = false, testResult = "Conectado — ${devices.size} dispositivo(s)") }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(isSaving = false, testResult = "Falhou: ${e.message}") }
                }
        }
    }

    fun dismissTestResult() {
        _uiState.update { it.copy(testResult = null) }
    }
}
