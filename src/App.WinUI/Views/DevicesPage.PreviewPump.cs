using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using Microsoft.UI.Dispatching;

namespace App.WinUI.Views;

public sealed partial class DevicesPage
{
    private void StartPreviewPump()
    {
        if (previewPumpTimer is not null)
        {
            return;
        }

        previewPumpTimer = DispatcherQueue.CreateTimer();
        previewPumpTimer.Interval = TimeSpan.FromMilliseconds(125);
        previewPumpTimer.Tick += OnPreviewPumpTick;
        previewPumpTimer.Start();
    }

    private void StopPreviewPump()
    {
        if (previewPumpTimer is null)
        {
            return;
        }

        previewPumpTimer.Stop();
        previewPumpTimer.Tick -= OnPreviewPumpTick;
        previewPumpTimer = null;
    }

    private void OnPreviewPumpTick(DispatcherQueueTimer sender, object args)
    {
        if (isApplyingDeviceList)
        {
            return;
        }

        var snapshot = renderedItemsByDeviceId.Values.ToArray();
        MicaAudio.Core.Presets.RgbaColor[]? frameCache = null;

        foreach (var visualItem in snapshot)
        {
            if (!string.Equals(visualItem.Source.AppId, Hub75VisualizerSessionService.VisualizerAppId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            frameCache ??= simulatorLedOutput.GetFrameSnapshot();
            visualItem.SetRuntimeFrame(frameCache);
        }
    }

    private void RefreshVisiblePreviewConfigsIfNeeded()
    {
        _ = RefreshVisiblePreviewConfigsAsync(force: false);
    }

    private async Task RefreshVisiblePreviewConfigsAsync(bool force)
    {
        var requests = renderedItemsByDeviceId.Values
            .Select(static item => new PreviewConfigRequest(item.DeviceId, item.Source.AppId, item.PreviewItem))
            .OrderBy(static item => item.DeviceId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var signature = string.Join(
            "|",
            requests.Select(static item => $"{item.DeviceId}:{item.AppId}:{item.PreviewItem?.Id ?? "-"}"));

        if (!force && string.Equals(lastAppliedPreviewConfigSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        lastAppliedPreviewConfigSignature = signature;
        var refreshVersion = Interlocked.Increment(ref previewConfigRefreshVersion);
        var resolvedConfig = new Dictionary<string, IReadOnlyDictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);

        foreach (var request in requests)
        {
            if (request.PreviewItem is null || string.IsNullOrWhiteSpace(request.AppId))
            {
                resolvedConfig[request.DeviceId] = null;
                continue;
            }

            resolvedConfig[request.DeviceId] = await ResolvePreviewConfigAsync(request.DeviceId, request.AppId, request.PreviewItem).ConfigureAwait(false);
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (refreshVersion != previewConfigRefreshVersion)
            {
                return;
            }

            foreach (var visualItem in renderedItemsByDeviceId.Values)
            {
                visualItem.SetPreviewConfig(
                    resolvedConfig.TryGetValue(visualItem.DeviceId, out var values)
                        ? values
                        : null);
            }
        });
    }

    private async Task<IReadOnlyDictionary<string, string>?> ResolvePreviewConfigAsync(string deviceId, string appId, AppCatalogItem previewItem)
    {
        var scopedDraft = await modifierStore.GetDraftAsync(deviceId, appId).ConfigureAwait(false)
            ?? await modifierStore.GetDraftAsync(LocalDraftScope, appId).ConfigureAwait(false);
        var normalized = WeatherAppFixedLocation.NormalizeRawValues(previewItem, scopedDraft?.Values);
        return normalized.Count == 0 ? null : normalized;
    }

    private AppCatalogItem? ResolvePreviewApp(DeviceListItem item)
    {
        return DevicePreviewResolver.Resolve(item.AppId, item.AppName, appCatalogById);
    }

    private static void AddLocalLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[DevicesPage] {message}");
    }

    private sealed record PreviewConfigRequest(string DeviceId, string AppId, AppCatalogItem? PreviewItem);
}
