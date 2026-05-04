using App.WinUI.Services.Devices;
using Device.Protocol.Models;
using System.Globalization;

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
            _ = PushDashboardSelectionAsync();
            return;
        }

        SetDetailsPaneVisible(true);
        _ = PushDashboardSelectionAsync();
    }

    private void SetDetailsPaneVisible(bool visible)
    {
        DeviceDetailsGrid.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        DevicesDetailsColumn.Width = visible
            ? new GridLength(3, GridUnitType.Star)
            : new GridLength(0);
    }

    // DOCS: docs/wiki/reference/device-observability-dashboard.md
    private void ApplyButtonState()
    {
        if (CopyDashboardLinkButton is not null)
        {
            CopyDashboardLinkButton.IsEnabled = BuildDashboardShareUri(currentState.ServerBaseAddress, GetSelectedDeviceId()) is not null;
        }
    }

    private static void LogSelectionBreadcrumb(
        string stage,
        string? deviceId,
        DeviceSnapshot? snapshot,
        DeviceMetricsPresentation? metrics)
    {
        AddLocalLog(
            $"Breadcrumb {stage} (device={deviceId ?? "-"}, status={snapshot?.Status.ToString() ?? "-"}, " +
            $"hasMetrics={metrics?.HasMetrics.ToString() ?? "-"}, lastTelemetry={snapshot?.LastTelemetryUtc?.ToString("O", CultureInfo.InvariantCulture) ?? "-"})");
    }
}
