using App.WinUI.Models.Apps;
using App.WinUI.Views.Controls.Renderers;

namespace App.WinUI.Views.Controls;

internal static class AppPreviewRendererRegistry
{
    private static readonly IReadOnlyDictionary<string, IAppPreviewRenderer> ByKind = new Dictionary<string, IAppPreviewRenderer>(StringComparer.OrdinalIgnoreCase)
    {
        ["clock"] = new ClockPreviewRenderer(),
        ["weather"] = new WeatherPreviewRenderer(),
        ["scores"] = new ScoresPreviewRenderer(),
        ["news"] = new NewsTickerPreviewRenderer(),
        ["productivity"] = new ProductivityPreviewRenderer(),
        ["finance"] = new FinancePreviewRenderer(),
        ["decorative"] = new DecorativePreviewRenderer(),
        ["generic"] = new DecorativePreviewRenderer(),
        ["relogio"] = new ClockPreviewRenderer(),
        ["relógio"] = new ClockPreviewRenderer(),
        ["clima"] = new WeatherPreviewRenderer(),
    };

    public static IAppPreviewRenderer Resolve(AppCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var previewKind = item.Preview?.Kind;
        if (!string.IsNullOrWhiteSpace(previewKind) && ByKind.TryGetValue(previewKind, out var byKind))
        {
            return byKind;
        }

        if (ByKind.TryGetValue(item.Category, out var byCategory))
        {
            return byCategory;
        }

        if (string.Equals(item.Id, "analogclock", StringComparison.OrdinalIgnoreCase))
        {
            return ByKind["clock"];
        }

        if (string.Equals(item.Id, "accuweather", StringComparison.OrdinalIgnoreCase))
        {
            return ByKind["weather"];
        }

        return ByKind["generic"];
    }
}
