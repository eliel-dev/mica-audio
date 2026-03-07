namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-startup-estavel-e-observabilidade-real
internal sealed class ShellPageContentFactory
{
    private readonly Func<object> createMainPage;
    private readonly Func<object> createDevicesPage;
    private readonly Func<object> createAppsPage;

    private object? mainPage;
    private object? devicesPage;
    private object? appsPage;

    internal ShellPageContentFactory(
        Func<object> createMainPage,
        Func<object> createDevicesPage,
        Func<object> createAppsPage)
    {
        this.createMainPage = createMainPage;
        this.createDevicesPage = createDevicesPage;
        this.createAppsPage = createAppsPage;
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
            "apps" => appsPage ??= createAppsPage(),
            _ => mainPage ??= createMainPage(),
        };
    }

    private static string NormalizeTag(string tag)
        => string.IsNullOrWhiteSpace(tag) ? "visualizer" : tag.Trim().ToLowerInvariant();
}
