package com.micaaudio.android.ui.screens.devices

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.data.api.DeviceLogMessage
import com.micaaudio.android.ui.components.StatusIndicator
import com.micaaudio.android.ui.components.TelemetryGauge

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DeviceDetailScreen(
    deviceId: String,
    onNavigateBack: () -> Unit,
    viewModel: DeviceDetailViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    var showOtaDialog by remember { mutableStateOf(false) }

    LaunchedEffect(deviceId) {
        viewModel.loadDevice(deviceId)
        viewModel.checkFirmwareUpdate(deviceId)
    }

    if (showOtaDialog) {
        AlertDialog(
            onDismissRequest = { showOtaDialog = false },
            title = { Text("Atualizar firmware") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text("Versão atual: ${state.device?.firmwareVersion ?: "—"}")
                    Text("Versão disponível: ${state.availableVersion ?: "—"}")
                    Spacer(Modifier.height(4.dp))
                    Text("O dispositivo será reiniciado durante a atualização.", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            },
            confirmButton = {
                Button(onClick = {
                    showOtaDialog = false
                    viewModel.triggerOtaUpdate(deviceId)
                    viewModel.selectTab(DeviceDetailTab.Control)
                }) { Text("Atualizar agora") }
            },
            dismissButton = {
                TextButton(onClick = { showOtaDialog = false }) { Text("Cancelar") }
            },
        )
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(state.device?.name?.ifBlank { deviceId.take(8) } ?: "Dispositivo") },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, "Voltar")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background),
            )
        },
    ) { innerPadding ->
        val device = state.device
        if (device == null) {
            Box(Modifier.fillMaxSize().padding(innerPadding), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
            return@Scaffold
        }

        Column(modifier = Modifier.fillMaxSize().padding(innerPadding)) {
            TabRow(selectedTabIndex = state.selectedTab.ordinal) {
                DeviceDetailTab.entries.forEach { tab ->
                    Tab(
                        selected = state.selectedTab == tab,
                        onClick = { viewModel.selectTab(tab) },
                        text = {
                            Text(when (tab) {
                                DeviceDetailTab.Info -> "Info"
                                DeviceDetailTab.Logs -> "Logs"
                                DeviceDetailTab.Control -> "Controle"
                            })
                        },
                    )
                }
            }

            when (state.selectedTab) {
                DeviceDetailTab.Info -> InfoTab(deviceId, device, state, viewModel)
                DeviceDetailTab.Logs -> LogsTab(state.logs)
                DeviceDetailTab.Control -> ControlTab(state, onOtaClick = { showOtaDialog = true }, viewModel, deviceId)
            }
        }
    }
}

@Composable
private fun InfoTab(
    deviceId: String,
    device: com.micaaudio.android.data.api.DeviceSnapshot,
    state: DeviceDetailUiState,
    viewModel: DeviceDetailViewModel,
) {
    Column(
        modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        // Status card
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
        ) {
            Row(Modifier.padding(16.dp), verticalAlignment = Alignment.CenterVertically) {
                StatusIndicator(device.isConnected, size = 14.dp)
                Spacer(Modifier.width(10.dp))
                Text(
                    if (device.isConnected) "Online" else "Offline",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold,
                )
                Spacer(Modifier.weight(1f))
                device.firmwareVersion?.let {
                    Text("FW v$it", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }

        // Telemetry gauges
        Text("Telemetria", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
        Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceEvenly) {
            val heapTotal = device.heapTotalBytes ?: 1L
            val heapUsed = heapTotal - (device.freeHeapBytes ?: heapTotal)
            TelemetryGauge("Heap", (heapUsed.toFloat() / heapTotal).coerceIn(0f, 1f), formatBytes(device.freeHeapBytes))

            if (device.psramAvailable == true) {
                val psramTotal = device.psramTotalBytes ?: 1L
                val psramUsed = psramTotal - (device.freePsramBytes ?: psramTotal)
                TelemetryGauge("PSRAM", (psramUsed.toFloat() / psramTotal).coerceIn(0f, 1f), formatBytes(device.freePsramBytes))
            }

            device.chipTemperatureCelsius?.let { temp ->
                TelemetryGauge("Temp", (temp.toFloat() / 85f).coerceIn(0f, 1f), "${temp.toInt()}°C")
            }

            device.lastKnownRssi?.let { rssi ->
                val strength = ((rssi + 90).toFloat() / 60f).coerceIn(0f, 1f)
                TelemetryGauge("RSSI", 1f - strength, "${rssi}dBm")
            }
        }

        // Details
        Text("Informações", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
        ) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                DetailRow("Device ID", device.deviceId)
                device.lastKnownIp?.let { DetailRow("IP", it) }
                device.boardModel?.let { DetailRow("Board", it) }
                device.chipModel?.let { DetailRow("Chip", "${it} rev${device.chipRevision ?: 0}") }
                device.cpuFreqMHz?.let { DetailRow("CPU", "${it} MHz (${device.chipCores ?: 0} cores)") }
                device.sdkVersion?.let { DetailRow("SDK", it) }
                device.activeAppName?.let { DetailRow("App ativo", it) }
                device.uptimeSeconds?.let { DetailRow("Uptime", formatDetailUptime(it)) }
                device.loopHealthyPercent?.let { DetailRow("Loop Health", "$it%") }
                device.loopLoadPercent?.let { DetailRow("Loop Load", "$it%") }
                device.wifiState?.let { DetailRow("Wi-Fi", it) }
            }
        }

        // Brightness slider
        Text("Controles", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
        ) {
            Column(Modifier.padding(16.dp)) {
                Text("Brilho", style = MaterialTheme.typography.labelLarge)
                Spacer(Modifier.height(4.dp))
                var brightness by remember { mutableFloatStateOf((device.brightnessApplied ?: 50).toFloat()) }
                Slider(
                    value = brightness,
                    onValueChange = { brightness = it },
                    onValueChangeFinished = { viewModel.setBrightness(deviceId, brightness.toInt()) },
                    valueRange = 0f..255f,
                )
                Text("${brightness.toInt()}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }

        Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
            OutlinedButton(onClick = { viewModel.testLed(deviceId) }, modifier = Modifier.weight(1f)) {
                Icon(Icons.Default.Lightbulb, null, Modifier.size(18.dp))
                Spacer(Modifier.width(6.dp))
                Text("Test LED")
            }
            OutlinedButton(
                onClick = { viewModel.removeDevice(deviceId) },
                modifier = Modifier.weight(1f),
                colors = ButtonDefaults.outlinedButtonColors(contentColor = MaterialTheme.colorScheme.error),
            ) {
                Icon(Icons.Default.Delete, null, Modifier.size(18.dp))
                Spacer(Modifier.width(6.dp))
                Text("Remover")
            }
        }
    }
}

@Composable
private fun LogsTab(logs: List<DeviceLogMessage>) {
    val listState = rememberLazyListState()

    LaunchedEffect(logs.size) {
        if (logs.isNotEmpty()) listState.animateScrollToItem(logs.lastIndex)
    }

    if (logs.isEmpty()) {
        Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
            Text("Aguardando logs...", color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        return
    }

    LazyColumn(
        state = listState,
        modifier = Modifier.fillMaxSize().background(Color(0xFF0D0D0D)).padding(horizontal = 8.dp, vertical = 4.dp),
        verticalArrangement = Arrangement.spacedBy(1.dp),
    ) {
        items(logs) { log ->
            val color = when (log.level.uppercase()) {
                "ERROR", "ERR" -> Color(0xFFFF5555)
                "WARN", "WARNING" -> Color(0xFFFFD93D)
                "INFO" -> Color(0xFF6BCB77)
                else -> Color(0xFFB0B0B0)
            }
            Row(Modifier.fillMaxWidth().padding(vertical = 1.dp)) {
                Text(
                    "[${log.level.take(1).uppercase()}]",
                    color = color,
                    fontFamily = FontFamily.Monospace,
                    fontSize = 11.sp,
                    modifier = Modifier.width(28.dp),
                )
                Text(
                    log.message,
                    color = Color(0xFFD0D0D0),
                    fontFamily = FontFamily.Monospace,
                    fontSize = 11.sp,
                )
            }
        }
    }
}

@Composable
private fun ControlTab(
    state: DeviceDetailUiState,
    onOtaClick: () -> Unit,
    viewModel: DeviceDetailViewModel,
    deviceId: String,
) {
    Column(
        modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState()).padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        // Firmware card
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
        ) {
            Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                Text("Firmware", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                DetailRow("Versão atual", state.device?.firmwareVersion ?: "—")
                if (state.showUpdateBanner && state.availableVersion != null) {
                    DetailRow("Disponível", state.availableVersion)
                    Spacer(Modifier.height(4.dp))
                    Button(
                        onClick = onOtaClick,
                        enabled = state.device?.isConnected == true && !state.commandInProgress,
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        Icon(Icons.Default.SystemUpdate, null, Modifier.size(18.dp))
                        Spacer(Modifier.width(8.dp))
                        Text("Atualizar firmware")
                    }
                } else if (!state.showUpdateBanner && state.device?.firmwareVersion != null) {
                    Text("Firmware atualizado", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }

        // Command progress card
        if (state.commandInProgress || state.commandStatus != null) {
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
            ) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text("Progresso do comando", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                    if (state.commandInProgress) {
                        LinearProgressIndicator(
                            progress = { state.commandPercent / 100f },
                            modifier = Modifier.fillMaxWidth(),
                        )
                    } else {
                        LinearProgressIndicator(
                            progress = { 1f },
                            modifier = Modifier.fillMaxWidth(),
                        )
                    }
                    state.commandStatus?.let {
                        Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                    if (!state.commandInProgress) {
                        TextButton(onClick = { viewModel.clearCommandStatus() }) {
                            Text("Limpar")
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun DetailRow(label: String, value: String) {
    Row(Modifier.fillMaxWidth()) {
        Text(
            label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.width(120.dp),
        )
        Text(value, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Medium)
    }
}

private fun formatBytes(bytes: Long?): String {
    if (bytes == null) return "—"
    return when {
        bytes >= 1_048_576 -> "${bytes / 1_048_576}MB"
        bytes >= 1024 -> "${bytes / 1024}KB"
        else -> "${bytes}B"
    }
}

private fun formatDetailUptime(seconds: Int): String {
    val d = seconds / 86400; val h = (seconds % 86400) / 3600; val m = (seconds % 3600) / 60
    return buildString {
        if (d > 0) append("${d}d "); if (h > 0) append("${h}h "); append("${m}m")
    }.trim()
}
