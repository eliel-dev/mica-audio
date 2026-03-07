using App.WinUI.Models.Apps;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace App.WinUI.Views;

public sealed partial class AppsPage
{
    private async void OnInstallClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelection(out var deviceId, out var item, out var error))
        {
            AppendLog(error);
            return;
        }

        if (!TryBuildConfigFromEditor(item, out _, out var rawValues, out var validationError))
        {
            AppendLog(validationError);
            return;
        }

        var result = await deployAppUseCase.ExecuteAsync(LocalDraftScope, deviceId, item, rawValues).ConfigureAwait(false);
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ApplyPreviewDraftToCard(item.Id, result.RawValues ?? rawValues);
            if (!result.Success || result.CommandResult is null)
            {
                AppendLog($"Falha ao instalar '{item.Name}': {result.Message}");
                return;
            }

            AppendLog(RenderResult("instalar", item.Name, result.CommandResult));
        });
    }

    private async void OnSaveModifiersClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedItem(out var item, out var error))
        {
            AppendLog(error);
            return;
        }

        if (!TryBuildConfigFromEditor(item, out _, out var rawValues, out var validationError))
        {
            AppendLog(validationError);
            return;
        }

        if (!appConfigValidationUseCase.TryBuildPayload(item, rawValues, out _, out validationError))
        {
            AppendLog(validationError);
            return;
        }

        var saveResult = await saveAppConfigUseCase.ExecuteAsync(LocalDraftScope, item, rawValues).ConfigureAwait(false);
        if (!saveResult.Success)
        {
            AppendLog(saveResult.Message);
            return;
        }

        if (activeRuntimeProvider is not null && selectedItem is not null)
        {
            await activeRuntimeProvider.OnConfigSavedAsync(selectedItem, saveResult.RawValues ?? rawValues, CancellationToken.None).ConfigureAwait(false);
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ApplyPreviewDraftToCard(item.Id, saveResult.RawValues ?? rawValues);
            AppendLog($"ModificaÃ§Ãµes salvas localmente para {item.Name}.");
        });
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

    private bool TryGetSelectedItem(out AppCatalogItem item, out string error)
    {
        if (selectedItem is null)
        {
            item = new AppCatalogItem();
            error = "Selecione um app antes de continuar.";
            return false;
        }

        item = selectedItem;
        error = string.Empty;
        return true;
    }

    private void UpdateActionButtonsEnabled()
    {
        var hasSelection = selectedItem is not null;
        var hasDevice = TargetDeviceCombo.SelectedItem is ComboBoxItem;
        var busy = currentState.CommandInProgress;

        InstallButton.IsEnabled = hasSelection && hasDevice && !busy;
        SaveModifiersButton.IsEnabled = hasSelection;
        GifOpenFileButton.IsEnabled = hasSelection && !busy && IsGifFileModeSelected();
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private static string RenderResult(string verb, string appName, CommandDispatchResult result)
    {
        return result.Success
            ? $"App '{appName}' {verb} com sucesso em {result.DeviceId}."
            : $"Falha ao {verb} '{appName}' em {result.DeviceId}: {result.Message ?? result.ErrorCode ?? "erro"}.";
    }

    private Task InstallSelectedAppAsync()
    {
        OnInstallClicked(this, new RoutedEventArgs());
        return Task.CompletedTask;
    }

    private Task SaveSelectedModifierDraftAsync()
    {
        OnSaveModifiersClicked(this, new RoutedEventArgs());
        return Task.CompletedTask;
    }

    private async void OnReloadCatalogClicked(object sender, RoutedEventArgs e)
    {
        await viewModel.ReloadCatalogCommand.ExecuteAsync(null);
    }

    private async Task ReloadCatalogFromDiskAsyncCommandAsync()
    {
        await ReloadCatalogFromDiskAsync().ConfigureAwait(false);
        await EnsureGifRuntimeWhileAppsPageIsVisibleAsync().ConfigureAwait(false);
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        viewModel.OperationStatus = $"OperaÃ§Ãµes: {message}";
        OperationStatusText.Text = viewModel.OperationStatus;
        if (!currentState.CommandInProgress)
        {
            viewModel.OperationPercent = 0;
            OperationPercentText.Text = "0%";
        }
    }
}
