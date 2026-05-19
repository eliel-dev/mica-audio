package com.micaaudio.android.data.settings

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.floatPreferencesKey
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import com.micaaudio.android.data.audio.FrequencyScale
import com.micaaudio.android.data.audio.WeightingFilter
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "mica_settings")

// DOCS: docs/wiki/modules/visual-win2d.md#audiomotion-clone
// DOCS: docs/wiki/modules/paineis.md#editor-hub75
@Singleton
class AppSettings @Inject constructor(
    @ApplicationContext private val context: Context,
) {
    companion object {
        private val KEY_SERVER_URL = stringPreferencesKey("server_url")
        private val KEY_ADMIN_TOKEN = stringPreferencesKey("admin_token")
        private val KEY_GIPHY_API_KEY = stringPreferencesKey("giphy_api_key")
        private val KEY_DARK_MODE = stringPreferencesKey("dark_mode") // "system", "dark", "light"
        private val KEY_DYNAMIC_COLOR = booleanPreferencesKey("dynamic_color")

        private val KEY_VISUALIZER_GAIN = floatPreferencesKey("visualizer_gain")
        private val KEY_VISUALIZER_BOOST = floatPreferencesKey("visualizer_boost")
        private val KEY_VISUALIZER_RISE = floatPreferencesKey("visualizer_rise")
        private val KEY_VISUALIZER_FALL = floatPreferencesKey("visualizer_fall")
        private val KEY_VISUALIZER_FFT_SMOOTHING = floatPreferencesKey("visualizer_fft_smoothing")
        private val KEY_VISUALIZER_MAX_FREQ = floatPreferencesKey("visualizer_max_freq")
        private val KEY_VISUALIZER_BAR_COUNT = intPreferencesKey("visualizer_bar_count")
        private val KEY_VISUALIZER_MIN_FREQ = floatPreferencesKey("visualizer_min_freq")
        private val KEY_VISUALIZER_FREQ_SCALE = stringPreferencesKey("visualizer_freq_scale")
        private val KEY_VISUALIZER_WEIGHTING = stringPreferencesKey("visualizer_weighting")

        const val DEFAULT_SERVER_URL = "http://192.168.1.100:5272"
        const val DEFAULT_DARK_MODE = "system"
    }

    val serverUrl: Flow<String> = context.dataStore.data.map { prefs ->
        prefs[KEY_SERVER_URL] ?: DEFAULT_SERVER_URL
    }

    val adminToken: Flow<String> = context.dataStore.data.map { prefs ->
        prefs[KEY_ADMIN_TOKEN] ?: ""
    }

    val giphyApiKey: Flow<String> = context.dataStore.data.map { prefs ->
        prefs[KEY_GIPHY_API_KEY] ?: ""
    }

    val darkMode: Flow<String> = context.dataStore.data.map { prefs ->
        prefs[KEY_DARK_MODE] ?: DEFAULT_DARK_MODE
    }

    val dynamicColor: Flow<Boolean> = context.dataStore.data.map { prefs ->
        prefs[KEY_DYNAMIC_COLOR] ?: true
    }

    val visualizerGain: Flow<Float> = context.dataStore.data.map { prefs ->
        prefs[KEY_VISUALIZER_GAIN] ?: 2.0f
    }

    val visualizerBoost: Flow<Float> = context.dataStore.data.map { prefs ->
        prefs[KEY_VISUALIZER_BOOST] ?: 1.5f
    }

    val visualizerRise: Flow<Float> = context.dataStore.data.map { prefs ->
        prefs[KEY_VISUALIZER_RISE] ?: 0.8f
    }

    val visualizerFall: Flow<Float> = context.dataStore.data.map { prefs ->
        prefs[KEY_VISUALIZER_FALL] ?: 0.3f
    }

    /** FFT temporal smoothing — 0.09 matches the Windows "Centered Lines" preset default. */
    val visualizerFftSmoothing: Flow<Float> = context.dataStore.data.map { prefs ->
        prefs[KEY_VISUALIZER_FFT_SMOOTHING] ?: 0.09f
    }

    /** Frequência máxima analisada (Hz). Default 1000 Hz — igual ao preset "Centered Lines" do Windows. */
    val visualizerMaxFreq: Flow<Float> = context.dataStore.data.map { prefs ->
        prefs[KEY_VISUALIZER_MAX_FREQ] ?: 1000f
    }

    /** Número de barras exibidas. Default 71 — igual ao Windows "Centered Lines". */
    val visualizerBarCount: Flow<Int> = context.dataStore.data.map { prefs ->
        prefs[KEY_VISUALIZER_BAR_COUNT] ?: 71
    }

    /** Frequência mínima analisada (Hz). Default 20 Hz. */
    val visualizerMinFreq: Flow<Float> = context.dataStore.data.map { prefs ->
        prefs[KEY_VISUALIZER_MIN_FREQ] ?: 20f
    }

    /** Escala de frequência. Default Bark — psicoacusticamente mais precisa. */
    val visualizerFreqScale: Flow<FrequencyScale> = context.dataStore.data.map { prefs ->
        FrequencyScale.entries.firstOrNull { it.name == prefs[KEY_VISUALIZER_FREQ_SCALE] }
            ?: FrequencyScale.Bark
    }

    /** Filtro de ponderação. Default B — igual ao Windows "Centered Lines". */
    val visualizerWeighting: Flow<WeightingFilter> = context.dataStore.data.map { prefs ->
        WeightingFilter.entries.firstOrNull { it.name == prefs[KEY_VISUALIZER_WEIGHTING] }
            ?: WeightingFilter.B
    }

    suspend fun setServerUrl(url: String) {
        context.dataStore.edit { it[KEY_SERVER_URL] = url.trim() }
    }

    suspend fun setAdminToken(token: String) {
        context.dataStore.edit { it[KEY_ADMIN_TOKEN] = token.trim() }
    }

    suspend fun setGiphyApiKey(key: String) {
        context.dataStore.edit { it[KEY_GIPHY_API_KEY] = key.trim() }
    }

    suspend fun setDarkMode(mode: String) {
        context.dataStore.edit { it[KEY_DARK_MODE] = mode }
    }

    suspend fun setDynamicColor(enabled: Boolean) {
        context.dataStore.edit { it[KEY_DYNAMIC_COLOR] = enabled }
    }

    suspend fun setVisualizerGain(value: Float) {
        context.dataStore.edit { it[KEY_VISUALIZER_GAIN] = value }
    }

    suspend fun setVisualizerBoost(value: Float) {
        context.dataStore.edit { it[KEY_VISUALIZER_BOOST] = value }
    }

    suspend fun setVisualizerRise(value: Float) {
        context.dataStore.edit { it[KEY_VISUALIZER_RISE] = value }
    }

    suspend fun setVisualizerFall(value: Float) {
        context.dataStore.edit { it[KEY_VISUALIZER_FALL] = value }
    }

    suspend fun setVisualizerFftSmoothing(value: Float) {
        context.dataStore.edit { it[KEY_VISUALIZER_FFT_SMOOTHING] = value }
    }

    suspend fun setVisualizerMaxFreq(value: Float) {
        context.dataStore.edit { it[KEY_VISUALIZER_MAX_FREQ] = value }
    }

    suspend fun setVisualizerBarCount(value: Int) {
        context.dataStore.edit { it[KEY_VISUALIZER_BAR_COUNT] = value }
    }

    suspend fun setVisualizerMinFreq(value: Float) {
        context.dataStore.edit { it[KEY_VISUALIZER_MIN_FREQ] = value }
    }

    suspend fun setVisualizerFreqScale(scale: FrequencyScale) {
        context.dataStore.edit { it[KEY_VISUALIZER_FREQ_SCALE] = scale.name }
    }

    suspend fun setVisualizerWeighting(filter: WeightingFilter) {
        context.dataStore.edit { it[KEY_VISUALIZER_WEIGHTING] = filter.name }
    }
}
