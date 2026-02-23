using App.WinUI.Models.Apps;
using App.WinUI.Views.Controls.Renderers;

namespace App.WinUI.Views.Controls;

internal static class AppPreviewRendererRegistry
{
    private static readonly IAppPreviewRenderer GenericRenderer = new DecorativePreviewRenderer();

    private static readonly IReadOnlyList<IAppPreviewProvider> Providers =
    [
        new AppPreviewProvider(new ClockPreviewRenderer(), "clock", "relogio", "relógio"),
        new AppPreviewProvider(new WeatherPreviewRenderer(), "weather", "clima"),
        new AppPreviewProvider(new ScoresPreviewRenderer(), "scores"),
        new AppPreviewProvider(new NewsTickerPreviewRenderer(), "news"),
        new AppPreviewProvider(new ProductivityPreviewRenderer(), "productivity"),
        new AppPreviewProvider(new FinancePreviewRenderer(), "finance"),
        new AppPreviewProvider(new GifPreviewRenderer(), "gif", "midia"),
        new AppPreviewProvider(GenericRenderer, "decorative", "generic"),
    ];

    public static IAppPreviewRenderer Resolve(AppCatalogItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var provider = Providers.FirstOrDefault(candidate => candidate.CanHandle(item));
        return provider?.Renderer ?? GenericRenderer;
    }
}
