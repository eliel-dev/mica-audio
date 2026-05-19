package com.micaaudio.android.ui.screens.panels

import android.content.pm.ActivityInfo
import androidx.activity.compose.BackHandler
import androidx.activity.compose.LocalActivity
import androidx.core.view.WindowCompat
import androidx.core.view.WindowInsetsCompat
import androidx.core.view.WindowInsetsControllerCompat
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.gestures.detectDragGesturesAfterLongPress
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.itemsIndexed
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.AddCircle
import androidx.compose.material.icons.filled.ArrowDownward
import androidx.compose.material.icons.filled.ArrowUpward
import androidx.compose.material.icons.filled.ChevronRight
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Fullscreen
import androidx.compose.material.icons.filled.FullscreenExit
import androidx.compose.material.icons.filled.Widgets
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
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
    var showWidgetPicker by rememberSaveable { mutableStateOf(false) }
    var isFullScreen by rememberSaveable { mutableStateOf(false) }
    val activity = LocalActivity.current

    // Save before navigating away — covers both the system back gesture and the
    // back arrow in the TopAppBar. Without this the async savePanel() coroutine
    // would be cancelled by the back-stack pop before the HTTP call completes.
    BackHandler { viewModel.savePanel(onSaved = onNavigateBack) }

    LaunchedEffect(isFullScreen) {
        val window = activity?.window ?: return@LaunchedEffect
        val controller = WindowCompat.getInsetsController(window, window.decorView)

        if (isFullScreen) {
            activity.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_SENSOR
            controller.hide(WindowInsetsCompat.Type.systemBars())
            controller.systemBarsBehavior =
                WindowInsetsControllerCompat.BEHAVIOR_SHOW_TRANSIENT_BARS_BY_SWIPE
        } else {
            activity.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_PORTRAIT
            controller.show(WindowInsetsCompat.Type.systemBars())
        }
    }

    DisposableEffect(Unit) {
        onDispose {
            val window = activity?.window ?: return@onDispose
            val controller = WindowCompat.getInsetsController(window, window.decorView)
            controller.show(WindowInsetsCompat.Type.systemBars())
            activity.requestedOrientation = ActivityInfo.SCREEN_ORIENTATION_UNSPECIFIED
        }
    }

    LaunchedEffect(panelId) { viewModel.selectPanel(panelId) }

    Scaffold(
        topBar = {
            if (!isFullScreen) {
                TopAppBar(
                    title = { Text("Editar Painel") },
                    navigationIcon = {
                        IconButton(onClick = { viewModel.savePanel(onSaved = onNavigateBack) }) {
                            Icon(
                                Icons.AutoMirrored.Filled.ArrowBack,
                                null
                            )
                        }
                    },
                    actions = {
                        TextButton(onClick = { viewModel.savePanel(onSaved = onNavigateBack) }) {
                            Text("SALVAR", fontWeight = FontWeight.Bold)
                        }
                    },
                )
            }
        },
        floatingActionButton = {
            FloatingActionButton(onClick = { showWidgetPicker = true }) {
                Icon(Icons.Default.Add, "Adicionar Widget")
            }
        },
    ) { innerPadding ->
        val panel = state.panelResponse?.panel

        if (state.isPanelLoading) {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
            return@Scaffold
        }

        Column(
            Modifier
                .then(if (isFullScreen) Modifier.fillMaxSize() else Modifier.padding(innerPadding))
                .fillMaxSize()
        ) {
            // ── Touch-drag canvas ────────────────────────────────────────────
            val panelW = (panel?.width ?: 128)
            val panelH = (panel?.height ?: 64)

            var dragWidgetId by remember { mutableStateOf<String?>(null) }
            var dragAccX by remember { mutableFloatStateOf(0f) }
            var dragAccY by remember { mutableFloatStateOf(0f) }

            BoxWithConstraints(
                modifier = Modifier
                    .then(
                        if (isFullScreen) Modifier.fillMaxSize()
                        else Modifier
                            .padding(16.dp)
                            .aspectRatio(panelW.toFloat() / panelH.toFloat())
                            .fillMaxWidth()
                            .align(Alignment.CenterHorizontally)
                    )
                    .clip(RoundedCornerShape(8.dp))
                    .background(Color.Black)
                    .border(
                        1.dp,
                        MaterialTheme.colorScheme.outline.copy(alpha = 0.4f),
                        RoundedCornerShape(8.dp)
                    ),
                contentAlignment = Alignment.Center
            ) {
                // Fator de escala dinâmico para preencher o espaço disponível
                val dynamicScale = remember(maxWidth, maxHeight, panelW, panelH) {
                    val scaleX = maxWidth.value / panelW
                    val scaleY = maxHeight.value / panelH
                    minOf(scaleX, scaleY)
                }

                Box(
                    modifier = Modifier
                        .size(
                            width = (panelW * dynamicScale).dp,
                            height = (panelH * dynamicScale).dp
                        )
                        .background(Color.DarkGray.copy(alpha = 0.1f))
                        .pointerInput(panelW, panelH, dynamicScale) {
                            detectDragGestures(
                                onDragStart = { offset ->
                                    val widgets = panel?.widgets ?: return@detectDragGestures
                                    val selectedId = state.selectedWidgetId

                                    // 1. Verificar se o toque foi dentro do widget que já está selecionado
                                    val selectedWidget = widgets.find { it.widgetId == selectedId }
                                    val hitSelected = selectedWidget?.let { w ->
                                        val wx = (w.x * dynamicScale).dp.toPx()
                                        val wy = (w.y * dynamicScale).dp.toPx()
                                        val ww = (w.width * dynamicScale).dp.toPx()
                                        val wh = (w.height * dynamicScale).dp.toPx()
                                        offset.x >= wx && offset.x <= wx + ww &&
                                        offset.y >= wy && offset.y <= wy + wh
                                    } ?: false

                                    // 2. Se tocou no selecionado, priorizar ele. Senão, procurar o do topo.
                                    dragWidgetId = if (hitSelected) {
                                        selectedId
                                    } else {
                                        widgets.findLast { w ->
                                            val wx = (w.x * dynamicScale).dp.toPx()
                                            val wy = (w.y * dynamicScale).dp.toPx()
                                            val ww = (w.width * dynamicScale).dp.toPx()
                                            val wh = (w.height * dynamicScale).dp.toPx()
                                            offset.x >= wx && offset.x <= wx + ww &&
                                            offset.y >= wy && offset.y <= wy + wh
                                        }?.widgetId
                                    }

                                    dragAccX = 0f
                                    dragAccY = 0f

                                    dragWidgetId?.let { viewModel.selectWidget(it) }
                                },
                                onDrag = { change, dragAmount ->
                                    if (dragWidgetId != null) {
                                        change.consume()
                                        // Converter px do dragAmount para Dp e depois remover a escala
                                        // Usando floats para manter a precisão do movimento
                                        dragAccX += (dragAmount.x / density) / dynamicScale
                                        dragAccY += (dragAmount.y / density) / dynamicScale

                                        // Só mover o widget quando o acumulador atingir pelo menos 1 pixel do painel
                                        val dx = if (dragAccX >= 1f || dragAccX <= -1f) dragAccX.toInt() else 0
                                        val dy = if (dragAccY >= 1f || dragAccY <= -1f) dragAccY.toInt() else 0

                                        if (dx != 0 || dy != 0) {
                                            viewModel.moveWidget(dragWidgetId!!, dx.toFloat(), dy.toFloat())
                                            dragAccX -= dx
                                            dragAccY -= dy
                                        }
                                    }
                                },
                                onDragEnd = {
                                    dragWidgetId = null
                                    dragAccX = 0f
                                    dragAccY = 0f
                                },
                                onDragCancel = {
                                    dragWidgetId = null
                                    dragAccX = 0f
                                    dragAccY = 0f
                                },
                            )
                        },
                ) {
                    panel?.widgets?.forEach { widget ->
                        val isSelected = widget.widgetId == state.selectedWidgetId
                        Box(
                            modifier = Modifier
                                .offset(
                                    x = (widget.x * dynamicScale).dp,
                                    y = (widget.y * dynamicScale).dp
                                )
                                .size(
                                    width = (widget.width * dynamicScale).dp,
                                    height = (widget.height * dynamicScale).dp
                                )
                                .clickable { viewModel.selectWidget(widget.widgetId) }
                                .background(
                                    if (isSelected) MicaPrimary.copy(alpha = 0.6f)
                                    else MicaPrimary.copy(alpha = 0.3f)
                                )
                                .border(if (isSelected) 1.dp else 0.5.dp, MicaPrimary),
                            contentAlignment = Alignment.Center,
                        ) {
                            Text(
                                widget.appId.take(6),
                                color = Color.White,
                                fontSize = (4 * dynamicScale).sp,
                            )
                            if (isSelected) {
                                ResizeHandles(
                                    widgetId = widget.widgetId,
                                    scale = dynamicScale,
                                    currentWidth = widget.width,
                                    currentHeight = widget.height,
                                    onResize = viewModel::resizeWidget,
                                )
                            }
                        }
                    }
                }

                // Botão de Tela Cheia / Sair da Tela Cheia
                IconButton(
                    onClick = { isFullScreen = !isFullScreen },
                    modifier = Modifier
                        .align(Alignment.TopEnd)
                        .padding(8.dp)
                        .background(Color.Black.copy(alpha = 0.5f), CircleShape)
                ) {
                    Icon(
                        imageVector = if (isFullScreen) Icons.Default.FullscreenExit else Icons.Default.Fullscreen,
                        contentDescription = "Tela Cheia",
                        tint = Color.White
                    )
                }
            }

            if (!isFullScreen) {
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
                            Text(
                                "Nenhum widget. Toque no + para começar.",
                                style = MaterialTheme.typography.bodyMedium
                            )
                        }
                    } else {
                        // Reverter a lista para que a camada superior (índice maior) apareça primeiro na UI
                        val widgetsInOrder = panel!!.widgets.reversed()
                        itemsIndexed(widgetsInOrder, key = { _, w -> w.widgetId }) { index, widget ->
                            val actualIndex = panel.widgets.indexOf(widget)
                            WidgetEditorItem(
                                widget = widget,
                                layerIndex = actualIndex,
                                isFirst = actualIndex == 0,
                                isLast = actualIndex == panel.widgets.size - 1,
                                isSelected = widget.widgetId == state.selectedWidgetId,
                                onSelect = {
                                    viewModel.selectWidget(widget.widgetId)
                                    onWidgetConfig(widget.widgetId)
                                },
                                onDelete = { viewModel.removeWidget(widget.widgetId) },
                                onMoveLayer = { up -> viewModel.moveWidgetLayer(widget.widgetId, up) }
                            )
                        }
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
    scale: Float,
    currentWidth: Int,
    currentHeight: Int,
    onResize: (String, Int, Int, Int, Int) -> Unit,
) {
    ResizeHandle.entries.forEach { handle ->
        ResizeHandleDot(
            handle = handle,
            scale = scale,
            modifier = Modifier.align(handle.alignment),
            onDragDelta = { dx, dy ->
                // Impedir que o widget fique menor que 29x29 pixels
                val minSize = 15

                val finalDx = if (handle.left && currentWidth - dx < minSize) currentWidth - minSize
                else if (handle.right && currentWidth + dx < minSize) minSize - currentWidth
                else dx

                val finalDy = if (handle.top && currentHeight - dy < minSize) currentHeight - minSize
                else if (handle.bottom && currentHeight + dy < minSize) minSize - currentHeight
                else dy

                if (finalDx != 0 || finalDy != 0) {
                    onResize(
                        widgetId,
                        if (handle.left) finalDx else 0,
                        if (handle.top) finalDy else 0,
                        if (handle.right) finalDx else 0,
                        if (handle.bottom) finalDy else 0,
                    )
                }
            },
        )
    }
}

@Composable
private fun BoxScope.ResizeHandleDot(
    handle: ResizeHandle,
    scale: Float,
    modifier: Modifier = Modifier,
    onDragDelta: (Int, Int) -> Unit,
) {
    // Usando referências estáveis para os acumuladores
    var dragAccX by remember { mutableFloatStateOf(0f) }
    var dragAccY by remember { mutableFloatStateOf(0f) }

    Box(
        modifier = modifier
            .size(if (handle.isCorner) 18.dp else 14.dp)
            .background(MaterialTheme.colorScheme.surface, CircleShape)
            .border(1.dp, MicaPrimary, CircleShape)
            .pointerInput(handle, scale) {
                detectDragGestures(
                    onDragStart = {
                        dragAccX = 0f
                        dragAccY = 0f
                    },
                    onDrag = { change, dragAmount ->
                        change.consume()
                        // Converter pixels da tela para pixels do painel
                        dragAccX += (dragAmount.x / density) / scale
                        dragAccY += (dragAmount.y / density) / scale

                        val dx = if (dragAccX >= 1f || dragAccX <= -1f) dragAccX.toInt() else 0
                        val dy = if (dragAccY >= 1f || dragAccY <= -1f) dragAccY.toInt() else 0

                        if (dx != 0 || dy != 0) {
                            onDragDelta(dx, dy)
                            dragAccX -= dx
                            dragAccY -= dy
                        }
                    },
                    onDragEnd = {
                        dragAccX = 0f
                        dragAccY = 0f
                    },
                    onDragCancel = {
                        dragAccX = 0f
                        dragAccY = 0f
                    }
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
    layerIndex: Int,
    isFirst: Boolean,
    isLast: Boolean,
    isSelected: Boolean,
    onSelect: () -> Unit,
    onDelete: () -> Unit,
    onMoveLayer: (up: Boolean) -> Unit,
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onSelect),
        colors = CardDefaults.cardColors(
            containerColor = if (isSelected)
                MaterialTheme.colorScheme.primaryContainer
            else
                MaterialTheme.colorScheme.surfaceContainerHigh,
        ),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Surface(
                color = MicaPrimary.copy(alpha = 0.1f),
                shape = CircleShape,
                modifier = Modifier.size(24.dp)
            ) {
                Box(contentAlignment = Alignment.Center) {
                    Text(
                        (layerIndex + 1).toString(),
                        fontSize = 12.sp,
                        fontWeight = FontWeight.Bold,
                        color = MicaPrimary
                    )
                }
            }
            Spacer(Modifier.width(12.dp))
            Column(Modifier.weight(1f)) {
                Text(widget.appId, fontWeight = FontWeight.Bold, fontSize = 14.sp)
                Text(
                    "Camada ${layerIndex + 1}",
                    fontSize = 11.sp,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            IconButton(onClick = { onMoveLayer(true) }, enabled = !isLast, modifier = Modifier.size(32.dp)) {
                Icon(Icons.Default.ArrowUpward, null, modifier = Modifier.size(18.dp))
            }
            IconButton(onClick = { onMoveLayer(false) }, enabled = !isFirst, modifier = Modifier.size(32.dp)) {
                Icon(Icons.Default.ArrowDownward, null, modifier = Modifier.size(18.dp))
            }

            Spacer(Modifier.width(4.dp))
            IconButton(onClick = onDelete, modifier = Modifier.size(32.dp)) {
                Icon(
                    Icons.Default.Delete,
                    null,
                    tint = MaterialTheme.colorScheme.error,
                    modifier = Modifier.size(18.dp)
                )
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
                val fallback = listOf("analogclock" to "Relógio", "gifhub75" to "Imagem/GIF", "visualizer" to "Visualizador")
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
