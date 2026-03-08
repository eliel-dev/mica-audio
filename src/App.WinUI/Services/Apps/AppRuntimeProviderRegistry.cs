using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Apps;

internal sealed class AppRuntimeProviderRegistry : IDisposable
{
    private readonly IReadOnlyList<IAppRuntimeProvider> providers;

    public AppRuntimeProviderRegistry(IReadOnlyList<IAppRuntimeProvider> providers)
    {
        this.providers = providers;
    }

    public IReadOnlyList<IAppRuntimeProvider> Providers => providers;

    public IAppRuntimeProvider? Resolve(AppCatalogItem item)
    {
        return providers.FirstOrDefault(provider => provider.CanHandle(item));
    }

    public void Dispose()
    {
        foreach (var provider in providers)
        {
            provider.Dispose();
        }
    }
}
