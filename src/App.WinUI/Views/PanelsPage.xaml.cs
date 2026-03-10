using App.WinUI.Models.Apps;
using App.WinUI.Models.Panels;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Panels;
using App.WinUI.ViewModels;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/paineis.md#galeria-de-paineis
public sealed partial class PanelsPage : Page, IDisposable
{
    private static readonly RgbaColor[] EmptyFrame = Enumerable.Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray();
    private const string DraggedWidgetAppIdKey = "panelWidgetAppId";
    private const int DefaultNewWidgetWidth = 64;
    private const int DefaultNewWidgetHeight = 32;

    private readonly PanelsPageViewModel viewModel;
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly IAppCatalogService catalogService;
    private readonly PanelsStore panelsStore;
    private readonly PanelsFrameComposer frameComposer;
    private readonly PanelsPlaybackService playbackService;
    private readonly AppModifierEditorHost modifierEditor;

    private readonly List<AppCatalogItem> catalogItems = [];
    private readonly Dictionary<string, AppCatalogItem> catalogById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PanelCardVisualState> panelCards = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RgbaColor[]> panelThumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherQueueTimer previewTimer;

    private DeviceOperationsState currentState = new();
    private PanelsStoreDocument storeDocument = new();
    private PanelsFrameComposer.PanelCompositionSession? previewSession;
    private PanelDefinition? currentPanel;
    private PanelWidgetDefinition? selectedWidget;
    private AppCatalogItem? selectedWidgetCatalogItem;
    private PanelsPageMode currentMode = PanelsPageMode.Gallery;
    private string? animatedPanelId;
    private bool dirty;
    private bool updatingInspector;

    internal PanelsPage(
        PanelsPageViewModel viewModel,
        DeviceOperationsCoordinator deviceOps,
        IAppCatalogService catalogService,
        PanelsStore panelsStore,
        PanelsFrameComposer frameComposer,
        PanelsPlaybackService playbackService,
        CityAutocompleteService cityService)
    {
        this.viewModel = viewModel;
        this.deviceOps = deviceOps;
        this.catalogService = catalogService;
        this.panelsStore = panelsStore;
        this.frameComposer = frameComposer;
        this.playbackService = playbackService;
        modifierEditor = new AppModifierEditorHost(cityService, message => SetStatus(message, isError: true));
        modifierEditor.ModifierChanged += OnWidgetModifierChanged;

        previewTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
        previewTimer.Interval = TimeSpan.FromMilliseconds(1000d / PanelsFrameComposer.TargetFps);
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
        deviceOps.StateChanged += OnDeviceOpsStateChanged;
        playbackService.StateChanged += OnPlaybackStateChanged;
        playbackService.FrameUpdated += OnPlaybackFrameUpdated;

        currentState = deviceOps.GetStateSnapshot();
        ApplyDevices(currentState.DeviceListSnapshot);
        SetPageMode(PanelsPageMode.Gallery);
        UpdateAdaptiveLayout(ActualWidth);

        await LoadCatalogAsync();
        await LoadPanelsAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        previewTimer.Stop();
        deviceOps.StateChanged -= OnDeviceOpsStateChanged;
        playbackService.StateChanged -= OnPlaybackStateChanged;
        playbackService.FrameUpdated -= OnPlaybackFrameUpdated;
        DisposePreviewSession();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAdaptiveLayout(e.NewSize.Width);
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
        modifierEditor.Dispose();
        DisposePreviewSession();
        EditorCanvas.Dispose();
    }

    private async Task LoadCatalogAsync()
    {
        var catalog = await catalogService.LoadCatalogAsync();
        catalogItems.Clear();
        catalogItems.AddRange(catalog.OrderBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase));
        catalogById.Clear();
        foreach (var item in catalogItems)
        {
            if (item.IsValid())
            {
                catalogById[item.Id] = item;
            }
        }

        RebuildWidgetLibrary();
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

        storeDocument = loadedDocument;
        var initialPanelId = storeDocument.LastSelectedPanelId ?? storeDocument.Panels.First().PanelId;
        await SelectPanelAsync(initialPanelId, saveDirty: false, refreshPreview: false);
        await RegenerateAllThumbnailsAsync();
        RebuildPanelsGallery();
        UpdateGalleryCardStates();
        ClearEditorPreview();
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
        await RegenerateThumbnailAsync(panel, force: true);
        RebuildPanelsGallery();
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
        await RegenerateThumbnailAsync(duplicate, force: true);
        RebuildPanelsGallery();

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

        if (IsActivePanel(panelId))
        {
            await playbackService.StopAsync();
        }

        storeDocument.Panels.RemoveAll(panel => string.Equals(panel.PanelId, panelId, StringComparison.OrdinalIgnoreCase));
        panelThumbnailCache.Remove(panelId);

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
        await RegenerateAllThumbnailsAsync();

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

        dirty = false;
        await RegenerateThumbnailAsync(currentPanel, force: true);
        RebuildPanelsGallery();
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
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            currentState = state;
            ApplyDevices(state.DeviceListSnapshot);
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
        var liveFrame = frame.ToArray();
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            panelThumbnailCache[panelId] = liveFrame.ToArray();
            if (panelCards.TryGetValue(panelId, out var card))
            {
                card.Thumbnail.Frame = liveFrame;
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

    private void OnWidgetLibraryDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (!TryResolveDraggedCatalogItem(e.Items, out var catalogItem))
        {
            return;
        }

        e.Data.Properties[DraggedWidgetAppIdKey] = catalogItem.Id;
        e.Data.SetText(catalogItem.Id);
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
        if (string.IsNullOrWhiteSpace(appId) || !catalogById.TryGetValue(appId, out var item))
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
            ConfigValues = BuildDefaultWidgetValues(item),
        };
        widget.Normalize(currentPanel.Width, currentPanel.Height);
        currentPanel.Widgets.Add(widget);
        currentPanel.Normalize();
        selectedWidget = currentPanel.Widgets.FirstOrDefault(entry => string.Equals(entry.WidgetId, widget.WidgetId, StringComparison.OrdinalIgnoreCase));
        BringSelectedWidgetToFront();
        EditorCanvas.Panel = currentPanel;
        EditorCanvas.SelectedWidgetId = selectedWidget?.WidgetId;
        UpdateWidgetInspector();
        MarkDirty($"Widget '{item.Name}' adicionado.");
        await RefreshPreviewSessionAsync();
    }

    private async void OnEditorWidgetSelected(object? sender, string? widgetId)
    {
        if (currentPanel is null)
        {
            return;
        }

        selectedWidget = currentPanel.Widgets.FirstOrDefault(widget => string.Equals(widget.WidgetId, widgetId, StringComparison.OrdinalIgnoreCase));
        EditorCanvas.SelectedWidgetId = selectedWidget?.WidgetId;
        var changedZ = BringSelectedWidgetToFront();
        UpdateWidgetInspector();
        if (changedZ)
        {
            MarkDirty("Widget selecionado movido para o topo.");
            await RefreshPreviewSessionAsync();
        }
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
        await RefreshPreviewSessionAsync();
    }

    private void OnPreviewTimerTick(DispatcherQueueTimer sender, object args)
    {
        if (currentMode != PanelsPageMode.Editor || previewSession is null)
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
            await RegenerateThumbnailAsync(currentPanel, force: true);
            RebuildPanelsGallery();
        }

        SetPageMode(PanelsPageMode.Gallery);
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
            previewSession = await frameComposer.CreateSessionAsync(currentPanel.Clone());
            EditorCanvas.Frame = previewSession.RenderFrame(DateTimeOffset.UtcNow);
            EditorCanvas.WidgetErrors = previewSession.GetWidgetErrors();
            if (!previewTimer.IsRunning)
            {
                previewTimer.Start();
            }
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
        EditorCanvas.Frame = EmptyFrame.ToArray();
        EditorCanvas.WidgetErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    private async Task RegenerateAllThumbnailsAsync()
    {
        panelThumbnailCache.Clear();
        foreach (var panel in storeDocument.Panels)
        {
            await RegenerateThumbnailAsync(panel, force: true);
        }
    }

    private async Task RegenerateThumbnailAsync(PanelDefinition panel, bool force)
    {
        if (!force && panelThumbnailCache.ContainsKey(panel.PanelId))
        {
            return;
        }

        try
        {
            using var session = await frameComposer.CreateSessionAsync(panel.Clone());
            panelThumbnailCache[panel.PanelId] = session.RenderFrame(DateTimeOffset.UtcNow);
        }
        catch
        {
            panelThumbnailCache[panel.PanelId] = EmptyFrame.ToArray();
        }
    }

    private void RebuildPanelsGallery()
    {
        PanelsGalleryGrid.Items.Clear();
        panelCards.Clear();

        foreach (var panel in storeDocument.Panels.OrderBy(panel => panel.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var item = BuildPanelCard(panel);
            PanelsGalleryGrid.Items.Add(item);
        }

        UpdateAdaptiveLayout(ActualWidth);
    }

    private GridViewItem BuildPanelCard(PanelDefinition panel)
    {
        var panelId = panel.PanelId;
        var previewButton = new Button
        {
            Padding = new Thickness(0),
            Background = null,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Tag = panelId,
            MinHeight = 190,
        };
        previewButton.Click += OnPanelPreviewClicked;

        var thumbnail = new Hub75PanelThumbnailControl
        {
            Margin = new Thickness(16),
            Height = 180,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Frame = panelThumbnailCache.TryGetValue(panelId, out var frame) ? frame : EmptyFrame.ToArray(),
        };
        previewButton.Content = thumbnail;

        var previewHost = new Grid();
        previewHost.Children.Add(previewButton);

        var activeBadge = new Border
        {
            Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(999),
            Background = UiResourceResolver.ResolveBrush("SystemAccentColor", Windows.UI.Color.FromArgb(255, 0, 120, 212)),
            Child = new TextBlock
            {
                Text = "ATIVO",
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            },
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12),
            Visibility = Visibility.Collapsed,
        };
        previewHost.Children.Add(activeBadge);

        var menuButton = new Button
        {
            Content = new SymbolIcon(Symbol.More),
            Padding = new Thickness(8),
            Width = 36,
            Height = 36,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12),
            Tag = panelId,
        };
        var menuFlyout = new MenuFlyout();
        var editItem = new MenuFlyoutItem
        {
            Text = "Editar",
            Icon = new SymbolIcon(Symbol.Edit),
            Tag = panelId,
        };
        editItem.Click += OnPanelEditMenuClicked;
        var duplicateItem = new MenuFlyoutItem
        {
            Text = "Duplicar",
            Icon = new SymbolIcon(Symbol.Copy),
            Tag = panelId,
        };
        duplicateItem.Click += OnPanelDuplicateMenuClicked;
        var deleteItem = new MenuFlyoutItem
        {
            Text = "Excluir",
            Icon = new SymbolIcon(Symbol.Delete),
            Tag = panelId,
        };
        deleteItem.Click += OnPanelDeleteMenuClicked;
        menuFlyout.Items.Add(editItem);
        menuFlyout.Items.Add(duplicateItem);
        menuFlyout.Items.Add(deleteItem);
        menuButton.Flyout = menuFlyout;
        previewHost.Children.Add(menuButton);

        var infoButton = new Button
        {
            Padding = new Thickness(0),
            Background = null,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Tag = panelId,
        };
        infoButton.Click += OnPanelPreviewClicked;

        var titleText = new TextBlock
        {
            Text = panel.Name,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.WrapWholeWords,
            MaxLines = 2,
        };
        var subtitleText = new TextBlock
        {
            Text = $"{panel.Widgets.Count} widget(s)",
            Opacity = 0.72,
            TextWrapping = TextWrapping.WrapWholeWords,
        };
        infoButton.Content = new StackPanel
        {
            Spacing = 2,
            Children =
            {
                titleText,
                subtitleText,
            },
        };

        var toggle = new ToggleSwitch
        {
            OffContent = string.Empty,
            OnContent = string.Empty,
            Tag = panelId,
        };
        toggle.Toggled += OnPanelToggleToggled;

        var footerActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock
                {
                    Text = "Ativo",
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.86,
                },
                toggle,
            },
        };

        var footerGrid = new Grid
        {
            Padding = new Thickness(16, 14, 16, 14),
            ColumnSpacing = 16,
        };
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footerGrid.Children.Add(infoButton);
        Grid.SetColumn(footerActions, 1);
        footerGrid.Children.Add(footerActions);

        var cardGrid = new Grid();
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        cardGrid.Children.Add(previewHost);
        Grid.SetRow(footerGrid, 1);
        cardGrid.Children.Add(footerGrid);

        var cardBorder = CreateCard(cardGrid, padding: 0, elevated: true);
        cardBorder.Width = 320;

        var item = new GridViewItem
        {
            Content = cardBorder,
            Tag = panelId,
            Padding = new Thickness(6),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };

        panelCards[panelId] = new PanelCardVisualState(item, cardBorder, thumbnail, toggle, subtitleText, activeBadge, titleText);
        return item;
    }

    private void RebuildWidgetLibrary()
    {
        WidgetLibraryList.Items.Clear();
        foreach (var item in catalogItems)
        {
            var card = new AppCatalogCardControl(item);
            card.SetPreviewPlayback(true);
            var listItem = new ListViewItem
            {
                Tag = item,
                Content = card,
            };
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

        if (!string.Equals(animatedPanelId, activePanelId, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(animatedPanelId)
            && panelCards.TryGetValue(animatedPanelId, out var previousCard)
            && panelThumbnailCache.TryGetValue(animatedPanelId, out var previousPoster))
        {
            previousCard.Thumbnail.Frame = previousPoster;
        }

        animatedPanelId = activePanelId;
        foreach (var panel in storeDocument.Panels)
        {
            if (!panelCards.TryGetValue(panel.PanelId, out var card))
            {
                continue;
            }

            var isActive = string.Equals(panel.PanelId, activePanelId, StringComparison.OrdinalIgnoreCase);
            var canActivate = isActive || !string.IsNullOrWhiteSpace(selectedDeviceId);
            card.SuppressToggle = true;
            card.ActiveToggle.IsOn = isActive;
            card.ActiveToggle.IsEnabled = canActivate;
            card.SuppressToggle = false;

            card.CardBorder.BorderThickness = isActive ? new Thickness(2) : new Thickness(1);
            card.CardBorder.BorderBrush = isActive
                ? UiResourceResolver.ResolveBrush("SystemAccentColor", Windows.UI.Color.FromArgb(255, 0, 120, 212))
                : UiResourceResolver.ResolveBrush("AppSurfaceStrokeBrush", Windows.UI.Color.FromArgb(255, 55, 68, 86));
            card.ActiveBadge.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
            card.TitleText.Text = panel.Name;
            card.SubtitleText.Text = isActive
                ? $"Ativo em {activeDeviceId}"
                : $"{panel.Widgets.Count} widget(s)";

            if (isActive)
            {
                card.Thumbnail.Frame = playbackService.GetLatestFrame();
            }
            else if (panelThumbnailCache.TryGetValue(panel.PanelId, out var poster))
            {
                card.Thumbnail.Frame = poster;
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

        GifSourceButton.Content = ResolveSelectedSourceType() ? "Selecionar pasta" : "Selecionar arquivo";
        GifSourcePathText.Text = selectedWidget!.RuntimeState.TryGetValue("sourcePath", out var path)
            ? path
            : string.Empty;
    }

    private void UpdateEditorHeader()
    {
        var nextName = currentPanel?.Name ?? string.Empty;
        if (!string.Equals(EditorNameText.Text, nextName, StringComparison.Ordinal))
        {
            EditorNameText.Text = nextName;
        }
    }

    private void UpdateAdaptiveLayout(double width)
    {
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
        foreach (var card in panelCards.Values)
        {
            card.Item.Width = width < 760d ? Math.Max(260d, width - 56d) : 332d;
        }

        EditorContentLayout.ColumnDefinitions.Clear();
        EditorContentLayout.RowDefinitions.Clear();

        if (width < 920d)
        {
            EditorContentLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetColumn(CanvasPane, 0);
            Grid.SetRow(CanvasPane, 0);
            Grid.SetColumn(InspectorPane, 0);
            Grid.SetRow(InspectorPane, 1);
            Grid.SetColumn(WidgetLibraryPane, 0);
            Grid.SetRow(WidgetLibraryPane, 2);
            return;
        }

        if (width < 1380d)
        {
            EditorContentLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            EditorContentLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Grid.SetColumn(CanvasPane, 0);
            Grid.SetColumnSpan(CanvasPane, 2);
            Grid.SetRow(CanvasPane, 0);
            Grid.SetColumn(WidgetLibraryPane, 0);
            Grid.SetColumnSpan(WidgetLibraryPane, 1);
            Grid.SetRow(WidgetLibraryPane, 1);
            Grid.SetColumn(InspectorPane, 1);
            Grid.SetColumnSpan(InspectorPane, 1);
            Grid.SetRow(InspectorPane, 1);
            return;
        }

        EditorContentLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
        EditorContentLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
        EditorContentLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        EditorContentLayout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        Grid.SetColumn(WidgetLibraryPane, 0);
        Grid.SetRow(WidgetLibraryPane, 0);
        Grid.SetColumn(CanvasPane, 1);
        Grid.SetRow(CanvasPane, 0);
        Grid.SetColumn(InspectorPane, 2);
        Grid.SetRow(InspectorPane, 0);
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
        else if (previewSession is not null && !previewTimer.IsRunning)
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

    private bool BringSelectedWidgetToFront()
    {
        if (currentPanel is null || selectedWidget is null)
        {
            return false;
        }

        var maxZ = currentPanel.Widgets.Count == 0 ? 0 : currentPanel.Widgets.Max(widget => widget.ZIndex);
        if (selectedWidget.ZIndex >= maxZ)
        {
            return false;
        }

        var widgetId = selectedWidget.WidgetId;
        selectedWidget.ZIndex = maxZ + 1;
        currentPanel.Normalize();
        selectedWidget = currentPanel.Widgets.FirstOrDefault(widget => string.Equals(widget.WidgetId, widgetId, StringComparison.OrdinalIgnoreCase));
        EditorCanvas.Panel = currentPanel;
        return true;
    }

    private int GetNextWidgetZIndex()
    {
        return currentPanel?.Widgets.Count > 0
            ? currentPanel.Widgets.Max(widget => widget.ZIndex) + 1
            : 1;
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

    private static Dictionary<string, string> BuildDefaultWidgetValues(AppCatalogItem item)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in item.Modifiers.Where(static modifier => modifier.IsValid()))
        {
            values[modifier.Key] = modifier.Type == AppModifierFieldType.Toggle
                ? (modifier.DefaultToggle is true ? "true" : "false")
                : (modifier.DefaultValue ?? string.Empty);
        }

        WeatherAppFixedLocation.NormalizeRawValuesInPlace(item, values);
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

    private static bool TryResolveDraggedCatalogItem(IEnumerable<object> dragItems, out AppCatalogItem catalogItem)
    {
        foreach (var candidate in dragItems)
        {
            switch (candidate)
            {
                case AppCatalogItem appItem when appItem.IsValid():
                    catalogItem = appItem;
                    return true;
                case ListViewItem listViewItem when listViewItem.Tag is AppCatalogItem taggedItem && taggedItem.IsValid():
                    catalogItem = taggedItem;
                    return true;
                case AppCatalogCardControl card when card.Item is { } cardItem && cardItem.IsValid():
                    catalogItem = cardItem;
                    return true;
            }
        }

        catalogItem = null!;
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

    private void OnPanelPreviewClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string panelId)
        {
            _ = OpenEditorAsync(panelId, saveDirty: currentMode == PanelsPageMode.Editor);
        }
    }

    private async void OnPanelEditMenuClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string panelId)
        {
            await OpenEditorAsync(panelId, saveDirty: currentMode == PanelsPageMode.Editor);
        }
    }

    private async void OnPanelDuplicateMenuClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string panelId)
        {
            await DuplicatePanelAsync(panelId, openEditor: true);
        }
    }

    private async void OnPanelDeleteMenuClicked(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string panelId)
        {
            await DeletePanelAsync(panelId, returnToGallery: true);
        }
    }

    private async void OnPanelToggleToggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggle
            || toggle.Tag is not string panelId
            || !panelCards.TryGetValue(panelId, out var card)
            || card.SuppressToggle)
        {
            return;
        }

        if (toggle.IsOn)
        {
            if (!TryGetPanel(panelId, out var panel))
            {
                UpdateGalleryCardStates();
                return;
            }

            await LoadPanelAsync(panel);
            return;
        }

        if (IsActivePanel(panelId))
        {
            await StopPlaybackAsync();
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
        return activePanel is not null
            && string.Equals(activePanel.PanelId, panelId, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetPanel(string panelId, out PanelDefinition panel)
    {
        panel = storeDocument.Panels.FirstOrDefault(candidate => string.Equals(candidate.PanelId, panelId, StringComparison.OrdinalIgnoreCase))!;
        return panel is not null;
    }

    private enum PanelsPageMode
    {
        Gallery,
        Editor,
    }

    private sealed class PanelCardVisualState(
        GridViewItem item,
        Border cardBorder,
        Hub75PanelThumbnailControl thumbnail,
        ToggleSwitch activeToggle,
        TextBlock subtitleText,
        Border activeBadge,
        TextBlock titleText)
    {
        public GridViewItem Item { get; } = item;

        public Border CardBorder { get; } = cardBorder;

        public Hub75PanelThumbnailControl Thumbnail { get; } = thumbnail;

        public ToggleSwitch ActiveToggle { get; } = activeToggle;

        public TextBlock SubtitleText { get; } = subtitleText;

        public Border ActiveBadge { get; } = activeBadge;

        public TextBlock TitleText { get; } = titleText;

        public bool SuppressToggle { get; set; }
    }
}
