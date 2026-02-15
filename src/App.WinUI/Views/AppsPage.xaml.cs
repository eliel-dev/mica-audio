using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.WinUI.Views;

public sealed partial class AppsPage : Page
{
    private readonly List<AppCatalogItem> allItems = new();
    private readonly List<AppCatalogItem> filteredItems = new();

    private AppCatalogItem? selectedItem;
    private DeviceOperationsState currentState = new();

    public AppsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private DeviceOperationsCoordinator? DeviceOps => App.DeviceOps;

    private AppCatalogService? CatalogService => App.AppCatalog;

    private AppDeploymentService? DeploymentService => App.AppDeployment;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is not null)
        {
            DeviceOps.StateChanged += OnDeviceOpsStateChanged;
            currentState = DeviceOps.GetStateSnapshot();
            ApplyDevices(currentState.DeviceListSnapshot);
            ApplyState(currentState);
        }

        await LoadCatalogAsync().ConfigureAwait(false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is not null)
        {
            DeviceOps.StateChanged -= OnDeviceOpsStateChanged;
        }
    }

    private async Task LoadCatalogAsync()
    {
        var service = CatalogService;
        if (service is null)
        {
            AppendLog("Servico de catalogo indisponivel.");
            return;
        }

        try
        {
            var catalog = await service.LoadCatalogAsync().ConfigureAwait(false);
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                allItems.Clear();
                allItems.AddRange(catalog);
                ApplyFilter();
                AppendLog($"Catalogo carregado: {allItems.Count} apps.");
            });
        }
        catch (Exception ex)
        {
            _ = DispatcherQueue.TryEnqueue(() => AppendLog($"Falha ao carregar catalogo: {ex.Message}"));
        }
    }

    private void OnDeviceOpsStateChanged(object? sender, EventArgs e)
    {
        var ops = DeviceOps;
        if (ops is null)
        {
            return;
        }

        var state = ops.GetStateSnapshot();
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            currentState = state;
            ApplyState(state);
            ApplyDevices(state.DeviceListSnapshot);
        });
    }

    private void ApplyState(DeviceOperationsState state)
    {
        OperationProgressRing.IsActive = state.CommandInProgress;
        OperationProgressRing.Visibility = state.CommandInProgress ? Visibility.Visible : Visibility.Collapsed;
        OperationStatusText.Text = state.CommandStatus;
        OperationPercentText.Text = $"{Math.Clamp(state.CommandPercent, 0, 100)}%";
    }

    private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)
    {
        var currentSelection = TargetDeviceCombo.SelectedItem as ComboBoxItem;
        var selectedId = currentSelection?.Tag as string;

        TargetDeviceCombo.Items.Clear();
        foreach (var device in devices.Where(d => d.Status == DeviceStatus.Online))
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
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
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
                || item.Author.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Summary.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        filteredItems.AddRange(source);
        CatalogGrid.ItemsSource = null;
        CatalogGrid.ItemsSource = filteredItems;

        if (selectedItem is not null)
        {
            var match = filteredItems.FirstOrDefault(item => string.Equals(item.Id, selectedItem.Id, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                CatalogGrid.SelectedItem = match;
            }
        }
    }

    private void OnCatalogItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not AppCatalogItem item)
        {
            return;
        }

        SetSelectedItem(item);
    }

    private void SetSelectedItem(AppCatalogItem item)
    {
        selectedItem = item;
        SelectedAppNameText.Text = item.Name;
        SelectedAppMetaText.Text = $"{item.Category} | Autor: {item.Author} | Intervalo recomendado: {item.RecommendedIntervalMinutes} min";
        SelectedAppDescriptionText.Text = item.Description;
    }

    private async void OnInstallClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelection(out var deviceId, out var item, out var error))
        {
            AppendLog(error);
            return;
        }

        var deployment = DeploymentService;
        if (deployment is null)
        {
            AppendLog("Servico de deploy indisponivel.");
            return;
        }

        var result = await deployment.InstallAsync(deviceId, item).ConfigureAwait(false);
        _ = DispatcherQueue.TryEnqueue(() => AppendLog(RenderResult("instalar", item.Name, result)));
    }

    private async void OnActivateClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelection(out var deviceId, out var item, out var error))
        {
            AppendLog(error);
            return;
        }

        var deployment = DeploymentService;
        if (deployment is null)
        {
            AppendLog("Servico de deploy indisponivel.");
            return;
        }

        var result = await deployment.ActivateAsync(deviceId, item).ConfigureAwait(false);
        _ = DispatcherQueue.TryEnqueue(() => AppendLog(RenderResult("ativar", item.Name, result)));
    }

    private bool TryGetSelection(out string deviceId, out AppCatalogItem item, out string error)
    {
        deviceId = string.Empty;
        item = selectedItem ?? new AppCatalogItem();

        if (selectedItem is null)
        {
            error = "Selecione um app antes de continuar.";
            return false;
        }

        if (TargetDeviceCombo.SelectedItem is not ComboBoxItem selectedDevice || selectedDevice.Tag is not string selectedDeviceId)
        {
            error = "Selecione um dispositivo online.";
            return false;
        }

        deviceId = selectedDeviceId;
        item = selectedItem;
        error = string.Empty;
        return true;
    }

    private static string RenderResult(string verb, string appName, CommandDispatchResult result)
    {
        if (result.Success)
        {
            return $"App '{appName}' {verb} com sucesso em {result.DeviceId}.";
        }

        return $"Falha ao {verb} '{appName}' em {result.DeviceId}: {result.Message ?? result.ErrorCode ?? "erro"}.";
    }

    private async void OnReloadCatalogClicked(object sender, RoutedEventArgs e)
    {
        await LoadCatalogAsync().ConfigureAwait(false);
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        LogsTextBox.Text += $"[{DateTimeOffset.Now:HH:mm:ss}] {message}\r\n";
    }
}

