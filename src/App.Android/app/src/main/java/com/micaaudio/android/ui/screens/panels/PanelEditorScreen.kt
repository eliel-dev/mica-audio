package com.micaaudio.android.ui.screens.panels

import android.app.Activity
import android.content.pm.ActivityInfo
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectDragGesturesAfterLongPress
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.AddCircle
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Widgets
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.data.api.PanelWidgetDefinition
import com.micaaudio.android.data.api.WidgetDefinition
import com.micaaudio.android.ui.theme.MicaPrimary

// DOCS: docs/wiki/modules/paineis.md#editor-hub75
private const val SCALE = 2f // 1 pixel = 2 dp

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PanelEditorScreen(
    panelId: String,
    onNavigateBack: () -> Unit,
    onWidgetConfig: (widgetId: String) -> Unit = {},
    viewModel: PanelsViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    var showWidgetPicker by remember { mutableStateOf(false) }
    val activity = LocalContext.current as? Activity

    LaunchedEffect(panelId) { viewModel.selectPanel(panelId) }
    DisposableEffect(activity) {
        val previousOrientation = activity?.requestedOrientation
        activity?.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_LANDSCAPE
        onDispose {
            if (previousOrientation != null) {
                activity?.requestedOrientation = previousOrientation
            }
        }
    }

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
                        detectDragGesturesAfterLongPress(
                            onDragStart = { offset ->
                                val widgets = panel?.widgets ?: return@detectDragGesturesAfterLongPress
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
                                val id = dragWidgetId ?: return@detectDragGesturesAfterLongPress
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
                            .clickable { viewModel.selectWidget(widget.widgetId) }
                            .background(if (isSelected) MicaPrimary.copy(alpha = 0.6f) else MicaPrimary.copy(alpha = 0.3f))
                            .border(if (isSelected) 1.dp else 0.5.dp, MicaPrimary),
                        contentAlignment = Alignment.Center,
                    ) {
                        Text(
                            widget.appId.take(6),
                            color = Color.White,
                            fontSize = 8.sp,
                        )
                        if (isSelected) {
                            ResizeHandles(
                                widgetId = widget.widgetId,
                                onResize = viewModel::resizeWidget,
                            )
                        }
                    }
                }
            }

            OutlinedTextField(
                value = panel?.name ?: "",
                onValueChange = { viewModel.updatePanelName(it) },
                label = { Text("Nome do painel") },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 16.dp, vertical = 4.dp),
                singleLine = true,
            )

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
                            onSelect = {
                                viewModel.selectWidget(widget.widgetId)
                                onWidgetConfig(widget.widgetId)
                            },
                            onDelete = { viewModel.removeWidget(widget.widgetId) },
                        )
                    }
                }
            }
        }
    }

    if (showWidgetPicker) {
        WidgetPickerSheet(
            widgets = state.availableWidgets,
            onDismiss = { showWidgetPicker = false },
            onWidgetSelected = { appId ->
                viewModel.addWidget(appId)
                showWidgetPicker = false
            },
        )
    }
}

@Composable
private fun BoxScope.ResizeHandles(
    widgetId: String,
    onResize: (String, Int, Int, Int, Int) -> Unit,
) {
    ResizeHandle.values().forEach { handle ->
        ResizeHandleDot(
            handle = handle,
            modifier = Modifier.align(handle.alignment),
            onDragDelta = { dx, dy ->
                onResize(
                    widgetId,
                    if (handle.left) dx else 0,
                    if (handle.top) dy else 0,
                    if (handle.right) dx else 0,
                    if (handle.bottom) dy else 0,
                )
            },
        )
    }
}

@Composable
private fun BoxScope.ResizeHandleDot(
    handle: ResizeHandle,
    modifier: Modifier = Modifier,
    onDragDelta: (Int, Int) -> Unit,
) {
    var dragAccX by remember { mutableFloatStateOf(0f) }
    var dragAccY by remember { mutableFloatStateOf(0f) }

    Box(
        modifier = modifier
            .size(if (handle.isCorner) 18.dp else 14.dp)
            .background(MaterialTheme.colorScheme.surface, CircleShape)
            .border(1.dp, MicaPrimary, CircleShape)
            .pointerInput(handle) {
                detectDragGestures(
                    onDragStart = {
                        dragAccX = 0f
                        dragAccY = 0f
                    },
                    onDrag = { _, dragAmount ->
                        dragAccX += dragAmount.x / SCALE
                        dragAccY += dragAmount.y / SCALE
                        val dx = dragAccX.toInt()
                        val dy = dragAccY.toInt()
                        if (dx != 0 || dy != 0) {
                            onDragDelta(dx, dy)
                            dragAccX -= dx
                            dragAccY -= dy
                        }
                    },
                )
            },
    )
}

private enum class ResizeHandle(
    val alignment: Alignment,
    val left: Boolean = false,
    val top: Boolean = false,
    val right: Boolean = false,
    val bottom: Boolean = false,
) {
    TopStart(Alignment.TopStart, left = true, top = true),
    Top(Alignment.TopCenter, top = true),
    TopEnd(Alignment.TopEnd, right = true, top = true),
    End(Alignment.CenterEnd, right = true),
    BottomEnd(Alignment.BottomEnd, right = true, bottom = true),
    Bottom(Alignment.BottomCenter, bottom = true),
    BottomStart(Alignment.BottomStart, left = true, bottom = true),
    Start(Alignment.CenterStart, left = true);

    val isCorner: Boolean get() = (left || right) && (top || bottom)
}

@Composable
fun WidgetEditorItem(
    widget: PanelWidgetDefinition,
    isSelected: Boolean,
    onSelect: () -> Unit,
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
        Row(
            modifier = Modifier.padding(horizontal = 16.dp, vertical = 12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Icon(Icons.Default.Widgets, null, tint = MicaPrimary, modifier = Modifier.size(20.dp))
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(widget.appId, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                Text(
                    "x=${widget.x} y=${widget.y}  ${widget.width}×${widget.height}",
                    fontSize = 12.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
            Icon(Icons.Default.ChevronRight, null, tint = MaterialTheme.colorScheme.onSurfaceVariant)
            Spacer(Modifier.width(4.dp))
            IconButton(onClick = onDelete, modifier = Modifier.size(32.dp)) {
                Icon(Icons.Default.Delete, null, tint = MaterialTheme.colorScheme.error, modifier = Modifier.size(18.dp))
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun WidgetPickerSheet(
    widgets: List<WidgetDefinition>,
    onDismiss: () -> Unit,
    onWidgetSelected: (String) -> Unit,
) {
    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(Modifier.padding(bottom = 32.dp)) {
            Text("Escolha um Widget", style = MaterialTheme.typography.titleLarge, modifier = Modifier.padding(16.dp))
            if (widgets.isEmpty()) {
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
                widgets.forEach { widget ->
                    ListItem(
                        headlineContent = { Text(widget.name) },
                        supportingContent = { Text("${widget.category} · ${widget.summary}") },
                        leadingContent = { Icon(Icons.Default.AddCircle, null, tint = MicaPrimary) },
                        modifier = Modifier.clickable { onWidgetSelected(widget.id) },
                    )
                }
            }
        }
    }
}
