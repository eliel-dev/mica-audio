using Microsoft.Extensions.Caching.Hybrid;

namespace App.WinUI.Infrastructure.Cache;

// DOCS: docs/wiki/modules/app-winui.md#cache-compartilhado
// DOCS: docs/wiki/modules/apps-catalog-deployment.md#cache-compartilhado
internal static class AppCacheKeys
{
    public const string AppCatalog = "apps:catalog:effective";

    internal static HybridCacheEntryOptions AppCatalogOptions { get; } = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        LocalCacheExpiration = TimeSpan.FromMinutes(10),
    };
}
