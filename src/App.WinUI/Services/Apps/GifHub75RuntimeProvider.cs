using App.WinUI.Models.Apps;
using App.WinUI.Services.Gif;
using MicaAudio.Core.Presets;
using System.Runtime.InteropServices;

namespace App.WinUI.Services.Apps;

internal sealed class GifHub75RuntimeProvider : IAppRuntimeProvider
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gif", ".png", ".jpg", ".jpeg", ".bmp" };

    private AppRuntimeHost? host;
    private List<string> sessionPaths = [];
    private CancellationTokenSource? requestCts;
    private bool disposed;

    public IReadOnlyList<string> SupportedKinds => ["gifhub75"];

    public void Attach(AppRuntimeHost runtimeHost)
    {
        host = runtimeHost;
        host.GifRuntimeService.StatusChanged += OnStatusChanged;
        host.GifRuntimeService.FrameUpdated += OnFrameUpdated;
        host.OpenFileButton.Click += OnOpenFileClicked;
    }

    public void OnSelected(AppCatalogItem item)
    {
        if (host is null)
        {
            return;
        }

        host.SetStatus("App GIF selecionado. Configure e salve para atualizar a miniatura.");
    }

    public async Task OnConfigSavedAsync(AppCatalogItem item, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        if (host is null)
        {
            return;
        }

        CancellationToken linked;
        try
        {
            linked = BeginRequest(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            var scale = host.ResolveScaleMode() ?? GifScaleMode.Fit;
            var isSlideshow = IsSlideshow(values);

            if (sessionPaths.Count == 0)
            {
                host.SetStatus(isSlideshow
                    ? "Clique em 'Selecionar Pasta' para iniciar."
                    : "Clique em 'Selecionar Arquivo' para iniciar.");
                return;
            }

            if (!isSlideshow)
            {
                await host.GifRuntimeService.StartFromFileAsync(sessionPaths[0], scale, linked).ConfigureAwait(false);
                return;
            }

            var intervalMs = ParseIntervalMs(values);
            var shuffle = ParseShuffle(values);
            _ = RunSlideshowAsync(sessionPaths.ToList(), scale, intervalMs, shuffle, linked);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            host.SetStatus($"Erro no runtime GIF: {ex.Message}");
        }
    }

    public void OnDeselected(AppCatalogItem item)
    {
        // Mantém runtime ativo enquanto a aba Apps estiver aberta.
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelRequest();

        if (host is null)
        {
            return;
        }

        host.OpenFileButton.Click -= OnOpenFileClicked;
        host.GifRuntimeService.StatusChanged -= OnStatusChanged;
        host.GifRuntimeService.FrameUpdated -= OnFrameUpdated;

        try
        {
            host.GifRuntimeService.Stop();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidCastException)
        {
        }
        catch (COMException)
        {
        }
    }

    private async void OnOpenFileClicked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        if (host is null)
        {
            return;
        }

        var values = await host.ResolveCurrentValuesAsync().ConfigureAwait(true);
        var isSlideshow = IsSlideshow(values);

        if (isSlideshow)
        {
            var folder = await host.PickImageFolderAsync().ConfigureAwait(true);
            if (folder is null)
            {
                return;
            }

            var files = await folder.GetFilesAsync();
            sessionPaths = files
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f.Path)))
                .Select(f => f.Path)
                .ToList();

            if (sessionPaths.Count == 0)
            {
                host.SetStatus("Nenhuma imagem encontrada na pasta selecionada.");
                return;
            }

            host.SetStatus($"Pasta selecionada: {sessionPaths.Count} imagens.");
        }
        else
        {
            var file = await host.PickImageFileAsync().ConfigureAwait(true);
            if (file is null)
            {
                return;
            }

            sessionPaths = [file.Path];
            host.SetStatus($"Arquivo selecionado: {file.Name}");
        }

        await OnConfigSavedAsync(
            new AppCatalogItem { Runtime = new AppRuntimeDefinition { Kind = "gifhub75" } },
            values,
            CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RunSlideshowAsync(
        List<string> paths, GifScaleMode scale, int intervalMs, bool shuffle, CancellationToken ct)
    {
        if (shuffle)
        {
            paths = [.. paths.OrderBy(_ => Guid.NewGuid())];
        }

        var idx = 0;
        while (!ct.IsCancellationRequested)
        {
            var path = paths[idx % paths.Count];
            idx++;
            try
            {
                await host!.GifRuntimeService.StartFromFileAsync(path, scale, ct).ConfigureAwait(false);
                await Task.Delay(intervalMs, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                host?.SetStatus($"Erro em '{Path.GetFileName(path)}': {ex.Message}");
                try
                {
                    await Task.Delay(2000, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private void OnStatusChanged(object? sender, string status)
    {
        host?.SetStatus(status);
    }

    private void OnFrameUpdated(object? sender, RgbaColor[] frame)
    {
        if (host is null || disposed)
        {
            return;
        }

        host.UpdateFrame(frame.ToArray());
    }

    private CancellationToken BeginRequest(CancellationToken outer)
    {
        CancelRequest();
        requestCts = CancellationTokenSource.CreateLinkedTokenSource(outer);
        return requestCts.Token;
    }

    private void CancelRequest()
    {
        requestCts?.Cancel();
        requestCts?.Dispose();
        requestCts = null;
    }

    private static bool IsSlideshow(IReadOnlyDictionary<string, string> values)
    {
        values.TryGetValue("sourceType", out var v);
        return string.Equals(v, "slideshow", StringComparison.OrdinalIgnoreCase);
    }

    private static int ParseIntervalMs(IReadOnlyDictionary<string, string> values)
    {
        values.TryGetValue("slideshowInterval", out var v);
        return (v?.Trim().ToLowerInvariant()) switch
        {
            "5s" => 5_000,
            "30s" => 30_000,
            "1min" => 60_000,
            "5min" => 300_000,
            "10min" => 600_000,
            _ => 10_000,
        };
    }

    private static bool ParseShuffle(IReadOnlyDictionary<string, string> values)
    {
        values.TryGetValue("slideshowShuffle", out var v);
        return bool.TryParse(v, out var b) && b;
    }
}
