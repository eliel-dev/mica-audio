using App.WinUI.Models.Apps;
using Device.Protocol.Models;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#modulo-apps-catalog-and-deployment
internal interface IAppDeploymentService
{
    Task<CommandDispatchResult> InstallAsync(string deviceId, AppCatalogItem item, string? configJson = null, CancellationToken cancellationToken = default);

    Task<CommandDispatchResult> ActivateAsync(string deviceId, AppCatalogItem item, CancellationToken cancellationToken = default);

    Task<CommandDispatchResult> SetConfigAsync(string deviceId, AppCatalogItem item, string configJson, CancellationToken cancellationToken = default);
}
