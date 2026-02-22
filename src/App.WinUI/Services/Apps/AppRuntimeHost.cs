using App.WinUI.Services.Gif;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Apps;

internal sealed class AppRuntimeHost
{
    public required Border RuntimePanel { get; init; }

    public required TextBlock RuntimeStatusText { get; init; }

    public required Button OpenFileButton { get; init; }

    public required TextBlock FirmwareWarningText { get; init; }

    public required Microsoft.Graphics.Canvas.UI.Xaml.CanvasControl RuntimeCanvas { get; init; }

    public required DispatcherQueue DispatcherQueue { get; init; }

    public required GifCatalogAppRuntimeService GifRuntimeService { get; init; }

    public required Func<Task<Windows.Storage.StorageFile?>> PickGifFileAsync { get; init; }

    public required Func<GifScaleMode?> ResolveScaleMode { get; init; }

    public required Func<Task<IReadOnlyDictionary<string, string>>> ResolveCurrentValuesAsync { get; init; }

    public required Func<Task> PersistCurrentValuesAsync { get; init; }

    public required Action<RgbaColor[]> UpdateFrame { get; init; }

    public required Action<string> SetStatus { get; init; }
}
