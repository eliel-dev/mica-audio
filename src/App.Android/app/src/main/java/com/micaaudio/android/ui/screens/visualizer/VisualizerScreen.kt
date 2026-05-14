package com.micaaudio.android.ui.screens.visualizer

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.pm.PackageManager
import android.media.projection.MediaProjectionManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.ui.theme.MicaPrimary
import com.micaaudio.android.ui.theme.MicaSecondary
import java.util.Locale

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun VisualizerScreen(
    viewModel: VisualizerViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val bins by viewModel.spectrumBins.collectAsStateWithLifecycle()
    val context = LocalContext.current

    val permissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission()
    ) { isGranted ->
        if (isGranted) {
            viewModel.toggleVisualizer()
        }
    }

    val mediaProjectionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.StartActivityForResult()
    ) { result ->
        if (result.resultCode == Activity.RESULT_OK && result.data != null) {
            viewModel.onMediaProjectionGranted(result.resultCode, result.data!!)
        } else {
            viewModel.onMediaProjectionRejected()
        }
    }

    LaunchedEffect(state.needsMediaProjection) {
        if (state.needsMediaProjection) {
            val mpManager = context.getSystemService(Context.MEDIA_PROJECTION_SERVICE) as MediaProjectionManager
            mediaProjectionLauncher.launch(mpManager.createScreenCaptureIntent())
        }
    }

    if (state.showFftConfig) {
        FftConfigSheet(
            state = state,
            onDismiss = { viewModel.toggleFftConfig(false) },
            onSmoothingChange = { viewModel.setFftSmoothing(it) },
        )
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Visualizador", fontWeight = FontWeight.Bold) },
                actions = {
                    IconButton(onClick = { viewModel.toggleSensitivityDialog(true) }) {
                        Icon(Icons.Default.Tune, "Sensibilidade")
                    }
                    IconButton(onClick = { viewModel.toggleFftConfig(true) }) {
                        Icon(Icons.Default.Equalizer, "Config FFT")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background),
            )
        },
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(20.dp),
        ) {
            // Spectrum Canvas
            Card(
                modifier = Modifier.fillMaxWidth().height(200.dp),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = Color.Black),
            ) {
                Canvas(modifier = Modifier.fillMaxSize().padding(horizontal = 8.dp, vertical = 16.dp)) {
                    val width = size.width
                    val height = size.height
                    val barWidth = width / bins.size.toFloat()
                    bins.forEachIndexed { index, value ->
                        val barHeight = value * height
                        drawRect(
                            brush = Brush.verticalGradient(
                                colors = listOf(MicaSecondary, MicaPrimary),
                                startY = height - barHeight,
                                endY = height,
                            ),
                            topLeft = Offset(index * barWidth, height - barHeight),
                            size = Size(barWidth * 0.8f, barHeight),
                        )
                    }
                }
            }

            // Preset navigation row
            if (state.isVisualizerActive || state.selectedDeviceId != null) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.Center,
                ) {
                    IconButton(onClick = { viewModel.previousPreset() }) {
                        Icon(Icons.Default.SkipPrevious, "Preset anterior")
                    }
                    val presetLabel = state.devices.find { it.deviceId == state.selectedDeviceId }?.activeAppName ?: "—"
                    Text(
                        presetLabel,
                        style = MaterialTheme.typography.bodyMedium,
                        modifier = Modifier.weight(1f),
                        textAlign = androidx.compose.ui.text.style.TextAlign.Center,
                    )
                    IconButton(onClick = { viewModel.nextPreset() }) {
                        Icon(Icons.Default.SkipNext, "Próximo preset")
                    }
                }
            }

            // HUB75 Mode Toggle
            Button(
                onClick = {
                    if (ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) {
                        viewModel.toggleVisualizer()
                    } else {
                        permissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
                    }
                },
                modifier = Modifier.fillMaxWidth().height(56.dp),
                shape = RoundedCornerShape(12.dp),
                colors = if (state.isVisualizerActive)
                    ButtonDefaults.buttonColors(containerColor = MicaPrimary)
                else
                    ButtonDefaults.filledTonalButtonColors(),
            ) {
                Icon(Icons.Default.SettingsInputComponent, null)
                Spacer(Modifier.width(8.dp))
                Text(if (state.isVisualizerActive) "HUB75: ON" else "Modo HUB75")
            }

            // Brightness slider
            if (state.selectedDeviceId != null) {
                Column {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text("Brilho", style = MaterialTheme.typography.labelLarge, modifier = Modifier.weight(1f))
                        Text("${state.brightness}", style = MaterialTheme.typography.labelSmall)
                    }
                    Slider(
                        value = state.brightness.toFloat(),
                        onValueChange = {},
                        onValueChangeFinished = { },
                        valueRange = 0f..255f,
                    )
                }
            }

            if (state.showSensitivityDialog) {
                SensitivityDialog(
                    state = state,
                    onDismiss = { viewModel.toggleSensitivityDialog(false) },
                    onGainChange = { viewModel.setGain(it) },
                    onLinearBoostChange = { viewModel.setLinearBoost(it) },
                    onRiseChange = { viewModel.setRiseSpeed(it) },
                    onFallChange = { viewModel.setFallSpeed(it) },
                    onReset = { viewModel.resetSensitivity() },
                )
            }

            // Device selector
            if (state.devices.isNotEmpty()) {
                Column {
                    Text("Dispositivo Alvo", style = MaterialTheme.typography.labelLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Spacer(Modifier.height(8.dp))
                    SingleChoiceSegmentedButtonRow(modifier = Modifier.fillMaxWidth()) {
                        state.devices.forEachIndexed { index, device ->
                            SegmentedButton(
                                selected = device.deviceId == state.selectedDeviceId,
                                onClick = { viewModel.selectDevice(device.deviceId) },
                                shape = SegmentedButtonDefaults.itemShape(index, state.devices.size),
                            ) {
                                Text(device.name.ifBlank { device.deviceId.take(6) }, maxLines = 1)
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
fun SensitivityDialog(
    state: VisualizerUiState,
    onDismiss: () -> Unit,
    onGainChange: (Float) -> Unit,
    onLinearBoostChange: (Float) -> Unit,
    onRiseChange: (Float) -> Unit,
    onFallChange: (Float) -> Unit,
    onReset: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Configurar Sensibilidade", fontWeight = FontWeight.Bold) },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(16.dp)) {
                // Gain
                SensitivitySlider(label = "Ganho (Input)", value = state.gain, range = 0.5f..5f, onValueChange = onGainChange)
                // Linear Boost
                SensitivitySlider(label = "Linear Boost", value = state.linearBoost, range = 1f..4f, onValueChange = onLinearBoostChange)
                // Rise
                SensitivitySlider(label = "Velocidade Subida", value = state.riseSpeed, range = 0.1f..1f, onValueChange = onRiseChange)
                // Fall
                SensitivitySlider(label = "Velocidade Descida", value = state.fallSpeed, range = 0.05f..1f, onValueChange = onFallChange)

                TextButton(
                    onClick = onReset,
                    modifier = Modifier.fillMaxWidth(),
                    colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.error)
                ) {
                    Icon(Icons.Default.Refresh, null, Modifier.size(16.dp))
                    Spacer(Modifier.width(8.dp))
                    Text("RESTAURAR PADRÕES")
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) { Text("FECHAR") }
        }
    )
}

@Composable
fun SensitivitySlider(label: String, value: Float, range: ClosedFloatingPointRange<Float>, onValueChange: (Float) -> Unit) {
    Column {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(label, style = MaterialTheme.typography.labelMedium, modifier = Modifier.weight(1f))
            Text(String.format(Locale.US, "%.2f", value), style = MaterialTheme.typography.labelSmall, fontWeight = FontWeight.Bold)
        }
        Slider(value = value, onValueChange = onValueChange, valueRange = range)
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun FftConfigSheet(
    state: VisualizerUiState,
    onDismiss: () -> Unit,
    onSmoothingChange: (Float) -> Unit,
) {
    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(
            modifier = Modifier.fillMaxWidth().padding(horizontal = 24.dp).padding(bottom = 32.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            Text("Configuração FFT", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.Bold)
            SensitivitySlider(
                label = "Suavização (Smoothing)",
                value = state.fftSmoothing,
                range = 0.1f..0.99f,
                onValueChange = onSmoothingChange,
            )
        }
    }
}
