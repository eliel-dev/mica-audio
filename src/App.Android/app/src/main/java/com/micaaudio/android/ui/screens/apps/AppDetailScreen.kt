package com.micaaudio.android.ui.screens.apps

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.FileOpen
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.micaaudio.android.data.api.AppModifierDefinition
import com.micaaudio.android.data.api.ModifierFieldType

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppDetailScreen(
    appId: String,
    onNavigateBack: () -> Unit,
    viewModel: AppDetailViewModel = hiltViewModel(),
) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()

    LaunchedEffect(appId) { viewModel.loadApp(appId) }

    val gifLauncher = rememberLauncherForActivityResult(ActivityResultContracts.GetContent()) { uri: Uri? ->
        uri?.let { viewModel.uploadGif(it) }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(state.app?.name ?: "Widget") },
                navigationIcon = {
                    IconButton(onClick = onNavigateBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, "Voltar")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(containerColor = MaterialTheme.colorScheme.background),
            )
        },
    ) { innerPadding ->
        if (state.isLoading) {
            Box(Modifier.fillMaxSize().padding(innerPadding), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
            return@Scaffold
        }

        val app = state.app ?: return@Scaffold

        Column(
            modifier = Modifier.fillMaxSize().padding(innerPadding).verticalScroll(rememberScrollState()).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            // Header card
            Card(
                modifier = Modifier.fillMaxWidth(),
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
            ) {
                Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                    Text(app.name, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                    SuggestionChip(
                        onClick = {},
                        label = { Text(app.category.replaceFirstChar { it.uppercase() }, style = MaterialTheme.typography.labelSmall) },
                        modifier = Modifier.height(24.dp),
                    )
                    if (app.description.isNotBlank()) {
                        Text(app.description, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    }
                }
            }

            // Device selector
            if (state.devices.isNotEmpty()) {
                Text("Dispositivo alvo", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                var expanded by remember { mutableStateOf(false) }
                ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = !expanded }) {
                    OutlinedTextField(
                        value = state.devices.find { it.deviceId == state.selectedDeviceId }?.name ?: "Selecionar...",
                        onValueChange = {},
                        readOnly = true,
                        trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
                        modifier = Modifier.menuAnchor(MenuAnchorType.PrimaryNotEditable).fillMaxWidth(),
                        label = { Text("Dispositivo") },
                    )
                    ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
                        state.devices.forEach { device ->
                            DropdownMenuItem(
                                text = { Text(device.name.ifBlank { device.deviceId.take(8) }) },
                                onClick = { viewModel.selectDevice(device.deviceId); expanded = false },
                            )
                        }
                    }
                }
            } else {
                Text("Nenhum dispositivo conectado", style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.error)
            }

            // Modifiers
            if (app.modifiers.isNotEmpty()) {
                Text("Configuração", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
                ) {
                    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                        app.modifiers.forEach { mod ->
                            ModifierField(
                                modifier = mod,
                                value = state.modifierValues[mod.key] ?: "",
                                onValueChange = { viewModel.updateModifier(mod.key, it) },
                            )
                        }
                    }
                }
            }

            // GIF upload (only for gifhub75 runtime)
            if (app.runtime?.kind == "gifhub75") {
                Text("Arquivo GIF", style = MaterialTheme.typography.titleSmall, fontWeight = FontWeight.SemiBold)
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
                ) {
                    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        when (val gifState = state.gifUploadState) {
                            is GifUploadState.Idle -> {
                                OutlinedButton(
                                    onClick = { gifLauncher.launch("image/gif") },
                                    modifier = Modifier.fillMaxWidth(),
                                    enabled = state.selectedDeviceId != null,
                                ) {
                                    Icon(Icons.Default.FileOpen, null, Modifier.size(18.dp))
                                    Spacer(Modifier.width(8.dp))
                                    Text("Selecionar GIF")
                                }
                            }
                            is GifUploadState.Uploading -> {
                                LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                                Text("Enviando...", style = MaterialTheme.typography.bodySmall)
                            }
                            is GifUploadState.Done -> {
                                Text("GIF enviado com sucesso", color = MaterialTheme.colorScheme.primary, style = MaterialTheme.typography.bodySmall)
                                TextButton(onClick = { gifLauncher.launch("image/gif") }) { Text("Substituir") }
                            }
                            is GifUploadState.Error -> {
                                Text("Erro: ${gifState.message}", color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
                                TextButton(onClick = { gifLauncher.launch("image/gif") }) { Text("Tentar novamente") }
                            }
                        }
                    }
                }
            }

            // Action buttons
            Row(horizontalArrangement = Arrangement.spacedBy(12.dp)) {
                Button(
                    onClick = { viewModel.deployApp() },
                    enabled = state.selectedDeviceId != null && !state.isDeploying,
                    modifier = Modifier.weight(1f),
                ) {
                    Text("Instalar")
                }
                OutlinedButton(
                    onClick = { viewModel.activateApp() },
                    enabled = state.selectedDeviceId != null && !state.isDeploying,
                    modifier = Modifier.weight(1f),
                ) {
                    Text("Ativar")
                }
            }

            // Deploy status card
            if (state.isDeploying || state.deployResult != null) {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceContainer),
                ) {
                    Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                        if (state.isDeploying) {
                            LinearProgressIndicator(
                                progress = { state.commandPercent / 100f },
                                modifier = Modifier.fillMaxWidth(),
                            )
                        }
                        state.deployResult?.let {
                            Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                        if (!state.isDeploying) {
                            TextButton(onClick = { viewModel.clearDeployResult() }) { Text("Limpar") }
                        }
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun ModifierField(
    modifier: AppModifierDefinition,
    value: String,
    onValueChange: (String) -> Unit,
) {
    Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
        when (modifier.type) {
            ModifierFieldType.Toggle -> {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Column(Modifier.weight(1f)) {
                        Text(modifier.label, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Medium)
                        modifier.description?.let {
                            Text(it, style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                        }
                    }
                    Switch(
                        checked = value == "true",
                        onCheckedChange = { onValueChange(it.toString()) },
                    )
                }
            }
            ModifierFieldType.Select -> {
                var expanded by remember { mutableStateOf(false) }
                val selectedLabel = modifier.options.find { it.value == value }?.label ?: value
                Text(modifier.label, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Medium)
                ExposedDropdownMenuBox(expanded = expanded, onExpandedChange = { expanded = !expanded }) {
                    OutlinedTextField(
                        value = selectedLabel,
                        onValueChange = {},
                        readOnly = true,
                        trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded) },
                        modifier = Modifier.menuAnchor(MenuAnchorType.PrimaryNotEditable).fillMaxWidth(),
                    )
                    ExposedDropdownMenu(expanded = expanded, onDismissRequest = { expanded = false }) {
                        modifier.options.forEach { opt ->
                            DropdownMenuItem(
                                text = { Text(opt.label) },
                                onClick = { onValueChange(opt.value); expanded = false },
                            )
                        }
                    }
                }
            }
            ModifierFieldType.Number -> {
                Text(modifier.label, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Medium)
                OutlinedTextField(
                    value = value,
                    onValueChange = onValueChange,
                    modifier = Modifier.fillMaxWidth(),
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    placeholder = modifier.placeholder?.let { { Text(it) } },
                    supportingText = modifier.description?.let { { Text(it) } },
                )
            }
            ModifierFieldType.Text, ModifierFieldType.CityAutocomplete -> {
                Text(modifier.label, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Medium)
                OutlinedTextField(
                    value = value,
                    onValueChange = onValueChange,
                    modifier = Modifier.fillMaxWidth(),
                    placeholder = modifier.placeholder?.let { { Text(it) } },
                    supportingText = modifier.description?.let { { Text(it) } },
                )
            }
        }
    }
}
