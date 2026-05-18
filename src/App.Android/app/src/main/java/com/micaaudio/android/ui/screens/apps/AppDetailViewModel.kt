package com.micaaudio.android.ui.screens.apps

import android.content.Context
import android.net.Uri
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.data.api.GiphyItem
import com.micaaudio.android.data.api.WidgetDefinition
import com.micaaudio.android.data.repository.AppModifierStateStore
import com.micaaudio.android.data.repository.DeviceRepository
import com.micaaudio.android.data.repository.WidgetCatalogRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import okhttp3.MediaType.Companion.toMediaTypeOrNull
import okhttp3.RequestBody.Companion.toRequestBody
import javax.inject.Inject

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#android-app-detail

// ── Upload state ─────────────────────────────────────────────────────────────

sealed interface GifUploadState {
    object Idle : GifUploadState
    data class Uploading(val progress: Float = 0f) : GifUploadState
    data class Done(val mediaId: String) : GifUploadState
    data class Error(val message: String) : GifUploadState
}

// ── UI state ─────────────────────────────────────────────────────────────────

data class AppDetailUiState(
    val app: WidgetDefinition? = null,
    val isLoading: Boolean = true,
    val modifierValues: Map<String, String> = emptyMap(),
    val devices: List<DeviceSnapshot> = emptyList(),
    val selectedDeviceId: String? = null,
    val isDeploying: Boolean = false,
    val deployResult: String? = null,
    val commandPercent: Int = 0,
    val gifUploadState: GifUploadState = GifUploadState.Idle,
    val activeCommandId: String? = null,
    // GIPHY search
    val giphyQuery: String = "",
    val giphyResults: List<GiphyItem> = emptyList(),
    val isGiphySearching: Boolean = false,
    val showGiphySheet: Boolean = false,
    val error: String? = null,
)

// ── ViewModel ─────────────────────────────────────────────────────────────────

@HiltViewModel
class AppDetailViewModel @Inject constructor(
    private val catalogRepository: WidgetCatalogRepository,
    private val modifierStore: AppModifierStateStore,
    private val deviceRepository: DeviceRepository,
    @ApplicationContext private val context: Context,
) : ViewModel() {

    private val _uiState = MutableStateFlow(AppDetailUiState())
    val uiState: StateFlow<AppDetailUiState> = _uiState.asStateFlow()

    init {
        collectCommandProgress()
        loadDevices()
    }

    private fun collectCommandProgress() {
        viewModelScope.launch {
            deviceRepository.commandProgress.collect { progress ->
                if (progress.commandId == _uiState.value.activeCommandId) {
                    val percent = when {
                        progress.status.equals("completed", ignoreCase = true) -> 100
                        progress.status.equals("failed", ignoreCase = true) -> 0
                        else -> _uiState.value.commandPercent
                    }
                    val result = when {
                        progress.status.equals("completed", ignoreCase = true) ->
                            "Concluído: ${progress.detail ?: "OK"}"
                        progress.status.equals("failed", ignoreCase = true) ->
                            "Falhou: ${progress.detail ?: "Erro desconhecido"}"
                        else -> null
                    }
                    _uiState.update { it.copy(
                        commandPercent = percent,
                        deployResult = result ?: it.deployResult,
                        isDeploying = progress.status.equals("pending", ignoreCase = true)
                            || progress.status.equals("running", ignoreCase = true),
                    ) }
                }
            }
        }
    }

    private fun loadDevices() {
        viewModelScope.launch {
            deviceRepository.getDevices().onSuccess { devices ->
                val connected = devices.filter { it.isConnected }
                _uiState.update { state ->
                    state.copy(
                        devices = connected,
                        selectedDeviceId = connected.find { it.deviceId == state.selectedDeviceId }?.deviceId
                            ?: connected.firstOrNull()?.deviceId,
                    )
                }
            }
        }
    }

    fun loadApp(appId: String) {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }
            catalogRepository.getWidgets().onSuccess { widgets ->
                val app = widgets.find { it.id.equals(appId, ignoreCase = true) }
                if (app != null) {
                    val saved = modifierStore.load(appId)
                    val defaults = buildDefaultModifiers(app)
                    val merged = defaults + saved
                    _uiState.update { it.copy(app = app, modifierValues = merged, isLoading = false) }
                } else {
                    _uiState.update { it.copy(isLoading = false, error = "App '$appId' não encontrado no catálogo.") }
                }
            }.onFailure { e ->
                _uiState.update { it.copy(isLoading = false, error = e.message) }
            }
        }
    }

    fun updateModifier(key: String, value: String) {
        _uiState.update { it.copy(modifierValues = it.modifierValues + (key to value)) }
        val appId = _uiState.value.app?.id ?: return
        viewModelScope.launch {
            modifierStore.save(appId, _uiState.value.modifierValues)
        }
    }

    fun selectDevice(deviceId: String) {
        _uiState.update { it.copy(selectedDeviceId = deviceId) }
    }

    fun deployApp() {
        val app = _uiState.value.app ?: return
        val deviceId = _uiState.value.selectedDeviceId ?: return
        viewModelScope.launch {
            _uiState.update { it.copy(isDeploying = true, deployResult = null, commandPercent = 0) }
            // Encode modifier values as JSON object: {"key":"value",...}
            val config = buildString {
                append("{")
                _uiState.value.modifierValues.entries.forEachIndexed { index, (k, v) ->
                    if (index > 0) append(",")
                    append("\"").append(k.replace("\"", "\\\"")).append("\":")
                    append("\"").append(v.replace("\"", "\\\"")).append("\"")
                }
                append("}")
            }
            deviceRepository.installApp(deviceId, app.id, config)
                .onSuccess { result ->
                    _uiState.update { it.copy(activeCommandId = result.commandId) }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(isDeploying = false, deployResult = "Falha: ${e.message}") }
                }
        }
    }

    fun activateApp() {
        val app = _uiState.value.app ?: return
        val deviceId = _uiState.value.selectedDeviceId ?: return
        viewModelScope.launch {
            deviceRepository.activateApp(deviceId, app.id)
                .onSuccess { _uiState.update { it.copy(deployResult = "App ativado.") } }
                .onFailure { e -> _uiState.update { it.copy(deployResult = "Falha ao ativar: ${e.message}") } }
        }
    }

    fun uploadGif(uri: Uri) {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        viewModelScope.launch {
            _uiState.update { it.copy(gifUploadState = GifUploadState.Uploading()) }
            runCatching {
                val bytes = context.contentResolver.openInputStream(uri)?.readBytes()
                    ?: throw Exception("Não foi possível ler o arquivo selecionado.")
                val body = bytes.toRequestBody("application/octet-stream".toMediaTypeOrNull())
                deviceRepository.uploadMedia(deviceId, "gif_primary.gif", body).getOrThrow()
            }.onSuccess {
                _uiState.update { it.copy(gifUploadState = GifUploadState.Done("gif_primary.gif")) }
            }.onFailure { e ->
                _uiState.update { it.copy(gifUploadState = GifUploadState.Error(e.message ?: "Erro desconhecido")) }
            }
        }
    }

    fun clearDeployResult() {
        _uiState.update { it.copy(deployResult = null) }
    }

    // ── GIPHY ─────────────────────────────────────────────────────────────────

    fun openGiphySheet() {
        _uiState.update { it.copy(showGiphySheet = true) }
    }

    fun closeGiphySheet() {
        _uiState.update { it.copy(showGiphySheet = false, giphyResults = emptyList(), giphyQuery = "") }
    }

    fun setGiphyQuery(query: String) {
        _uiState.update { it.copy(giphyQuery = query) }
    }

    fun searchGiphy() {
        val q = _uiState.value.giphyQuery.trim()
        if (q.isBlank()) return
        viewModelScope.launch {
            _uiState.update { it.copy(isGiphySearching = true, giphyResults = emptyList()) }
            deviceRepository.searchGiphy(q)
                .onSuccess { resp ->
                    _uiState.update { it.copy(isGiphySearching = false, giphyResults = resp.items) }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(isGiphySearching = false, error = "Busca GIPHY falhou: ${e.message}") }
                }
        }
    }

    fun selectGiphyItem(item: GiphyItem) {
        val deviceId = _uiState.value.selectedDeviceId ?: return
        _uiState.update { it.copy(showGiphySheet = false, gifUploadState = GifUploadState.Uploading()) }
        viewModelScope.launch {
            deviceRepository.downloadGifBytes(item.gifUrl)
                .mapCatching { bytes ->
                    val body = bytes.toRequestBody("application/octet-stream".toMediaTypeOrNull())
                    deviceRepository.uploadMedia(deviceId, "gif_primary.gif", body).getOrThrow()
                }
                .onSuccess {
                    _uiState.update { it.copy(gifUploadState = GifUploadState.Done("gif_primary.gif")) }
                }
                .onFailure { e ->
                    _uiState.update { it.copy(gifUploadState = GifUploadState.Error(e.message ?: "Erro no download")) }
                }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private fun buildDefaultModifiers(app: WidgetDefinition): Map<String, String> {
        return app.modifiers.associate { mod ->
            val default = when {
                mod.defaultToggle != null -> if (mod.defaultToggle) "true" else "false"
                mod.defaultValue != null -> mod.defaultValue
                else -> ""
            }
            mod.key to default
        }
    }
}
