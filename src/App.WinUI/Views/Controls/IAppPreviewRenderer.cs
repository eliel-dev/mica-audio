namespace App.WinUI.Views.Controls;

internal interface IAppPreviewRenderer
{
    string Kind { get; }

    void Draw(in AppPreviewRenderContext context);
}
