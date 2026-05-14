package com.micaaudio.android

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.core.splashscreen.SplashScreen.Companion.installSplashScreen
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import com.micaaudio.android.data.settings.AppSettings
import com.micaaudio.android.ui.shell.MicaShell
import com.micaaudio.android.ui.theme.MicaAudioTheme
import dagger.hilt.android.AndroidEntryPoint
import javax.inject.Inject

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    @Inject
    lateinit var appSettings: AppSettings

    override fun onCreate(savedInstanceState: Bundle?) {
        installSplashScreen()
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            val darkMode by appSettings.darkMode.collectAsState(initial = AppSettings.DEFAULT_DARK_MODE)
            val dynamicColor by appSettings.dynamicColor.collectAsState(initial = true)

            val isDark = when (darkMode) {
                "dark" -> true
                "light" -> false
                else -> isSystemInDarkTheme()
            }

            MicaAudioTheme(darkTheme = isDark, dynamicColor = dynamicColor) {
                MicaShell()
            }
        }
    }
}
