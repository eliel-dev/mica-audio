using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using App.WinUI.Models.Apps;
using App.WinUI.Models.Panels;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Panels;
using App.WinUI.ViewModels;
using App.WinUI.Views.Controls;
using App.WinUI.Views.Dialogs;
using Device.Protocol.Models;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/paineis.md#editor-hub75
// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-regra-global-de-scroll-canvas-first
public sealed partial class PanelsPage : Page, IDisposable
{
    private static readonly RgbaColor[] EmptyFrame = Enumerable.Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray();
    private const string LocalDraftScope = "__local__";
    private const string DraggedWidgetAppIdKey = "panelWidgetAppId";
    private const int DefaultNewWidgetWidth = 64;
    private const int DefaultNewWidgetHeight = 32;
    private const double DesktopInspectorSidebarWidth = 340d;

    private readonly PanelsPageViewModel viewModel;
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly IAppCatalogService catalogService;
    private readonly IAppModifierStateStore modifierStore;
    private readonly PanelsStore panelsStore;
    private readonly PanelsFrameComposer frameComposer;
    private readonly PanelsPlaybackService playbackService;
    private readonly Hub75VisualizerSessionService hub75VisualizerSessionService;
    private readonly RemoteDeviceServerSecretStore secretStore;
    private readonly AppModifierEditorHost modifierEditor;

    private readonly List<AppCatalogItem> catalogItems = [];
    private readonly List<WidgetLibraryEntry> widgetLibraryEntries = [];
    private readonly List<WidgetLibraryEntry> filteredWidgetLibraryEntries = [];
    private readonly ObservableCollection<PanelGalleryItemState> panelGalleryItems = [];
    private readonly Dictionary<string, AppCatalogItem> catalogById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PanelGalleryItemState> panelGalleryItemsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RgbaColor[]> panelThumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Task> panelPosterTasks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> panelPosterGenerations = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherQueueTimer previewTimer;

    private DeviceOperationsState currentState = new();
    private PanelsStoreDocument storeDocument = new();
    private PanelsFrameComposer.PanelCompositionSession? previewSession;
    private PanelDefinition? currentPanel;
    private PanelWidgetDefinition? selectedWidget;
    private AppCatalogItem? selectedWidgetCatalogItem;
    private PanelsPageMode currentMode = PanelsPageMode.Gallery;
    private PanelsPreviewMode previewMode = PanelsPreviewMode.Off;
    private CancellationTokenSource? pageLifetimeCts;
    private CancellationTokenSource? previewRefreshCts;
    private bool handlersAttached;
    private bool initialized;
    private bool dirty;
    private bool updatingInspector;

    internal PanelsPage(
        PanelsPageViewModel viewModel,
        DeviceOperationsCoordinator deviceOps,
        IAppCatalogService catalogService,
        IAppModifierStateStore modifierStore,
        PanelsStore panelsStore,
        PanelsFrameComposer frameComposer,
        PanelsPlaybackService playbackService,
        Hub75VisualizerSessionService hub75VisualizerSessionService,
        CityAutocompleteService cityService,
        RemoteDeviceServerSecretStore secretStore)
    {
        this.viewModel = viewModel;
        this.deviceOps = deviceOps;
        this.catalogService = catalogService;
        this.modifierStore = modifierStore;
        this.panelsStore = panelsStore;
        this.frameComposer = frameComposer;
        this.playbackService = playbackService;
        this.hub75VisualizerSessionService = hub75VisualizerSessionService;
        this.secretStore = secretStore;
        modifierEditor = new AppModifierEditorHost(cityService, message => SetStatus(message, isError: true));
        modifierEditor.ModifierChanged += OnWidgetModifierChanged;

        previewTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        previewTimer.Interval = TimeSpan.FromMilliseconds(1000d / 6d);
        previewTimer.Tick += OnPreviewTimerTick;

        InitializeComponent();
        DataContext = viewModel;
        viewModel.ConfigureCommands(
            CreatePanelAsync,
            DuplicateCurrentPanelAsync,
            SaveCurrentPanelAsync,
            LoadCurrentPanelAsync,
            StopPlaybackAsync,
            DeleteCurrentPanelAsync);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnPageSizeChanged;
        KeyDown += OnPageKeyDown;
        IsTabStop = true;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!handlersAttached)
            {
                deviceOps.StateChanged += OnDeviceOpsStateChanged;
                playbackService.StateChanged += OnPlaybackStateChanged;
                playbackService.FrameUpdated += OnPlaybackFrameUpdated;
                handlersAttached = true;
            }

            pageLifetimeCts?.Cancel();
            pageLifetimeCts?.Dispose();
            pageLifetimeCts = new CancellationTokenSource();
            currentState = deviceOps.GetStateSnapshot();
            ApplyDevices(currentState.DeviceListSnapshot);
            SetPageMode(PanelsPageMode.Gallery);
            SetPreviewMode(PanelsPreviewMode.Off, syncToggle: true);
            UpdateAdaptiveLayout(ActualWidth, ActualHeight);

            if (!initialized)
            {
                await LoadCatalogAsync();
                await LoadPanelsAsync();
                initialized = true;
                return;
            }

            RebuildPanelsGallery();
            QueueInitialGalleryPosterBatch();
            UpdateGalleryCardStates();
            ClearEditorPreview();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            HandleLoadFailure(ex);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        previewTimer.Stop();
        pageLifetimeCts?.Cancel();
        pageLifetimeCts?.Dispose();
        pageLifetimeCts = null;
        previewRefreshCts?.Cancel();
        previewRefreshCts?.Dispose();
        previewRefreshCts = null;

        if (handlersAttached)
        {
            deviceOps.StateChanged -= OnDeviceOpsStateChanged;
            playbackService.StateChanged -= OnPlaybackStateChanged;
            playbackService.FrameUpdated -= OnPlaybackFrameUpdated;
            handlersAttached = false;
        }

        DisposePreviewSession();
        lock (panelPosterTasks)
        {
            panelPosterTasks.Clear();
            panelPosterGenerations.Clear();
        }
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAdaptiveLayout(e.NewSize.Width, e.NewSize.Height);
    }

    private void HandleLoadFailure(Exception ex)
    {
        initialized = false;
        App.ReportError("PanelsPage.OnLoaded failed", ex);

        storeDocument = new PanelsStoreDocument();
        storeDocument.Normalize();
        currentPanel = null;
        selectedWidget = null;
        selectedWidgetCatalogItem = null;
        dirty = false;

        panelGalleryItems.Clear();
        panelGalleryItemsById.Clear();
        panelThumbnailCache.Clear();
        lock (panelPosterTasks)
        {
            panelPosterTasks.Clear();
            panelPosterGenerations.Clear();
        }

        SetPageMode(PanelsPageMode.Gallery);
        SetPreviewMode(PanelsPreviewMode.Off, syncToggle: true);
        EditorCanvas.Panel = null;
        EditorCanvas.SelectedWidgetId = null;
        ClearEditorPreview();
        UpdatePanelInspector();
        UpdateWidgetInspector();
        UpdateGalleryCardStates();
        SetStatus("Falha ao carregar paineis. O store local foi restaurado/reiniciado.", isError: true);
    }

    private void OnPageKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (currentMode == PanelsPageMode.Editor
            && e.Key == Windows.System.VirtualKey.Delete
            && selectedWidget is not null)
        {
            _ = DeleteSelectedWidgetAsync();
            e.Handled = true;
        }
    }

    public void Dispose()
    {
        previewTimer.Stop();
        pageLifetimeCts?.Cancel();
        pageLifetimeCts?.Dispose();
        previewRefreshCts?.Cancel();
        previewRefreshCts?.Dispose();
        modifierEditor.Dispose();
        DisposePreviewSession();
        EditorCanvas.Dispose();
    }

    private async Task LoadCatalogAsync()
    {
        var catalog = await catalogService.LoadCatalogAsync();
        catalogItems.Clear();
        catalogItems.AddRange(catalog.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase));
        widgetLibraryEntries.Clear();
        catalogById.Clear();
        foreach (var item in catalogItems)
        {
            if (item.IsValid())
            {
                catalogById[item.Id] = item;
                var previewDraft = await ResolveWidgetDefaultValuesAsync(item);
                widgetLibraryEntries.Add(new WidgetLibraryEntry(
                    item,
                    PanelsFrameComposer.SupportsWidgetApp(item.Id),
                    PanelsFrameComposer.SupportsWidgetApp(item.Id) ? null : "Em breve",
                    previewDraft));
            }
        }

        ApplyWidgetLibraryFilter();
    }

    private async Task LoadPanelsAsync()
    {
        var loadedDocument = await panelsStore.LoadAsync();
        if (loadedDocument.Panels.Count == 0)
        {
            loadedDocument.Panels.Add(CreateDefaultPanel("Painel 1"));
            loadedDocument.LastSelectedPanelId = loadedDocument.Panels[0].PanelId;
            await panelsStore.SaveAsync(loadedDocument);
        }
        else
        {
            var normalizedAnyPanel = false;
            foreach (var panel in loadedDocument.Panels)
            {
                normalizedAnyPanel |= NormalizePanel(panel);
            }

            if (normalizedAnyPanel)
            {
                await panelsStore.SaveAsync(loadedDocument);
            }
        }

        if (await MergeServerCatalogPanelsAsync(loadedDocument).ConfigureAwait(true))
        {
            await panelsStore.SaveAsync(loadedDocument);
        }

        storeDocument = loadedDocument;
        var initialPanelId = storeDocument.LastSelectedPanelId ?? storeDocument.Panels.First().PanelId;
        await SelectPanelAsync(initialPanelId, saveDirty: false, refreshPreview: false);
        RebuildPanelsGallery();
        QueueInitialGalleryPosterBatch();

        // Sync all local panels to the server catalog on startup so the server
        // catalog is always in sync with what the Windows client has locally.
        foreach (var panel in storeDocument.Panels)
        {
            playbackService.FireSyncCatalogPanel(panel);
        }

        // Server is source of truth: discover any panel currently rendering
        // server-side BEFORE the gallery card states are computed, so panels
        // already running on the device show as "ativo" right after load.
        await SyncServerActivePanelsAsync().ConfigureAwait(true);

        UpdateGalleryCardStates();
        ClearEditorPreview();
    }

    private async Task<bool> MergeServerCatalogPanelsAsync(PanelsStoreDocument document)
    {
        try
        {
            var catalogJson = await playbackService.GetCatalogJsonAsync(pageLifetimeCts?.Token ?? CancellationToken.None).ConfigureAwait(true);
            if (string.IsNullOrWhiteSpace(catalogJson))
            {
                return false;
            }

            var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
            {
                PropertyNameCaseInsensitive = true,
            };

            var changed = false;
            using var catalog = System.Text.Json.JsonDocument.Parse(catalogJson);
            if (catalog.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array)
            {
                return false;
            }

            foreach (var entry in catalog.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("panel", out var panelElement)
                    || panelElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                {
                    continue;
                }

                var panelJson = NormalizeRemotePanelJson(panelElement);
                var remotePanel = System.Text.Json.JsonSerializer.Deserialize<PanelDefinition>(panelJson, jsonOptions);
                if (remotePanel is null)
                {
                    continue;
                }

                remotePanel.Normalize();
                var localIndex = document.Panels.FindIndex(panel =>
                    string.Equals(panel.PanelId, remotePanel.PanelId, StringComparison.OrdinalIgnoreCase));
                if (localIndex < 0)
                {
                    document.Panels.Add(remotePanel);
                    changed = true;
                    continue;
                }

                if (remotePanel.UpdatedAtUtc > document.Panels[localIndex].UpdatedAtUtc)
                {
                    document.Panels[localIndex] = remotePanel;
                    changed = true;
                }
            }

            if (changed)
            {
                document.Panels = document.Panels
                    .OrderBy(panel => panel.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();
            }

            return changed;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeRemotePanelJson(System.Text.Json.JsonElement panelElement)
    {
        var panelJson = panelElement.GetRawText();
        if (panelElement.TryGetProperty("updatedAtUtc", out var updatedAt)
            && updatedAt.ValueKind == System.Text.Json.JsonValueKind.String
            && string.IsNullOrWhiteSpace(updatedAt.GetString()))
        {
            panelJson = panelJson.Replace(
                "\"updatedAtUtc\":\"\"",
                $"\"updatedAtUtc\":\"{DateTimeOffset.MinValue:O}\"",
                StringComparison.Ordinal);
        }

        return panelJson;
    }

    /// <summary>
    /// Asks the server which panel (if any) is currently being rendered for
    /// each known device, and surfaces the result via PanelsPlaybackService so
    /// the gallery cards reflect the truth — not just what THIS WinUI session
    /// happens to have started locally.
    /// </summary>
    private async Task SyncServerActivePanelsAsync()
    {
        try
        {
            var deviceIds = currentState.DeviceListSnapshot
                .Select(d => d.DeviceId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (deviceIds.Length == 0)
            {
                return;
            }

            var ok = await playbackService.RefreshServerStateAsync(deviceIds).ConfigureAwait(true);
            if (!ok)
            {
                SetStatus("Servidor parcialmente indisponivel; alguns paineis podem nao refletir o estado real.", isError: false);
                return;
            }

            // Surface a friendly status when something is already running.
            foreach (var deviceId in deviceIds)
            {
                var serverInfo = playbackService.GetServerActivePanelFor(deviceId);
                if (serverInfo is null)
                {
                    continue;
                }

                if (string.Equals(serverInfo.Capability, "ServerCapable", StringComparison.OrdinalIgnoreCase))
                {
                    SetStatus($"Servidor renderizando '{serverInfo.PanelName}' em {deviceId} (autonomo).");
                }
                else
                {
                    SetStatus($"Painel '{serverInfo.PanelName}' armazenado em {deviceId} (precisa do cliente para renderizar).");
                }
                break; // mostrar apenas o primeiro para nao spammar a status bar
            }
        }
        catch (Exception ex)
        {
            App.ReportError("PanelsPage.SyncServerActivePanelsAsync failed", ex);
        }
    }

    private async Task CreatePanelAsync()
    {
        if (!await SaveCurrentPanelIfDirtyAsync())
        {
            return;
        }

        var nextName = $"Painel {storeDocument.Panels.Count + 1}";
        var panel = CreateDefaultPanel(nextName);
        storeDocument.Panels.Add(panel);
        storeDocument.LastSelectedPanelId = panel.PanelId;
        await panelsStore.SaveAsync(storeDocument);
        playbackService.FireSyncCatalogPanel(panel);
        InvalidatePanelPoster(panel.PanelId);
        RebuildPanelsGallery();
        _ = QueuePosterGenerationAsync(panel.PanelId, force: true);
        await OpenEditorAsync(panel.PanelId, saveDirty: false);
        SetStatus($"Painel '{panel.Name}' criado.");
    }

    private Task DuplicateCurrentPanelAsync()
    {
        if (currentPanel is null)
        {
            SetStatus("Selecione um painel para duplicar.", isError: true);
            return Task.CompletedTask;
        }

        return DuplicatePanelAsync(currentPanel.PanelId, openEditor: true);
    }

    private Task DeleteCurrentPanelAsync()
    {
        if (currentPanel is null)
        {
            SetStatus("Nenhum painel selecionado para excluir.", isError: true);
            return Task.CompletedTask;
        }

        return DeletePanelAsync(currentPanel.PanelId, returnToGallery: true);
    }

    private async Task DuplicatePanelAsync(string panelId, bool openEditor)
    {
        if (!TryGetPanel(panelId, out var sourcePanel))
        {
            SetStatus("Painel nao encontrado para duplicar.", isError: true);
            return;
        }

        if (currentMode == PanelsPageMode.Editor && !await SaveCurrentPanelIfDirtyAsync())
        {
            return;
        }

        var duplicate = sourcePanel.Clone();
        duplicate.PanelId = Guid.NewGuid().ToString("N");
        duplicate.Name = sourcePanel.Name + " copia";
        duplicate.UpdatedAtUtc = DateTimeOffset.UtcNow;
        NormalizePanel(duplicate);
        foreach (var widget in duplicate.Widgets)
        {
            widget.WidgetId = Guid.NewGuid().ToString("N");
        }

        storeDocument.Panels.Add(duplicate);
        storeDocument.LastSelectedPanelId = duplicate.PanelId;
        await panelsStore.SaveAsync(storeDocument);
        playbackService.FireSyncCatalogPanel(duplicate);
        InvalidatePanelPoster(duplicate.PanelId);
        RebuildPanelsGallery();
        _ = QueuePosterGenerationAsync(duplicate.PanelId, force: true);

        if (openEditor)
        {
            await OpenEditorAsync(duplicate.PanelId, saveDirty: false);
        }
        else
        {
            await SelectPanelAsync(duplicate.PanelId, saveDirty: false, refreshPreview: false);
            UpdateGalleryCardStates();
        }

        SetStatus($"Painel '{duplicate.Name}' duplicado.");
    }

    private async Task DeletePanelAsync(string panelId, bool returnToGallery)
    {
        if (!TryGetPanel(panelId, out var panelToDelete))
        {
            SetStatus("Painel nao encontrado para excluir.", isError: true);
            return;
        }

        var deletingCurrentPanel = currentPanel is not null
            && string.Equals(currentPanel.PanelId, panelId, StringComparison.OrdinalIgnoreCase);
        if (deletingCurrentPanel && currentMode == PanelsPageMode.Editor)
        {
            dirty = false;
        }

        if (IsRetainedPanel(panelId))
        {
            await playbackService.StopAsync();
        }

        storeDocument.Panels.RemoveAll(panel => string.Equals(panel.PanelId, panelId, StringComparison.OrdinalIgnoreCase));
        playbackService.FireDeleteCatalogPanel(panelId);
        InvalidatePanelPoster(panelId);

        if (storeDocument.Panels.Count == 0)
        {
            var replacement = CreateDefaultPanel("Painel 1");
            storeDocument.Panels.Add(replacement);
            storeDocument.LastSelectedPanelId = replacement.PanelId;
        }
        else if (string.Equals(storeDocument.LastSelectedPanelId, panelId, StringComparison.OrdinalIgnoreCase))
        {
            storeDocument.LastSelectedPanelId = storeDocument.Panels[0].PanelId;
        }

        if (deletingCurrentPanel || currentPanel is null || !TryGetPanel(currentPanel.PanelId, out _))
        {
            currentPanel = null;
        }

        await panelsStore.SaveAsync(storeDocument);

        var nextPanelId = currentPanel?.PanelId;
        if (string.IsNullOrWhiteSpace(nextPanelId) || !TryGetPanel(nextPanelId, out _))
        {
            nextPanelId = storeDocument.LastSelectedPanelId ?? storeDocument.Panels[0].PanelId;
        }

        if (!string.IsNullOrWhiteSpace(nextPanelId))
        {
            await SelectPanelAsync(nextPanelId, saveDirty: false, refreshPreview: false);
        }

        RebuildPanelsGallery();
        QueueInitialGalleryPosterBatch();
        if (returnToGallery)
        {
            SetPageMode(PanelsPageMode.Gallery);
            ClearEditorPreview();
        }

        UpdateGalleryCardStates();
        SetStatus($"Painel '{panelToDelete.Name}' excluido.");
    }

    private async Task SaveCurrentPanelAsync()
    {
        if (currentPanel is null)
        {
            SetStatus("Nenhum painel selecionado para salvar.", isError: true);
            return;
        }

        NormalizePanel(currentPanel);
        currentPanel.UpdatedAtUtc = DateTimeOffset.UtcNow;
        storeDocument.LastSelectedPanelId = currentPanel.PanelId;
        await panelsStore.SaveAsync(storeDocument);
        playbackService.FireSyncCatalogPanel(currentPanel);

        dirty = false;
        InvalidatePanelPoster(currentPanel.PanelId);
        UpsertGalleryItemState(currentPanel);
        _ = QueuePosterGenerationAsync(currentPanel.PanelId, force: true);
        UpdatePanelInspector();

        var reapplied = await ReapplyActivePanelIfNeededAsync();
        if (currentMode == PanelsPageMode.Editor)
        {
            await RefreshPreviewSessionAsync();
        }

        UpdateGalleryCardStates();
        SetStatus(reapplied
            ? $"Painel '{currentPanel.Name}' salvo e reaplicado."
            : $"Painel '{currentPanel.Name}' salvo.");
    }

    private async Task LoadCurrentPanelAsync()
    {
        if (currentPanel is null)
        {
            SetStatus("Selecione um painel antes de carregar.", isError: true);
            return;
        }

        await LoadPanelAsync(currentPanel);
    }

    private async Task LoadPanelAsync(PanelDefinition panel)
    {
        var deviceId = GetSelectedDeviceId();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            SetStatus("Selecione um dispositivo online para ativar o painel.", isError: true);
            UpdateGalleryCardStates();
            return;
        }

        if (!EnsurePanelActivationAllowed())
        {
            UpdateGalleryCardStates();
            return;
        }

        if (currentMode == PanelsPageMode.Editor
            && currentPanel is not null
            && string.Equals(currentPanel.PanelId, panel.PanelId, StringComparison.OrdinalIgnoreCase)
            && !await SaveCurrentPanelIfDirtyAsync())
        {
            UpdateGalleryCardStates();
            return;
        }

        await playbackService.StartAsync(panel.Clone(), deviceId);
        UpdateGalleryCardStates();
        SetStatus($"Painel '{panel.Name}' carregado em {deviceId}.");
    }

    private async Task StopPlaybackAsync()
    {
        await playbackService.StopAsync();
        UpdateGalleryCardStates();
        SetStatus("Runtime de painel interrompido.");
    }

    private void OnTargetDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateGalleryCardStates();
    }

    private void OnDeviceOpsStateChanged(object? sender, EventArgs e)
    {
        var state = deviceOps.GetStateSnapshot();
        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            currentState = state;
            ApplyDevices(state.DeviceListSnapshot);
            UpdateGalleryCardStates();

            // Refresh server-side panel state whenever the device list changes
            // (e.g. a device connects after the page was already loaded) so that
            // SuspendAsync can find and stop server-capable panels when needed.
            await SyncServerActivePanelsAsync().ConfigureAwait(true);
            UpdateGalleryCardStates();
        });
    }

    private void OnPlaybackStateChanged(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(UpdateGalleryCardStates);
    }

    private void OnPlaybackFrameUpdated(object? sender, RgbaColor[] frame)
    {
        var activePanel = playbackService.GetActivePanelSnapshot();
        if (activePanel is null)
        {
            return;
        }

        var panelId = activePanel.PanelId;
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            panelThumbnailCache[panelId] = frame;
            if (panelGalleryItemsById.TryGetValue(panelId, out var state))
            {
                state.Frame = frame;
            }
        });
    }

    private async void OnNewPanelClicked(object sender, RoutedEventArgs e)
    {
        await CreatePanelAsync();
    }

    private async void OnSavePanelClicked(object sender, RoutedEventArgs e)
    {
        await TrySaveCurrentPanelAsync();
    }

    private async void OnDuplicatePanelClicked(object sender, RoutedEventArgs e)
    {
        if (currentPanel is not null)
        {
            await DuplicatePanelAsync(currentPanel.PanelId, openEditor: true);
        }
    }

    private async void OnDeletePanelClicked(object sender, RoutedEventArgs e)
    {
        if (currentPanel is not null)
        {
            await DeletePanelAsync(currentPanel.PanelId, returnToGallery: true);
        }
    }

    private async void OnEditorBackClicked(object sender, RoutedEventArgs e)
    {
        await TryReturnToGalleryAsync();
    }

    private async void OnEditorPreviewToggleToggled(object sender, RoutedEventArgs e)
    {
        if (updatingInspector)
        {
            return;
        }

        SetPreviewMode(EditorPreviewToggle.IsOn ? PanelsPreviewMode.OnDemand : PanelsPreviewMode.Off, syncToggle: false);
        await RefreshPreviewSessionAsync();
        SetStatus(EditorPreviewToggle.IsOn ? "Preview do editor ativado." : "Preview do editor pausado.");
    }

    private void OnPanelNameChanged(object sender, TextChangedEventArgs e)
    {
        if (updatingInspector || currentPanel is null)
        {
            return;
        }

        currentPanel.Name = string.IsNullOrWhiteSpace(EditorNameText.Text) ? "Painel" : EditorNameText.Text.Trim();
        viewModel.SelectedPanelName = currentPanel.Name;
        MarkDirty("Nome do painel alterado.");
    }

    private async void OnDeleteWidgetClicked(object sender, RoutedEventArgs e)
    {
        await DeleteSelectedWidgetAsync();
    }

    private async Task DeleteSelectedWidgetAsync()
    {
        if (currentPanel is null || selectedWidget is null)
        {
            return;
        }

        currentPanel.Widgets.RemoveAll(widget => string.Equals(widget.WidgetId, selectedWidget.WidgetId, StringComparison.OrdinalIgnoreCase));
        currentPanel.Normalize();
        selectedWidget = null;
        selectedWidgetCatalogItem = null;
        EditorCanvas.SelectedWidgetId = null;
        UpdateWidgetInspector();
        EditorCanvas.Panel = currentPanel;
        MarkDirty("Widget removido do painel.");
        await RefreshPreviewSessionAsync();
    }

    private async void OnGifSourceButtonClicked(object sender, RoutedEventArgs e)
    {
        if (selectedWidget is null || !string.Equals(selectedWidget.AppId, "gifhub75", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (ResolveSelectedSourceType())
        {
            var folder = await PickImageFolderAsync();
            if (folder is null)
            {
                return;
            }

            selectedWidget.RuntimeState["sourcePath"] = folder.Path;
        }
        else
        {
            var file = await PickImageFileAsync();
            if (file is null)
            {
                return;
            }

            selectedWidget.RuntimeState["sourcePath"] = file.Path;
        }

        UpdateWidgetSourceUi();
        MarkDirty("Fonte do widget GIF atualizada.");
        await RefreshPreviewSessionAsync();
    }

    private async void OnGifGiphyButtonClicked(object sender, RoutedEventArgs e)
    {
        if (selectedWidget is null || !string.Equals(selectedWidget.AppId, "gifhub75", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var xamlRoot = XamlRoot;
        if (xamlRoot is null)
        {
            return;
        }

        var adminToken = await secretStore.LoadAdminTokenAsync().ConfigureAwait(true);
        var giphyService = new GiphySearchService(deviceOps.GetServerBaseAddress(), adminToken);
        var dialog = new GiphySearchDialog(giphyService, xamlRoot);
        await dialog.ShowAsync();

        if (dialog.SelectedItem is null)
        {
            return;
        }

        SetStatus("Baixando GIF do GIPHY…");
        var bytes = await GiphySearchService.DownloadGifBytesAsync(dialog.SelectedItem.GifUrl).ConfigureAwait(true);
        if (bytes is null || bytes.Length == 0)
        {
            SetStatus("Falha ao baixar o GIF selecionado.", isError: true);
            return;
        }

        var tempPath = Path.Combine(Path.GetTempPath(), $"mica_giphy_{dialog.SelectedItem.Id}.gif");
        await File.WriteAllBytesAsync(tempPath, bytes).ConfigureAwait(true);

        selectedWidget.RuntimeState["sourcePath"] = tempPath;
        UpdateWidgetSourceUi();
        MarkDirty($"GIF do GIPHY selecionado: {dialog.SelectedItem.Title}");
        await RefreshPreviewSessionAsync().ConfigureAwait(false);
    }

    private async void OnEditorMoveSelectedWidgetBackwardRequested(object? sender, EventArgs e)
    {
        await ReorderSelectedWidgetAsync(moveForward: false);
    }

    private async void OnEditorMoveSelectedWidgetForwardRequested(object? sender, EventArgs e)
    {
        await ReorderSelectedWidgetAsync(moveForward: true);
    }

    private void OnEditorCycleOverlappingWidgetRequested(object? sender, EventArgs e)
    {
        if (currentPanel is null || selectedWidget is null)
        {
            return;
        }

        if (currentPanel.TryGetNextOverlappingWidgetId(selectedWidget.WidgetId, out var nextWidgetId))
        {
            SelectWidgetById(nextWidgetId);
            SetStatus("Widget sobreposto selecionado.");
        }
    }

    private void OnWidgetLibrarySearchChanged(object sender, TextChangedEventArgs e)
    {
        ApplyWidgetLibraryFilter();
    }

    private void OnWidgetLibraryDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (!TryResolveDraggedCatalogItem(e.Items, out var entry) || !entry.IsSupported)
        {
            return;
        }

        e.Data.Properties[DraggedWidgetAppIdKey] = entry.Item.Id;
        e.Data.SetText(entry.Item.Id);
        e.Data.RequestedOperation = DataPackageOperation.Copy;
    }

    private void OnCanvasDragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = CanResolveDraggedWidgetAppId(e.DataView)
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
    }

    private async void OnCanvasDrop(object sender, DragEventArgs e)
    {
        if (currentPanel is null)
        {
            return;
        }

        var appId = await TryResolveDraggedWidgetAppIdAsync(e.DataView);
        if (string.IsNullOrWhiteSpace(appId)
            || !catalogById.TryGetValue(appId, out var item)
            || !PanelsFrameComposer.SupportsWidgetApp(item.Id))
        {
            return;
        }

        var point = e.GetPosition(EditorCanvas);
        _ = EditorCanvas.TryMapToMatrix(point, out var matrixX, out var matrixY);
        var widget = new PanelWidgetDefinition
        {
            WidgetId = Guid.NewGuid().ToString("N"),
            AppId = item.Id,
            X = matrixX,
            Y = matrixY,
            Width = Math.Min(DefaultNewWidgetWidth, currentPanel.Width),
            Height = Math.Min(DefaultNewWidgetHeight, currentPanel.Height),
            ZIndex = GetNextWidgetZIndex(),
            ConfigValues = await ResolveWidgetDefaultValuesAsync(item),
        };
        widget.Normalize(currentPanel.Width, currentPanel.Height);
        currentPanel.Widgets.Add(widget);
        currentPanel.Normalize();
        selectedWidget = currentPanel.Widgets.FirstOrDefault(entry => string.Equals(entry.WidgetId, widget.WidgetId, StringComparison.OrdinalIgnoreCase));
        EditorCanvas.Panel = currentPanel;
        EditorCanvas.SelectedWidgetId = selectedWidget?.WidgetId;
        UpdateWidgetInspector();
        MarkDirty($"Widget '{item.Name}' adicionado.");
        await RefreshPreviewSessionAsync();
    }

    private void OnEditorWidgetSelected(object? sender, string? widgetId)
    {
        if (currentPanel is null)
        {
            return;
        }

        SelectWidgetById(widgetId);
    }

    private async void OnEditorWidgetBoundsChanged(object? sender, Hub75PanelWidgetBoundsChangedEventArgs e)
    {
        if (currentPanel is null)
        {
            return;
        }

        dirty = true;

        if (e.IsCommit)
        {
            currentPanel.Normalize();
            selectedWidget = currentPanel.Widgets.FirstOrDefault(widget => string.Equals(widget.WidgetId, e.WidgetId, StringComparison.OrdinalIgnoreCase));
            EditorCanvas.Panel = currentPanel;
            SetBoundsChangedStatus(e);
            UpdateWidgetInspector();
            await RefreshPreviewSessionAsync();
        }
    }

    private async void OnWidgetModifierChanged(object? sender, string key)
    {
        if (selectedWidget is null || selectedWidgetCatalogItem is null)
        {
            return;
        }

        if (!modifierEditor.TryBuildConfig(selectedWidgetCatalogItem, out _, out var rawValues, out _))
        {
            return;
        }

        selectedWidget.ConfigValues = new Dictionary<string, string>(rawValues, StringComparer.OrdinalIgnoreCase);
        UpdateWidgetSourceUi();
        MarkDirty($"Configuracao do widget '{selectedWidgetCatalogItem.Name}' atualizada.");
        ScheduleEditorSurfaceRefresh();
    }

    private void OnPreviewTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (currentMode != PanelsPageMode.Editor || previewMode != PanelsPreviewMode.OnDemand || previewSession is null)
        {
            return;
        }

        EditorCanvas.Frame = previewSession.RenderFrame(DateTimeOffset.UtcNow);
        EditorCanvas.WidgetErrors = previewSession.GetWidgetErrors();
    }

    private async Task<bool> OpenEditorAsync(string panelId, bool saveDirty)
    {
        var previousMode = currentMode;
        SetPageMode(PanelsPageMode.Editor);
        SetPreviewMode(PanelsPreviewMode.Off, syncToggle: true);
        if (!await SelectPanelAsync(panelId, saveDirty, refreshPreview: true))
        {
            SetPageMode(previousMode);
            return false;
        }

        EditorCanvas.Focus(FocusState.Programmatic);
        return true;
    }

    private async Task<bool> TryReturnToGalleryAsync()
    {
        if (!await SaveCurrentPanelIfDirtyAsync())
        {
            return false;
        }

        if (currentPanel is not null)
        {
            UpsertGalleryItemState(currentPanel);
            _ = QueuePosterGenerationAsync(currentPanel.PanelId, force: false);
        }

        SetPageMode(PanelsPageMode.Gallery);
        SetPreviewMode(PanelsPreviewMode.Off, syncToggle: true);
        ClearEditorPreview();
        UpdateGalleryCardStates();
        return true;
    }

    private async Task<bool> SelectPanelAsync(string panelId, bool saveDirty, bool refreshPreview)
    {
        if (!TryGetPanel(panelId, out var targetPanel))
        {
            return false;
        }

        var switchingPanel = currentPanel is null
            || !string.Equals(currentPanel.PanelId, targetPanel.PanelId, StringComparison.OrdinalIgnoreCase);
        if (switchingPanel && saveDirty && !await SaveCurrentPanelIfDirtyAsync())
        {
            return false;
        }

        currentPanel = targetPanel;
        NormalizePanel(currentPanel);
        selectedWidget = null;
        selectedWidgetCatalogItem = null;
        dirty = false;
        viewModel.SelectedPanelName = targetPanel.Name;
        storeDocument.LastSelectedPanelId = targetPanel.PanelId;

        UpdatePanelInspector();
        UpdateWidgetInspector();
        EditorCanvas.Panel = currentPanel;
        EditorCanvas.SelectedWidgetId = null;

        await PersistSelectionAsync();
        if (refreshPreview)
        {
            await RefreshPreviewSessionAsync();
        }
        else
        {
            ClearEditorPreview();
        }

        return true;
    }

    private async Task PersistSelectionAsync()
    {
        if (currentPanel is null)
        {
            return;
        }

        storeDocument.LastSelectedPanelId = currentPanel.PanelId;
        await panelsStore.SaveAsync(storeDocument);
    }

    private async Task<bool> SaveCurrentPanelIfDirtyAsync()
    {
        return !dirty || await TrySaveCurrentPanelAsync();
    }

    private async Task<bool> TrySaveCurrentPanelAsync()
    {
        try
        {
            await SaveCurrentPanelAsync();
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Falha ao salvar painel: {ex.Message}", isError: true);
            return false;
        }
    }

    private async Task<bool> ReapplyActivePanelIfNeededAsync()
    {
        if (currentPanel is null)
        {
            return false;
        }

        if (IsVisualizerHub75PriorityActive())
        {
            return false;
        }

        var activePanel = playbackService.GetActivePanelSnapshot();
        var targetDeviceId = playbackService.TargetDeviceId;
        if (activePanel is null
            || string.IsNullOrWhiteSpace(targetDeviceId)
            || !string.Equals(activePanel.PanelId, currentPanel.PanelId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        await playbackService.StartAsync(currentPanel.Clone(), targetDeviceId);
        return true;
    }

    private async Task RefreshPreviewSessionAsync()
    {
        DisposePreviewSession();

        if (currentPanel is null || currentMode != PanelsPageMode.Editor)
        {
            ClearEditorPreview();
            return;
        }

        try
        {
            if (previewMode == PanelsPreviewMode.Off)
            {
                previewTimer.Stop();
                var posterResult = await frameComposer.CreatePosterAsync(currentPanel.Clone(), pageLifetimeCts?.Token ?? CancellationToken.None);
                panelThumbnailCache[currentPanel.PanelId] = posterResult.Frame;
                if (panelGalleryItemsById.TryGetValue(currentPanel.PanelId, out var state))
                {
                    state.PosterStatus = posterResult.WidgetErrors.Count == 0 ? PanelPosterStatus.Ready : PanelPosterStatus.Error;
                    if (!state.IsActive)
                    {
                        state.Frame = posterResult.Frame;
                    }
                }

                EditorCanvas.Frame = posterResult.Frame;
                EditorCanvas.WidgetErrors = posterResult.WidgetErrors;
                return;
            }

            previewSession = await frameComposer.CreateSessionAsync(currentPanel.Clone(), pageLifetimeCts?.Token ?? CancellationToken.None);
            EditorCanvas.Frame = previewSession.RenderFrame(DateTimeOffset.UtcNow);
            EditorCanvas.WidgetErrors = previewSession.GetWidgetErrors();
            if (!previewTimer.IsRunning)
            {
                previewTimer.Start();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ClearEditorPreview();
            SetStatus($"Falha ao recompor preview: {ex.Message}", isError: true);
        }
    }

    private void DisposePreviewSession()
    {
        previewSession?.Dispose();
        previewSession = null;
    }

    private void ClearEditorPreview()
    {
        previewTimer.Stop();
        DisposePreviewSession();
        EditorCanvas.Frame = EmptyFrame;
        EditorCanvas.WidgetErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private void SetPreviewMode(PanelsPreviewMode mode, bool syncToggle)
    {
        previewMode = mode;
        if (syncToggle && EditorPreviewToggle is not null)
        {
            updatingInspector = true;
            try
            {
                EditorPreviewToggle.IsOn = mode == PanelsPreviewMode.OnDemand;
            }
            finally
            {
                updatingInspector = false;
            }
        }
    }

    private void ScheduleEditorSurfaceRefresh()
    {
        previewRefreshCts?.Cancel();
        previewRefreshCts?.Dispose();
        previewRefreshCts = new CancellationTokenSource();
        var token = previewRefreshCts.Token;
        _ = DebounceEditorSurfaceRefreshAsync(token);
    }

    private async Task DebounceEditorSurfaceRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            var delay = previewMode == PanelsPreviewMode.OnDemand
                ? TimeSpan.FromMilliseconds(180)
                : TimeSpan.FromMilliseconds(320);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (DispatcherQueue is null)
            {
                return;
            }

            await DispatcherQueue.EnqueueAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    _ = RefreshPreviewSessionAsync();
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void InvalidatePanelPoster(string panelId)
    {
        panelThumbnailCache.Remove(panelId);
        if (panelGalleryItemsById.TryGetValue(panelId, out var state))
        {
            state.PosterStatus = PanelPosterStatus.Missing;
            if (!state.IsActive)
            {
                state.Frame = EmptyFrame;
            }
        }
    }

    private void QueueInitialGalleryPosterBatch()
    {
        foreach (var item in panelGalleryItems.Take(4))
        {
            _ = QueuePosterGenerationAsync(item.PanelId, force: false);
        }
    }

    private Task QueuePosterGenerationAsync(string panelId, bool force)
    {
        if (!TryGetPanel(panelId, out var panel))
        {
            return Task.CompletedTask;
        }

        var generation = 0;
        if (!force && panelThumbnailCache.ContainsKey(panelId))
        {
            if (panelGalleryItemsById.TryGetValue(panelId, out var cachedState))
            {
                cachedState.PosterStatus = PanelPosterStatus.Ready;
                if (!cachedState.IsActive)
                {
                    cachedState.Frame = panelThumbnailCache[panelId];
                }
            }
            return Task.CompletedTask;
        }

        lock (panelPosterTasks)
        {
            if (panelPosterTasks.TryGetValue(panelId, out var existing))
            {
                if (existing.IsCompleted)
                {
                    panelPosterTasks.Remove(panelId);
                }
                else if (!force)
                {
                    return existing;
                }
            }

            generation = panelPosterGenerations.TryGetValue(panelId, out var currentGeneration)
                ? currentGeneration + 1
                : 1;
            panelPosterGenerations[panelId] = generation;
        }

        if (panelGalleryItemsById.TryGetValue(panelId, out var loadingState))
        {
            loadingState.PosterStatus = PanelPosterStatus.Loading;
        }

        var task = GeneratePosterAsync(panel.Clone(), generation, pageLifetimeCts?.Token ?? CancellationToken.None);
        lock (panelPosterTasks)
        {
            panelPosterTasks[panelId] = task;
        }

        return task;
    }

    private async Task GeneratePosterAsync(PanelDefinition panel, int generation, CancellationToken cancellationToken)
    {
        try
        {
            var posterResult = await frameComposer.CreatePosterAsync(panel, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested || !IsLatestPosterGeneration(panel.PanelId, generation))
            {
                return;
            }

            await DispatcherQueue.EnqueueAsync(() =>
            {
                panelThumbnailCache[panel.PanelId] = posterResult.Frame;
                if (panelGalleryItemsById.TryGetValue(panel.PanelId, out var state))
                {
                    state.PosterStatus = posterResult.WidgetErrors.Count == 0 ? PanelPosterStatus.Ready : PanelPosterStatus.Error;
                    if (!state.IsActive)
                    {
                        state.Frame = posterResult.Frame;
                    }
                }

                if (currentPanel is not null
                    && string.Equals(currentPanel.PanelId, panel.PanelId, StringComparison.OrdinalIgnoreCase)
                    && currentMode == PanelsPageMode.Editor
                    && previewMode == PanelsPreviewMode.Off)
                {
                    EditorCanvas.Frame = posterResult.Frame;
                    EditorCanvas.WidgetErrors = posterResult.WidgetErrors;
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await DispatcherQueue.EnqueueAsync(() =>
            {
                if (!IsLatestPosterGeneration(panel.PanelId, generation))
                {
                    return;
                }

                panelThumbnailCache[panel.PanelId] = EmptyFrame;
                if (panelGalleryItemsById.TryGetValue(panel.PanelId, out var state))
                {
                    state.PosterStatus = PanelPosterStatus.Error;
                    if (!state.IsActive)
                    {
                        state.Frame = EmptyFrame;
                    }
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            lock (panelPosterTasks)
            {
                if (panelPosterGenerations.TryGetValue(panel.PanelId, out var trackedGeneration)
                    && trackedGeneration == generation)
                {
                    panelPosterTasks.Remove(panel.PanelId);
                }
            }
        }
    }

    private bool IsLatestPosterGeneration(string panelId, int generation)
    {
        lock (panelPosterTasks)
        {
            return panelPosterGenerations.TryGetValue(panelId, out var trackedGeneration)
                && trackedGeneration == generation;
        }
    }

    private void RebuildPanelsGallery()
    {
        panelGalleryItems.Clear();
        panelGalleryItemsById.Clear();

        foreach (var panel in storeDocument.Panels.OrderBy(panel => panel.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var state = CreateGalleryItemState(panel);
            panelGalleryItems.Add(state);
            panelGalleryItemsById[state.PanelId] = state;
        }

        UpdateAdaptiveLayout(ActualWidth, ActualHeight);
    }

    private void UpsertGalleryItemState(PanelDefinition panel)
    {
        if (!panelGalleryItemsById.TryGetValue(panel.PanelId, out var state))
        {
            RebuildPanelsGallery();
            return;
        }

        state.Name = panel.Name;
        state.WidgetCount = panel.Widgets.Count;
        state.Subtitle = state.IsActive
            ? $"Ativo em {playbackService.TargetDeviceId}"
            : $"{panel.Widgets.Count} widget(s)";
    }

    private PanelGalleryItemState CreateGalleryItemState(PanelDefinition panel)
    {
        return new PanelGalleryItemState(
            panel.PanelId,
            panel.Name,
            panel.Widgets.Count,
            ResolveGalleryCardWidth(ActualWidth),
            panelThumbnailCache.TryGetValue(panel.PanelId, out var frame) ? frame : EmptyFrame,
            openEditorAsync: panelId => OpenEditorAsync(panelId, saveDirty: currentMode == PanelsPageMode.Editor),
            duplicateAsync: panelId => DuplicatePanelAsync(panelId, openEditor: true),
            deleteAsync: panelId => DeletePanelAsync(panelId, returnToGallery: true),
            setActiveAsync: SetPanelActivationAsync)
        {
            PosterStatus = panelThumbnailCache.ContainsKey(panel.PanelId) ? PanelPosterStatus.Ready : PanelPosterStatus.Missing,
        };
    }

    private void RebuildWidgetLibrary()
    {
        WidgetLibraryList.Items.Clear();
        foreach (var entry in filteredWidgetLibraryEntries)
        {
            var chip = new WidgetRailCardControl(
                entry.Item,
                ResolveWidgetLibraryShortLabel(entry.Item),
                ResolveWidgetLibraryCategoryLabel(entry.Item));
            chip.Tag = entry;
            chip.SetAvailability(entry.IsSupported);
            var listItem = new ListViewItem
            {
                Tag = entry,
                Content = chip,
                IsEnabled = true,
                CanDrag = entry.IsSupported,
            };
            if (!entry.IsSupported)
            {
                ToolTipService.SetToolTip(listItem, "Este widget ainda nao possui renderer HUB75 no compositor.");
            }

            WidgetLibraryList.Items.Add(listItem);
        }
    }

    private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)
    {
        var selectedDeviceId = GetSelectedDeviceId();
        var activeTargetDeviceId = playbackService.TargetDeviceId;
        var onlineDevices = devices
            .Where(static device => device.Status == DeviceStatus.Online)
            .OrderBy(device => device.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        TargetDeviceCombo.Items.Clear();
        foreach (var device in onlineDevices)
        {
            TargetDeviceCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{device.Name} ({device.DeviceId})",
                Tag = device.DeviceId,
            });
        }

        var preferredDeviceId = selectedDeviceId;
        if (string.IsNullOrWhiteSpace(preferredDeviceId)
            && !string.IsNullOrWhiteSpace(activeTargetDeviceId)
            && onlineDevices.Any(device => string.Equals(device.DeviceId, activeTargetDeviceId, StringComparison.OrdinalIgnoreCase)))
        {
            preferredDeviceId = activeTargetDeviceId;
        }

        if (string.IsNullOrWhiteSpace(preferredDeviceId) && onlineDevices.Count == 1)
        {
            preferredDeviceId = onlineDevices[0].DeviceId;
        }

        if (!string.IsNullOrWhiteSpace(preferredDeviceId))
        {
            foreach (var item in TargetDeviceCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag as string, preferredDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    TargetDeviceCombo.SelectedItem = item;
                    break;
                }
            }
        }
    }

    private void UpdateGalleryCardStates()
    {
        var activePanel = playbackService.GetActivePanelSnapshot();
        var activePanelId = activePanel?.PanelId;
        var activeDeviceId = playbackService.TargetDeviceId;
        var selectedDeviceId = GetSelectedDeviceId();
        var canActivateNewPanel = !IsVisualizerHub75PriorityActive() && !string.IsNullOrWhiteSpace(selectedDeviceId);

        foreach (var state in panelGalleryItems)
        {
            if (!TryGetPanel(state.PanelId, out var panel))
            {
                continue;
            }

            state.Name = panel.Name;
            state.WidgetCount = panel.Widgets.Count;

            var isLocallyActive = string.Equals(panel.PanelId, activePanelId, StringComparison.OrdinalIgnoreCase);
            var isServerActive  = !isLocallyActive && playbackService.IsServerActivePanel(panel.PanelId);
            state.IsActive   = isLocallyActive || isServerActive;
            state.CanActivate = state.IsActive || canActivateNewPanel;

            if (isLocallyActive)
            {
                state.Subtitle = $"Ativo em {activeDeviceId}";
                state.Frame = playbackService.GetLatestFrame()
                    ?? (panelThumbnailCache.TryGetValue(state.PanelId, out var activePoster) ? activePoster : EmptyFrame);
            }
            else if (isServerActive)
            {
                var serverDeviceId = playbackService.GetServerActiveDeviceFor(panel.PanelId);
                state.Subtitle = string.IsNullOrWhiteSpace(serverDeviceId)
                    ? "Ativo no servidor"
                    : $"Ativo no servidor ({serverDeviceId})";
                state.Frame = panelThumbnailCache.TryGetValue(state.PanelId, out var serverPoster) ? serverPoster : EmptyFrame;
            }
            else
            {
                state.Subtitle = $"{panel.Widgets.Count} widget(s)";
                state.Frame = panelThumbnailCache.TryGetValue(state.PanelId, out var poster) ? poster : EmptyFrame;
            }
        }
    }

    private void UpdatePanelInspector()
    {
        updatingInspector = true;
        try
        {
            UpdateEditorHeader();
        }
        finally
        {
            updatingInspector = false;
        }
    }

    private async void UpdateWidgetInspector()
    {
        updatingInspector = true;
        try
        {
            if (selectedWidget is null)
            {
                WidgetInspectorTitle.Text = "Widget";
                DeleteWidgetButton.IsEnabled = false;
                WidgetSourceCard.Visibility = Visibility.Collapsed;
                GifSourcePathText.Text = string.Empty;
                EditorCanvas.SelectedWidgetId = null;
                selectedWidgetCatalogItem = null;
            }
            else
            {
                selectedWidgetCatalogItem = catalogById.GetValueOrDefault(selectedWidget.AppId);
                WidgetInspectorTitle.Text = selectedWidgetCatalogItem is null
                    ? $"Widget: {selectedWidget.AppId}"
                    : $"Widget: {selectedWidgetCatalogItem.Name}";
                DeleteWidgetButton.IsEnabled = true;
                EditorCanvas.SelectedWidgetId = selectedWidget.WidgetId;
                UpdateWidgetSourceUi();
            }
        }
        finally
        {
            updatingInspector = false;
        }

        await modifierEditor.LoadAsync(
            selectedWidgetCatalogItem,
            WidgetModifiersPanel,
            WidgetModifiersHintText,
            selectedWidget?.ConfigValues,
            emptySelectionHint: "Selecione um widget para editar a configuracao.",
            configuredHint: "As alteracoes valem apenas para este widget ate salvar o painel.",
            noModifiersHint: "Este widget nao possui modificadores configuraveis.");
    }

    private void UpdateWidgetSourceUi()
    {
        var isGifWidget = selectedWidget is not null && string.Equals(selectedWidget.AppId, "gifhub75", StringComparison.OrdinalIgnoreCase);
        WidgetSourceCard.Visibility = isGifWidget ? Visibility.Visible : Visibility.Collapsed;
        if (!isGifWidget)
        {
            GifSourcePathText.Text = string.Empty;
            return;
        }

        var isSlideshow = ResolveSelectedSourceType();
        GifSourceButton.Content = isSlideshow ? "Selecionar pasta" : "Selecionar arquivo";
        // GIPHY only makes sense for single-GIF mode (not slideshow)
        GifGiphyButton.Visibility = isSlideshow ? Visibility.Collapsed : Visibility.Visible;
        GifSourcePathText.Text = selectedWidget!.RuntimeState.TryGetValue("sourcePath", out var path)
            ? path
            : string.Empty;
    }

    private string ResolveWidgetLayerDisplayLabel(PanelWidgetDefinition widget)
    {
        var displayName = catalogById.TryGetValue(widget.AppId, out var item)
            ? ResolveWidgetLibraryShortLabel(item)
            : widget.AppId;
        var shortId = widget.WidgetId.Length > 4 ? widget.WidgetId[..4] : widget.WidgetId;
        return $"{displayName} · {shortId}";
    }

    private static string ResolveWidgetLayerSubtitle(PanelWidgetDefinition widget, int visualIndex, int totalCount)
    {
        var position = visualIndex switch
        {
            0 => "Topo",
            var last when last == totalCount - 1 => "Fundo",
            _ => $"Camada {totalCount - visualIndex}",
        };
        return $"{position} · {widget.Width}x{widget.Height}";
    }

    private void UpdateEditorHeader()
    {
        var nextName = currentPanel?.Name ?? string.Empty;
        if (!string.Equals(EditorNameText.Text, nextName, StringComparison.Ordinal))
        {
            EditorNameText.Text = nextName;
        }
    }

    private static CanvasFirstPageLayoutMode ResolveEditorAdaptiveLayoutMode(double width)
    {
        return CanvasFirstPageScrollPolicy.ResolveMode(width);
    }

    private static CanvasFirstPageLayoutPlan ResolveEditorLayoutPlan(double width, double height)
    {
        return CanvasFirstPageScrollPolicy.Resolve(width, height);
    }

    private static EditorPaneLayout ResolveEditorPaneLayout(double width, double height)
    {
        var plan = ResolveEditorLayoutPlan(width, height);
        return plan.Mode switch
        {
            CanvasFirstPageLayoutMode.CompactStacked => new EditorPaneLayout(
                Mode: plan.Mode,
                WidgetLibraryColumn: 0,
                WidgetLibraryRow: 0,
                WidgetLibraryColumnSpan: 1,
                InspectorColumn: 0,
                InspectorRow: 2,
                InspectorColumnSpan: 1,
                CanvasColumn: 0,
                CanvasRow: 1),
            _ => new EditorPaneLayout(
                Mode: plan.Mode,
                WidgetLibraryColumn: 0,
                WidgetLibraryRow: 0,
                WidgetLibraryColumnSpan: 2,
                InspectorColumn: 0,
                InspectorRow: 1,
                InspectorColumnSpan: 1,
                CanvasColumn: 1,
                CanvasRow: 1),
        };
    }

    private void UpdateAdaptiveLayout(double width, double height)
    {
        var layoutPlan = ResolveEditorLayoutPlan(width, height);
        var paneLayout = ResolveEditorPaneLayout(width, height);
        var compactHeader = width < 900d;
        GalleryHeaderGrid.ColumnDefinitions.Clear();
        GalleryHeaderGrid.RowDefinitions.Clear();
        EditorHeaderGrid.ColumnDefinitions.Clear();
        EditorHeaderGrid.RowDefinitions.Clear();

        if (compactHeader)
        {
            GalleryHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            GalleryHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            GalleryHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(GalleryHeaderActionsPanel, 0);
            Grid.SetRow(GalleryHeaderActionsPanel, 1);
            GalleryHeaderActionsPanel.Orientation = width < 640d ? Orientation.Vertical : Orientation.Horizontal;
            GalleryHeaderActionsPanel.HorizontalAlignment = HorizontalAlignment.Left;

            EditorHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            EditorHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            EditorHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(EditorHeaderActionsPanel, 0);
            Grid.SetRow(EditorHeaderActionsPanel, 1);
            EditorHeaderActionsPanel.Orientation = width < 740d ? Orientation.Vertical : Orientation.Horizontal;
            EditorHeaderActionsPanel.HorizontalAlignment = HorizontalAlignment.Left;
        }
        else
        {
            GalleryHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            GalleryHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            GalleryHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(GalleryHeaderActionsPanel, 1);
            Grid.SetRow(GalleryHeaderActionsPanel, 0);
            GalleryHeaderActionsPanel.Orientation = Orientation.Horizontal;
            GalleryHeaderActionsPanel.HorizontalAlignment = HorizontalAlignment.Right;

            EditorHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            EditorHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            EditorHeaderGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(EditorHeaderActionsPanel, 1);
            Grid.SetRow(EditorHeaderActionsPanel, 0);
            EditorHeaderActionsPanel.Orientation = Orientation.Horizontal;
            EditorHeaderActionsPanel.HorizontalAlignment = HorizontalAlignment.Right;
        }

        TargetDeviceCombo.Width = width < 640d ? double.NaN : 260d;
        var cardWidth = ResolveGalleryCardWidth(width);
        foreach (var state in panelGalleryItems)
        {
            state.CardWidth = cardWidth;
        }

        EditorContentLayout.ColumnDefinitions.Clear();
        EditorContentLayout.RowDefinitions.Clear();
        Grid.SetColumnSpan(CanvasPane, 1);
        Grid.SetColumnSpan(WidgetLibraryPane, 1);
        Grid.SetColumnSpan(InspectorPane, 1);
        WidgetLibraryPane.MaxHeight = double.PositiveInfinity;
        InspectorPane.MaxHeight = double.PositiveInfinity;
        EditorCanvas.MinHeight = layoutPlan.PrimarySurfaceMinHeight;
        EditorCanvas.Height = paneLayout.Mode == CanvasFirstPageLayoutMode.CompactStacked
            ? layoutPlan.PrimarySurfacePreferredHeight
            : double.NaN;
        EditorBodyContentHost.Padding = new Thickness(0, 0, layoutPlan.ScrollViewportRightPadding, 0);
        EditorBodyScrollViewer.VerticalScrollBarVisibility = layoutPlan.UsePageBodyScroll
            ? ScrollBarVisibility.Auto
            : ScrollBarVisibility.Disabled;

        if (paneLayout.Mode == CanvasFirstPageLayoutMode.CompactStacked)
        {
            EditorContentLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetColumn(WidgetLibraryPane, paneLayout.WidgetLibraryColumn);
            Grid.SetRow(WidgetLibraryPane, paneLayout.WidgetLibraryRow);
            Grid.SetColumnSpan(WidgetLibraryPane, paneLayout.WidgetLibraryColumnSpan);
            Grid.SetColumn(CanvasPane, paneLayout.CanvasColumn);
            Grid.SetRow(CanvasPane, paneLayout.CanvasRow);
            Grid.SetColumn(InspectorPane, paneLayout.InspectorColumn);
            Grid.SetRow(InspectorPane, paneLayout.InspectorRow);
            Grid.SetColumnSpan(InspectorPane, paneLayout.InspectorColumnSpan);
            return;
        }

        EditorContentLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DesktopInspectorSidebarWidth) });
        EditorContentLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        Grid.SetColumn(WidgetLibraryPane, paneLayout.WidgetLibraryColumn);
        Grid.SetRow(WidgetLibraryPane, paneLayout.WidgetLibraryRow);
        Grid.SetColumnSpan(WidgetLibraryPane, paneLayout.WidgetLibraryColumnSpan);
        Grid.SetColumn(CanvasPane, paneLayout.CanvasColumn);
        Grid.SetRow(CanvasPane, paneLayout.CanvasRow);
        Grid.SetColumn(InspectorPane, paneLayout.InspectorColumn);
        Grid.SetRow(InspectorPane, paneLayout.InspectorRow);
        Grid.SetColumnSpan(InspectorPane, paneLayout.InspectorColumnSpan);
    }

    private void SetPageMode(PanelsPageMode mode)
    {
        currentMode = mode;
        var galleryVisible = mode == PanelsPageMode.Gallery;
        GalleryHeader.Visibility = galleryVisible ? Visibility.Visible : Visibility.Collapsed;
        GalleryView.Visibility = galleryVisible ? Visibility.Visible : Visibility.Collapsed;
        EditorHeader.Visibility = galleryVisible ? Visibility.Collapsed : Visibility.Visible;
        EditorView.Visibility = galleryVisible ? Visibility.Collapsed : Visibility.Visible;

        if (galleryVisible)
        {
            previewTimer.Stop();
        }
        else if (previewMode == PanelsPreviewMode.OnDemand && previewSession is not null && !previewTimer.IsRunning)
        {
            previewTimer.Start();
        }
    }

    private void MarkDirty(string statusMessage)
    {
        dirty = true;
        SetStatus(statusMessage);
    }

    private void SetStatus(string message, bool isError = false)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        viewModel.StatusText = message;
        StatusTextBlock.Text = isError ? $"Erro: {message}" : message;
    }

    private async Task ReorderSelectedWidgetAsync(bool moveForward)
    {
        if (currentPanel is null || selectedWidget is null)
        {
            return;
        }

        var changed = moveForward
            ? currentPanel.TryMoveWidgetForward(selectedWidget.WidgetId)
            : currentPanel.TryMoveWidgetBackward(selectedWidget.WidgetId);
        if (!changed)
        {
            UpdateWidgetInspector();
            return;
        }

        var widgetId = selectedWidget.WidgetId;
        selectedWidget = currentPanel.Widgets.FirstOrDefault(widget => string.Equals(widget.WidgetId, widgetId, StringComparison.OrdinalIgnoreCase));
        EditorCanvas.Panel = currentPanel;
        EditorCanvas.SelectedWidgetId = selectedWidget?.WidgetId;
        UpdateWidgetInspector();
        MarkDirty(moveForward ? "Widget trazido para frente." : "Widget movido para tras.");
        await RefreshPreviewSessionAsync();
    }

    private int GetNextWidgetZIndex()
    {
        return currentPanel?.Widgets.Count > 0
            ? currentPanel.Widgets.Max(widget => widget.ZIndex) + 1
            : 1;
    }

    private void SelectWidgetById(string? widgetId)
    {
        if (currentPanel is null)
        {
            selectedWidget = null;
            selectedWidgetCatalogItem = null;
            EditorCanvas.SelectedWidgetId = null;
            UpdateWidgetInspector();
            return;
        }

        selectedWidget = currentPanel.Widgets.FirstOrDefault(widget => string.Equals(widget.WidgetId, widgetId, StringComparison.OrdinalIgnoreCase));
        EditorCanvas.SelectedWidgetId = selectedWidget?.WidgetId;
        UpdateWidgetInspector();
    }

    private bool ResolveSelectedSourceType()
    {
        if (modifierEditor.TryGetCurrentRawValue("sourceType", out var raw))
        {
            return string.Equals(raw, "slideshow", StringComparison.OrdinalIgnoreCase);
        }

        return selectedWidget is not null
            && selectedWidget.ConfigValues.TryGetValue("sourceType", out var saved)
            && string.Equals(saved, "slideshow", StringComparison.OrdinalIgnoreCase);
    }

    private void SetBoundsChangedStatus(Hub75PanelWidgetBoundsChangedEventArgs e)
    {
        var action = e.InteractionKind switch
        {
            Hub75PanelWidgetInteractionKind.Move => "Posicao do widget atualizada.",
            Hub75PanelWidgetInteractionKind.ResizeLeft
            or Hub75PanelWidgetInteractionKind.ResizeTop
            or Hub75PanelWidgetInteractionKind.ResizeRight
            or Hub75PanelWidgetInteractionKind.ResizeBottom
            or Hub75PanelWidgetInteractionKind.ResizeTopLeft
            or Hub75PanelWidgetInteractionKind.ResizeTopRight
            or Hub75PanelWidgetInteractionKind.ResizeBottomLeft
            or Hub75PanelWidgetInteractionKind.ResizeBottomRight => "Tamanho do widget atualizado.",
            _ => "Widget atualizado.",
        };

        SetStatus(action);
    }

    private void ApplyWidgetLibraryFilter()
    {
        var query = WidgetLibrarySearchBox?.Text?.Trim() ?? string.Empty;
        filteredWidgetLibraryEntries.Clear();

        IEnumerable<WidgetLibraryEntry> source = widgetLibraryEntries;
        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(entry =>
                entry.Item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Item.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || entry.Item.Summary.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        filteredWidgetLibraryEntries.AddRange(source.OrderBy(entry => entry.Item.Name, StringComparer.CurrentCultureIgnoreCase));
        RebuildWidgetLibrary();
    }

    private static string ResolveWidgetLibraryShortLabel(AppCatalogItem item)
    {
        return item.Id.Trim().ToLowerInvariant() switch
        {
            "analogclock" => "Relogio",
            "gifhub75" => "Foto / GIF",
            _ => string.IsNullOrWhiteSpace(item.Name) ? item.Id : item.Name.Trim(),
        };
    }

    private static string ResolveWidgetLibraryCategoryLabel(AppCatalogItem item)
    {
        return item.Category.Trim().ToLowerInvariant() switch
        {
            "relogio" => "Relogio",
            "relógio" => "Relogio",
            "midia" => "Midia",
            "mídia" => "Midia",
            "clima" => "Clima",
            "finance" => "Financas",
            "financas" => "Financas",
            "finanças" => "Financas",
            "audio" => "Audio",
            "esportes" => "Esportes",
            _ => string.IsNullOrWhiteSpace(item.Category) ? "Widget" : item.Category.Trim(),
        };
    }

    private string BuildWidgetLibrarySummary()
    {
        if (filteredWidgetLibraryEntries.Count == 0)
        {
            return "Nenhum widget encontrado para o filtro atual.";
        }

        var supportedCount = filteredWidgetLibraryEntries.Count(static entry => entry.IsSupported);
        var unavailableCount = filteredWidgetLibraryEntries.Count - supportedCount;
        return unavailableCount == 0
            ? $"{supportedCount} widget(s) disponivel(is) para HUB75."
            : $"{supportedCount} widget(s) disponivel(is), {unavailableCount} indisponivel(is) no compositor HUB75.";
    }

    private async Task<Dictionary<string, string>> ResolveWidgetDefaultValuesAsync(AppCatalogItem item)
    {
        var values = BuildCatalogDefaultWidgetValues(item);
        var draft = await modifierStore.GetDraftAsync(LocalDraftScope, item.Id).ConfigureAwait(true);
        if (draft?.Values is not null)
        {
            foreach (var pair in draft.Values)
            {
                values[pair.Key] = pair.Value;
            }
        }

        WeatherAppFixedLocation.NormalizeRawValuesInPlace(item, values);
        return values;
    }

    private static Dictionary<string, string> BuildCatalogDefaultWidgetValues(AppCatalogItem item)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in item.Modifiers.Where(static modifier => modifier.IsValid()))
        {
            values[modifier.Key] = modifier.Type == AppModifierFieldType.Toggle
                ? (modifier.DefaultToggle is true ? "true" : "false")
                : (modifier.DefaultValue ?? string.Empty);
        }

        return values;
    }

    private static bool NormalizePanel(PanelDefinition panel)
    {
        ArgumentNullException.ThrowIfNull(panel);

        var previousWidth = panel.Width;
        var previousHeight = panel.Height;
        panel.Normalize();
        return panel.Width != previousWidth || panel.Height != previousHeight;
    }

    private static bool TryResolveDraggedCatalogItem(IEnumerable<object> dragItems, out WidgetLibraryEntry entry)
    {
        foreach (var candidate in dragItems)
        {
            switch (candidate)
            {
                case WidgetLibraryEntry widgetEntry when widgetEntry.Item.IsValid():
                    entry = widgetEntry;
                    return true;
                case AppCatalogItem appItem when appItem.IsValid():
                    entry = new WidgetLibraryEntry(appItem, PanelsFrameComposer.SupportsWidgetApp(appItem.Id), null, null);
                    return true;
                case ListViewItem listViewItem when listViewItem.Tag is WidgetLibraryEntry taggedEntry && taggedEntry.Item.IsValid():
                    entry = taggedEntry;
                    return true;
                case WidgetRailCardControl railCard when railCard.Item.IsValid():
                    entry = new WidgetLibraryEntry(
                        railCard.Item,
                        PanelsFrameComposer.SupportsWidgetApp(railCard.Item.Id),
                        null,
                        null);
                    return true;
                case FrameworkElement element when element.Tag is WidgetLibraryEntry taggedEntry && taggedEntry.Item.IsValid():
                    entry = taggedEntry;
                    return true;
                case AppCatalogCardControl card when card.Item is { } cardItem && cardItem.IsValid():
                    entry = new WidgetLibraryEntry(cardItem, PanelsFrameComposer.SupportsWidgetApp(cardItem.Id), null, null);
                    return true;
            }
        }

        entry = null!;
        return false;
    }

    internal static bool TryResolveDraggedWidgetAppId(object? propertyValue, string? fallbackText, out string appId)
    {
        if (TryNormalizeAppId(propertyValue as string, out appId))
        {
            return true;
        }

        return TryNormalizeAppId(fallbackText, out appId);
    }

    private static bool CanResolveDraggedWidgetAppId(DataPackageView dataView)
    {
        if (dataView is null)
        {
            return false;
        }

        if (dataView.Properties.TryGetValue(DraggedWidgetAppIdKey, out var propertyValue)
            && TryResolveDraggedWidgetAppId(propertyValue, null, out _))
        {
            return true;
        }

        return dataView.Contains(StandardDataFormats.Text);
    }

    private static async Task<string?> TryResolveDraggedWidgetAppIdAsync(DataPackageView dataView)
    {
        var fallbackText = dataView.Contains(StandardDataFormats.Text)
            ? await dataView.GetTextAsync()
            : null;
        return dataView.Properties.TryGetValue(DraggedWidgetAppIdKey, out var propertyValue)
            && TryResolveDraggedWidgetAppId(propertyValue, fallbackText, out var appId)
            ? appId
            : (TryResolveDraggedWidgetAppId(null, fallbackText, out appId) ? appId : null);
    }

    private static bool TryNormalizeAppId(string? candidate, out string appId)
    {
        appId = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        appId = candidate.Trim();
        return appId.Length > 0;
    }

    private static PanelDefinition CreateDefaultPanel(string name)
    {
        return new PanelDefinition
        {
            PanelId = Guid.NewGuid().ToString("N"),
            Name = name,
            Width = LedDefaults.MatrixWidth,
            Height = LedDefaults.MatrixHeight,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private static async Task<StorageFile?> PickImageFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        foreach (var extension in new[] { ".gif", ".png", ".jpg", ".jpeg", ".bmp" })
        {
            picker.FileTypeFilter.Add(extension);
        }

        if (App.MainWindow is not null)
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return await picker.PickSingleFileAsync();
    }

    private static async Task<StorageFolder?> PickImageFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };
        picker.FileTypeFilter.Add("*");

        if (App.MainWindow is not null)
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return await picker.PickSingleFolderAsync();
    }

    private async Task SetPanelActivationAsync(string panelId, bool activate)
    {
        if (activate)
        {
            if (!TryGetPanel(panelId, out var panel))
            {
                UpdateGalleryCardStates();
                return;
            }

            await LoadPanelAsync(panel);
            return;
        }

        // Check local loop first; fall back to server-only stop when needed.
        var localActive = playbackService.GetActivePanelSnapshot();
        var isLocallyActive = localActive is not null
            && string.Equals(localActive.PanelId, panelId, StringComparison.OrdinalIgnoreCase);

        if (isLocallyActive)
        {
            await StopPlaybackAsync();      // stops local loop + deletes from server
        }
        else if (playbackService.IsServerActivePanel(panelId))
        {
            await playbackService.DeleteServerPanelForPanel(panelId);
            UpdateGalleryCardStates();
            SetStatus("Painel desativado no servidor.");
        }
        else
        {
            UpdateGalleryCardStates();
        }
    }

    private string? GetSelectedDeviceId()
    {
        return (TargetDeviceCombo.SelectedItem as ComboBoxItem)?.Tag as string;
    }

    private bool IsActivePanel(string panelId)
    {
        var activePanel = playbackService.GetActivePanelSnapshot();
        if (activePanel is not null
            && string.Equals(activePanel.PanelId, panelId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Server is the source of truth: even if THIS WinUI session has not
        // started a local playback loop, treat panels rendered autonomously
        // by the server as "active" so the gallery shows the correct state
        // when the user reopens the app.
        return playbackService.IsServerActivePanel(panelId);
    }

    private bool IsRetainedPanel(string panelId)
    {
        if (IsActivePanel(panelId))
        {
            return true;
        }

        var suspendedPanel = playbackService.GetSuspendedPanelSnapshot();
        return suspendedPanel is not null
            && string.Equals(suspendedPanel.PanelId, panelId, StringComparison.OrdinalIgnoreCase);
    }

    private bool EnsurePanelActivationAllowed()
    {
        if (!IsVisualizerHub75PriorityActive())
        {
            return true;
        }

        SetStatus("Desative o visualizador HUB75 antes de ativar um painel.", isError: true);
        return false;
    }

    private bool IsVisualizerHub75PriorityActive()
        => hub75VisualizerSessionService.IsHub75Enabled;

    private bool TryGetPanel(string panelId, out PanelDefinition panel)
    {
        panel = storeDocument.Panels.FirstOrDefault(candidate => string.Equals(candidate.PanelId, panelId, StringComparison.OrdinalIgnoreCase))!;
        return panel is not null;
    }

    private static double ResolveGalleryCardWidth(double width)
        => width < 760d ? Math.Max(260d, width - 56d) : 332d;

    private enum PanelsPageMode
    {
        Gallery,
        Editor,
    }

    private enum PanelsPreviewMode
    {
        Off,
        OnDemand,
    }

    internal enum PanelPosterStatus
    {
        Missing,
        Loading,
        Ready,
        Error,
    }

    internal sealed class PanelGalleryItemState : INotifyPropertyChanged
    {
        private string name;
        private int widgetCount;
        private string subtitle;
        private bool isActive;
        private bool canActivate;
        private PanelPosterStatus posterStatus;
        private RgbaColor[] frame;
        private double cardWidth;

        public PanelGalleryItemState(
            string panelId,
            string name,
            int widgetCount,
            double cardWidth,
            RgbaColor[] frame,
            Func<string, Task> openEditorAsync,
            Func<string, Task> duplicateAsync,
            Func<string, Task> deleteAsync,
            Func<string, bool, Task> setActiveAsync)
        {
            PanelId = panelId;
            this.name = name;
            this.widgetCount = widgetCount;
            subtitle = $"{widgetCount} widget(s)";
            this.cardWidth = cardWidth;
            this.frame = frame;
            OpenEditorAsync = openEditorAsync;
            DuplicateAsync = duplicateAsync;
            DeleteAsync = deleteAsync;
            SetActiveAsync = setActiveAsync;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string PanelId { get; }

        public Func<string, Task> OpenEditorAsync { get; }

        public Func<string, Task> DuplicateAsync { get; }

        public Func<string, Task> DeleteAsync { get; }

        public Func<string, bool, Task> SetActiveAsync { get; }

        public string Name
        {
            get => name;
            set => SetField(ref name, value);
        }

        public int WidgetCount
        {
            get => widgetCount;
            set => SetField(ref widgetCount, value);
        }

        public string Subtitle
        {
            get => subtitle;
            set => SetField(ref subtitle, value);
        }

        public bool IsActive
        {
            get => isActive;
            set => SetField(ref isActive, value);
        }

        public bool CanActivate
        {
            get => canActivate;
            set => SetField(ref canActivate, value);
        }

        internal PanelPosterStatus PosterStatus
        {
            get => posterStatus;
            set => SetField(ref posterStatus, value, propertyName: nameof(PosterStatus));
        }

        public RgbaColor[] Frame
        {
            get => frame;
            set => SetField(ref frame, value);
        }

        public double CardWidth
        {
            get => cardWidth;
            set => SetField(ref cardWidth, value);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private readonly record struct EditorPaneLayout(
        CanvasFirstPageLayoutMode Mode,
        int WidgetLibraryColumn,
        int WidgetLibraryRow,
        int WidgetLibraryColumnSpan,
        int InspectorColumn,
        int InspectorRow,
        int InspectorColumnSpan,
        int CanvasColumn,
        int CanvasRow);

    private sealed record WidgetLibraryEntry(
        AppCatalogItem Item,
        bool IsSupported,
        string? BadgeLabel,
        IReadOnlyDictionary<string, string>? PreviewValues);

}
