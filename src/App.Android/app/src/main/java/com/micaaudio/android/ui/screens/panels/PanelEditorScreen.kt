package com.micaaudio.android.ui.screens.panels

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.data.api.AppCatalogItem
import com.micaaudio.android.data.api.PanelWidgetDefinition
import com.micaaudio.android.ui.theme.MicaPrimary

private const val SCALE = 2f // 1 pixel = 2 dp

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PanelEditorScreen(
    deviceId: String,
    onNavigateBack: () -> Unit,
    viewModel: PanelsViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    var showWidgetPicker by remember { mutableStateOf(false) }

    LaunchedEffect(deviceId) { viewModel.selectDevice(deviceId) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Editar Painel") },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, null) }
                },
                actions = {
                    TextButton(onClick = { viewModel.savePanel(); onNavigateBack() }) {
                        Text("SALVAR", fontWeight = FontWeight.Bold)
                    }
                },
            )
        },
        floatingActionButton = {
            FloatingActionButton(onClick = { showWidgetPicker = true }) {
                Icon(Icons.Default.Add, "Adicionar Widget")
            }
        },
    ) { innerPadding ->
        val panel = state.panelResponse?.panel

        if (state.isPanelLoading) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
            return@Scaffold
        }

        Column(Modifier.padding(innerPadding).fillMaxSize()) {
            // ── Touch-drag canvas ────────────────────────────────────────────
            val panelW = (panel?.width ?: 128)
            val panelH = (panel?.height ?: 64)
            val canvasW = (panelW * SCALE).dp
            val canvasH = (panelH * SCALE).dp

            var dragWidgetId by remember { mutableStateOf<String?>(null) }
            var dragAccX by remember { mutableFloatStateOf(0f) }
            var dragAccY by remember { mutableFloatStateOf(0f) }

            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(canvasH + 32.dp)
                    .padding(16.dp)
                    .clip(RoundedCornerShape(8.dp))
                    .background(Color.Black)
                    .border(1.dp, MaterialTheme.colorScheme.outline.copy(alpha = 0.4f), RoundedCornerShape(8.dp))
                    .pointerInput(panel?.widgets) {
                        detectDragGestures(
                            onDragStart = { offset ->
                                val widgets = panel?.widgets ?: return@detectDragGestures
                                dragWidgetId = widgets.firstOrNull { w ->
                                    val wx = w.x * SCALE
                                    val wy = w.y * SCALE
                                    val ww = w.width * SCALE
                                    val wh = w.height * SCALE
                                    offset.x >= wx && offset.x <= wx + ww &&
                                    offset.y >= wy && offset.y <= wy + wh
                                }?.widgetId
                                dragAccX = 0f
                                dragAccY = 0f
                                if (dragWidgetId != null) viewModel.selectWidget(dragWidgetId)
                            },
                            onDrag = { _, dragAmount ->
                                val id = dragWidgetId ?: return@detectDragGestures
                                dragAccX += dragAmount.x / SCALE
                                dragAccY += dragAmount.y / SCALE
                                val dx = dragAccX.toInt()
                                val dy = dragAccY.toInt()
                                if (dx != 0 || dy != 0) {
                                    viewModel.moveWidget(id, dx.toFloat(), dy.toFloat())
                                    dragAccX -= dx
                                    dragAccY -= dy
                                }
                            },
                            onDragEnd = { dragWidgetId = null },
                            onDragCancel = { dragWidgetId = null },
                        )
                    },
            ) {
                panel?.widgets?.forEach { widget ->
                    val isSelected = widget.widgetId == state.selectedWidgetId
                    Box(
                        modifier = Modifier
                            .offset(x = (widget.x * SCALE).dp, y = (widget.y * SCALE).dp)
                            .size(width = (widget.width * SCALE).dp, height = (widget.height * SCALE).dp)
                            .background(if (isSelected) MicaPrimary.copy(alpha = 0.6f) else MicaPrimary.copy(alpha = 0.3f))
                            .border(if (isSelected) 1.dp else 0.5.dp, MicaPrimary),
                        contentAlignment = Alignment.Center,
                    ) {
                        Text(
                            widget.appId.take(6),
                            color = Color.White,
                            fontSize = 8.sp,
                        )
                    }
                }
            }

            Text(
                "Widgets no Layout",
                style = MaterialTheme.typography.titleMedium,
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
            )

            LazyColumn(
                modifier = Modifier.weight(1f),
                contentPadding = PaddingValues(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                if (panel?.widgets.isNullOrEmpty()) {
                    item {
                        Text("Nenhum widget. Toque no + para começar.", style = MaterialTheme.typography.bodyMedium)
                    }
                } else {
                    items(panel!!.widgets, key = { it.widgetId }) { widget ->
                        WidgetEditorItem(
                            widget = widget,
                            isSelected = widget.widgetId == state.selectedWidgetId,
                            onSelect = { viewModel.selectWidget(widget.widgetId) },
                            onUpdate = { viewModel.updateWidget(it) },
                            onDelete = { viewModel.removeWidget(widget.widgetId) },
                        )
                    }
                }
            }
        }
    }

    if (showWidgetPicker) {
        WidgetPickerSheet(
            apps = state.availableApps,
            onDismiss = { showWidgetPicker = false },
            onWidgetSelected = { appId ->
                viewModel.addWidget(appId)
                showWidgetPicker = false
            },
        )
    }
}

@Composable
fun WidgetEditorItem(
    widget: PanelWidgetDefinition,
    isSelected: Boolean,
    onSelect: () -> Unit,
    onUpdate: (PanelWidgetDefinition) -> Unit,
    onDelete: () -> Unit,
) {
    Card(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onSelect),
        colors = CardDefaults.cardColors(
            containerColor = if (isSelected)
                MaterialTheme.colorScheme.primaryContainer
            else
                MaterialTheme.colorScheme.surfaceContainerHigh,
        ),
    ) {
        Column(Modifier.padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                Icon(Icons.Default.Widgets, null, tint = MicaPrimary, modifier = Modifier.size(20.dp))
                Spacer(Modifier.width(8.dp))
                Text(widget.appId.uppercase(), fontWeight = FontWeight.Bold, fontSize = 14.sp)
                Spacer(Modifier.weight(1f))
                IconButton(onClick = onDelete, modifier = Modifier.size(24.dp)) {
                    Icon(Icons.Default.Delete, null, tint = MaterialTheme.colorScheme.error, modifier = Modifier.size(20.dp))
                }
            }
            Spacer(Modifier.height(12.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                OutlinedTextField(
                    value = widget.x.toString(),
                    onValueChange = { onUpdate(widget.copy(x = it.toIntOrNull() ?: 0)) },
                    label = { Text("X") }, modifier = Modifier.weight(1f), singleLine = true,
                )
                OutlinedTextField(
                    value = widget.y.toString(),
                    onValueChange = { onUpdate(widget.copy(y = it.toIntOrNull() ?: 0)) },
                    label = { Text("Y") }, modifier = Modifier.weight(1f), singleLine = true,
                )
            }
            Spacer(Modifier.height(8.dp))
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                OutlinedTextField(
                    value = widget.width.toString(),
                    onValueChange = { onUpdate(widget.copy(width = it.toIntOrNull() ?: 1)) },
                    label = { Text("Largura") }, modifier = Modifier.weight(1f), singleLine = true,
                )
                OutlinedTextField(
                    value = widget.height.toString(),
                    onValueChange = { onUpdate(widget.copy(height = it.toIntOrNull() ?: 1)) },
                    label = { Text("Altura") }, modifier = Modifier.weight(1f), singleLine = true,
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun WidgetPickerSheet(
    apps: List<AppCatalogItem>,
    onDismiss: () -> Unit,
    onWidgetSelected: (String) -> Unit,
) {
    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(Modifier.padding(bottom = 32.dp)) {
            Text("Escolha um Widget", style = MaterialTheme.typography.titleLarge, modifier = Modifier.padding(16.dp))
            if (apps.isEmpty()) {
                val fallback = listOf("analogclock" to "Relógio", "gifhub75" to "GIF Player", "visualizer" to "Visualizador")
                fallback.forEach { (id, name) ->
                    ListItem(
                        headlineContent = { Text(name) },
                        supportingContent = { Text(id) },
                        leadingContent = { Icon(Icons.Default.AddCircle, null) },
                        modifier = Modifier.clickable { onWidgetSelected(id) },
                    )
                }
            } else {
                apps.forEach { app ->
                    ListItem(
                        headlineContent = { Text(app.name) },
                        supportingContent = { Text("${app.category} · ${app.summary}") },
                        leadingContent = { Icon(Icons.Default.AddCircle, null, tint = MicaPrimary) },
                        modifier = Modifier.clickable { onWidgetSelected(app.id) },
                    )
                }
            }
        }
    }
}
