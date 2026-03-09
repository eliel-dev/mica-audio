using App.WinUI.Models.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.WinUI.Views;

public sealed partial class AppsPage
{
    // DOCS: docs/wiki/guides/add-app-catalog-item.md#passos
    private async Task LoadCatalogAsync()
    {
        var service = catalogService;

        try
        {
            var catalog = await service.LoadCatalogAsync().ConfigureAwait(false);
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                allItems.Clear();
                allItems.AddRange(catalog);
                ApplyFilter();
                AppendLog($"Catálogo carregado: {allItems.Count} apps.");
            });
        }
        catch (Exception ex)
        {
            _ = DispatcherQueue.TryEnqueue(() => AppendLog($"Falha ao carregar catálogo: {ex.Message}"));
        }
    }

    private void OnDeviceOpsStateChanged(object? sender, EventArgs e)
    {
        var state = deviceOps.GetStateSnapshot();
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            currentState = state;
            ApplyState(state);
            ApplyDevices(state.DeviceListSnapshot);
        });
    }

    private void ApplyState(DeviceOperationsState state)
    {
        viewModel.OperationInProgress = state.CommandInProgress;
        viewModel.OperationStatus = state.CommandStatus;
        viewModel.OperationPercent = Math.Clamp(state.CommandPercent, 0, 100);

        var msg = viewModel.OperationInProgress && viewModel.OperationPercent > 0
            ? $"{viewModel.OperationStatus} ({viewModel.OperationPercent}%)"
            : viewModel.OperationStatus;
        ShowOperationNotification(msg, viewModel.OperationInProgress);
        UpdateGifOpenFileButtonVisibility();
        UpdateActionButtonsEnabled();
    }

    private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)
    {
        var currentSelection = TargetDeviceCombo.SelectedItem as ComboBoxItem;
        var selectedId = currentSelection?.Tag as string;

        TargetDeviceCombo.Items.Clear();
        foreach (var device in devices.Where(static d => d.Status == DeviceStatus.Online))
        {
            TargetDeviceCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{device.Name} ({device.DeviceId})",
                Tag = device.DeviceId,
            });
        }

        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            foreach (var item in TargetDeviceCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag as string, selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    TargetDeviceCombo.SelectedItem = item;
                    break;
                }
            }
        }

        UpdateActionButtonsEnabled();
        _ = RefreshPreviewDraftsAsync();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        viewModel.SearchText = SearchBox.Text?.Trim() ?? string.Empty;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        filteredItems.Clear();

        IEnumerable<AppCatalogItem> source = allItems;
        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(item =>
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Summary.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        filteredItems.AddRange(source);
        RebuildCatalogCards();
    }

    private void RebuildCatalogCards()
    {
        foreach (var card in catalogCards)
        {
            card.SetPreviewPlayback(false);
        }

        catalogCards.Clear();
        activePreviewCards.Clear();
        CatalogGrid.Items.Clear();

        foreach (var item in filteredItems)
        {
            var card = new AppCatalogCardControl(item);
            catalogCards.Add(card);
            CatalogGrid.Items.Add(card);
        }

        EnsureCatalogScrollViewer();
        ApplyGifRuntimeFrameToCards(latestGifRuntimeFrame);

        if (selectedItem is not null)
        {
            var card = catalogCards.FirstOrDefault(c => string.Equals(c.Item.Id, selectedItem.Id, StringComparison.OrdinalIgnoreCase));
            if (card is not null)
            {
                CatalogGrid.SelectedItem = card;
                SetSelectedItem(card.Item);
            }
            else if (filteredItems.Count == 0)
            {
                ClearSelectedItem();
            }
        }
        else if (catalogCards.Count > 0)
        {
            CatalogGrid.SelectedItem = catalogCards[0];
            SetSelectedItem(catalogCards[0].Item);
        }
        else
        {
            ClearSelectedItem();
        }

        UpdateSelectionVisuals();
        RefreshPreviewPlayback();
        _ = RefreshPreviewDraftsAsync();
    }

    private void OnCatalogItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not AppCatalogCardControl card)
        {
            return;
        }

        pendingRuntimeAutostart = runtimeProviderRegistry.Resolve(card.Item) is not null;
        if (ReferenceEquals(CatalogGrid.SelectedItem, card))
        {
            SetSelectedItem(card.Item);
            return;
        }

        CatalogGrid.SelectedItem = card;
    }

    public async Task ReloadCatalogFromDiskAsync()
    {
        if (catalogReloadInProgress)
        {
            return;
        }

        catalogReloadInProgress = true;
        try
        {
            var catalog = await catalogService.ReloadCatalogAsync().ConfigureAwait(false);
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                allItems.Clear();
                allItems.AddRange(catalog);
                ApplyFilter();
                AppendLog($"Catalogo recarregado do disco: {allItems.Count} apps.");
            });
        }
        finally
        {
            catalogReloadInProgress = false;
        }
    }

    private void OnCatalogSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CatalogGrid.SelectedItem is AppCatalogCardControl card)
        {
            SetSelectedItem(card.Item);
            return;
        }

        if (selectedItem is not null)
        {
            UpdateSelectionVisuals();
        }
    }

    private void SetSelectedItem(AppCatalogItem item)
    {
        var previousItem = selectedItem;
        var previousProvider = activeRuntimeProvider;
        selectedItem = item;
        activeRuntimeProvider = runtimeProviderRegistry.Resolve(item);

        viewModel.SelectedAppName = item.Name;
        viewModel.SelectedAppMeta = $"{item.Category} | Intervalo recomendado: {item.RecommendedIntervalMinutes} min";
        viewModel.SelectedAppDescription = item.Description;

        SelectedAppNameText.Text = viewModel.SelectedAppName;
        SelectedAppMetaText.Text = viewModel.SelectedAppMeta;
        SelectedAppDescriptionText.Text = viewModel.SelectedAppDescription;

        _ = LoadModifierEditorAsync();

        if (previousProvider is not null && !ReferenceEquals(previousProvider, activeRuntimeProvider))
        {
            previousProvider.OnDeselected(previousItem ?? new AppCatalogItem());
        }

        _ = EvaluateRuntimeAutostartAsync();

        UpdateSelectionVisuals();
        RefreshPreviewPlayback();
        UpdateGifOpenFileButtonVisibility();
        UpdateActionButtonsEnabled();
    }

    private void ClearSelectedItem()
    {
        var previousItem = selectedItem;
        var previousProvider = activeRuntimeProvider;
        selectedItem = null;
        activeRuntimeProvider = null;

        viewModel.SelectedAppName = "Selecione um app";
        viewModel.SelectedAppMeta = "-";
        viewModel.SelectedAppDescription = "Nenhum app selecionado.";

        SelectedAppNameText.Text = viewModel.SelectedAppName;
        SelectedAppMetaText.Text = viewModel.SelectedAppMeta;
        SelectedAppDescriptionText.Text = viewModel.SelectedAppDescription;
        ModifiersHintText.Text = "Selecione um app e um dispositivo para editar modificadores.";
        ModifiersPanel.Children.Clear();
        modifierBindings.Clear();

        previousProvider?.OnDeselected(previousItem ?? new AppCatalogItem());

        UpdateSelectionVisuals();
        RefreshPreviewPlayback();
        UpdateGifOpenFileButtonVisibility();
        UpdateActionButtonsEnabled();
    }

    private void UpdateSelectionVisuals()
    {
        var selectedId = selectedItem?.Id;
        foreach (var card in catalogCards)
        {
            card.SetSelectedVisual(string.Equals(card.Item.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void EnsureCatalogScrollViewer()
    {
        if (catalogScrollViewer is not null)
        {
            return;
        }

        catalogScrollViewer = FindDescendant<ScrollViewer>(CatalogGrid);
        if (catalogScrollViewer is null)
        {
            return;
        }

        catalogScrollViewer.ViewChanged += OnCatalogScrollViewChanged;
    }

    private void OnCatalogViewportChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshPreviewPlayback();
    }

    private void OnCatalogScrollViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        RefreshPreviewPlayback();
    }

    private void RefreshPreviewPlayback()
    {
        if (catalogCards.Count == 0)
        {
            foreach (var card in activePreviewCards)
            {
                card.SetPreviewPlayback(false);
            }

            activePreviewCards.Clear();
            return;
        }

        var stale = activePreviewCards.Except(catalogCards).ToArray();
        foreach (var removed in stale)
        {
            removed.SetPreviewPlayback(false);
            activePreviewCards.Remove(removed);
        }

        foreach (var card in catalogCards)
        {
            card.SetPreviewPlayback(true);
            activePreviewCards.Add(card);
        }
    }

    private void ShowOperationNotification(string message, bool inProgress)
    {
        if (string.IsNullOrWhiteSpace(message)) return;

        var hasError = message.Contains("erro", StringComparison.OrdinalIgnoreCase)
            || message.Contains("falh", StringComparison.OrdinalIgnoreCase);

        OperationInfoBar.Severity = inProgress
            ? InfoBarSeverity.Informational
            : (hasError ? InfoBarSeverity.Error : InfoBarSeverity.Success);
        OperationInfoBar.Message = message;
        OperationInfoBar.IsOpen = true;

        if (!inProgress)
        {
            EnsureNotificationTimer();
            _notificationTimer!.Stop();
            _notificationTimer.Start();
        }
        else
        {
            _notificationTimer?.Stop();
        }
    }

    private void EnsureNotificationTimer()
    {
        if (_notificationTimer is not null) return;
        _notificationTimer = DispatcherQueue.CreateTimer();
        _notificationTimer.Interval = TimeSpan.FromSeconds(4);
        _notificationTimer.IsRepeating = false;
        _notificationTimer.Tick += (_, _) => { OperationInfoBar.IsOpen = false; };
    }
}
