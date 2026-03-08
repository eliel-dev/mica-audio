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
                AppendLog($"CatÃ¡logo carregado: {allItems.Count} apps.");
            });
        }
        catch (Exception ex)
        {
            _ = DispatcherQueue.TryEnqueue(() => AppendLog($"Falha ao carregar catÃ¡logo: {ex.Message}"));
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

        OperationProgressRing.IsActive = viewModel.OperationInProgress;
        OperationProgressRing.Visibility = viewModel.OperationInProgress ? Visibility.Visible : Visibility.Collapsed;
        OperationStatusText.Text = viewModel.OperationStatus;
        OperationPercentText.Text = $"{viewModel.OperationPercent}%";
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
            card.Preview.Stop();
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
            await LoadCatalogAsync().ConfigureAwait(false);
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
            return;
        }

        var stale = activePreviewCards.Except(catalogCards).ToArray();
        foreach (var removed in stale)
        {
            removed.Preview.Stop();
            activePreviewCards.Remove(removed);
        }

        activePreviewCards.Clear();
        foreach (var card in catalogCards)
        {
            activePreviewCards.Add(card);
        }
    }
}
