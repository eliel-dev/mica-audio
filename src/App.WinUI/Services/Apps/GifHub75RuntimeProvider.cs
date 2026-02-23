using App.WinUI.Models.Apps;
using App.WinUI.Services.Gif;
using Microsoft.UI.Xaml;
using MicaAudio.Core.Presets;
using System.Runtime.InteropServices;

namespace App.WinUI.Services.Apps;

internal sealed class GifHub75RuntimeProvider : IAppRuntimeProvider
{
    private AppRuntimeHost? host;
    private string? sessionFilePath;
    private CancellationTokenSource? requestCts;
    private bool frameSubscriptionActive;
    private bool isStoppingOrDeselected;
    private bool disposed;

    public IReadOnlyList<string> SupportedKinds => ["gifhub75"];

    public void Attach(AppRuntimeHost runtimeHost)
    {
        host = runtimeHost;
        host.GifRuntimeService.StatusChanged += OnStatusChanged;
        host.OpenFileButton.Click += OnOpenFileClicked;
    }

    public void OnSelected(AppCatalogItem item)
    {
        if (host is null)
        {
            return;
        }

        isStoppingOrDeselected = false;
        SetFrameSubscription(enabled: true);
        host.RuntimePanel.Visibility = Visibility.Visible;
        host.RuntimeStatusText.Text = "App GIF selecionado. O runtime inicia apenas em clique manual no card.";
        SafeInvalidateRuntimeCanvas();
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

        isStoppingOrDeselected = false;
        SafeInvalidateRuntimeCanvas();

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
            SafeInvalidateRuntimeCanvas();
        }
    }

    public void OnDeselected(AppCatalogItem item)
    {
        if (host is null)
        {
            return;
        }

        isStoppingOrDeselected = true;
        SetFrameSubscription(enabled: false);
        CancelRequest();

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

        host.UpdateFrame(host.GifRuntimeService.GetLatestFrame());
        host.RuntimePanel.Visibility = Visibility.Collapsed;
        SafeInvalidateRuntimeCanvas();
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        isStoppingOrDeselected = true;
        CancelRequest();
        if (host is not null)
        {
            SetFrameSubscription(enabled: false);
            host.OpenFileButton.Click -= OnOpenFileClicked;
            host.GifRuntimeService.StatusChanged -= OnStatusChanged;
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
        if (host is null || disposed || isStoppingOrDeselected)
        {
            return;
        }

        host.UpdateFrame(frame.ToArray());
        SafeInvalidateRuntimeCanvas();
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

    private void SetFrameSubscription(bool enabled)
    {
        if (host is null)
        {
            return;
        }

        if (enabled && !frameSubscriptionActive)
        {
            host.GifRuntimeService.FrameUpdated += OnFrameUpdated;
            frameSubscriptionActive = true;
            return;
        }

        if (!enabled && frameSubscriptionActive)
        {
            host.GifRuntimeService.FrameUpdated -= OnFrameUpdated;
            frameSubscriptionActive = false;
        }
    }

    private void SafeInvalidateRuntimeCanvas()
    {
        if (host is null)
        {
            return;
        }

        try
        {
            if (host.DispatcherQueue.HasThreadAccess)
            {
                host.RuntimeCanvas.Invalidate();
                return;
            }

            _ = host.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    host.RuntimeCanvas.Invalidate();
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
            });
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
}

