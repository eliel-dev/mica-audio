package com.micaaudio.android.data.api

import okhttp3.Interceptor
import okhttp3.Response
import com.micaaudio.android.data.settings.AppSettings
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.runBlocking
import javax.inject.Inject
import javax.inject.Singleton

/**
 * OkHttp interceptor that adds the Bearer admin token to every request.
 * Reads the token from DataStore on each call so changes take effect immediately.
 */
@Singleton
class AuthInterceptor @Inject constructor(
    private val appSettings: AppSettings,
) : Interceptor {

    override fun intercept(chain: Interceptor.Chain): Response {
        val token = runBlocking { appSettings.adminToken.first() }
        val request = if (token.isNotBlank()) {
            chain.request().newBuilder()
                .header("Authorization", "Bearer $token")
                .build()
        } else {
            chain.request()
        }
        return chain.proceed(request)
    }
}
