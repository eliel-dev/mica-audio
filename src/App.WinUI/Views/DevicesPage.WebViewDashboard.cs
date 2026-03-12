using System.Text.Json;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md
// DOCS: docs/wiki/reference/device-observability-dashboard.md
public sealed partial class DevicesPage
{
    private static readonly JsonSerializerOptions DashboardBridgeJsonOptions = new(JsonSerializerDefaults.Web);

    private async Task PushDashboardSelectionAsync()
    {
        var selectedId = GetSelectedDeviceId();
        if (string.IsNullOrWhiteSpace(selectedId))
        {
            if (dashboardWebViewReady)
            {
                PostDashboardMessage(new DashboardBridgeMessage("clear-selection", null, null));
                lastDashboardPostedDeviceId = null;
            }

            return;
        }

        if (!await EnsureDashboardWebViewReadyAsync().ConfigureAwait(true) || !dashboardWebViewReady)
        {
            return;
        }

        if (string.Equals(lastDashboardPostedDeviceId, selectedId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        PostDashboardMessage(new DashboardBridgeMessage("select-device", selectedId, null));
        lastDashboardPostedDeviceId = selectedId;
    }

    private async Task<bool> EnsureDashboardWebViewReadyAsync()
    {
        if (DeviceDashboardWebView is null)
        {
            return false;
        }

        var targetUri = BuildDashboardWebViewUri(currentState.ServerBaseAddress);
        if (targetUri is null)
        {
            return false;
        }

        if (!dashboardWebViewInitialized)
        {
            await DeviceDashboardWebView.EnsureCoreWebView2Async();
            dashboardWebViewInitialized = true;
        }

        var core = DeviceDashboardWebView.CoreWebView2;
        if (core is null)
        {
            return false;
        }

        if (!dashboardWebViewEventsHooked)
        {
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.WebMessageReceived += OnDashboardWebMessageReceived;
            DeviceDashboardWebView.NavigationCompleted += OnDashboardNavigationCompleted;
            dashboardWebViewEventsHooked = true;
        }

        if (dashboardWebViewUri is null || !Equals(dashboardWebViewUri, targetUri))
        {
            dashboardWebViewReady = false;
            lastDashboardPostedDeviceId = null;
            dashboardWebViewUri = targetUri;
            core.Navigate(targetUri.ToString());
        }

        return true;
    }

    private void DetachDashboardWebViewBridge()
    {
        if (!dashboardWebViewEventsHooked || DeviceDashboardWebView?.CoreWebView2 is not CoreWebView2 core)
        {
            return;
        }

        core.WebMessageReceived -= OnDashboardWebMessageReceived;
        DeviceDashboardWebView.NavigationCompleted -= OnDashboardNavigationCompleted;
        dashboardWebViewEventsHooked = false;
    }

    private static Uri? BuildDashboardWebViewUri(string? serverBaseAddress)
    {
        if (!Uri.TryCreate(serverBaseAddress, UriKind.Absolute, out var baseAddress))
        {
            return null;
        }

        var builder = new UriBuilder(baseAddress)
        {
            Host = "127.0.0.1",
            Path = "/dashboard",
            Query = "embedded=1",
        };

        return builder.Uri;
    }

    private void OnDashboardNavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess)
        {
            AddLocalLog($"Falha ao navegar o dashboard WebView2: {args.WebErrorStatus}");
        }
    }

    private async void OnDashboardWebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        DashboardBridgeMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<DashboardBridgeMessage>(args.WebMessageAsJson, DashboardBridgeJsonOptions);
        }
        catch (Exception ex)
        {
            AddLocalLog($"Falha ao ler mensagem do dashboard: {ex.Message}");
            return;
        }

        if (message is null || string.IsNullOrWhiteSpace(message.Type))
        {
            return;
        }

        switch (message.Type)
        {
            case "ready":
                dashboardWebViewReady = true;
                lastDashboardPostedDeviceId = null;
                PostDashboardMessage(new DashboardBridgeMessage("ready-ack", null, null));
                await PushDashboardSelectionAsync().ConfigureAwait(true);
                break;

            case "set-brightness":
                if (message.Brightness.HasValue)
                {
                    await ExecuteSetBrightnessAsync(message.Brightness.Value, message.DeviceId).ConfigureAwait(true);
                }

                break;

            case "test-led":
                await ExecuteTestLedAsync(message.DeviceId).ConfigureAwait(true);
                break;

            case "remove-device":
                await ExecuteRemoveDeviceAsync(message.DeviceId).ConfigureAwait(true);
                break;
        }
    }

    private void PostDashboardMessage(DashboardBridgeMessage message)
    {
        var core = DeviceDashboardWebView?.CoreWebView2;
        if (core is null)
        {
            return;
        }

        core.PostWebMessageAsJson(JsonSerializer.Serialize(message, DashboardBridgeJsonOptions));
    }

    private sealed record DashboardBridgeMessage(string Type, string? DeviceId, int? Brightness);
}
