using App.WinUI.Services.Devices;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.WinUI.Views;

public sealed partial class DevicesPage
{
    private DeviceListVisualItem? GetSelectedVisualItem()
    {
        if (!string.IsNullOrWhiteSpace(selectedDeviceId)
            && renderedItemsByDeviceId.TryGetValue(selectedDeviceId, out var selectedVisual))
        {
            return selectedVisual;
        }

        var selectedFromList = ResolveSelectedDeviceIdFromListSelection(DevicesList.SelectedItem);
        if (!string.IsNullOrWhiteSpace(selectedFromList)
            && renderedItemsByDeviceId.TryGetValue(selectedFromList, out var selectedFromControl))
        {
            return selectedFromControl;
        }

        return null;
    }

    private DeviceListItem? GetSelectedDeviceItem()
    {
        return GetSelectedVisualItem()?.Source;
    }

    private string? GetSelectedDeviceId()
    {
        return selectedDeviceId;
    }

    private DeviceSnapshot? FindDeviceSnapshot(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        foreach (var snapshot in currentState.DeviceListSnapshot)
        {
            if (string.Equals(snapshot.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return snapshot;
            }
        }

        return null;
    }

    private void OnDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressDeviceSelectionChanged)
        {
            return;
        }

        selectedDeviceId = ResolveSelectedDeviceIdFromListSelection(DevicesList.SelectedItem);
        var snapshot = !string.IsNullOrWhiteSpace(selectedDeviceId)
            ? FindDeviceSnapshot(selectedDeviceId)
            : null;
        DeviceMetricsPresentation? metrics = snapshot is null
            ? null
            : DeviceMetricsFormatter.Build(snapshot);
        LogSelectionBreadcrumb("selection", selectedDeviceId, snapshot, metrics);
        UpdateDeviceRowSelection();
        ApplySelectionDetails();
        ApplyButtonState();
    }

    private void UpdateDeviceRowSelection()
    {
        var selectedId = GetSelectedDeviceId();
        foreach (var item in renderedItemsByDeviceId.Values)
        {
            item.SetSelectedVisual(string.Equals(item.DeviceId, selectedId, StringComparison.OrdinalIgnoreCase));
        }
    }

    // DOCS: docs/wiki/guides/setup-new-device.md#tela-dispositivos
    private void ApplySelectionDetails()
    {
        var selectedVisual = GetSelectedVisualItem();
        if (selectedVisual is null)
        {
            SetDetailsPaneVisible(false);
            SelectedDeviceTitleText.Text = "Nenhum dispositivo selecionado";
            SelectedDeviceSubtitleText.Text = "-";
            SelectedDeviceRegistrationText.Text = "-";
            SelectedDeviceAppText.Text = "App ativo: -";
            SelectedDeviceSignalText.Text = "Sinal -";
            TestLedButton.Label = "Testar LED";
            ApplyDashboard(selectionDeviceId: null, hasSelection: false, snapshot: null, DeviceMetricsFormatter.Build(null));
            UpdateDeviceLogs(deviceId: null, entries: Array.Empty<string>(), placeholder: "Selecione um dispositivo para ver logs do dispositivo.");
            return;
        }

        SetDetailsPaneVisible(true);

        var selected = selectedVisual.Source;
        SelectedDeviceTitleText.Text = selected.Name;
        SelectedDeviceSubtitleText.Text = selected.StatusLine;
        SelectedDeviceRegistrationText.Text = BuildRegistrationLine(selected);
        SelectedDeviceAppText.Text = DevicePreviewVisibilityPolicy.BuildSelectedAppLabel(selected.Status, selected.AppName);

        var selectedSnapshot = FindDeviceSnapshot(selected.DeviceId);
        SelectedDeviceSignalText.Text = BuildSelectedSignalLabel(selectedSnapshot);
        var testLedAvailable = selectedSnapshot?.TestLedAvailable != false;
        TestLedButton.Label = testLedAvailable ? "Testar LED" : "LED indisponivel";
        try
        {
            var metrics = DeviceMetricsFormatter.Build(selectedSnapshot);
            ApplyDashboard(selectionDeviceId: selected.DeviceId, hasSelection: true, snapshot: selectedSnapshot, metrics);
        }
        catch (Exception ex)
        {
            LogRenderException("selection", selected.DeviceId, selectedSnapshot, ex);
            ApplyOfflineDashboardFallback(
                hasSelection: true,
                snapshot: selectedSnapshot,
                metrics: DeviceMetricsFormatter.Build(selectedSnapshot),
                placeholder: OfflineDashboardFallbackText);
            lastRenderedDashboardSignature = BuildOfflineDashboardSignature(selected.DeviceId, selectedSnapshot);
        }

        var deviceLogs = DeviceOps?.GetDeviceLogs(selected.DeviceId) ?? Array.Empty<string>();
        var logsPlaceholder = deviceLogs.Count == 0
            ? "Sem eventos para este dispositivo ainda."
            : string.Empty;
        UpdateDeviceLogs(selected.DeviceId, deviceLogs, logsPlaceholder);
    }

    private void SetDetailsPaneVisible(bool visible)
    {
        DeviceDetailsGrid.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        DevicesDetailsColumn.Width = visible
            ? new GridLength(3, GridUnitType.Star)
            : new GridLength(0);
    }

    private void ApplyButtonState()
    {
        var selected = GetSelectedDeviceItem();
        var selectedSnapshot = selected is null ? null : FindDeviceSnapshot(selected.DeviceId);
        var canRunCommand = selected is not null
            && selected.Presence.CanRunCommands
            && !currentState.CommandInProgress;
        var canRemove = selected is not null && !currentState.CommandInProgress;
        var testLedAvailable = selectedSnapshot?.TestLedAvailable != false;

        TestLedButton.IsEnabled = canRunCommand && testLedAvailable;
        if (selected is null)
        {
            TestLedButton.Label = "Testar LED";
        }
        else
        {
            TestLedButton.Label = testLedAvailable ? "Testar LED" : "LED indisponivel";
        }

        DashboardBrightnessSlider.IsEnabled = canRunCommand;
        RemoveDeviceButton.IsEnabled = canRemove;
    }

    private static string BuildRegistrationLine(DeviceListItem selected)
    {
        if (selected.Status == DeviceStatus.Pairing)
        {
            return "Vinculo: pareamento iniciado; aguardando a primeira sessao do dispositivo";
        }

        if (selected.Presence.IsNeverSeen)
        {
            return "Vinculo: registrado localmente; aguardando primeiro provisionamento";
        }

        if (selected.Status == DeviceStatus.Online)
        {
            return "Vinculo: configurado e em comunicacao";
        }

        if (selected.Presence.IsConfigUncertain)
        {
            return "Vinculo: registro salvo localmente; a configuracao no ESP pode ter mudado";
        }

        return "Vinculo: registro salvo localmente; sem telemetria no momento";
    }
}
