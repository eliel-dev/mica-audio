package com.micaaudio.android.data.repository

import android.content.Context
import com.micaaudio.android.data.api.AppCatalogDocument
import com.micaaudio.android.data.api.AppCatalogItem
import com.micaaudio.android.data.api.MicaServerApi
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.serialization.json.Json
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class AppCatalogRepository @Inject constructor(
    private val api: MicaServerApi,
    @ApplicationContext private val context: Context,
) {
    private val json = Json { ignoreUnknownKeys = true }
    private var cache: List<AppCatalogItem>? = null

    suspend fun getCatalog(): Result<List<AppCatalogItem>> = runCatching {
        cache?.let { return Result.success(it) }
        val items = fetchFromServer() ?: loadFromAssets()
        cache = items
        items
    }

    fun invalidateCache() {
        cache = null
    }

    private suspend fun fetchFromServer(): List<AppCatalogItem>? = runCatching {
        val response = api.getApps()
        if (response.isSuccessful) {
            val apps = response.body()?.apps
            if (!apps.isNullOrEmpty()) apps else null
        } else null
    }.getOrNull()

    private fun loadFromAssets(): List<AppCatalogItem> {
        val text = context.assets.open("apps-catalog.seed.json").bufferedReader().readText()
        return json.decodeFromString<AppCatalogDocument>(text).apps
    }
}
