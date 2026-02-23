using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Apps;

internal interface IAppRuntimeProvider : IDisposable
{
    IReadOnlyList<string> SupportedKinds { get; }

    bool CanHandle(AppCatalogItem item)
    {
        var runtimeKind = item.Runtime?.Kind;
        if (string.IsNullOrWhiteSpace(runtimeKind))
        {
            return false;
        }

        return SupportedKinds.Any(kind => string.Equals(kind, runtimeKind, StringComparison.OrdinalIgnoreCase));
    }

    void Attach(AppRuntimeHost host);

    void OnSelected(AppCatalogItem item);

    Task OnConfigSavedAsync(AppCatalogItem item, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);

    void OnDeselected(AppCatalogItem item);
}
