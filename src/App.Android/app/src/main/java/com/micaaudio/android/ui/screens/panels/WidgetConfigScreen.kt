package com.micaaudio.android.ui.screens.panels

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
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
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Check
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Image
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.vector.rememberVectorPainter
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil.compose.AsyncImage
import coil.request.ImageRequest
import com.micaaudio.android.data.api.ModifierFieldType
import com.micaaudio.android.data.api.PanelWidgetDefinition
import com.micaaudio.android.data.api.WidgetModifier
import com.micaaudio.android.ui.theme.MicaPrimary

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
    val context = LocalContext.current

    LaunchedEffect(mediaDeviceId) {
        viewModel.loadMediaForDevice(mediaDeviceId)
    }

    if (widget == null) {
        onNavigateBack()
        return
    }

    val hasMostradoresTab = widget.appId == "analogclock"
    val hasMediaTab = widget.appId == "gifhub75"
    var selectedTab by remember { mutableIntStateOf(if (hasMediaTab) 1 else 0) }

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
            selectedIds = selectedIds,
            isLoading = state.isMediaLoading,
            deviceId = mediaDeviceId,
            serverUrl = state.serverUrl,
            authToken = state.authToken,
            totalBytes = state.mediaTotalBytes,
            maxBytes = 8 * 1024 * 1024L,
            viewModel = viewModel,
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
            onDeleteMedia = { mediaId -> viewModel.deleteMedia(mediaDeviceId, mediaId) },
        )
        return
    }

    Scaffold(
        topBar = {
            Column {
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
                    },
                )
                if (hasMediaTab) {
                    TabRow(selectedTabIndex = selectedTab) {
                        Tab(
                            selected = selectedTab == 0,
                            onClick = { selectedTab = 0 },
                            text = { Text("Configurações") }
                        )
                        Tab(
                            selected = selectedTab == 1,
                            onClick = { selectedTab = 1 },
                            text = { Text("Mídias") }
                        )
                    }
                } else if (hasMostradoresTab) {
                    TabRow(selectedTabIndex = selectedTab) {
                        Tab(
                            selected = selectedTab == 0,
                            onClick = { selectedTab = 0 },
                            text = { Text("Configurações") }
                        )
                        Tab(
                            selected = selectedTab == 1,
                            onClick = { selectedTab = 1 },
                            text = { Text("Mostradores") }
                        )
                    }
                }
            }
        },
    ) { innerPadding ->
        // ── Aba Mostradores (relógio): full-screen picker com previews ─────
        if (hasMostradoresTab && selectedTab == 1) {
            MostradoresTab(
                selectedValue = widget.configValues["mostrador"] ?: "cyberterminal",
                onSelect = { value ->
                    viewModel.updateWidget(
                        widget.copy(configValues = widget.configValues + ("mostrador" to value))
                    )
                },
                modifier = Modifier.padding(innerPadding),
            )
            return@Scaffold
        }

        LazyColumn(
            modifier = Modifier
                .padding(innerPadding)
                .fillMaxSize(),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            // ── Configurações (modifiers dinâmicos) ──────────────────────────
            if (selectedTab == 0 && !catalogItem?.modifiers.isNullOrEmpty()) {
                item {
                    val mediaCount = selectedMediaIds(widget).size
                    SectionCard(title = "Configurações") {
                        catalogItem!!.modifiers
                            .filter { modifier ->
                                when (modifier.key) {
                                    // Ocultar opções de slideshow se houver apenas 1 mídia ou menos
                                    "slideshowInterval", "slideshowShuffle" -> mediaCount > 1
                                    // O mostrador tem aba dedicada para o relógio — não duplicar na lista.
                                    "mostrador" -> !hasMostradoresTab
                                    else -> true
                                }
                            }
                            .forEachIndexed { index, modifier ->
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
            if (selectedTab == 1 && widget.appId == "gifhub75") {
                item {
                    val selectedIds = selectedMediaIds(widget)

                    SectionCard(title = "Mídia") {
                        if (selectedIds.isNotEmpty()) {
                            Text(
                                "Mídias no Widget:",
                                fontSize = 12.sp,
                                fontWeight = FontWeight.Medium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            Spacer(Modifier.height(8.dp))

                            FlowRow(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.spacedBy(8.dp),
                                verticalArrangement = Arrangement.spacedBy(8.dp),
                                maxItemsInEachRow = 4
                            ) {
                                selectedIds.forEach { id ->
                                    MediaChip(
                                        label = id,
                                        selected = true,
                                        deviceId = mediaDeviceId,
                                        serverUrl = state.serverUrl,
                                        authToken = state.authToken,
                                        onClick = {
                                            val newIds = (selectedIds - id).joinToString(",")
                                            viewModel.updateWidget(
                                                widget.copy(runtimeState = widget.runtimeState + ("mediaIds" to newIds))
                                            )
                                        },
                                    )
                                }
                            }
                            Spacer(Modifier.height(16.dp))
                        } else {
                            Text(
                                "Nenhuma mídia selecionada.",
                                fontSize = 13.sp,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.padding(vertical = 8.dp)
                            )
                        }

                        OutlinedButton(
                            onClick = { showMediaGrid = true },
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Icon(Icons.Default.Image, null, modifier = Modifier.size(18.dp))
                            Spacer(Modifier.width(8.dp))
                            Text("Abrir galeria de imagens")
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
    selectedIds: List<String>,
    isLoading: Boolean,
    deviceId: String,
    serverUrl: String,
    authToken: String,
    totalBytes: Long = 0L,
    maxBytes: Long = 8 * 1024 * 1024L,
    viewModel: PanelsViewModel,
    onNavigateBack: () -> Unit,
    onAddMedia: () -> Unit,
    onMediaClick: (String) -> Unit,
    onDeleteMedia: (String) -> Unit = {},
) {
    var selectedTab by remember { mutableIntStateOf(0) }
    val state by viewModel.uiState.collectAsStateWithLifecycle()

    Scaffold(
        topBar = {
            Column {
                TopAppBar(
                    title = {
                        Column {
                            Text("Galeria de Imagens", fontWeight = FontWeight.Bold)
                            if (selectedTab == 0 && !isLoading && mediaIds.isNotEmpty()) {
                                Text(
                                    "${formatBytes(totalBytes)} / ${formatBytes(maxBytes)}",
                                    fontSize = 11.sp,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                            }
                        }
                    },
                    navigationIcon = {
                        IconButton(onClick = onNavigateBack) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, null)
                        }
                    },
                )
                TabRow(selectedTabIndex = selectedTab) {
                    Tab(selected = selectedTab == 0, onClick = { selectedTab = 0 }, text = { Text("Servidor") })
                    Tab(selected = selectedTab == 1, onClick = { selectedTab = 1 }, text = { Text("GIPHY") })
                    Tab(selected = selectedTab == 2, onClick = { selectedTab = 2 }, text = { Text("Local") })
                }
            }
        },
    ) { innerPadding ->
        when (selectedTab) {
            0 -> { // Servidor
                if (isLoading) {
                    Box(Modifier.fillMaxSize().padding(innerPadding), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
                } else if (mediaIds.isEmpty()) {
                    Box(Modifier.fillMaxSize().padding(innerPadding), contentAlignment = Alignment.Center) {
                        Text("Nenhuma mídia no servidor.", color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                } else {
                    LazyVerticalGrid(
                        columns = GridCells.Adaptive(112.dp),
                        modifier = Modifier.fillMaxSize().padding(innerPadding),
                        contentPadding = PaddingValues(16.dp),
                        horizontalArrangement = Arrangement.spacedBy(12.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        gridItems(mediaIds, key = { it }) { mediaId ->
                            MediaGridItem(
                                mediaId = mediaId,
                                deviceId = deviceId,
                                serverUrl = serverUrl,
                                authToken = authToken,
                                isSelected = mediaId in selectedIds,
                                onClick = { onMediaClick(mediaId) },
                                onDelete = { onDeleteMedia(mediaId) },
                            )
                        }
                    }
                }
            }
            1 -> { // GIPHY
                GiphyTab(
                    results = state.giphyResults,
                    isLoading = state.isGiphyLoading,
                    onSearch = { viewModel.searchGiphy(it) },
                    onImport = { viewModel.importGiphyToDevice(deviceId, it) },
                    modifier = Modifier.padding(innerPadding)
                )
            }
            2 -> { // Local
                Box(Modifier.fillMaxSize().padding(innerPadding), contentAlignment = Alignment.Center) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Icon(Icons.Default.Image, null, modifier = Modifier.size(64.dp), tint = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.4f))
                        Spacer(Modifier.height(16.dp))
                        Text("Selecione imagens do seu dispositivo", color = MaterialTheme.colorScheme.onSurfaceVariant)
                        Spacer(Modifier.height(24.dp))
                        Button(onClick = onAddMedia) {
                            Icon(Icons.Default.Add, null)
                            Spacer(Modifier.width(8.dp))
                            Text("Selecionar e Enviar")
                        }
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun GiphyTab(
    results: List<com.micaaudio.android.data.api.GiphyItem>,
    isLoading: Boolean,
    onSearch: (String) -> Unit,
    onImport: (com.micaaudio.android.data.api.GiphyItem) -> Unit,
    modifier: Modifier = Modifier
) {
    var query by remember { mutableStateOf("") }

    Column(modifier = modifier.fillMaxSize()) {
        OutlinedTextField(
            value = query,
            onValueChange = { query = it; onSearch(it) },
            placeholder = { Text("Pesquisar no GIPHY...") },
            modifier = Modifier.fillMaxWidth().padding(16.dp),
            leadingIcon = { Icon(Icons.Default.Image, null) },
            singleLine = true
        )

        if (isLoading) {
            Box(Modifier.weight(1f).fillMaxWidth(), contentAlignment = Alignment.Center) { CircularProgressIndicator() }
        } else if (results.isEmpty()) {
            Box(Modifier.weight(1f).fillMaxWidth(), contentAlignment = Alignment.Center) {
                Text("Nenhum resultado.", color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        } else {
            LazyVerticalGrid(
                columns = GridCells.Adaptive(112.dp),
                modifier = Modifier.weight(1f).fillMaxWidth(),
                contentPadding = PaddingValues(16.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                gridItems(results, key = { it.id }) { item ->
                    Card(
                        modifier = Modifier.aspectRatio(1f).clickable { onImport(item) },
                        colors = CardDefaults.cardColors(containerColor = Color.Black)
                    ) {
                        AsyncImage(
                            model = item.previewUrl,
                            contentDescription = item.title,
                            modifier = Modifier.fillMaxSize(),
                            contentScale = ContentScale.Crop
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun MediaGridItem(
    mediaId: String,
    deviceId: String,
    serverUrl: String,
    authToken: String,
    isSelected: Boolean,
    onClick: () -> Unit,
    onDelete: () -> Unit = {},
) {
    var showConfirm by remember { mutableStateOf(false) }
    val context = LocalContext.current
    val imageUrl = "$serverUrl/api/v1/admin/devices/$deviceId/media/$mediaId"

    if (showConfirm) {
        AlertDialog(
            onDismissRequest = { showConfirm = false },
            title = { Text("Excluir mídia?") },
            text = { Text("\"$mediaId\" será removido do servidor permanentemente.") },
            confirmButton = {
                TextButton(onClick = { showConfirm = false; onDelete() }) { Text("Excluir", color = MaterialTheme.colorScheme.error) }
            },
            dismissButton = {
                TextButton(onClick = { showConfirm = false }) { Text("Cancelar") }
            },
        )
    }

    Card(
        modifier = Modifier
            .fillMaxWidth()
            .aspectRatio(1f)
            .clickable(onClick = onClick),
        colors = CardDefaults.cardColors(
            containerColor = if (isSelected)
                MaterialTheme.colorScheme.primaryContainer
            else
                MaterialTheme.colorScheme.surfaceContainerHigh,
        ),
        border = if (isSelected) CardDefaults.outlinedCardBorder() else null,
    ) {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center,
        ) {
            AsyncImage(
                model = ImageRequest.Builder(context)
                    .data(imageUrl)
                    .setHeader("Authorization", "Bearer $authToken")
                    .crossfade(enable = true)
                    .build(),
                contentDescription = mediaId,
                contentScale = ContentScale.Crop,
                modifier = Modifier.fillMaxSize(),
                placeholder = rememberVectorPainter(Icons.Default.Image),
                error = rememberVectorPainter(Icons.Default.Image),
            )

            if (isSelected) {
                Surface(
                    color = MicaPrimary,
                    shape = CircleShape,
                    modifier = Modifier.align(Alignment.TopEnd).padding(6.dp),
                ) {
                    Icon(
                        Icons.Default.Check,
                        null,
                        tint = Color.White,
                        modifier = Modifier.size(16.dp).padding(2.dp),
                    )
                }
            }

            // Delete button (top-left)
            IconButton(
                onClick = { showConfirm = true },
                modifier = Modifier
                    .align(Alignment.TopStart)
                    .size(28.dp)
                    .background(Color.Black.copy(alpha = 0.3f), CircleShape),
            ) {
                Icon(
                    Icons.Default.Delete,
                    "Excluir",
                    tint = Color.White,
                    modifier = Modifier.size(16.dp),
                )
            }
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

/** Formats a byte count as a human-readable string (e.g. "1.2 MB", "450 KB"). */
private fun formatBytes(bytes: Long): String = when {
    bytes >= 1_048_576L -> "%.1f MB".format(bytes / 1_048_576.0)
    bytes >= 1_024L     -> "%.0f KB".format(bytes / 1_024.0)
    else                -> "$bytes B"
}

/**
 * Horizontal bar showing used / max storage with a filled progress indicator.
 * e.g. "1.2 MB / 8.0 MB"
 */
@Composable
private fun StorageCounterBar(usedBytes: Long, maxBytes: Long) {
    val fraction = if (maxBytes > 0L) (usedBytes.toFloat() / maxBytes.toFloat()).coerceIn(0f, 1f) else 0f
    val colorScheme = MaterialTheme.colorScheme
    val barColor = when {
        fraction >= 0.9f -> colorScheme.error
        fraction >= 0.7f -> MaterialTheme.colorScheme.tertiary
        else             -> MicaPrimary
    }
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.SpaceBetween,
        ) {
            Text(
                "Armazenamento no servidor",
                fontSize = 12.sp,
                fontWeight = FontWeight.Medium,
                color = colorScheme.onSurfaceVariant,
            )
            Text(
                "${formatBytes(usedBytes)} / ${formatBytes(maxBytes)}",
                fontSize = 12.sp,
                color = colorScheme.onSurfaceVariant,
            )
        }
        LinearProgressIndicator(
            progress = { fraction },
            modifier = Modifier.fillMaxWidth(),
            color = barColor,
            trackColor = colorScheme.surfaceContainerHighest,
        )
    }
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
private fun MediaChip(
    label: String,
    selected: Boolean,
    deviceId: String,
    serverUrl: String,
    authToken: String,
    onClick: () -> Unit
) {
    val context = LocalContext.current
    val imageUrl = "$serverUrl/api/v1/admin/devices/$deviceId/media/$label"
    val containerColor = if (selected) MicaPrimary else MaterialTheme.colorScheme.surfaceContainerHigh

    Surface(
        shape = RoundedCornerShape(8.dp),
        color = containerColor,
        modifier = Modifier
            .size(56.dp)
            .clickable(onClick = onClick)
            .border(
                1.dp,
                if (selected) MicaPrimary else MaterialTheme.colorScheme.outline,
                RoundedCornerShape(8.dp),
            ),
    ) {
        Box(contentAlignment = Alignment.Center) {
            AsyncImage(
                model = ImageRequest.Builder(context)
                    .data(imageUrl)
                    .setHeader("Authorization", "Bearer $authToken")
                    .crossfade(enable = true)
                    .build(),
                contentDescription = null,
                modifier = Modifier.fillMaxSize(),
                contentScale = ContentScale.Crop,
                placeholder = rememberVectorPainter(Icons.Default.Image),
                error = rememberVectorPainter(Icons.Default.Image),
            )

            if (selected) {
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .background(MicaPrimary.copy(alpha = 0.3f)),
                    contentAlignment = Alignment.Center
                ) {
                    Icon(
                        Icons.Default.Check,
                        null,
                        tint = Color.White,
                        modifier = Modifier.size(24.dp)
                    )
                }
            }
        }
    }
}

@Composable
private fun ServerMediaItem(
    mediaId: String,
    isSelected: Boolean,
    deviceId: String,
    serverUrl: String,
    authToken: String,
    onClick: () -> Unit,
    onDelete: () -> Unit = {},
) {
    var showConfirm by remember { mutableStateOf(false) }
    val context = LocalContext.current
    val imageUrl = "$serverUrl/api/v1/admin/devices/$deviceId/media/$mediaId"

    if (showConfirm) {
        AlertDialog(
            onDismissRequest = { showConfirm = false },
            title = { Text("Excluir mídia?") },
            text = { Text("A mídia será removida do servidor permanentemente.") },
            confirmButton = {
                TextButton(onClick = { showConfirm = false; onDelete() }) { Text("Excluir", color = MaterialTheme.colorScheme.error) }
            },
            dismissButton = {
                TextButton(onClick = { showConfirm = false }) { Text("Cancelar") }
            },
        )
    }

    Card(
        modifier = Modifier
            .size(72.dp)
            .clickable(onClick = onClick),
        colors = CardDefaults.cardColors(
            containerColor = if (isSelected)
                MaterialTheme.colorScheme.primaryContainer
            else
                MaterialTheme.colorScheme.surfaceContainerLow,
        ),
        border = if (isSelected) CardDefaults.outlinedCardBorder() else null,
    ) {
        Box(
            modifier = Modifier.fillMaxSize(),
            contentAlignment = Alignment.Center
        ) {
            AsyncImage(
                model = ImageRequest.Builder(context)
                    .data(imageUrl)
                    .setHeader("Authorization", "Bearer $authToken")
                    .crossfade(enable = true)
                    .build(),
                contentDescription = null,
                modifier = Modifier.fillMaxSize(),
                contentScale = ContentScale.Crop,
                placeholder = rememberVectorPainter(Icons.Default.Image),
                error = rememberVectorPainter(Icons.Default.Image),
            )

            if (isSelected) {
                Surface(
                    color = MicaPrimary,
                    shape = CircleShape,
                    modifier = Modifier.align(Alignment.TopEnd).padding(4.dp),
                ) {
                    Icon(
                        Icons.Default.Check,
                        null,
                        tint = Color.White,
                        modifier = Modifier.size(14.dp).padding(2.dp),
                    )
                }
            }

            // Botão excluir discreto
            Box(
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .padding(2.dp)
                    .size(20.dp)
                    .background(Color.Black.copy(alpha = 0.5f), CircleShape)
                    .clickable { showConfirm = true },
                contentAlignment = Alignment.Center
            ) {
                Icon(Icons.Default.Delete, null, tint = Color.White, modifier = Modifier.size(12.dp))
            }
        }
    }
}
