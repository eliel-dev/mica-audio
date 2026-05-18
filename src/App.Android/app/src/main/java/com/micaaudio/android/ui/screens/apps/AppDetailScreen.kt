package com.micaaudio.android.ui.screens.apps

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.aspectRatio
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.grid.GridCells
import androidx.compose.foundation.lazy.grid.LazyVerticalGrid
import androidx.compose.foundation.lazy.grid.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import coil.compose.AsyncImage
import com.micaaudio.android.data.api.GiphyItem
import com.micaaudio.android.data.api.ModifierFieldType
import com.micaaudio.android.data.api.WidgetDefinition
import com.micaaudio.android.data.api.WidgetModifier

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppDetailScreen(
    appId: String,
    onNavigateBack: () -> Unit,
    viewModel: AppDetailViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()

    LaunchedEffect(appId) {
        viewModel.loadApp(appId)
    }

    val gifLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent(),
    ) { uri ->
        uri?.let { viewModel.uploadGif(it) }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(state.app?.name ?: "Detalhes do App", fontWeight = FontWeight.Bold) },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Voltar")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                ),
            )
        },
    ) { innerPadding ->
        if (state.isLoading) {
            Box(
                modifier = Modifier.fillMaxSize().padding(innerPadding),
                contentAlignment = Alignment.Center,
            ) { CircularProgressIndicator() }
            return@Scaffold
        }

        val app = state.app
        if (app == null) {
            Box(
                modifier = Modifier.fillMaxSize().padding(innerPadding),
                contentAlignment = Alignment.Center,
            ) { Text(state.error ?: "App não encontrado.") }
            return@Scaffold
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .verticalScroll(rememberScrollState())
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            // ── App header ───────────────────────────────────────────────
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(app.name, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                    Text(app.category, style = MaterialTheme.typography.labelMedium, color = MaterialTheme.colorScheme.primary)
                    if (app.description.isNotBlank()) {
                        Spacer(Modifier.height(4.dp))
                        Text(app.description, style = MaterialTheme.typography.bodyMedium)
                    }
                }
            }

            // ── Device selector ─────────────────────────────────────────
            if (state.devices.isEmpty()) {
                Text(
                    "Nenhum dispositivo conectado. Conecte um dispositivo HUB75 primeiro.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                )
            } else {
                DeviceSelector(
                    devices = state.devices,
                    selectedDeviceId = state.selectedDeviceId,
                    onDeviceSelected = viewModel::selectDevice,
                )
            }

            // ── Modifiers ────────────────────────────────────────────────
            if (app.modifiers.isNotEmpty()) {
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                        Text("Configurações", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                        app.modifiers.forEach { modifier ->
                            ModifierField(
                                modifier = modifier,
                                value = state.modifierValues[modifier.key] ?: "",
                                onValueChange = { viewModel.updateModifier(modifier.key, it) },
                            )
                        }
                    }
                }
            }

            // ── GIF upload (gifhub75 only) ────────────────────────────
            if (app.runtime?.kind == "gifhub75") {
                GifSection(
                    uploadState = state.gifUploadState,
                    onPickFile = { gifLauncher.launch("image/*") },
                    onSearchGiphy = viewModel::openGiphySheet,
                )
            }

            // ── Action buttons ───────────────────────────────────────────
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Button(
                    onClick = viewModel::deployApp,
                    enabled = !state.isDeploying && state.selectedDeviceId != null,
                    modifier = Modifier.weight(1f),
                ) { Text("Instalar") }

                OutlinedButton(
                    onClick = viewModel::activateApp,
                    enabled = !state.isDeploying && state.selectedDeviceId != null,
                    modifier = Modifier.weight(1f),
                ) { Text("Ativar") }
            }

            // ── Deploy status ─────────────────────────────────────────────
            if (state.isDeploying || state.deployResult != null) {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant),
                ) {
                    Column(modifier = Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        if (state.isDeploying) {
                            LinearProgressIndicator(
                                progress = { state.commandPercent / 100f },
                                modifier = Modifier.fillMaxWidth(),
                            )
                        }
                        state.deployResult?.let { result ->
                            Text(result, style = MaterialTheme.typography.bodySmall)
                        }
                    }
                }
            }
        }
    }

    // ── GIPHY bottom sheet ────────────────────────────────────────────────────
    if (state.showGiphySheet) {
        GiphySearchSheet(
            query = state.giphyQuery,
            results = state.giphyResults,
            isSearching = state.isGiphySearching,
            onQueryChange = viewModel::setGiphyQuery,
            onSearch = viewModel::searchGiphy,
            onItemSelected = viewModel::selectGiphyItem,
            onDismiss = viewModel::closeGiphySheet,
        )
    }
}

// ── Device selector ───────────────────────────────────────────────────────────

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DeviceSelector(
    devices: List<com.micaaudio.android.data.api.DeviceSnapshot>,
    selectedDeviceId: String?,
    onDeviceSelected: (String) -> Unit,
) {
    var expanded by remember { mutableStateOf(false) }
    val selected = devices.find { it.deviceId == selectedDeviceId }

    ExposedDropdownMenuBox(
        expanded = expanded,
        onExpandedChange = { expanded = it },
    ) {
        OutlinedTextField(
            value = selected?.name?.ifBlank { selected.deviceId } ?: "Selecionar dispositivo",
            onValueChange = {},
            readOnly = true,
            label = { Text("Dispositivo alvo") },
            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
            modifier = Modifier.menuAnchor().fillMaxWidth(),
        )
        ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
            devices.forEach { device ->
                DropdownMenuItem(
                    text = { Text(device.name.ifBlank { device.deviceId }) },
                    onClick = {
                        onDeviceSelected(device.deviceId)
                        expanded = false
                    },
                )
            }
        }
    }
}

// ── Modifier field ────────────────────────────────────────────────────────────

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ModifierField(
    modifier: WidgetModifier,
    value: String,
    onValueChange: (String) -> Unit,
) {
    when (modifier.type) {
        ModifierFieldType.Toggle -> {
            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(modifier.label, style = MaterialTheme.typography.bodyMedium)
                    modifier.description?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                }
                Switch(
                    checked = value.equals("true", ignoreCase = true),
                    onCheckedChange = { onValueChange(if (it) "true" else "false") },
                )
            }
        }

        ModifierFieldType.Select -> {
            var expanded by remember { mutableStateOf(false) }
            val selectedOption = modifier.options.find { it.value == value }?.label ?: value

            Column {
                Text(modifier.label, style = MaterialTheme.typography.labelLarge)
                modifier.description?.let { Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant) }
                ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = it }) {
                    OutlinedTextField(
                        value = selectedOption,
                        onValueChange = {},
                        readOnly = true,
                        trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
                        placeholder = modifier.placeholder?.let { { Text(it) } },
                        modifier = Modifier.menuAnchor().fillMaxWidth(),
                    )
                    ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
                        modifier.options.forEach { option ->
                            DropdownMenuItem(
                                text = { Text(option.label) },
                                onClick = { onValueChange(option.value); expanded = false },
                            )
                        }
                    }
                }
            }
        }

        ModifierFieldType.Number -> {
            OutlinedTextField(
                value = value,
                onValueChange = onValueChange,
                label = { Text(modifier.label) },
                placeholder = modifier.placeholder?.let { { Text(it) } },
                supportingText = modifier.description?.let { { Text(it) } },
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
        }

        else -> {
            OutlinedTextField(
                value = value,
                onValueChange = onValueChange,
                label = { Text(modifier.label) },
                placeholder = modifier.placeholder?.let { { Text(it) } },
                supportingText = modifier.description?.let { { Text(it) } },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
        }
    }
}

// ── GIF upload section ────────────────────────────────────────────────────────

@Composable
private fun GifSection(
    uploadState: GifUploadState,
    onPickFile: () -> Unit,
    onSearchGiphy: () -> Unit,
) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(modifier = Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("Arquivo GIF / Imagem", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
            when (uploadState) {
                is GifUploadState.Idle -> {
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        OutlinedButton(onClick = onPickFile, modifier = Modifier.weight(1f)) {
                            Text("Selecionar arquivo")
                        }
                        OutlinedButton(onClick = onSearchGiphy, modifier = Modifier.weight(1f)) {
                            Text("Buscar no GIPHY")
                        }
                    }
                }
                is GifUploadState.Uploading -> {
                    Text("Enviando…", style = MaterialTheme.typography.bodySmall)
                    LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                }
                is GifUploadState.Done -> {
                    Text("✓ Enviado: ${uploadState.mediaId}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.primary)
                    OutlinedButton(onClick = onPickFile, modifier = Modifier.fillMaxWidth()) {
                        Text("Substituir arquivo")
                    }
                    OutlinedButton(onClick = onSearchGiphy, modifier = Modifier.fillMaxWidth()) {
                        Text("Buscar no GIPHY")
                    }
                }
                is GifUploadState.Error -> {
                    Text("Erro: ${uploadState.message}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.error)
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        OutlinedButton(onClick = onPickFile, modifier = Modifier.weight(1f)) {
                            Text("Tentar novamente")
                        }
                        OutlinedButton(onClick = onSearchGiphy, modifier = Modifier.weight(1f)) {
                            Text("Buscar no GIPHY")
                        }
                    }
                }
            }
        }
    }
}

// ── GIPHY search sheet ────────────────────────────────────────────────────────

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun GiphySearchSheet(
    query: String,
    results: List<GiphyItem>,
    isSearching: Boolean,
    onQueryChange: (String) -> Unit,
    onSearch: () -> Unit,
    onItemSelected: (GiphyItem) -> Unit,
    onDismiss: () -> Unit,
) {
    val keyboardController = LocalSoftwareKeyboardController.current

    ModalBottomSheet(onDismissRequest = onDismiss) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp)
                .padding(bottom = 24.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Text("Buscar no GIPHY", style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)

            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = query,
                    onValueChange = onQueryChange,
                    placeholder = { Text("Ex: fire, rain, space…") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                    keyboardActions = KeyboardActions(onSearch = {
                        keyboardController?.hide()
                        onSearch()
                    }),
                    modifier = Modifier.weight(1f),
                )
                IconButton(onClick = {
                    keyboardController?.hide()
                    onSearch()
                }) {
                    Icon(Icons.Default.Search, contentDescription = "Buscar")
                }
            }

            when {
                isSearching -> {
                    Box(
                        modifier = Modifier.fillMaxWidth().height(200.dp),
                        contentAlignment = Alignment.Center,
                    ) { CircularProgressIndicator() }
                }

                results.isEmpty() && query.isNotBlank() -> {
                    Text(
                        "Nenhum resultado para \"$query\". Tente outro termo.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        textAlign = TextAlign.Center,
                        modifier = Modifier.fillMaxWidth().padding(vertical = 24.dp),
                    )
                }

                results.isNotEmpty() -> {
                    LazyVerticalGrid(
                        columns = GridCells.Fixed(3),
                        horizontalArrangement = Arrangement.spacedBy(6.dp),
                        verticalArrangement = Arrangement.spacedBy(6.dp),
                        modifier = Modifier.height(320.dp),
                    ) {
                        items(results) { item ->
                            GiphyThumbnail(item = item, onClick = { onItemSelected(item) })
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun GiphyThumbnail(item: GiphyItem, onClick: () -> Unit) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .aspectRatio(1f)
            .clickable(onClick = onClick),
    ) {
        if (item.previewUrl.isNotBlank()) {
            AsyncImage(
                model = item.previewUrl,
                contentDescription = item.title.ifBlank { "GIF" },
                contentScale = ContentScale.Crop,
                modifier = Modifier.fillMaxSize(),
            )
        } else {
            Box(
                modifier = Modifier.fillMaxSize(),
                contentAlignment = Alignment.Center,
            ) {
                Text("GIF", style = MaterialTheme.typography.labelSmall, color = Color.Gray)
            }
        }
    }
}
