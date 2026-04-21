using App.WinUI.Models.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using System.Globalization;

namespace App.WinUI.Views;

public sealed partial class DevicesPage
{
    // DOCS: docs/wiki/guides/operate-device-lifecycle.md#passos
    // DOCS: docs/handoffs/2026-04-20-remove-usb-flash-flow.md
    private void ApplyState(DeviceOperationsState state)
    {
        currentState = state;

        viewModel.CommandInProgress = state.CommandInProgress;
        viewModel.CommandPercent = state.CommandPercent;
        viewModel.CommandStatus = state.CommandStatus;

        ApplySelectionDetails();
        ApplyButtonState();
    }

    private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)
    {
        if (isApplyingDeviceList)
        {
            pendingDeviceListSnapshot = devices.ToArray();
            return;
        }

        isApplyingDeviceList = true;
        try
        {
            var retainedSelectionDeviceId = selectedDeviceId;

            allItems.Clear();
            foreach (var device in devices)
            {
                var presence = DeviceLifecyclePolicy.Build(device, lifecycleThresholds, DateTimeOffset.UtcNow);
                var profile = string.IsNullOrWhiteSpace(device.Profile) ? "-" : device.Profile;

                allItems.Add(new DeviceListItem
                {
                    DeviceId = device.DeviceId,
                    Name = device.Name,
                    Status = device.Status,
                    StatusLine = $"{presence.PrimaryStateLabel} | {presence.SecondaryStateLabel} | {presence.LastSeenLabel} | Perfil {profile}",
                    AppId = string.IsNullOrWhiteSpace(device.ActiveAppId) ? string.Empty : device.ActiveAppId!,
                    AppName = string.IsNullOrWhiteSpace(device.ActiveAppName) ? string.Empty : device.ActiveAppName!,
                    Presence = presence,
                });
            }

            ApplyFilter(retainedSelectionDeviceId);
            ApplySelectionDetails();
            ApplyButtonState();
        }
        finally
        {
            isApplyingDeviceList = false;
        }

        if (pendingDeviceListSnapshot is { } pending)
        {
            pendingDeviceListSnapshot = null;
            ApplyDevices(pending);
        }
    }

    private void ApplyFilter(string? selectedDeviceId)
    {
        visibleItems.Clear();
        visibleItems.AddRange(allItems);
        ApplyRenderedItemsDiff(selectedDeviceId);
    }

    private void ApplyRenderedItemsDiff(string? selectedDeviceId)
    {
        var nextIds = visibleItems.Select(static item => item.DeviceId).ToList();
        var nextSignature = DeviceListRenderDiff.BuildSignature(visibleItems.Select(BuildRenderToken));
        var retainedSelectionDeviceId = ResolveRetainedSelectionDeviceId(selectedDeviceId, nextIds);
        if (string.Equals(lastAppliedDeviceListSignature, nextSignature, StringComparison.Ordinal))
        {
            RestoreSelection(retainedSelectionDeviceId);
            UpdateDeviceRowSelection();
            return;
        }

        var nextIdSet = new HashSet<string>(nextIds, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(retainedSelectionDeviceId) && DevicesList.SelectedItem is not null)
        {
            SetListSelectedItem(null);
        }

        foreach (var existingId in renderedOrder.ToArray())
        {
            if (nextIdSet.Contains(existingId))
            {
                continue;
            }

            if (!renderedItemsByDeviceId.TryGetValue(existingId, out var removed))
            {
                continue;
            }

            var currentIndex = DevicesList.Items.IndexOf(removed.RowControl);
            if (currentIndex >= 0)
            {
                DevicesList.Items.RemoveAt(currentIndex);
            }

            removed.StopPreview();
            renderedItemsByDeviceId.Remove(existingId);
        }

        var orderChanged = !DeviceListRenderDiff.HasSameOrder(renderedOrder, nextIds);
        for (var index = 0; index < visibleItems.Count; index++)
        {
            var item = visibleItems[index];
            var previewModel = ResolvePreviewApp(item);
            var inlinePreviewItem = DevicePreviewVisibilityPolicy.ShouldShowInlinePreview(item.Status, previewModel) ? previewModel : null;
            var previewPlaceholderText = DevicePreviewVisibilityPolicy.BuildInlinePreviewPlaceholder(item.Status, previewModel);

            if (!renderedItemsByDeviceId.TryGetValue(item.DeviceId, out var visualItem))
            {
                visualItem = new DeviceListVisualItem(item, inlinePreviewItem, previewPlaceholderText);
                renderedItemsByDeviceId[item.DeviceId] = visualItem;
            }
            else
            {
                visualItem.Update(item, inlinePreviewItem, previewPlaceholderText);
            }

            var currentIndex = DevicesList.Items.IndexOf(visualItem.RowControl);
            if (currentIndex < 0)
            {
                DevicesList.Items.Insert(Math.Min(index, DevicesList.Items.Count), visualItem.RowControl);
                continue;
            }

            if (orderChanged && currentIndex != index)
            {
                DevicesList.Items.RemoveAt(currentIndex);
                DevicesList.Items.Insert(Math.Min(index, DevicesList.Items.Count), visualItem.RowControl);
            }
        }

        renderedOrder.Clear();
        renderedOrder.AddRange(nextIds);
        lastAppliedDeviceListSignature = nextSignature;
        RefreshVisiblePreviewConfigsIfNeeded();

        RestoreSelection(retainedSelectionDeviceId);
        UpdateDeviceRowSelection();
    }

    private void RestoreSelection(string? selectedDeviceId)
    {
        this.selectedDeviceId = selectedDeviceId;

        if (!string.IsNullOrWhiteSpace(this.selectedDeviceId)
            && renderedItemsByDeviceId.TryGetValue(this.selectedDeviceId, out var selectedVisual))
        {
            SetListSelectedItem(selectedVisual.RowControl);
            return;
        }

        SetListSelectedItem(null);
    }

    private void SetListSelectedItem(object? item)
    {
        if (ReferenceEquals(DevicesList.SelectedItem, item))
        {
            return;
        }

        suppressDeviceSelectionChanged = true;
        try
        {
            DevicesList.SelectedItem = item;
        }
        finally
        {
            suppressDeviceSelectionChanged = false;
        }
    }

    private static string? ResolveRetainedSelectionDeviceId(string? selectedDeviceId, IReadOnlyList<string> nextIds)
    {
        if (string.IsNullOrWhiteSpace(selectedDeviceId))
        {
            return null;
        }

        return nextIds.Any(id => string.Equals(id, selectedDeviceId, StringComparison.OrdinalIgnoreCase))
            ? selectedDeviceId
            : null;
    }

    private static string? ResolveSelectedDeviceIdFromListSelection(object? selectedItem)
    {
        return selectedItem switch
        {
            DeviceListRowControl rowControl when rowControl.Tag is DeviceListVisualItem visual => visual.DeviceId,
            DeviceListVisualItem visual => visual.DeviceId,
            _ => null,
        };
    }

    private static string BuildRenderToken(DeviceListItem item)
    {
        return string.Concat(
            item.DeviceId,
            "|",
            item.Status,
            "|",
            item.StatusLine,
            "|",
            item.AppId,
            "|",
            item.AppName);
    }

    private void ClearRenderedItems()
    {
        foreach (var item in renderedItemsByDeviceId.Values)
        {
            item.StopPreview();
        }

        renderedItemsByDeviceId.Clear();
        renderedOrder.Clear();
        lastAppliedDeviceListSignature = null;
        lastAppliedPreviewConfigSignature = null;
        selectedDeviceId = null;
        SetListSelectedItem(null);
        DevicesList.Items.Clear();
    }
}
