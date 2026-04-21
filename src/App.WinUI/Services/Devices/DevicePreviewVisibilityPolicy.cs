using App.WinUI.Models.Apps;
using Device.Protocol.Models;
// DOCS: docs/wiki/reference/code-index.md#pontos-de-ui-para-operacao-de-devices
namespace App.WinUI.Services.Devices;

internal static class DevicePreviewVisibilityPolicy
{
    public static bool ShouldShowInlinePreview(DeviceStatus status, AppCatalogItem? previewItem)
        => status == DeviceStatus.Online && previewItem is not null;
    public static bool ShouldShowSelectedPreview(DeviceStatus status, AppCatalogItem? previewItem)
        => status == DeviceStatus.Online && previewItem is not null;
    public static string BuildSelectedAppLabel(DeviceStatus status, string? appName)
    {
        var normalizedName = string.IsNullOrWhiteSpace(appName) ? "-" : appName.Trim();
        return status == DeviceStatus.Online
            ? $"App ativo: {normalizedName}"
            : $"Ultimo app conhecido: {normalizedName}";
    }
    public static string BuildInlinePreviewPlaceholder(DeviceStatus status, AppCatalogItem? previewItem)
    {
        if (status != DeviceStatus.Online)
        {
            return "Offline";
        }
        return previewItem is null ? "Sem app" : string.Empty;
    }
}