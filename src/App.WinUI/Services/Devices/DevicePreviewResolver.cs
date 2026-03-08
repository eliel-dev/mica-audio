using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/guides/setup-new-device.md#passos
internal static class DevicePreviewResolver
{
    public static AppCatalogItem? Resolve(
        string appId,
        string appName,
        IReadOnlyDictionary<string, AppCatalogItem> catalogById)
    {
        var normalizedAppId = appId?.Trim() ?? string.Empty;
        var normalizedAppName = appName?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedAppId) && string.IsNullOrWhiteSpace(normalizedAppName))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(normalizedAppId)
            && catalogById.TryGetValue(normalizedAppId, out var catalogItem))
        {
            return catalogItem;
        }

        var kind = ResolvePreviewKind(normalizedAppId, normalizedAppName);
        var category = kind switch
        {
            "clock" => "relógio",
            "weather" => "clima",
            _ => "geral",
        };

        return new AppCatalogItem
        {
            Id = string.IsNullOrWhiteSpace(normalizedAppId) ? "device-preview" : normalizedAppId,
            Name = string.IsNullOrWhiteSpace(normalizedAppName) ? normalizedAppId : normalizedAppName,
            Category = category,
            Preview = new AppPreviewDefinition
            {
                Kind = kind,
                Speed = 1f,
            },
        };
    }

    private static string ResolvePreviewKind(string appId, string appName)
    {
        var id = appId.Trim().ToLowerInvariant();
        var name = appName.Trim().ToLowerInvariant();

        if (id.Contains("weather") || id.Contains("clima") || name.Contains("weather") || name.Contains("clima") || id.Contains("accuweather"))
        {
            return "weather";
        }

        if (id.Contains("clock") || id.Contains("relog") || id.Contains("relóg") || name.Contains("clock") || name.Contains("relog") || name.Contains("relóg") || id.Contains("analogclock"))
        {
            return "clock";
        }

        return "decorative";
    }
}
