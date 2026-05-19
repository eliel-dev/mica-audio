package com.micaaudio.android.ui.screens.panels

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
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
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material.icons.filled.PlayArrow
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.data.api.CatalogPanelResponse
import com.micaaudio.android.data.api.DeviceSnapshot
import com.micaaudio.android.ui.theme.MicaPrimary
import coil.compose.AsyncImage
import coil.request.ImageRequest
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.time.LocalTime
import java.time.format.DateTimeFormatter

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PanelsScreen(
    onEditPanel: (panelId: String) -> Unit = {},
    onCreatePanel: (panelId: String) -> Unit = {},
    viewModel: PanelsViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    var pendingDeleteEntry by remember { mutableStateOf<CatalogPanelResponse?>(null) }

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
                contentPadding = PaddingValues(
                    start = 16.dp,
                    top = 16.dp,
                    end = 16.dp,
                    bottom = 80.dp // Espaço extra para o FAB não cobrir o Switch do último card
                ),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                items(panelEntries, key = { it.panel.panelId }) { entry ->
                    val device = state.devices.find { it.deviceId == entry.activeOnDeviceId }
                    val isConnected = entry.activeOnDeviceId != null &&
                        entry.activeOnDeviceId in state.connectedDeviceIds

                    // Se o painel não estiver ativo, tentamos usar o ID do dispositivo selecionado
                    // ou do primeiro da lista para buscar as mídias da prévia.
                    val previewDeviceId = entry.activeOnDeviceId
                        ?: state.selectedDeviceId
                        ?: state.devices.firstOrNull()?.deviceId

                    ServerPanelCard(
                        entry = entry,
                        device = device,
                        isConnected = isConnected,
                        previewDeviceId = previewDeviceId,
                        serverUrl = state.serverUrl,
                        authToken = state.authToken,
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
private fun PanelPreview(
    widgets: List<com.micaaudio.android.data.api.PanelWidgetDefinition>,
    panelWidth: Int,
    panelHeight: Int,
    serverUrl: String,
    authToken: String,
    previewDeviceId: String?,
    modifier: Modifier = Modifier,
) {
    BoxWithConstraints(
        modifier = modifier
            .fillMaxWidth()
            .aspectRatio(panelWidth.toFloat() / panelHeight.toFloat())
            .background(Color.Black, RoundedCornerShape(4.dp))
            .border(0.5.dp, Color.White.copy(alpha = 0.1f), RoundedCornerShape(4.dp))
    ) {
        val scaleX = maxWidth.value / panelWidth
        val scaleY = maxHeight.value / panelHeight
        val scale = minOf(scaleX, scaleY)

        widgets.sortedBy { it.zIndex }.forEach { widget ->
            WidgetPreview(
                widget = widget,
                scale = scale,
                serverUrl = serverUrl,
                authToken = authToken,
                deviceId = previewDeviceId
            )
        }
    }
}

@Composable
private fun WidgetPreview(
    widget: com.micaaudio.android.data.api.PanelWidgetDefinition,
    scale: Float,
    serverUrl: String,
    authToken: String,
    deviceId: String?
) {
    val context = LocalContext.current
    Box(
        modifier = Modifier
            .offset(x = (widget.x * scale).dp, y = (widget.y * scale).dp)
            .size(width = (widget.width * scale).dp, height = (widget.height * scale).dp)
            .clip(RoundedCornerShape((1 * scale).dp)),
        contentAlignment = Alignment.Center
    ) {
        when {
            widget.appId == "gifhub75" -> {
                val mediaIds = remember(widget.runtimeState) {
                    widget.runtimeState["mediaIds"]?.split(",")?.map { it.trim() }?.filter { it.isNotEmpty() }
                        ?: widget.runtimeState["mediaId"]?.takeIf { it.isNotEmpty() }?.let { listOf(it) }
                        ?: emptyList()
                }

                var currentIndex by remember { mutableIntStateOf(0) }

                if (mediaIds.size > 1) {
                    LaunchedEffect(mediaIds) {
                        while (true) {
                            kotlinx.coroutines.delay(3000) // Troca a cada 3 segundos
                            currentIndex = (currentIndex + 1) % mediaIds.size
                        }
                    }
                }

                val mediaId = mediaIds.getOrNull(currentIndex)

                if (!mediaId.isNullOrBlank() && !deviceId.isNullOrBlank() && serverUrl.isNotBlank()) {
                    val imageUrl = "$serverUrl/api/v1/admin/devices/$deviceId/media/$mediaId"
                    AsyncImage(
                        model = ImageRequest.Builder(context)
                            .data(imageUrl)
                            .setHeader("Authorization", "Bearer $authToken")
                            .crossfade(enable = true)
                            .build(),
                        contentDescription = null,
                        modifier = Modifier.fillMaxSize(),
                        contentScale = ContentScale.Crop
                    )
                } else {
                    Box(Modifier.fillMaxSize().background(MicaPrimary.copy(alpha = 0.3f))) {
                        Icon(
                            Icons.Default.Dashboard,
                            null,
                            modifier = Modifier.size((12 * scale).dp),
                            tint = Color.White.copy(alpha = 0.5f)
                        )
                    }
                }
            }

            widget.appId.contains("clock") -> {
                val time = remember { LocalTime.now().format(DateTimeFormatter.ofPattern("HH:mm")) }
                Box(
                    modifier = Modifier.fillMaxSize().background(Color(0xFF001F3F)),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        time,
                        color = Color.Cyan,
                        fontSize = (widget.height * scale * 0.5f).sp,
                        fontWeight = FontWeight.Bold,
                        textAlign = TextAlign.Center
                    )
                }
            }

            else -> {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(MicaPrimary.copy(alpha = 0.4f))
                        .border(0.5.dp, MicaPrimary.copy(alpha = 0.6f))
                )
            }
        }
    }
}

@Composable
private fun ServerPanelCard(
    entry: CatalogPanelResponse,
    device: DeviceSnapshot?,
    isConnected: Boolean,
    previewDeviceId: String?,
    serverUrl: String,
    authToken: String,
    onActivate: () -> Unit,
    onEdit: () -> Unit,
    onDelete: () -> Unit,
) {
    val panel = entry.panel
    val deviceLabel = when {
        device != null -> device.name.ifBlank { device.deviceId.take(12) }
        entry.activeOnDeviceId != null -> entry.activeOnDeviceId.take(12)
        else -> null
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onEdit),
        colors = CardDefaults.cardColors(
            containerColor = if (isConnected)
                MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.25f)
            else
                MaterialTheme.colorScheme.surfaceContainer,
        ),
        border = if (isConnected)
            CardDefaults.outlinedCardBorder()
                .copy(brush = SolidColor(MicaPrimary.copy(alpha = 0.5f)), width = 1.dp)
        else null,
    ) {
        Column(Modifier.padding(12.dp)) {
            // ── Preview area with Options button ──────────────────────────────
            Box(modifier = Modifier.fillMaxWidth()) {
                PanelPreview(
                    widgets = panel.widgets,
                    panelWidth = panel.width,
                    panelHeight = panel.height,
                    serverUrl = serverUrl,
                    authToken = authToken,
                    previewDeviceId = previewDeviceId
                )

                var showMenu by remember { mutableStateOf(false) }
                Box(modifier = Modifier.align(Alignment.TopEnd)) {
                    IconButton(onClick = { showMenu = true }) {
                        Icon(
                            Icons.Default.MoreVert,
                            contentDescription = "Opções",
                            tint = Color.White.copy(alpha = 0.7f)
                        )
                    }
                    DropdownMenu(
                        expanded = showMenu,
                        onDismissRequest = { showMenu = false }
                    ) {
                        DropdownMenuItem(
                            text = { Text("Excluir", color = MaterialTheme.colorScheme.error) },
                            leadingIcon = {
                                Icon(
                                    Icons.Default.Delete,
                                    null,
                                    tint = MaterialTheme.colorScheme.error
                                )
                            },
                            onClick = {
                                showMenu = false
                                onDelete()
                            }
                        )
                    }
                }
            }

            Spacer(Modifier.height(12.dp))

            // ── Panel info + Status ──────────────────────────────────────────
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.SpaceBetween,
                modifier = Modifier.fillMaxWidth()
            ) {
                Column(Modifier.weight(1f)) {
                    Text(
                        panel.name.ifBlank { "Painel" },
                        fontWeight = FontWeight.Bold,
                        fontSize = 15.sp
                    )
                    if (deviceLabel != null) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Box(
                                modifier = Modifier
                                    .size(6.dp)
                                    .clip(CircleShape)
                                    .background(if (isConnected) Color(0xFF4CAF50) else Color(0xFF9E9E9E)),
                            )
                            Spacer(Modifier.width(6.dp))
                            Text(
                                deviceLabel,
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }
                    }
                }

                Switch(
                    checked = isConnected,
                    onCheckedChange = { onActivate() },
                    thumbContent = if (isConnected) {
                        {
                            Icon(
                                imageVector = Icons.Default.PlayArrow,
                                contentDescription = null,
                                modifier = Modifier.size(16.dp),
                            )
                        }
                    } else null
                )
            }
        }
    }
}
