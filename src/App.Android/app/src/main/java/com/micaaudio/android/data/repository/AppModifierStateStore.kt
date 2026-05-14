package com.micaaudio.android.data.repository

import android.content.Context
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.firstOrNull
import kotlinx.coroutines.flow.map
import kotlinx.serialization.builtins.MapSerializer
import kotlinx.serialization.builtins.serializer
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

private val mapSerializer = MapSerializer(String.serializer(), String.serializer())

private val Context.appModifierStore by preferencesDataStore(name = "app_modifier_state")

@Singleton
class AppModifierStateStore @Inject constructor(
    @ApplicationContext private val context: Context,
) {
    private val json = Json { ignoreUnknownKeys = true }

    suspend fun load(appId: String): Map<String, String> {
        val key = stringPreferencesKey("app_config_$appId")
        val raw = context.appModifierStore.data.map { it[key] }.firstOrNull()
        return if (raw != null) {
            runCatching { json.decodeFromString<Map<String, String>>(raw) }.getOrDefault(emptyMap())
        } else emptyMap()
    }

    suspend fun save(appId: String, values: Map<String, String>) {
        val key = stringPreferencesKey("app_config_$appId")
        context.appModifierStore.edit { prefs ->
            prefs[key] = json.encodeToString(mapSerializer, values)
        }
    }
}
