using App.WinUI.Models.Apps;
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
}
