package com.micaaudio.android.ui.screens.visualizer

import android.Manifest
import android.app.Activity
import android.content.Context
import android.content.pm.PackageManager
import android.media.projection.MediaProjectionConfig
import android.media.projection.MediaProjectionManager
import android.os.Build
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
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
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
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.ui.theme.MicaPrimary

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

    val permissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestPermission(),
    ) { isGranted ->
        if (isGranted) viewModel.toggleVisualizer()
    }

    val mediaProjectionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.StartActivityForResult(),
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
            val intent = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                val config = MediaProjectionConfig.createConfigForUserChoice()
                mpManager.createScreenCaptureIntent(config)
            } else {
                mpManager.createScreenCaptureIntent()
            }
            mediaProjectionLauncher.launch(intent)
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Visualizador", fontWeight = FontWeight.Bold) },
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

            if (state.isVisualizerActive || state.selectedDeviceId != null) {
                Text(
                    text = "AudioMotion Clone",
                    style = MaterialTheme.typography.bodyMedium,
                    textAlign = TextAlign.Center,
                    modifier = Modifier.fillMaxWidth(),
                )
            }

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
}

@Composable
private fun AudioMotionPreview(
    bins: FloatArray,
    modifier: Modifier = Modifier,
) {
    Canvas(modifier = modifier) {
        val binCount = bins.size.coerceAtMost(128)
        if (binCount == 0) return@Canvas

        val width = size.width
        val height = size.height
        val midY = height * 0.5f
        val slotWidth = width / binCount.toFloat()
        val lineWidth = (slotWidth * 0.45f).coerceIn(1.5f, 4f)
        val heightScale = 0.78f

        for (index in 0 until binCount) {
            val value = bins[index].coerceIn(0f, 1f)
            val halfHeight = (value * midY * heightScale).coerceAtMost(midY - 1f)
            if (halfHeight < 0.5f) continue

            val centerX = (index * slotWidth) + (slotWidth * 0.5f)
            drawLine(
                color = rainbowColorForColumn(index, binCount),
                start = Offset(centerX, midY - halfHeight),
                end = Offset(centerX, midY + halfHeight),
                strokeWidth = lineWidth,
            )
        }
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
