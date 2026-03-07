using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-startup-estavel-e-observabilidade-real
internal static class AppFailureViewFactory
{
    internal static ScrollViewer Build(
        string title,
        string message,
        Exception exception,
        string logPath)
    {
        var panel = new StackPanel
        {
            Padding = new Thickness(20),
            Spacing = 10,
        };

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 20,
        });

        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"Log de erro: {logPath}",
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"{exception.GetType().Name}: {exception.Message}",
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        return new ScrollViewer
        {
            Content = panel,
        };
    }
}
