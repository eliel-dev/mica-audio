package com.micaaudio.android.data.settings

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.floatPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "mica_settings")

@Singleton
class AppSettings @Inject constructor(
    @ApplicationContext private val context: Context,
) {
    companion object {
        private val KEY_SERVER_URL = stringPreferencesKey("server_url")
        private val KEY_ADMIN_TOKEN = stringPreferencesKey("admin_token")
        private val KEY_DARK_MODE = stringPreferencesKey("dark_mode") // "system", "dark", "light"
        private val KEY_DYNAMIC_COLOR = booleanPreferencesKey("dynamic_color")

        private val KEY_VISUALIZER_GAIN = floatPreferencesKey("visualizer_gain")
        private val KEY_VISUALIZER_BOOST = floatPreferencesKey("visualizer_boost")
        private val KEY_VISUALIZER_RISE = floatPreferencesKey("visualizer_rise")
        private val KEY_VISUALIZER_FALL = floatPreferencesKey("visualizer_fall")

        const val DEFAULT_SERVER_URL = "http://192.168.1.100:5272"
        const val DEFAULT_DARK_MODE = "system"
    }

    val serverUrl: Flow<String> = context.dataStore.data.map { prefs ->
        prefs[KEY_SERVER_URL] ?: DEFAULT_SERVER_URL
    }

    val adminToken: Flow<String> = context.dataStore.data.map { prefs ->
        prefs[KEY_ADMIN_TOKEN] ?: ""
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

    suspend fun setServerUrl(url: String) {
        context.dataStore.edit { it[KEY_SERVER_URL] = url.trim() }
    }

    suspend fun setAdminToken(token: String) {
        context.dataStore.edit { it[KEY_ADMIN_TOKEN] = token.trim() }
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
}
