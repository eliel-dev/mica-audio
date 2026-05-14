package com.micaaudio.android.ui.components

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.Spring
import androidx.compose.animation.core.spring
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Memory
import androidx.compose.material.icons.filled.SignalWifi4Bar
import androidx.compose.material.icons.filled.SignalWifiOff
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.ui.theme.GaugeCritical
import com.micaaudio.android.ui.theme.GaugeGood
import com.micaaudio.android.ui.theme.GaugeModerate
import com.micaaudio.android.ui.theme.MicaPrimary
import com.micaaudio.android.ui.theme.MicaSecondary
import com.micaaudio.android.ui.theme.StatusOffline
import com.micaaudio.android.ui.theme.StatusOnline

// ── Status Indicator ────────────────────────────────────────────────────────

@Composable
fun StatusIndicator(
    isOnline: Boolean,
    modifier: Modifier = Modifier,
    size: Dp = 10.dp,
) {
    val color by animateColorAsState(
        targetValue = if (isOnline) StatusOnline else StatusOffline,
        animationSpec = spring(stiffness = Spring.StiffnessLow),
        label = "statusColor",
    )
    Box(
        modifier = modifier
            .size(size)
            .clip(CircleShape)
            .background(color),
    )
}

// ── Device Card ─────────────────────────────────────────────────────────────

@Composable
fun DeviceCard(
    device: DeviceSnapshot,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    Card(
        onClick = onClick,
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(16.dp),
        colors = CardDefaults.cardColors(
            containerColor = MaterialTheme.colorScheme.surfaceContainer,
        ),
        elevation = CardDefaults.cardElevation(defaultElevation = 0.dp),
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
        ) {
            // Header row
            Row(
                verticalAlignment = Alignment.CenterVertically,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Box(
                    modifier = Modifier
                        .size(40.dp)
                        .clip(RoundedCornerShape(12.dp))
                        .background(
                            Brush.linearGradient(listOf(MicaPrimary, MicaSecondary))
                        ),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(
                        imageVector = Icons.Default.Memory,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.onPrimary,
                        modifier = Modifier.size(22.dp),
                    )
                }

                Spacer(Modifier.width(12.dp))

                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = device.name.ifBlank { device.deviceId.take(8) },
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.SemiBold,
                    )
                    Text(
                        text = device.deviceId.take(12),
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }

                StatusIndicator(isOnline = device.isConnected)
                Spacer(Modifier.width(6.dp))
                Text(
                    text = if (device.isConnected) "Online" else "Offline",
                    style = MaterialTheme.typography.labelMedium,
                    color = if (device.isConnected) StatusOnline else StatusOffline,
                )
            }

            Spacer(Modifier.height(12.dp))

            // Info chips
            Row(
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                modifier = Modifier.fillMaxWidth(),
            ) {
                device.lastKnownIp?.let { ip ->
                    InfoChip(
                        icon = {
                            Icon(
                                imageVector = if (device.isConnected) Icons.Default.SignalWifi4Bar
                                else Icons.Default.SignalWifiOff,
                                contentDescription = null,
                                modifier = Modifier.size(14.dp),
                            )
                        },
                        text = ip,
                    )
                }

                device.firmwareVersion?.let { fw ->
                    InfoChip(text = "v$fw")
                }

                device.uptimeSeconds?.let { uptime ->
                    InfoChip(text = formatUptime(uptime))
                }
            }
        }
    }
}

@Composable
private fun InfoChip(
    text: String,
    modifier: Modifier = Modifier,
    icon: @Composable (() -> Unit)? = null,
) {
    Row(
        verticalAlignment = Alignment.CenterVertically,
        modifier = modifier
            .clip(RoundedCornerShape(8.dp))
            .background(MaterialTheme.colorScheme.surfaceContainerHigh)
            .padding(horizontal = 8.dp, vertical = 4.dp),
    ) {
        if (icon != null) {
            icon()
            Spacer(Modifier.width(4.dp))
        }
        Text(
            text = text,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

private fun formatUptime(seconds: Int): String {
    val hours = seconds / 3600
    val minutes = (seconds % 3600) / 60
    return when {
        hours > 24 -> "${hours / 24}d ${hours % 24}h"
        hours > 0 -> "${hours}h ${minutes}m"
        else -> "${minutes}m"
    }
}

// ── Telemetry Gauge ─────────────────────────────────────────────────────────

@Composable
fun TelemetryGauge(
    label: String,
    value: Float, // 0..1
    displayText: String,
    modifier: Modifier = Modifier,
    size: Dp = 80.dp,
) {
    val gaugeColor by animateColorAsState(
        targetValue = when {
            value < 0.6f -> GaugeGood
            value < 0.85f -> GaugeModerate
            else -> GaugeCritical
        },
        label = "gaugeColor",
    )

    Column(
        horizontalAlignment = Alignment.CenterHorizontally,
        modifier = modifier,
    ) {
        Box(
            contentAlignment = Alignment.Center,
            modifier = Modifier.size(size),
        ) {
            val trackColor = MaterialTheme.colorScheme.surfaceContainerHigh
            Canvas(modifier = Modifier.size(size)) {
                val strokeWidth = 8.dp.toPx()
                val arcSize = Size(this.size.width - strokeWidth, this.size.height - strokeWidth)
                val topLeft = Offset(strokeWidth / 2, strokeWidth / 2)

                // Track
                drawArc(
                    color = trackColor,
                    startAngle = 135f,
                    sweepAngle = 270f,
                    useCenter = false,
                    topLeft = topLeft,
                    size = arcSize,
                    style = Stroke(width = strokeWidth, cap = StrokeCap.Round),
                )

                // Value
                drawArc(
                    color = gaugeColor,
                    startAngle = 135f,
                    sweepAngle = 270f * value.coerceIn(0f, 1f),
                    useCenter = false,
                    topLeft = topLeft,
                    size = arcSize,
                    style = Stroke(width = strokeWidth, cap = StrokeCap.Round),
                )
            }

            Text(
                text = displayText,
                style = MaterialTheme.typography.labelLarge,
                fontWeight = FontWeight.Bold,
            )
        }

        Spacer(Modifier.height(4.dp))

        Text(
            text = label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}

// ── Server Connection Banner ────────────────────────────────────────────────

@Composable
fun ServerConnectionBanner(
    isConnected: Boolean,
    serverUrl: String,
    modifier: Modifier = Modifier,
) {
    val backgroundColor by animateColorAsState(
        targetValue = if (isConnected) StatusOnline.copy(alpha = 0.1f)
        else MaterialTheme.colorScheme.error.copy(alpha = 0.1f),
        label = "bannerBg",
    )
    val textColor by animateColorAsState(
        targetValue = if (isConnected) StatusOnline else MaterialTheme.colorScheme.error,
        label = "bannerText",
    )

    Row(
        verticalAlignment = Alignment.CenterVertically,
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(backgroundColor)
            .padding(horizontal = 16.dp, vertical = 10.dp),
    ) {
        StatusIndicator(isOnline = isConnected)
        Spacer(Modifier.width(10.dp))
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = if (isConnected) "Conectado" else "Desconectado",
                style = MaterialTheme.typography.labelLarge,
                color = textColor,
                fontWeight = FontWeight.SemiBold,
            )
            Text(
                text = serverUrl,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}
