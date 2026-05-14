package com.micaaudio.android.ui.screens.apps

import android.content.Context
import android.net.Uri
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.api.AppCatalogItem
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.data.repository.AppCatalogRepository
import com.micaaudio.android.data.repository.AppModifierStateStore
import com.micaaudio.android.data.repository.DeviceRepository
import com.micaaudio.android.data.websocket.AdminEventsSocket
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.serialization.builtins.MapSerializer
import kotlinx.serialization.builtins.serializer
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.RequestBody.Companion.toRequestBody
import javax.inject.Inject

private val mapSerializer = MapSerializer(String.serializer(), String.serializer())

sealed interface GifUploadState {
    object Idle : GifUploadState
    data class Uploading(val progress: Float) : GifUploadState
    data class Done(val mediaId: String) : GifUploadState
    data class Error(val message: String) : GifUploadState
}

data class AppDetailUiState(
    val app: AppCatalogItem? = null,
    val isLoading: Boolean = true,
    val modifierValues: Map<String, String> = emptyMap(),
    val devices: List<DeviceSnapshot> = emptyList(),
    val selectedDeviceId: String? = null,
    val isDeploying: Boolean = false,
    val deployResult: String? = null,
    val commandPercent: Int = 0,
    val gifUploadState: GifUploadState = GifUploadState.Idle,
    val activeCommandId: String? = null,
)

@HiltViewModel
class AppDetailViewModel @Inject constructor(
    private val catalogRepository: AppCatalogRepository,
    private val modifierStore: AppModifierStateStore,
    private val deviceRepository: DeviceRepository,
    private val eventsSocket: AdminEventsSocket,
    @ApplicationContext private val context: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow(AppDetailUiState())
    val uiState: StateFlow<AppDetailUiState> = _uiState.asStateFlow()

    init {
        viewModelScope.launch {
            eventsSocket.commandProgress.collect { progress ->
                val state = _uiState.value
                if (state.activeCommandId != null && progress.commandId != state.activeCommandId) return@collect
                val done = progress.status == "success" || progress.status == "failed" || progress.status == "timeout"
                val percent = if (progress.status == "success") 100 else state.commandPercent.coerceAtLeast(5)
                _uiState.update {
                    it.copy(
                        commandPercent = percent,
                        isDeploying = !done,
                        deployResult = if (done) progress.detail ?: progress.status else it.deployResult,
                    )
                }
            }
        }

        viewModelScope.launch {
            deviceRepository.getDevices()
                .onSuccess { devices ->
                    val connected = devices.filter { it.isConnected }
                    _uiState.update { it.copy(devices = connected, selectedDeviceId = it.selectedDeviceId ?: connected.firstOrNull()?.deviceId) }
                }
        }
    }

    fun loadApp(appId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }
            catalogRepository.getCatalog()
                .onSuccess { items ->
                    val app = items.find { it.id == appId }
                    if (app != null) {
                        val saved = modifierStore.load(appId)
                        val defaults = buildDefaultModifiers(app)
                        _uiState.update { it.copy(app = app, modifierValues = defaults + saved, isLoading = false) }
                    } else {
                        _uiState.update { it.copy(isLoading = false) }
                    }
                }
                .onFailure { _uiState.update { it.copy(isLoading = false) } }
        }
    }

    fun updateModifier(key: String, value: String) {
        _uiState.update { it.copy(modifierValues = it.modifierValues + (key to value)) }
    }

    fun selectDevice(deviceId: String) {
        _uiState.update { it.copy(selectedDeviceId = deviceId) }
    }

    fun deployApp() {
        val state = _uiState.value
        val app = state.app ?: return
        val deviceId = state.selectedDeviceId ?: return
        val configJson = Json.encodeToString(mapSerializer, state.modifierValues)

        _uiState.update { it.copy(isDeploying = true, deployResult = null, commandPercent = 2) }

        viewModelScope.launch {
            modifierStore.save(app.id, state.modifierValues)
            deviceRepository.installApp(deviceId, app.id, configJson)
                .onSuccess { result ->
                    _uiState.update { it.copy(activeCommandId = result.commandId, deployResult = "Instalando...") }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(isDeploying = false, deployResult = "Erro: ${e.message}") }
                }
        }
    }

    fun activateApp() {
        val state = _uiState.value
        val app = state.app ?: return
        val deviceId = state.selectedDeviceId ?: return

        viewModelScope.launch {
            deviceRepository.activateApp(deviceId, app.id)
                .onSuccess { _uiState.update { it.copy(deployResult = "App ativado") } }
                .onFailure { e -> _uiState.update { it.copy(deployResult = "Erro: ${e.message}") } }
        }
    }

    fun uploadGif(uri: Uri) {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        _uiState.update { it.copy(gifUploadState = GifUploadState.Uploading(0f)) }

        viewModelScope.launch {
            runCatching {
                val bytes = context.contentResolver.openInputStream(uri)?.readBytes()
                    ?: throw IllegalStateException("Não foi possível ler o arquivo")
                val body = bytes.toRequestBody("application/octet-stream".toMediaType())
                // Use the MicaServerApi directly via deviceRepository's api (need to expose or call via separate path)
                // For now delegate to a helper in DeviceRepository
                deviceRepository.uploadMedia(deviceId, "gif_primary", body)
            }.onSuccess {
                _uiState.update { it.copy(gifUploadState = GifUploadState.Done("gif_primary")) }
            }.onFailure { e ->
                _uiState.update { it.copy(gifUploadState = GifUploadState.Error(e.message ?: "Erro desconhecido")) }
            }
        }
    }

    fun clearDeployResult() {
        _uiState.update { it.copy(deployResult = null, commandPercent = 0, isDeploying = false, activeCommandId = null) }
    }

    private fun buildDefaultModifiers(app: AppCatalogItem): Map<String, String> {
        return app.modifiers.mapNotNull { mod ->
            val value = when {
                mod.defaultValue != null -> mod.key to mod.defaultValue
                mod.defaultToggle != null -> mod.key to mod.defaultToggle.toString()
                mod.options.isNotEmpty() -> mod.key to mod.options.first().value
                else -> null
            }
            value
        }.toMap()
    }
}
