package com.micaaudio.android.ui.screens.panels

import android.net.Uri
import android.graphics.BitmapFactory
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items as gridItems
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Image
import androidx.compose.material.icons.filled.Remove
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.data.api.ModifierFieldType
import com.micaaudio.android.data.api.PanelWidgetDefinition
import com.micaaudio.android.data.api.WidgetModifier
import com.micaaudio.android.ui.theme.MicaPrimary
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

// DOCS: docs/wiki/modules/paineis.md#editor-hub75
// DOCS: docs/wiki/modules/device-server-protocol.md#atualizacao-2026-04-admin-api-e-winui-remote
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun WidgetConfigScreen(
    widgetId: String,
    deviceId: String,
    viewModel: PanelsViewModel,
    onNavigateBack: () -> Unit,
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val widget = state.panelResponse?.panel?.widgets?.firstOrNull { it.widgetId == widgetId }
    val catalogItem = widget?.let { w -> state.availableWidgets.firstOrNull { it.id == w.appId } }
    var showMediaGrid by remember { mutableStateOf(false) }
    val mediaDeviceId = deviceId.ifBlank {
        state.selectedDeviceId ?: state.devices.firstOrNull()?.deviceId.orEmpty()
    }

    LaunchedEffect(mediaDeviceId) {
        viewModel.loadMediaForDevice(mediaDeviceId)
    }

    if (widget == null) {
        onNavigateBack()
        return
    }

    val context = LocalContext.current

    val mediaPicker = rememberLauncherForActivityResult(
        ActivityResultContracts.GetContent()
    ) { uri: Uri? ->
        if (uri == null) return@rememberLauncherForActivityResult
        val ext = context.contentResolver.getType(uri)?.let { mime ->
            when {
                mime.contains("gif") -> ".gif"
                mime.contains("png") -> ".png"
                mime.contains("jpeg") || mime.contains("jpg") -> ".jpg"
                mime.contains("bmp") -> ".bmp"
                else -> ".gif"
            }
        } ?: ".gif"
        val shortId = widgetId.replace("-", "").take(8)
        val mediaId = "$shortId-${System.currentTimeMillis()}$ext"
        val bytes = context.contentResolver.openInputStream(uri)?.use { it.readBytes() } ?: return@rememberLauncherForActivityResult
        viewModel.uploadMedia(mediaDeviceId, mediaId, bytes)
    }

    if (showMediaGrid) {
        val selectedIds = selectedMediaIds(widget)
        MediaGridScreen(
            mediaIds = state.deviceMedia,
            mediaCache = state.deviceMediaCache,
            selectedIds = selectedIds,
            isLoading = state.isMediaLoading,
            onNavigateBack = { showMediaGrid = false },
            onAddMedia = { mediaPicker.launch("image/*") },
            onMediaClick = { mediaId ->
                val newIds = if (mediaId in selectedIds) {
                    (selectedIds - mediaId).joinToString(",")
                } else {
                    (selectedIds + mediaId).joinToString(",")
                }
                viewModel.updateWidget(
                    widget.copy(runtimeState = widget.runtimeState + ("mediaIds" to newIds))
                )
            },
        )
        return
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(catalogItem?.name ?: widget.appId, fontWeight = FontWeight.Bold)
                        Text(widget.appId, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, null)
                    }
                },
                actions = {
                    TextButton(onClick = { viewModel.savePanel(); onNavigateBack() }) {
                        Text("SALVAR", fontWeight = FontWeight.Bold)
                    }
                    IconButton(onClick = {
                        viewModel.removeWidget(widgetId)
                        onNavigateBack()
                    }) {
                        Icon(Icons.Default.Delete, null, tint = MaterialTheme.colorScheme.error)
                    }
                },
            )
        },
    ) { innerPadding ->
        LazyColumn(
            modifier = Modifier
                .padding(innerPadding)
                .fillMaxSize(),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            // ── Posição e tamanho ────────────────────────────────────────────
            item {
                val panelW = state.panelResponse?.panel?.width ?: 128
                val panelH = state.panelResponse?.panel?.height ?: 64
                SectionCard(title = "Posição e tamanho") {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        StepperField(
                            label = "X", value = widget.x, modifier = Modifier.weight(1f),
                            min = 0, max = panelW - widget.width.coerceAtLeast(1),
                        ) { viewModel.updateWidget(widget.copy(x = it)) }
                        StepperField(
                            label = "Y", value = widget.y, modifier = Modifier.weight(1f),
                            min = 0, max = panelH - widget.height.coerceAtLeast(1),
                        ) { viewModel.updateWidget(widget.copy(y = it)) }
                    }
                    Spacer(Modifier.height(8.dp))
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        StepperField(
                            label = "Largura", value = widget.width, modifier = Modifier.weight(1f),
                            min = 1, max = panelW - widget.x,
                        ) { viewModel.updateWidget(widget.copy(width = it)) }
                        StepperField(
                            label = "Altura", value = widget.height, modifier = Modifier.weight(1f),
                            min = 1, max = panelH - widget.y,
                        ) { viewModel.updateWidget(widget.copy(height = it)) }
                    }
                    Spacer(Modifier.height(8.dp))
                    StepperField(
                        label = "Z-Index (camada)", value = widget.zIndex,
                        modifier = Modifier.fillMaxWidth(), min = 0, max = 99,
                    ) { viewModel.updateWidget(widget.copy(zIndex = it)) }
                }
            }

            // ── Configurações (modifiers dinâmicos) ──────────────────────────
            if (!catalogItem?.modifiers.isNullOrEmpty()) {
                item {
                    SectionCard(title = "Configurações") {
                        catalogItem!!.modifiers.forEachIndexed { index, modifier ->
                            if (index > 0) Spacer(Modifier.height(12.dp))
                            ModifierField(
                                modifier = modifier,
                                currentValue = widget.configValues[modifier.key] ?: modifier.defaultValue ?: "",
                                currentToggle = (widget.configValues[modifier.key] ?: if (modifier.defaultToggle == true) "true" else "false") == "true",
                                onValueChange = { newVal ->
                                    viewModel.updateWidget(
                                        widget.copy(configValues = widget.configValues + (modifier.key to newVal))
                                    )
                                },
                            )
                        }
                    }
                }
            }

            // ── Mídia (só para gifhub75) ─────────────────────────────────────
            if (widget.appId == "gifhub75") {
                item {
                    val selectedIds = selectedMediaIds(widget)

                    SectionCard(title = "Mídia") {
                        if (selectedIds.isNotEmpty()) {
                            Text(
                                "Selecionada(s):",
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Medium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            Spacer(Modifier.height(6.dp))
                            LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                items(selectedIds) { id ->
                                    MediaChip(
                                        label = id,
                                        selected = true,
                                        onClick = {
                                            val newIds = (selectedIds - id).joinToString(",")
                                            viewModel.updateWidget(
                                                widget.copy(runtimeState = widget.runtimeState + ("mediaIds" to newIds))
                                            )
                                        },
                                    )
                                }
                            }
                            Spacer(Modifier.height(12.dp))
                        }

                        OutlinedButton(
                            onClick = { showMediaGrid = true },
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Icon(Icons.Default.Image, null, modifier = Modifier.size(18.dp))
                            Spacer(Modifier.width(8.dp))
                            Text("Abrir grade de mídia")
                        }
                        Spacer(Modifier.height(12.dp))

                        Text(
                            "Biblioteca do servidor:",
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Medium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                        Spacer(Modifier.height(6.dp))

                        if (state.isMediaLoading) {
                            CircularProgressIndicator(modifier = Modifier.size(24.dp))
                        } else if (state.deviceMedia.isEmpty()) {
                            Text(
                                "Nenhuma mídia no servidor para este dispositivo.",
                                fontSize = 13.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                        } else {
                            Column(verticalArrangement = Arrangement.spacedBy(6.dp)) {
                                state.deviceMedia.forEach { mediaId ->
                                    val isSelected = mediaId in selectedIds
                                    ServerMediaItem(
                                        mediaId = mediaId,
                                        isSelected = isSelected,
                                        onClick = {
                                            val newIds = if (isSelected) {
                                                (selectedIds - mediaId).joinToString(",")
                                            } else {
                                                (selectedIds + mediaId).joinToString(",")
                                            }
                                            viewModel.updateWidget(
                                                widget.copy(runtimeState = widget.runtimeState + ("mediaIds" to newIds))
                                            )
                                        },
                                    )
                                }
                            }
                        }

                        Spacer(Modifier.height(12.dp))
                        OutlinedButton(
                            onClick = { mediaPicker.launch("image/*") },
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Icon(Icons.Default.Add, null, modifier = Modifier.size(18.dp))
                            Spacer(Modifier.width(8.dp))
                            Text("Adicionar mídia do dispositivo")
                        }
                    }
                }
            }

            item { Spacer(Modifier.height(32.dp)) }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun MediaGridScreen(
    mediaIds: List<String>,
    mediaCache: Map<String, String>,
    selectedIds: List<String>,
    isLoading: Boolean,
    onNavigateBack: () -> Unit,
    onAddMedia: () -> Unit,
    onMediaClick: (String) -> Unit,
) {
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Mídia", fontWeight = FontWeight.Bold) },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, null)
                    }
                },
                actions = {
                    IconButton(onClick = onAddMedia) {
                        Icon(Icons.Default.Add, "Adicionar mídia")
                    }
                },
            )
        },
    ) { innerPadding ->
        when {
            isLoading -> Box(
                Modifier.fillMaxSize().padding(innerPadding),
                contentAlignment = Alignment.Center,
            ) {
                CircularProgressIndicator()
            }

            mediaIds.isEmpty() -> Box(
                Modifier.fillMaxSize().padding(innerPadding),
                contentAlignment = Alignment.Center,
            ) {
                OutlinedButton(onClick = onAddMedia) {
                    Icon(Icons.Default.Add, null, modifier = Modifier.size(18.dp))
                    Spacer(Modifier.width(8.dp))
                    Text("Adicionar mídia")
                }
            }

            else -> LazyVerticalGrid(
                columns = GridCells.Adaptive(112.dp),
                modifier = Modifier.fillMaxSize().padding(innerPadding),
                contentPadding = PaddingValues(16.dp),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                gridItems(mediaIds, key = { it }) { mediaId ->
                    MediaGridItem(
                        mediaId = mediaId,
                        filePath = mediaCache[mediaId],
                        isSelected = mediaId in selectedIds,
                        onClick = { onMediaClick(mediaId) },
                    )
                }
            }
        }
    }
}

@Composable
private fun MediaGridItem(
    mediaId: String,
    filePath: String?,
    isSelected: Boolean,
    onClick: () -> Unit,
) {
    val imageBitmap by produceState<androidx.compose.ui.graphics.ImageBitmap?>(initialValue = null, filePath) {
        value = withContext(Dispatchers.IO) {
            filePath?.let { BitmapFactory.decodeFile(it)?.asImageBitmap() }
        }
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
        colors = CardDefaults.cardColors(
            containerColor = if (isSelected)
                MaterialTheme.colorScheme.primaryContainer
            else
                MaterialTheme.colorScheme.surfaceContainerHigh,
        ),
        border = if (isSelected) CardDefaults.outlinedCardBorder() else null,
    ) {
        Column {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .aspectRatio(1f)
                    .clip(MaterialTheme.shapes.small)
                    .background(Color.Black),
                contentAlignment = Alignment.Center,
            ) {
                val bitmap = imageBitmap
                if (bitmap != null) {
                    Image(
                        bitmap = bitmap,
                        contentDescription = mediaId,
                        contentScale = ContentScale.Crop,
                        modifier = Modifier.fillMaxSize(),
                    )
                } else {
                    Icon(
                        Icons.Default.Image,
                        null,
                        tint = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.size(32.dp),
                    )
                }

                if (isSelected) {
                    Surface(
                        color = MicaPrimary,
                        shape = MaterialTheme.shapes.small,
                        modifier = Modifier.align(Alignment.TopEnd).padding(6.dp),
                    ) {
                        Icon(
                            Icons.Default.Check,
                            null,
                            tint = Color.White,
                            modifier = Modifier.size(18.dp).padding(2.dp),
                        )
                    }
                }
            }
            Text(
                mediaId,
                fontSize = 11.sp,
                maxLines = 2,
                modifier = Modifier.padding(8.dp),
            )
        }
    }
}

private fun selectedMediaIds(widget: PanelWidgetDefinition): List<String> {
    return widget.runtimeState["mediaIds"]
        ?.split(",")
        ?.map { it.trim() }
        ?.filter { it.isNotEmpty() }
        ?: widget.runtimeState["mediaId"]
            ?.takeIf { it.isNotEmpty() }
            ?.let { listOf(it) }
        ?: emptyList()
}

@Composable
private fun SectionCard(title: String, content: @Composable ColumnScope.() -> Unit) {
    Column {
        Text(
            title,
            style = MaterialTheme.typography.titleSmall,
            color = MicaPrimary,
            modifier = Modifier.padding(bottom = 8.dp),
        )
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainerHigh),
        ) {
            Column(modifier = Modifier.padding(16.dp)) {
                content()
            }
        }
    }
}

@Composable
private fun StepperField(
    label: String,
    value: Int,
    modifier: Modifier = Modifier,
    min: Int = 0,
    max: Int = Int.MAX_VALUE,
    onValueChange: (Int) -> Unit,
) {
    Column(modifier) {
        Text(label, fontSize = 11.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Spacer(Modifier.height(2.dp))
        Row(verticalAlignment = Alignment.CenterVertically) {
            FilledTonalIconButton(
                onClick = { onValueChange((value - 1).coerceAtLeast(min)) },
                modifier = Modifier.size(32.dp),
            ) { Icon(Icons.Default.Remove, null, Modifier.size(14.dp)) }
            OutlinedTextField(
                value = value.toString(),
                onValueChange = { it.toIntOrNull()?.coerceIn(min, max)?.let(onValueChange) },
                modifier = Modifier.weight(1f).padding(horizontal = 4.dp),
                singleLine = true,
                textStyle = LocalTextStyle.current.copy(textAlign = TextAlign.Center),
                keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(
                    keyboardType = KeyboardType.Number,
                ),
            )
            FilledTonalIconButton(
                onClick = { onValueChange((value + 1).coerceAtMost(max)) },
                modifier = Modifier.size(32.dp),
            ) { Icon(Icons.Default.Add, null, Modifier.size(14.dp)) }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ModifierField(
    modifier: WidgetModifier,
    currentValue: String,
    currentToggle: Boolean,
    onValueChange: (String) -> Unit,
) {
    when (modifier.type) {
        ModifierFieldType.Toggle -> {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(Modifier.weight(1f)) {
                    Text(modifier.label, fontSize = 14.sp, fontWeight = FontWeight.Medium)
                    if (!modifier.description.isNullOrBlank()) {
                        Text(modifier.description, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
                Switch(
                    checked = currentToggle,
                    onCheckedChange = { onValueChange(if (it) "true" else "false") },
                )
            }
        }

        ModifierFieldType.Select -> {
            var expanded by remember { mutableStateOf(false) }
            val selectedOption = modifier.options.firstOrNull { it.value == currentValue }
            Column {
                Text(modifier.label, fontSize = 14.sp, fontWeight = FontWeight.Medium)
                if (!modifier.description.isNullOrBlank()) {
                    Text(modifier.description, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Spacer(Modifier.height(4.dp))
                }
                ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
                    OutlinedTextField(
                        value = selectedOption?.label ?: currentValue,
                        onValueChange = {},
                        readOnly = true,
                        trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
                        modifier = Modifier
                            .fillMaxWidth()
                            .menuAnchor(MenuAnchorType.PrimaryNotEditable),
                        singleLine = true,
                    )
                    ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
                        modifier.options.forEach { opt ->
                            DropdownMenuItem(
                                text = { Text(opt.label) },
                                onClick = {
                                    onValueChange(opt.value)
                                    expanded = false
                                },
                            )
                        }
                    }
                }
            }
        }

        ModifierFieldType.Number -> {
            Column {
                Text(modifier.label, fontSize = 14.sp, fontWeight = FontWeight.Medium)
                if (!modifier.description.isNullOrBlank()) {
                    Text(modifier.description, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Spacer(Modifier.height(4.dp))
                }
                OutlinedTextField(
                    value = currentValue,
                    onValueChange = onValueChange,
                    placeholder = modifier.placeholder?.let { { Text(it) } },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    keyboardOptions = androidx.compose.foundation.text.KeyboardOptions(
                        keyboardType = KeyboardType.Number,
                    ),
                )
            }
        }

        ModifierFieldType.Text, ModifierFieldType.CityAutocomplete -> {
            Column {
                Text(modifier.label, fontSize = 14.sp, fontWeight = FontWeight.Medium)
                if (!modifier.description.isNullOrBlank()) {
                    Text(modifier.description, fontSize = 12.sp, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Spacer(Modifier.height(4.dp))
                }
                OutlinedTextField(
                    value = currentValue,
                    onValueChange = onValueChange,
                    placeholder = modifier.placeholder?.let { { Text(it) } },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                )
            }
        }
    }
}

@Composable
private fun MediaChip(label: String, selected: Boolean, onClick: () -> Unit) {
    val containerColor = if (selected) MicaPrimary else MaterialTheme.colorScheme.surfaceContainerHigh
    val contentColor = if (selected) MaterialTheme.colorScheme.onPrimary else MaterialTheme.colorScheme.onSurface
    Surface(
        shape = MaterialTheme.shapes.small,
        color = containerColor,
        contentColor = contentColor,
        modifier = Modifier
            .clickable(onClick = onClick)
            .border(
                1.dp,
                if (selected) MicaPrimary else MaterialTheme.colorScheme.outline,
                MaterialTheme.shapes.small,
            ),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 10.dp, vertical = 6.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(4.dp),
        ) {
            if (selected) Icon(Icons.Default.Check, null, modifier = Modifier.size(14.dp))
            Text(label.take(20), fontSize = 12.sp)
        }
    }
}

@Composable
private fun ServerMediaItem(mediaId: String, isSelected: Boolean, onClick: () -> Unit) {
    val containerColor = if (isSelected)
        MaterialTheme.colorScheme.primaryContainer
    else
        MaterialTheme.colorScheme.surfaceContainerLow
    Surface(
        shape = MaterialTheme.shapes.small,
        color = containerColor,
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
    ) {
        Row(
            modifier = Modifier.padding(horizontal = 12.dp, vertical = 8.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Icon(
                Icons.Default.Image,
                null,
                tint = if (isSelected) MicaPrimary else MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.size(20.dp),
            )
            Text(mediaId, fontSize = 13.sp, modifier = Modifier.weight(1f))
            if (isSelected) {
                Icon(Icons.Default.Check, null, tint = MicaPrimary, modifier = Modifier.size(18.dp))
            }
        }
    }
}
