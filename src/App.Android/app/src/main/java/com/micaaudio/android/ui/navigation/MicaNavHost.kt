package com.micaaudio.android.ui.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.navigation.NavHostController
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.navArgument
import com.micaaudio.android.ui.screens.devices.DeviceDetailScreen
import com.micaaudio.android.ui.screens.devices.DevicesScreen
import com.micaaudio.android.ui.screens.panels.PanelEditorScreen
import com.micaaudio.android.ui.screens.panels.PanelsScreen
import com.micaaudio.android.ui.screens.panels.WidgetConfigScreen
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

        composable(MicaDestination.Panels.route) {
            PanelsScreen(
                onEditPanel = { panelId ->
                    navController.navigate("panel_editor/$panelId")
                },
                onCreatePanel = { panelId ->
                    navController.navigate("panel_editor/$panelId")
                },
            )
        }

        composable(
            route = "panel_editor/{panelId}",
            arguments = listOf(navArgument("panelId") { type = NavType.StringType }),
        ) { backStackEntry ->
            val panelId = backStackEntry.arguments?.getString("panelId") ?: return@composable
            // Share the gallery ViewModel so edits aren't lost when the editor
            // back-stack entry is removed and so the catalog stays in sync.
            val parentEntry = remember(backStackEntry) {
                navController.getBackStackEntry(MicaDestination.Panels.route)
            }
            val viewModel: com.micaaudio.android.ui.screens.panels.PanelsViewModel =
                androidx.hilt.navigation.compose.hiltViewModel(parentEntry)
            PanelEditorScreen(
                panelId = panelId,
                onNavigateBack = { navController.popBackStack() },
                onWidgetConfig = { widgetId ->
                    navController.navigate("widget_config/$panelId/$widgetId")
                },
                viewModel = viewModel,
            )
        }

        composable(
            route = "widget_config/{panelId}/{widgetId}",
            arguments = listOf(
                navArgument("panelId") { type = NavType.StringType },
                navArgument("widgetId") { type = NavType.StringType },
            ),
        ) { backStackEntry ->
            val panelId = backStackEntry.arguments?.getString("panelId") ?: return@composable
            val widgetId = backStackEntry.arguments?.getString("widgetId") ?: return@composable
            // Same shared ViewModel as the editor — panels route is the single owner.
            val parentEntry = remember(backStackEntry) {
                navController.getBackStackEntry(MicaDestination.Panels.route)
            }
            val viewModel: com.micaaudio.android.ui.screens.panels.PanelsViewModel =
                androidx.hilt.navigation.compose.hiltViewModel(parentEntry)
            WidgetConfigScreen(
                widgetId = widgetId,
                deviceId = viewModel.uiState.value.selectedDeviceId ?: "",
                viewModel = viewModel,
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
