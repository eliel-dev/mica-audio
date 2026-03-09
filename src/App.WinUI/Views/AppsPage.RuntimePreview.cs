using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Gif;
using MicaAudio.Core.Presets;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace App.WinUI.Views;

// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
public sealed partial class AppsPage
{
    private void SetRuntimeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        _ = DispatcherQueue.TryEnqueue(() => AppendLog(status));
    }

    private void OnGifRuntimeFrameUpdated(RgbaColor[] frame)
    {
        if (frame.Length == 0)
        {
            return;
        }

        var snapshot = frame.ToArray();
        _ = DispatcherQueue.TryEnqueue(() => ApplyGifRuntimeFrameToCards(snapshot));
    }

    private void ApplyGifRuntimeFrameToCards(RgbaColor[]? frame)
    {
        latestGifRuntimeFrame = frame;
        foreach (var card in catalogCards.Where(static card => string.Equals(card.Item.Id, GifAppId, StringComparison.OrdinalIgnoreCase)))
        {
            card.SetRuntimeFrame(frame);
        }
    }

    private async Task EvaluateRuntimeAutostartAsync()
    {
        if (activeRuntimeProvider is null || selectedItem is null)
        {
            pendingRuntimeAutostart = false;
            return;
        }

        activeRuntimeProvider.OnSelected(selectedItem);
        if (!pendingRuntimeAutostart)
        {
            return;
        }

        pendingRuntimeAutostart = false;
        var values = await ResolveRuntimeValuesAsync().ConfigureAwait(false);
        await activeRuntimeProvider.OnConfigSavedAsync(selectedItem, values, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task EnsureGifRuntimeWhileAppsPageIsVisibleAsync()
    {
        var gifItem = allItems.FirstOrDefault(static item => string.Equals(item.Id, GifAppId, StringComparison.OrdinalIgnoreCase));
        if (gifItem is null)
        {
            return;
        }

        var provider = runtimeProviderRegistry.Resolve(gifItem);
        if (provider is null)
        {
            return;
        }

        var values = await ResolveStoredValuesAsync(gifItem).ConfigureAwait(false);
        if (!ShouldAutoStartGifRuntime(values))
        {
            return;
        }

        await provider.OnConfigSavedAsync(gifItem, values, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveRuntimeValuesAsync()
    {
        var item = selectedItem;
        if (item is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (TryBuildConfigFromEditor(item, out _, out var rawValues, out _))
        {
            return rawValues;
        }

        return await ResolveStoredValuesAsync(item).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveStoredValuesAsync(AppCatalogItem item)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in item.Modifiers.Where(static modifier => modifier.IsValid()))
        {
            values[modifier.Key] = modifier.Type == AppModifierFieldType.Toggle
                ? (modifier.DefaultToggle is true ? "true" : "false")
                : (modifier.DefaultValue ?? string.Empty);
        }

        var draft = await modifierStore.GetDraftAsync(LocalDraftScope, item.Id).ConfigureAwait(false);
        if (draft?.Values is not null)
        {
            foreach (var pair in draft.Values)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return WeatherAppFixedLocation.NormalizeRawValues(item, values);
    }

    private static bool ShouldAutoStartGifRuntime(IReadOnlyDictionary<string, string> values) => false;

    private void AttachRuntimeProviders()
    {
        var host = new AppRuntimeHost
        {
            OpenFileButton = GifOpenFileButton,
            DispatcherQueue = DispatcherQueue,
            GifRuntimeService = gifRuntimeService,
            PickImageFileAsync = PickImageFileAsync,
            PickImageFolderAsync = PickImageFolderAsync,
            ResolveScaleMode = () => selectedItem is null || !TryBuildConfigFromEditor(selectedItem, out _, out var values, out _)
                ? GifScaleMode.Fit
                : ParseGifScaleMode(values.TryGetValue("scaleMode", out var mode) ? mode : null),
            ResolveCurrentValuesAsync = ResolveRuntimeValuesAsync,
            UpdateFrame = OnGifRuntimeFrameUpdated,
            SetStatus = SetRuntimeStatus,
        };

        foreach (var provider in runtimeProviderRegistry.Providers)
        {
            provider.Attach(host);
        }
    }

    private static GifScaleMode ParseGifScaleMode(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "fill" => GifScaleMode.Fill,
            "stretch" => GifScaleMode.Stretch,
            _ => GifScaleMode.Fit,
        };
    }

    private static async Task<StorageFile?> PickImageFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        foreach (var ext in new[] { ".gif", ".png", ".jpg", ".jpeg", ".bmp" })
        {
            picker.FileTypeFilter.Add(ext);
        }

        if (App.MainWindow is not null)
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return await picker.PickSingleFileAsync();
    }

    private static async Task<Windows.Storage.StorageFolder?> PickImageFolderAsync()
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        picker.FileTypeFilter.Add("*");

        if (App.MainWindow is not null)
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return await picker.PickSingleFolderAsync();
    }

    private void OnTargetDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateGifOpenFileButtonVisibility();
        UpdateActionButtonsEnabled();
    }

    private bool IsGifFileModeSelected()
    {
        return string.Equals(selectedItem?.Id, GifAppId, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateGifOpenFileButtonVisibility()
    {
        var visible = IsGifFileModeSelected();
        GifOpenFileButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        if (visible)
        {
            GifOpenFileButton.Content = IsSlideshow() ? "Selecionar Pasta" : "Selecionar Arquivo";
        }
    }

    private bool IsSlideshow()
    {
        if (!modifierBindings.TryGetValue("sourceType", out var b)
            || b.Control is not ComboBox combo
            || combo.SelectedItem is not ComboBoxItem item
            || item.Tag is not string mode)
        {
            return false;
        }

        return string.Equals(mode, "slideshow", StringComparison.OrdinalIgnoreCase);
    }
}
