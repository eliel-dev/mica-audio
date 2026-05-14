package com.micaaudio.android.ui.theme

import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext

private val DarkColorScheme = darkColorScheme(
    primary = MicaPrimary,
    onPrimary = MicaOnPrimary,
    secondary = MicaSecondary,
    onSecondary = MicaOnSecondary,
    tertiary = MicaTertiary,
    onTertiary = MicaOnTertiary,
    background = MicaDarkBackground,
    onBackground = MicaDarkOnBackground,
    surface = MicaDarkSurface,
    onSurface = MicaDarkOnSurface,
    surfaceVariant = MicaDarkSurfaceVariant,
    onSurfaceVariant = MicaDarkOnSurfaceVariant,
    surfaceContainer = MicaDarkSurfaceContainer,
    surfaceContainerHigh = MicaDarkSurfaceContainerHigh,
    outline = MicaDarkOutline,
    error = StatusError,
)

private val LightColorScheme = lightColorScheme(
    primary = MicaPrimary,
    onPrimary = MicaOnPrimary,
    secondary = MicaSecondary,
    onSecondary = MicaOnSecondary,
    tertiary = MicaTertiary,
    onTertiary = MicaOnTertiary,
    background = MicaLightBackground,
    onBackground = MicaLightOnBackground,
    surface = MicaLightSurface,
    onSurface = MicaLightOnSurface,
    surfaceVariant = MicaLightSurfaceVariant,
    onSurfaceVariant = MicaLightOnSurfaceVariant,
    surfaceContainer = MicaLightSurfaceContainer,
    surfaceContainerHigh = MicaLightSurfaceContainerHigh,
    outline = MicaLightOutline,
    error = StatusError,
)

@Composable
fun MicaAudioTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    dynamicColor: Boolean = true,
    content: @Composable () -> Unit,
) {
    val colorScheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context)
            else dynamicLightColorScheme(context)
        }
        darkTheme -> DarkColorScheme
        else -> LightColorScheme
    }

    MaterialTheme(
        colorScheme = colorScheme,
        typography = MicaTypography,
        content = content,
    )
}
