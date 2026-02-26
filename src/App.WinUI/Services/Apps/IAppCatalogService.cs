using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#modulo-apps-catalog-and-deployment
internal interface IAppCatalogService
{
    Task<IReadOnlyList<AppCatalogItem>> LoadCatalogAsync(CancellationToken cancellationToken = default);
}


