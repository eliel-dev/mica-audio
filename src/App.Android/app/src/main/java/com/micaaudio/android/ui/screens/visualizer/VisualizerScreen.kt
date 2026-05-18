package com.micaaudio.android.ui.screens.visualizer

import android.Manifest
import android.content.pm.PackageManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.SettingsInputComponent
import androidx.compose.material.icons.filled.Tune
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SegmentedButton
import androidx.compose.material3.SegmentedButtonDefaults
import androidx.compose.material3.SingleChoiceSegmentedButtonRow
import androidx.compose.material3.Slider
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.runtime.withFrameNanos
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.data.audio.FrequencyScale
import com.micaaudio.android.data.audio.WeightingFilter
import com.micaaudio.android.ui.theme.MicaPrimary
import java.util.Locale
import kotlin.math.roundToInt

// DOCS: docs/wiki/modules/visual-win2d.md#audiomotion-clone
// DOCS: docs/wiki/reference/ws-protocol-v2.md#mensagem-tipo-1---bins128

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun VisualizerScreen(
    viewModel: VisualizerViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val bins by viewModel.spectrumBins.collectAsStateWithLifecycle()
    val context = LocalContext.current
    var showFftConfig by remember { mutableStateOf(false) }

    val permissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission(),
    ) { isGranted ->
        if (isGranted) viewModel.startLocalVisualization()
    }

    // Auto-start local visualisation when the screen opens (no HUB75 streaming)
    LaunchedEffect(Unit) {
        if (ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO)
            == PackageManager.PERMISSION_GRANTED
        ) {
            viewModel.startLocalVisualization()
        } else {
            permissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Visualizador", fontWeight = FontWeight.Bold) },
                actions = {
                    IconButton(onClick = { showFftConfig = true }) {
                        Icon(Icons.Default.Tune, "Configurar FFT")
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
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(220.dp),
                shape = RoundedCornerShape(16.dp),
                colors = CardDefaults.cardColors(containerColor = Color.Black),
            ) {
                AudioMotionPreview(
                    bins = bins,
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(horizontal = 6.dp, vertical = 10.dp),
                )
            }

            Text(
                text = "AudioMotion Clone",
                style = MaterialTheme.typography.bodyMedium,
                textAlign = TextAlign.Center,
                modifier = Modifier.fillMaxWidth(),
            )

            Button(
                onClick = {
                    if (ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO) == PackageManager.PERMISSION_GRANTED) {
                        viewModel.toggleVisualizer()
                    } else {
                        permissionLauncher.launch(Manifest.permission.RECORD_AUDIO)
                    }
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(56.dp),
                shape = RoundedCornerShape(12.dp),
                colors = if (state.isVisualizerActive) {
                    ButtonDefaults.buttonColors(containerColor = MicaPrimary)
                } else {
                    ButtonDefaults.filledTonalButtonColors()
                },
            ) {
                Icon(Icons.Default.SettingsInputComponent, null)
                Spacer(Modifier.width(8.dp))
                Text(if (state.isVisualizerActive) "HUB75: ON" else "Modo HUB75")
            }

            if (state.selectedDeviceId != null) {
                Column {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text("Brilho", style = MaterialTheme.typography.labelLarge, modifier = Modifier.weight(1f))
                        Text("${state.brightness}", style = MaterialTheme.typography.labelSmall)
                    }
                    Slider(
                        value = state.brightness.toFloat(),
                        onValueChange = { viewModel.setBrightness(it.toInt()) },
                        valueRange = 0f..255f,
                    )
                }
            }

            if (state.devices.isNotEmpty()) {
                Column {
                    Text(
                        "Dispositivo Alvo",
                        style = MaterialTheme.typography.labelLarge,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Spacer(Modifier.height(8.dp))
                    SingleChoiceSegmentedButtonRow(modifier = Modifier.fillMaxWidth()) {
                        state.devices.forEachIndexed { index, device ->
                            SegmentedButton(
                                selected = device.deviceId == state.selectedDeviceId,
                                onClick = { viewModel.selectDevice(device.deviceId) },
                                shape = SegmentedButtonDefaults.itemShape(index, state.devices.size),
                            ) {
                                Text(
                                    text = device.name.ifBlank { device.deviceId.take(6) },
                                    maxLines = 1,
                                    overflow = TextOverflow.Ellipsis,
                                )
                            }
                        }
                    }
                }
            }
        }
    }

    if (showFftConfig) {
        FftConfigSheet(
            state = state,
            onDismiss = { showFftConfig = false },
            onSmoothingChange = viewModel::setFftSmoothing,
            onMinFrequencyChange = viewModel::setMinFrequency,
            onMaxFrequencyChange = viewModel::setMaxFrequency,
            onBarCountChange = viewModel::setBarCount,
            onFreqScaleChange = viewModel::setFrequencyScale,
            onWeightingChange = viewModel::setWeightingFilter,
            onRestoreDefaults = viewModel::resetFftDefaults,
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun FftConfigSheet(
    state: VisualizerUiState,
    onDismiss: () -> Unit,
    onSmoothingChange: (Float) -> Unit,
    onMinFrequencyChange: (Float) -> Unit,
    onMaxFrequencyChange: (Float) -> Unit,
    onBarCountChange: (Int) -> Unit,
    onFreqScaleChange: (FrequencyScale) -> Unit,
    onWeightingChange: (WeightingFilter) -> Unit,
    onRestoreDefaults: () -> Unit,
) {
    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .verticalScroll(rememberScrollState())
                .padding(horizontal = 20.dp)
                .padding(bottom = 36.dp),
            verticalArrangement = Arrangement.spacedBy(20.dp),
        ) {
            Text("Análise FFT", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)

            // ── Smoothing ─────────────────────────────────────────────────────
            SensitivitySlider(
                label = "FFT Smoothing",
                value = state.fftSmoothing,
                valueRange = 0f..0.98f,
                onValueChange = onSmoothingChange,
                formatValue = { String.format(Locale.US, "%.2f", it) },
            )

            // ── Bar count ─────────────────────────────────────────────────────
            SensitivitySlider(
                label = "Quantidade de barras",
                value = state.barCount.toFloat(),
                valueRange = 10f..256f,
                onValueChange = { onBarCountChange(it.roundToInt()) },
                formatValue = { it.roundToInt().toString() },
            )

            // ── Frequency range ───────────────────────────────────────────────
            SensitivitySlider(
                label = "Frequência mínima",
                value = state.minFrequencyHz,
                valueRange = 10f..500f,
                onValueChange = onMinFrequencyChange,
                formatValue = ::formatFrequency,
            )
            SensitivitySlider(
                label = "Frequência máxima",
                value = state.maxFrequencyHz,
                valueRange = 500f..20_000f,
                onValueChange = onMaxFrequencyChange,
                formatValue = ::formatFrequency,
            )

            // ── Frequency scale ───────────────────────────────────────────────
            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text("Escala de frequência", style = MaterialTheme.typography.labelLarge)
                SingleChoiceSegmentedButtonRow(modifier = Modifier.fillMaxWidth()) {
                    val scales = listOf(
                        FrequencyScale.Logarithmic to "Log",
                        FrequencyScale.Bark        to "Bark",
                        FrequencyScale.Mel         to "Mel",
                    )
                    scales.forEachIndexed { index, (scale, label) ->
                        SegmentedButton(
                            selected = state.frequencyScale == scale,
                            onClick = { onFreqScaleChange(scale) },
                            shape = SegmentedButtonDefaults.itemShape(index, scales.size),
                        ) { Text(label) }
                    }
                }
            }

            // ── Weighting filter ──────────────────────────────────────────────
            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text("Filtro de ponderação", style = MaterialTheme.typography.labelLarge)
                SingleChoiceSegmentedButtonRow(modifier = Modifier.fillMaxWidth()) {
                    val filters = listOf(
                        WeightingFilter.Off to "Off",
                        WeightingFilter.A   to "A",
                        WeightingFilter.B   to "B",
                        WeightingFilter.C   to "C",
                    )
                    filters.forEachIndexed { index, (filter, label) ->
                        SegmentedButton(
                            selected = state.weightingFilter == filter,
                            onClick = { onWeightingChange(filter) },
                            shape = SegmentedButtonDefaults.itemShape(index, filters.size),
                        ) { Text(label) }
                    }
                }
            }

            // ── Actions ───────────────────────────────────────────────────────
            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                Text("Ações", style = MaterialTheme.typography.labelLarge)
                Text(
                    "Use quando quiser voltar rapidamente ao conjunto recomendado.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                OutlinedButton(
                    onClick = onRestoreDefaults,
                    modifier = Modifier.fillMaxWidth(),
                ) { Text("Restaurar padrão") }
            }

            OutlinedButton(onClick = onDismiss, modifier = Modifier.fillMaxWidth()) {
                Text("Fechar")
            }
        }
    }
}

@Composable
private fun SensitivitySlider(
    label: String,
    value: Float,
    valueRange: ClosedFloatingPointRange<Float>,
    onValueChange: (Float) -> Unit,
    formatValue: (Float) -> String = { String.format(Locale.US, "%.1f", it) },
) {
    Column {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(label, style = MaterialTheme.typography.labelLarge, modifier = Modifier.weight(1f))
            Text(formatValue(value), style = MaterialTheme.typography.labelMedium)
        }
        Slider(
            value = value.coerceIn(valueRange.start, valueRange.endInclusive),
            onValueChange = onValueChange,
            valueRange = valueRange,
        )
    }
}

private fun formatFrequency(value: Float): String {
    return if (value < 1000f) {
        "${value.roundToInt()} Hz"
    } else {
        String.format(Locale.US, "%.1f kHz", value / 1000f)
    }
}

private const val MaxRenderedBars = 256

@Composable
private fun AudioMotionPreview(
    bins: FloatArray,
    modifier: Modifier = Modifier,
) {
    // Grows on demand so changing barCount up to MaxRenderedBars never overflows.
    val gravity = remember { Array(MaxRenderedBars) { GravityModel() } }
    var frameNanos by remember { mutableStateOf(0L) }

    // Rolling average of the last 60 frame intervals — gives a smooth FPS reading.
    val frameIntervalsNs = remember { ArrayDeque<Long>() }
    var displayFps by remember { mutableStateOf(0) }
    var lastFrameNs by remember { mutableStateOf(0L) }

    LaunchedEffect(Unit) {
        while (true) {
            withFrameNanos { now ->
                frameNanos = now
                val prev = lastFrameNs
                lastFrameNs = now
                if (prev > 0L) {
                    frameIntervalsNs.addLast(now - prev)
                    if (frameIntervalsNs.size > 60) frameIntervalsNs.removeFirst()
                    val avg = frameIntervalsNs.sum() / frameIntervalsNs.size
                    if (avg > 0L) displayFps = (1_000_000_000L / avg).toInt()
                }
            }
        }
    }
    val tick = frameNanos

    androidx.compose.foundation.layout.Box(modifier = modifier) {
        Canvas(modifier = Modifier.fillMaxSize()) {
            // Always derive slot width from the live bins.size so the bars span the full
            // canvas width regardless of barCount.
            val binCount = bins.size.coerceAtMost(MaxRenderedBars)
            if (binCount == 0) return@Canvas

            val width = size.width
            val height = size.height
            val midY = height * 0.5f
            val slotWidth = width / binCount.toFloat()
            val lineWidth = (slotWidth * 0.45f).coerceAtLeast(1.5f)
            val heightScale = 0.78f

            drawLine(
                color = Color.White.copy(alpha = 0.07f),
                start = Offset(0f, midY),
                end = Offset(width, midY),
                strokeWidth = 1.dp.toPx(),
            )

            for (index in 0 until binCount) {
                val value = gravity[index].next(bins[index].coerceIn(0f, 1f), tick)
                val halfHeight = (value * midY * heightScale).coerceAtMost(midY - 1f)
                if (halfHeight < 0.5f) continue

                val centerX = (index * slotWidth) + (slotWidth * 0.5f)
                drawLine(
                    color = rainbowColorForColumn(index, binCount),
                    start = Offset(centerX, midY - halfHeight),
                    end = Offset(centerX, midY + halfHeight),
                    strokeWidth = lineWidth,
                    cap = StrokeCap.Round,
                )
            }
        }
        // FPS overlay — top-right corner. Canvas renders at display refresh rate;
        // the underlying FFT data updates at Android's hard limit (~20 Hz).
        Text(
            text = "${displayFps} fps",
            style = MaterialTheme.typography.labelSmall,
            color = Color.White.copy(alpha = 0.55f),
            modifier = Modifier
                .align(Alignment.TopEnd)
                .padding(end = 8.dp, top = 4.dp),
        )
    }
}

private class GravityModel {
    private var current = 0f
    private var velocity = 0f
    private var lastFrameNanos = 0L

    fun next(target: Float, frameNanos: Long): Float {
        val frameScale = if (lastFrameNanos == 0L || frameNanos <= lastFrameNanos) {
            1f
        } else {
            ((frameNanos - lastFrameNanos) / 16_666_667f).coerceIn(0.5f, 3f)
        }
        lastFrameNanos = frameNanos

        if (target >= current) {
            current = target
            velocity = 0f
            return current
        }

        velocity = (velocity + 0.006f * frameScale).coerceAtMost(0.09f)
        current = (current - velocity * frameScale).coerceAtLeast(target)
        return current
    }
}

private fun rainbowColorForColumn(index: Int, count: Int): Color {
    if (count <= 1) return rgbColor(255, 0, 0)

    val hue = (index * 255) / (count - 1)
    val region = hue / 43
    val remainder = (hue - region * 43) * 6
    val q = 255 - remainder
    val t = remainder

    return when (region) {
        0 -> rgbColor(255, t, 0)
        1 -> rgbColor(q, 255, 0)
        2 -> rgbColor(0, 255, t)
        3 -> rgbColor(0, q, 255)
        4 -> rgbColor(t, 0, 255)
        else -> rgbColor(255, 0, q)
    }
}

private fun rgbColor(red: Int, green: Int, blue: Int): Color {
    return Color(
        red = red.coerceIn(0, 255) / 255f,
        green = green.coerceIn(0, 255) / 255f,
        blue = blue.coerceIn(0, 255) / 255f,
        alpha = 1f,
    )
}
