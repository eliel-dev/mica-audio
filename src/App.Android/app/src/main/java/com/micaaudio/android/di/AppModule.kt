package com.micaaudio.android.di

import android.content.Context
import com.micaaudio.android.data.settings.AppSettings
import com.micaaudio.android.data.websocket.AdminEventsSocket
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object AppModule {

    @Provides
    @Singleton
    fun provideAppSettings(@ApplicationContext context: Context): AppSettings {
        return AppSettings(context)
    }

    @Provides
    @Singleton
    fun provideAdminEventsSocket(
        okHttpClient: okhttp3.OkHttpClient,
        appSettings: AppSettings,
    ): AdminEventsSocket {
        return AdminEventsSocket(okHttpClient, appSettings)
    }
}
