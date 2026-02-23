using App.WinUI.Models.Apps;
using App.WinUI.Services.Gif;
using Microsoft.UI.Xaml;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Apps;

internal sealed class GifHub75RuntimeProvider : IAppRuntimeProvider
{
    private AppRuntimeHost? host;
    private string? sessionFilePath;
    private CancellationTokenSource? requestCts;
    private bool runtimeBusy;
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

        host.RuntimePanel.Visibility = Visibility.Visible;
        host.RuntimeStatusText.Text = "App GIF selecionado. O runtime inicia apenas em clique manual no card.";
        host.RuntimeCanvas.Invalidate();
    }

    public async Task OnConfigSavedAsync(AppCatalogItem item, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        if (host is null)
        {
            return;
        }

        var sourceMode = values.TryGetValue("sourceMode", out var rawSource) ? rawSource.Trim().ToLowerInvariant() : "url";
        var gifUrl = values.TryGetValue("gifUrl", out var rawUrl) ? rawUrl.Trim() : string.Empty;

        CancellationToken linked;
        try
        {
            linked = BeginRequest(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        runtimeBusy = true;
        host.DispatcherQueue.TryEnqueue(host.RuntimeCanvas.Invalidate);

        try
        {
            var scale = host.ResolveScaleMode() ?? GifScaleMode.Fit;
            if (sourceMode == "file")
            {
                if (string.IsNullOrWhiteSpace(sessionFilePath))
                {
                    host.SetStatus("Modo arquivo ativo. Clique em 'Abrir arquivo GIF' para iniciar.");
                    return;
                }

                await host.GifRuntimeService.StartFromFileAsync(sessionFilePath, scale, linked).ConfigureAwait(false);
                return;
            }

            if (!Uri.TryCreate(gifUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                host.SetStatus("Modo URL ativo. Informe uma URL direta http/https e clique em Salvar.");
                return;
            }

            await host.GifRuntimeService.StartFromUrlAsync(uri.ToString(), scale, linked).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            host.SetStatus($"Erro no runtime GIF: {ex.Message}");
        }
        finally
        {
            runtimeBusy = false;
            host.DispatcherQueue.TryEnqueue(host.RuntimeCanvas.Invalidate);
        }
    }

    public void OnDeselected(AppCatalogItem item)
    {
        if (host is null)
        {
            return;
        }

        CancelRequest();
        runtimeBusy = false;
        host.GifRuntimeService.Stop();
        host.UpdateFrame(host.GifRuntimeService.GetLatestFrame());
        host.RuntimePanel.Visibility = Visibility.Collapsed;
        host.DispatcherQueue.TryEnqueue(host.RuntimeCanvas.Invalidate);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelRequest();
        if (host is not null)
        {
            host.OpenFileButton.Click -= OnOpenFileClicked;
            host.GifRuntimeService.StatusChanged -= OnStatusChanged;
            host.GifRuntimeService.FrameUpdated -= OnFrameUpdated;
            host.GifRuntimeService.Stop();
        }
    }

    private async void OnOpenFileClicked(object sender, RoutedEventArgs e)
    {
        if (host is null)
        {
            return;
        }

        var file = await host.PickGifFileAsync().ConfigureAwait(true);
        if (file is null)
        {
            return;
        }

        sessionFilePath = file.Path;
        host.SetStatus($"Arquivo selecionado: {file.Name}");
        var values = await host.ResolveCurrentValuesAsync().ConfigureAwait(false);
        await OnConfigSavedAsync(new AppCatalogItem { Runtime = new AppRuntimeDefinition { Kind = "gifhub75" } }, values, CancellationToken.None).ConfigureAwait(false);
    }

    private void OnStatusChanged(object? sender, string status)
    {
        host?.SetStatus(status);
    }

    private void OnFrameUpdated(object? sender, RgbaColor[] frame)
    {
        if (host is null)
        {
            return;
        }

        host.UpdateFrame(frame.ToArray());
        host.DispatcherQueue.TryEnqueue(host.RuntimeCanvas.Invalidate);
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
}


