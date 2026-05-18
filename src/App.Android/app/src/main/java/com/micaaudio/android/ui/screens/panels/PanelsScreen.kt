package com.micaaudio.android.ui.screens.panels

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Dashboard
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.DevicesOther
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.data.api.CatalogPanelResponse
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.ui.theme.MicaPrimary

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PanelsScreen(
    onEditPanel: (panelId: String) -> Unit = {},
    onCreatePanel: (panelId: String) -> Unit = {},
    viewModel: PanelsViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    var pendingDeleteEntry by remember { mutableStateOf<com.micaaudio.android.data.api.CatalogPanelResponse?>(null) }

    // Sort catalog by panel name
    val panelEntries = remember(state.catalogPanels) {
        state.catalogPanels.sortedBy { it.panel.name }
    }

    // ── Dialogs ───────────────────────────────────────────────────────────────
    if (state.error != null) {
        AlertDialog(
            onDismissRequest = { viewModel.dismissMessage() },
            title = { Text("Erro") },
            text = { Text(state.error!!) },
            confirmButton = { TextButton(onClick = { viewModel.dismissMessage() }) { Text("OK") } },
        )
    }
    if (state.successMessage != null) {
        AlertDialog(
            onDismissRequest = { viewModel.dismissMessage() },
            title = { Text("Sucesso") },
            text = { Text(state.successMessage!!) },
            confirmButton = { TextButton(onClick = { viewModel.dismissMessage() }) { Text("OK") } },
        )
    }

    // Delete confirmation dialog
    if (pendingDeleteEntry != null) {
        val entry = pendingDeleteEntry!!
        AlertDialog(
            onDismissRequest = { pendingDeleteEntry = null },
            title = { Text("Excluir painel") },
            text = { Text("Excluir '${entry.panel.name}' do catálogo? Esta ação não pode ser desfeita.") },
            confirmButton = {
                TextButton(
                    onClick = {
                        viewModel.deletePanel(entry.panel.panelId, entry.activeOnDeviceId)
                        pendingDeleteEntry = null
                    },
                ) { Text("Excluir", color = MaterialTheme.colorScheme.error) }
            },
            dismissButton = {
                TextButton(onClick = { pendingDeleteEntry = null }) { Text("Cancelar") }
            },
        )
    }

    // Device-picker when multiple connected devices
    if (state.pendingActivatePanel != null) {
        val connected = state.devices.filter { it.deviceId in state.connectedDeviceIds }
        AlertDialog(
            onDismissRequest = { viewModel.dismissActivateDialog() },
            title = { Text("Escolher dispositivo") },
            text = {
                Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(
                        "Em qual dispositivo ativar '${state.pendingActivatePanel!!.name}'?",
                        style = MaterialTheme.typography.bodyMedium,
                    )
                    connected.forEach { device ->
                        OutlinedButton(
                            onClick = { viewModel.activatePanel(state.pendingActivatePanel!!, device.deviceId) },
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Icon(Icons.Default.DevicesOther, null, Modifier.size(16.dp))
                            Spacer(Modifier.width(8.dp))
                            Text(device.name.ifBlank { device.deviceId.take(12) })
                        }
                    }
                }
            },
            confirmButton = {},
            dismissButton = {
                TextButton(onClick = { viewModel.dismissActivateDialog() }) { Text("Cancelar") }
            },
        )
    }

    Scaffold(
        floatingActionButton = {
            FloatingActionButton(onClick = { viewModel.createPanel(onCreatePanel) }) {
                Icon(Icons.Default.Add, "Novo painel")
            }
        },
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text("Painéis", fontWeight = FontWeight.Bold)
                        if (!state.isLoading) {
                            Text(
                                "${panelEntries.size} painel(is) no catálogo",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        }
                    }
                },
                actions = {
                    IconButton(onClick = { viewModel.refresh() }) {
                        Icon(Icons.Default.Refresh, "Atualizar")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                ),
            )
        },
    ) { innerPadding ->
        when {
            state.isLoading -> Box(
                Modifier.fillMaxSize().padding(innerPadding),
                contentAlignment = Alignment.Center,
            ) {
                CircularProgressIndicator()
            }

            panelEntries.isEmpty() -> Box(
                Modifier.fillMaxSize().padding(innerPadding),
                contentAlignment = Alignment.Center,
            ) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    Icon(
                        Icons.Default.Dashboard, null,
                        modifier = Modifier.size(64.dp),
                        tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.4f),
                    )
                    Text(
                        "Catálogo vazio",
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        "Crie um painel no editor ou salve um painel existente para vê-lo aqui.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f),
                    )
                    OutlinedButton(onClick = { viewModel.refresh() }) {
                        Icon(Icons.Default.Refresh, null, Modifier.size(18.dp))
                        Spacer(Modifier.width(6.dp))
                        Text("Atualizar")
                    }
                }
            }

            else -> LazyColumn(
                modifier = Modifier.fillMaxSize().padding(innerPadding),
                contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                items(panelEntries, key = { it.panel.panelId }) { entry ->
                    val device = state.devices.find { it.deviceId == entry.activeOnDeviceId }
                    val isConnected = entry.activeOnDeviceId != null &&
                        entry.activeOnDeviceId in state.connectedDeviceIds

                    ServerPanelCard(
                        entry = entry,
                        device = device,
                        isConnected = isConnected,
                        onActivate = { viewModel.requestActivate(entry.panel) },
                        onEdit = { onEditPanel(entry.panel.panelId) },
                        onDelete = { pendingDeleteEntry = entry },
                    )
                }
            }
        }
    }
}

@Composable
private fun ServerPanelCard(
    entry: CatalogPanelResponse,
    device: DeviceSnapshot?,
    isConnected: Boolean,
    onActivate: () -> Unit,
    onEdit: () -> Unit,
    onDelete: () -> Unit,
) {
    val panel = entry.panel
    val capability = entry.capability
    val deviceLabel = when {
        device != null -> device.name.ifBlank { device.deviceId.take(12) }
        entry.activeOnDeviceId != null -> entry.activeOnDeviceId.take(12)
        else -> null
    }

    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = if (isConnected)
                MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.25f)
            else
                MaterialTheme.colorScheme.surfaceContainer,
        ),
        border = if (isConnected)
            CardDefaults.outlinedCardBorder().copy(brush = SolidColor(MicaPrimary.copy(alpha = 0.5f)), width = 1.dp)
        else null,
    ) {
        Column(Modifier.padding(16.dp)) {

            // ── Panel name + active badge ─────────────────────────────────────
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(
                    Icons.Default.Dashboard, null,
                    modifier = Modifier.size(20.dp),
                    tint = if (isConnected) MicaPrimary else MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Spacer(Modifier.width(8.dp))
                Column(Modifier.weight(1f)) {
                    Text(panel.name.ifBlank { "Painel" }, fontWeight = FontWeight.Bold, fontSize = 16.sp)
                    Text(
                        buildString {
                            append("${panel.widgets.size} widget(s)  ·  ${panel.width}×${panel.height}px")
                            if (capability.isNotBlank()) append("  ·  $capability")
                        },
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                if (isConnected) {
                    Surface(shape = RoundedCornerShape(4.dp), color = MicaPrimary) {
                        Text(
                            "ATIVO",
                            modifier = Modifier.padding(horizontal = 6.dp, vertical = 2.dp),
                            style = MaterialTheme.typography.labelSmall,
                            color = Color.White,
                            fontWeight = FontWeight.Bold,
                        )
                    }
                }
            }

            Spacer(Modifier.height(10.dp))
            HorizontalDivider(color = MaterialTheme.colorScheme.outline.copy(alpha = 0.2f))
            Spacer(Modifier.height(10.dp))

            // ── Device info ───────────────────────────────────────────────────
            if (deviceLabel != null) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        modifier = Modifier.size(8.dp).clip(CircleShape)
                            .background(if (isConnected) Color(0xFF4CAF50) else Color(0xFF9E9E9E)),
                    )
                    Spacer(Modifier.width(6.dp))
                    Icon(
                        Icons.Default.DevicesOther, null,
                        modifier = Modifier.size(14.dp),
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Spacer(Modifier.width(4.dp))
                    Text(
                        deviceLabel,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.weight(1f),
                    )
                    Text(
                        if (isConnected) "Online" else "Offline",
                        style = MaterialTheme.typography.labelSmall,
                        color = if (isConnected) Color(0xFF4CAF50)
                        else MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.5f),
                    )
                }
            } else {
                Text(
                    "Não ativo em nenhum dispositivo",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.6f),
                )
            }

            Spacer(Modifier.height(12.dp))

            // ── Actions ───────────────────────────────────────────────────────
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedButton(
                    onClick = onEdit,
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(8.dp),
                ) {
                    Icon(Icons.Default.Edit, null, Modifier.size(16.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("Editar")
                }
                Button(
                    onClick = onActivate,
                    modifier = Modifier.weight(1f),
                    shape = RoundedCornerShape(8.dp),
                ) {
                    Icon(Icons.Default.PlayArrow, null, Modifier.size(16.dp))
                    Spacer(Modifier.width(6.dp))
                    Text("Ativar")
                }
                IconButton(onClick = onDelete) {
                    Icon(
                        Icons.Default.Delete, null,
                        tint = MaterialTheme.colorScheme.error,
                    )
                }
            }
        }
    }
}
