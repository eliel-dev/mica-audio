using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

internal static class UiResourceResolver
{
    public static Brush ResolveBrush(string key, Color fallback)
    {
        if (Application.Current?.Resources is ResourceDictionary resources &&
            TryResolveBrush(resources, key, out var brush))
        {
            return brush;
        }

        return new SolidColorBrush(fallback);
    }

    private static bool TryResolveBrush(ResourceDictionary dictionary, string key, out Brush brush)
    {
        if (TryResolveBrushFromDictionary(dictionary, key, out brush))
        {
            return true;
        }

        if (TryResolveBrushFromThemeDictionary(dictionary, key, out brush))
        {
            return true;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (TryResolveBrush(merged, key, out brush))
            {
                return true;
            }
        }

        brush = null!;
        return false;
    }

    private static bool TryResolveBrushFromDictionary(ResourceDictionary dictionary, string key, out Brush brush)
    {
        if (dictionary.TryGetValue(key, out var value) && value is Brush typedBrush)
        {
            brush = typedBrush;
            return true;
        }

        brush = null!;
        return false;
    }

    private static bool TryResolveBrushFromThemeDictionary(ResourceDictionary dictionary, string key, out Brush brush)
    {
        var themeKey = Application.Current?.RequestedTheme switch
        {
            ApplicationTheme.Dark => "Dark",
            ApplicationTheme.Light => "Light",
            _ => null,
        };

        if (themeKey is not null &&
            dictionary.ThemeDictionaries.TryGetValue(themeKey, out var themedObject) &&
            themedObject is ResourceDictionary themedDictionary &&
            TryResolveBrushFromDictionary(themedDictionary, key, out brush))
        {
            return true;
        }

        if (dictionary.ThemeDictionaries.TryGetValue("Default", out var defaultObject) &&
            defaultObject is ResourceDictionary defaultDictionary &&
            TryResolveBrushFromDictionary(defaultDictionary, key, out brush))
        {
            return true;
        }

        brush = null!;
        return false;
    }
}
