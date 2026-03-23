namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-startup-estavel-e-observabilidade-real
// DOCS: docs/wiki/modules/app-winui.md#studio-de-visualizacoes
internal sealed class ShellPageContentFactory
{
    private readonly Func<object> createMainPage;
    private readonly Func<object> createDevicesPage;
    private readonly Func<object> createPanelsPage;
    private readonly Func<object> createMonitoringPage;
    private readonly Func<object> createSettingsPage;
    private readonly Func<object> createVisualizerStudioPage;

    private object? mainPage;
    private object? devicesPage;
    private object? panelsPage;
    private object? monitoringPage;
    private object? settingsPage;
    private object? visualizerStudioPage;

    internal ShellPageContentFactory(
        Func<object> createMainPage,
        Func<object> createDevicesPage,
        Func<object> createPanelsPage,
        Func<object> createMonitoringPage,
        Func<object> createSettingsPage,
        Func<object> createVisualizerStudioPage)
    {
        this.createMainPage = createMainPage;
        this.createDevicesPage = createDevicesPage;
        this.createPanelsPage = createPanelsPage;
        this.createMonitoringPage = createMonitoringPage;
        this.createSettingsPage = createSettingsPage;
        this.createVisualizerStudioPage = createVisualizerStudioPage;
    }

    internal bool TryResolve(string tag, out object? page, out Exception? exception)
    {
        try
        {
            page = Resolve(tag);
            exception = null;
            return true;
        }
        catch (Exception ex)
        {
            page = null;
            exception = ex;
            return false;
        }
    }

    internal object Resolve(string tag)
    {
        return NormalizeTag(tag) switch
        {
            "devices" => devicesPage ??= createDevicesPage(),
            "panels" => panelsPage ??= createPanelsPage(),
            "monitoring" => monitoringPage ??= createMonitoringPage(),
            "settings" => settingsPage ??= createSettingsPage(),
            "visualizer-studio" => visualizerStudioPage ??= createVisualizerStudioPage(),
            "visualizer-editor" => visualizerStudioPage ??= createVisualizerStudioPage(),
            _ => mainPage ??= createMainPage(),
        };
    }

    private static string NormalizeTag(string tag)
        => string.IsNullOrWhiteSpace(tag) ? "visualizer" : tag.Trim().ToLowerInvariant();
}
