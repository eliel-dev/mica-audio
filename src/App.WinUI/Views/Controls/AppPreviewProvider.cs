using App.WinUI.Models.Apps;

namespace App.WinUI.Views.Controls;

internal sealed class AppPreviewProvider : IAppPreviewProvider
{
    public AppPreviewProvider(IAppPreviewRenderer renderer, params string[] supportedKinds)
    {
        Renderer = renderer;
        SupportedKinds = supportedKinds;
    }

    public IReadOnlyList<string> SupportedKinds { get; }

    public IAppPreviewRenderer Renderer { get; }

    public bool CanHandle(AppCatalogItem item)
    {
        return !string.IsNullOrWhiteSpace(item.Preview?.Kind)
            && SupportedKinds.Any(supported => string.Equals(supported, item.Preview?.Kind, StringComparison.OrdinalIgnoreCase));
    }
}
