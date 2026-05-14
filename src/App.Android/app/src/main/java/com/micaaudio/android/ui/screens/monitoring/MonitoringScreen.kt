package com.micaaudio.android.ui.screens.monitoring

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.ui.components.ServerConnectionBanner
import com.micaaudio.android.ui.components.TelemetryGauge
import com.micaaudio.android.ui.theme.StatusError
import com.micaaudio.android.ui.theme.StatusWarning

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MonitoringScreen(
    viewModel: MonitoringViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val listState = rememberLazyListState()

    // Auto-scroll to bottom when new logs arrive
    LaunchedEffect(state.logs.size) {
        if (state.logs.isNotEmpty()) {
            listState.animateScrollToItem(state.logs.size - 1)
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Monitoramento", fontWeight = FontWeight.Bold) },
                actions = {
                    IconButton(onClick = { viewModel.clearLogs() }) {
                        Icon(Icons.Default.ClearAll, "Limpar logs")
                    }
                    IconButton(onClick = { viewModel.refresh() }) {
                        Icon(Icons.Default.Refresh, "Atualizar")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background),
            )
        },
    ) { innerPadding ->
        LazyColumn(
            state = listState,
            modifier = Modifier.fillMaxSize().padding(innerPadding),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            item {
                ServerConnectionBanner(isConnected = state.isConnected, serverUrl = "WebSocket Events")
                Spacer(Modifier.height(8.dp))
            }

            // Device telemetry summary
            if (state.devices.isNotEmpty()) {
                item {
                    Text("Dispositivos", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    Spacer(Modifier.height(8.dp))
                }
                items(state.devices.filter { it.isConnected }, key = { it.deviceId }) { device ->
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
                    ) {
                        Column(Modifier.padding(12.dp)) {
                            Text(device.name.ifBlank { device.deviceId.take(8) }, style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                            Spacer(Modifier.height(8.dp))
                            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
                                val heapTotal = device.heapTotalBytes ?: 1L
                                val heapFree = device.freeHeapBytes ?: heapTotal
                                TelemetryGauge("Heap", ((heapTotal - heapFree).toFloat() / heapTotal).coerceIn(0f, 1f), "${heapFree / 1024}KB", size = 60.dp)
                                device.chipTemperatureCelsius?.let { TelemetryGauge("Temp", (it.toFloat() / 85f).coerceIn(0f, 1f), "${it.toInt()}°C", size = 60.dp) }
                                device.loopHealthyPercent?.let { TelemetryGauge("Loop", (1f - it / 100f), "$it%", size = 60.dp) }
                            }
                        }
                    }
                }
            }

            // Logs section
            item {
                Spacer(Modifier.height(8.dp))
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text("Logs", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    Spacer(Modifier.width(8.dp))
                    Surface(shape = RoundedCornerShape(12.dp), color = MaterialTheme.colorScheme.primaryContainer) {
                        Text("${state.logs.size}", modifier = Modifier.padding(horizontal = 8.dp, vertical = 2.dp), style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onPrimaryContainer)
                    }
                }
                Spacer(Modifier.height(4.dp))
            }

            if (state.logs.isEmpty()) {
                item {
                    Box(Modifier.fillMaxWidth().height(120.dp), contentAlignment = Alignment.Center) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Icon(Icons.Default.Terminal, null, Modifier.size(32.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.4f))
                            Spacer(Modifier.height(8.dp))
                            Text("Aguardando logs...", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                }
            } else {
                items(state.logs, key = { "${it.deviceId}_${it.timestampUtc}_${it.message.hashCode()}" }) { log ->
                    val levelColor = when (log.level.lowercase()) {
                        "error", "fatal" -> StatusError
                        "warn", "warning" -> StatusWarning
                        else -> MaterialTheme.colorScheme.onSurfaceVariant
                    }
                    Row(
                        modifier = Modifier.fillMaxWidth().clip(RoundedCornerShape(6.dp)).background(MaterialTheme.colorScheme.surfaceContainerHigh).padding(horizontal = 10.dp, vertical = 6.dp),
                    ) {
                        Text(log.level.take(1).uppercase(), style = MaterialTheme.typography.labelSmall.copy(fontFamily = FontFamily.Monospace), color = levelColor, modifier = Modifier.width(16.dp))
                        Spacer(Modifier.width(6.dp))
                        Text(log.deviceId.take(6), style = MaterialTheme.typography.labelSmall.copy(fontFamily = FontFamily.Monospace, fontSize = 10.sp), color = MaterialTheme.colorScheme.primary, modifier = Modifier.width(48.dp))
                        Spacer(Modifier.width(6.dp))
                        Text(log.message, style = MaterialTheme.typography.bodySmall.copy(fontFamily = FontFamily.Monospace, fontSize = 11.sp), color = MaterialTheme.colorScheme.onSurface, modifier = Modifier.weight(1f))
                    }
                }
            }
        }
    }
}
