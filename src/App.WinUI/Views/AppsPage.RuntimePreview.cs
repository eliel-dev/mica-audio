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

        return values;
    }

    private static bool ShouldAutoStartGifRuntime(IReadOnlyDictionary<string, string> values)
    {
        var sourceMode = values.TryGetValue("sourceMode", out var rawSource)
            ? rawSource.Trim().ToLowerInvariant()
            : "url";
        if (sourceMode == "file")
        {
            return false;
        }

        if (!values.TryGetValue("gifUrl", out var rawUrl))
        {
            return false;
        }

        var url = rawUrl.Trim();
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    private void AttachRuntimeProviders()
    {
        var host = new AppRuntimeHost
        {
            OpenFileButton = GifOpenFileButton,
            DispatcherQueue = DispatcherQueue,
            GifRuntimeService = gifRuntimeService,
            PickGifFileAsync = PickGifFileAsync,
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

    private static async Task<StorageFile?> PickGifFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        picker.FileTypeFilter.Add(".gif");

        if (App.MainWindow is not null)
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return await picker.PickSingleFileAsync();
    }

    private void OnTargetDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateGifOpenFileButtonVisibility();
        UpdateActionButtonsEnabled();
    }

    private void OnGifSourceModeSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateGifOpenFileButtonVisibility();
    }

    private bool IsGifFileModeSelected()
    {
        if (!string.Equals(selectedItem?.Id, GifAppId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!modifierBindings.TryGetValue("sourceMode", out var binding)
            || binding.Control is not ComboBox combo
            || combo.SelectedItem is not ComboBoxItem selected
            || selected.Tag is not string mode)
        {
            return false;
        }

        return string.Equals(mode, "file", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateGifOpenFileButtonVisibility()
    {
        var visible = IsGifFileModeSelected();
        GifOpenFileButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
}
