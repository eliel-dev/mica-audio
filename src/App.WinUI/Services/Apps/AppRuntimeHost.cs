using App.WinUI.Services.Gif;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Apps;

internal sealed class AppRuntimeHost
{
    public required Button OpenFileButton { get; init; }

    public required Button GiphyButton { get; init; }

    public required GiphySearchService GiphySearchService { get; init; }

    public required Func<XamlRoot?> GetXamlRoot { get; init; }

    public required DispatcherQueue DispatcherQueue { get; init; }

    public required GifCatalogAppRuntimeService GifRuntimeService { get; init; }

    public required Func<Task<Windows.Storage.StorageFile?>> PickImageFileAsync { get; init; }

    public required Func<Task<Windows.Storage.StorageFolder?>> PickImageFolderAsync { get; init; }

    public required Func<GifScaleMode?> ResolveScaleMode { get; init; }

    public required Func<Task<IReadOnlyDictionary<string, string>>> ResolveCurrentValuesAsync { get; init; }

    public required Action<RgbaColor[]> UpdateFrame { get; init; }

    public required Action<string> SetStatus { get; init; }
}
