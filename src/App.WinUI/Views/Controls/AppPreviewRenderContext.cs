using App.WinUI.Models.Apps;
using Microsoft.Graphics.Canvas;
using MicaAudio.Core.Presets;

namespace App.WinUI.Views.Controls;

internal readonly ref struct AppPreviewRenderContext
{
    public AppPreviewRenderContext(
        CanvasDrawingSession drawingSession,
        float width,
        float height,
        float time,
        AppCatalogItem item,
        bool isSelected,
        IReadOnlyDictionary<string, string> configValues,
        IReadOnlyList<RgbaColor>? runtimeFrame)
    {
        DrawingSession = drawingSession;
        Width = width;
        Height = height;
        Time = time;
        Item = item;
        IsSelected = isSelected;
        ConfigValues = configValues;
        RuntimeFrame = runtimeFrame;
    }

    public CanvasDrawingSession DrawingSession { get; }

    public float Width { get; }

    public float Height { get; }

    public float Time { get; }

    public AppCatalogItem Item { get; }

    public bool IsSelected { get; }

    public IReadOnlyDictionary<string, string> ConfigValues { get; }

    public IReadOnlyList<RgbaColor>? RuntimeFrame { get; }

    public string? GetConfigValue(string key)
    {
        if (ConfigValues.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return null;
    }
}
