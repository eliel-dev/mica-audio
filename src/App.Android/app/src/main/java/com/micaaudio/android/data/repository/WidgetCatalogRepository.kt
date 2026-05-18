package com.micaaudio.android.data.repository

import android.content.Context
import com.micaaudio.android.data.api.MicaServerApi
import com.micaaudio.android.data.api.WidgetCatalogDocument
import com.micaaudio.android.data.api.WidgetDefinition
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class WidgetCatalogRepository @Inject constructor(
    private val api: MicaServerApi,
    @ApplicationContext private val context: Context,
) {
    private val json = Json { ignoreUnknownKeys = true }
    private var cache: List<WidgetDefinition>? = null

    suspend fun getWidgets(): Result<List<WidgetDefinition>> = runCatching {
        cache?.let { return Result.success(it) }
        val items = fetchFromServer() ?: loadFromAssets()
        cache = items
        items
    }

    fun invalidateCache() {
        cache = null
    }

    private suspend fun fetchFromServer(): List<WidgetDefinition>? = runCatching {
        val response = api.getWidgets()
        if (response.isSuccessful) {
            val widgets = response.body()?.apps
            if (!widgets.isNullOrEmpty()) widgets else null
        } else null
    }.getOrNull()

    private fun loadFromAssets(): List<WidgetDefinition> {
        val text = context.assets.open("apps-catalog.seed.json").bufferedReader().readText()
        return json.decodeFromString<WidgetCatalogDocument>(text).apps
    }
}
