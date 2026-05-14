package com.micaaudio.android.ui.navigation

import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.navArgument
import com.micaaudio.android.ui.screens.apps.AppCatalogScreen
import com.micaaudio.android.ui.screens.apps.AppDetailScreen
import com.micaaudio.android.ui.screens.devices.DeviceDetailScreen
import com.micaaudio.android.ui.screens.devices.DevicesScreen
import com.micaaudio.android.ui.screens.panels.PanelEditorScreen
import com.micaaudio.android.ui.screens.panels.PanelsScreen
import com.micaaudio.android.ui.screens.settings.SettingsScreen
import com.micaaudio.android.ui.screens.visualizer.VisualizerScreen

@Composable
fun MicaNavHost(
    navController: NavHostController,
    modifier: Modifier = Modifier,
) {
    NavHost(
        navController = navController,
        startDestination = MicaDestination.Panels.route,
        modifier = modifier,
    ) {
        composable(MicaDestination.Devices.route) {
            DevicesScreen(
                onDeviceClick = { deviceId ->
                    navController.navigate("device_detail/$deviceId")
                },
            )
        }

        composable(
            route = "device_detail/{deviceId}",
            arguments = listOf(navArgument("deviceId") { type = NavType.StringType }),
        ) { backStackEntry ->
            val deviceId = backStackEntry.arguments?.getString("deviceId") ?: return@composable
            DeviceDetailScreen(
                deviceId = deviceId,
                onNavigateBack = { navController.popBackStack() },
            )
        }

        composable(MicaDestination.Apps.route) {
            AppCatalogScreen(
                onAppDetail = { appId -> navController.navigate("app_detail/$appId") },
            )
        }

        composable(
            route = "app_detail/{appId}",
            arguments = listOf(navArgument("appId") { type = NavType.StringType }),
        ) { backStackEntry ->
            val appId = backStackEntry.arguments?.getString("appId") ?: return@composable
            AppDetailScreen(
                appId = appId,
                onNavigateBack = { navController.popBackStack() },
            )
        }

        composable(MicaDestination.Panels.route) {
            PanelsScreen(
                onEditPanel = { deviceId ->
                    navController.navigate("panel_editor/$deviceId")
                },
            )
        }

        composable(
            route = "panel_editor/{deviceId}",
            arguments = listOf(navArgument("deviceId") { type = NavType.StringType }),
        ) { backStackEntry ->
            val deviceId = backStackEntry.arguments?.getString("deviceId") ?: return@composable
            PanelEditorScreen(
                deviceId = deviceId,
                onNavigateBack = { navController.popBackStack() },
            )
        }

        composable(MicaDestination.Visualizer.route) {
            VisualizerScreen()
        }

        composable(MicaDestination.Settings.route) {
            SettingsScreen()
        }
    }
}
