using App.WinUI.Models.Apps;

namespace App.WinUI.Views.Controls;

internal interface IAppPreviewProvider
{
    IReadOnlyList<string> SupportedKinds { get; }

    IAppPreviewRenderer Renderer { get; }

    bool CanHandle(AppCatalogItem item)
    {
        var kind = item.Preview?.Kind;
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return SupportedKinds.Any(supported => string.Equals(supported, kind, StringComparison.OrdinalIgnoreCase));
    }
}
