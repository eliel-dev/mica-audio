using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#modulo-apps-catalog-and-deployment
internal interface IAppModifierStateStore
{
    Task LoadAsync(CancellationToken cancellationToken = default);

    Task<AppConfigDraft?> GetDraftAsync(string deviceId, string appId, CancellationToken cancellationToken = default);

    Task SetDraftAsync(string deviceId, string appId, AppConfigDraft draft, CancellationToken cancellationToken = default);

    Task ClearDraftAsync(string deviceId, string appId, CancellationToken cancellationToken = default);
}


