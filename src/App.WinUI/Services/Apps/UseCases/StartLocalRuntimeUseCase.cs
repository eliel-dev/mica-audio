using App.WinUI.Models.Apps;
using App.WinUI.Services.Gif;
using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Apps.UseCases;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#fluxo-de-execucao
internal sealed class StartLocalRuntimeUseCase
{
    private const string GifAppId = "gifhub75";
    private readonly GifCatalogAppRuntimeService gifRuntimeService;
    private CancellationTokenSource? runtimeCts;

    public StartLocalRuntimeUseCase(GifCatalogAppRuntimeService gifRuntimeService)
    {
        this.gifRuntimeService = gifRuntimeService;
        LastStatus = "Selecione o app GIF para iniciar.";
    }

    public bool IsBusy { get; private set; }

    public string LastStatus { get; private set; }

    public string? SessionFilePath { get; private set; }

    public static bool IsGifApp(AppCatalogItem? item)
        => item is not null && string.Equals(item.Id, GifAppId, StringComparison.OrdinalIgnoreCase);

    public void SetSessionFilePath(string? filePath)
    {
        SessionFilePath = filePath;
    }

    public async Task EvaluateAutostartAsync(bool pendingAutostart, AppCatalogItem? selectedItem, IReadOnlyDictionary<string, string> values)
    {
        if (!IsGifApp(selectedItem))
        {
            return;
        }

        if (!pendingAutostart)
        {
            SetStatus("App GIF selecionado. O runtime inicia apenas em clique manual no card.");
            return;
        }

        await StartAsync(selectedItem, values).ConfigureAwait(false);
    }

    public async Task StartAsync(AppCatalogItem? selectedItem, IReadOnlyDictionary<string, string> values)
    {
        if (!IsGifApp(selectedItem))
        {
            return;
        }

        values.TryGetValue("sourceMode", out var sourceModeRaw);
        values.TryGetValue("gifUrl", out var gifUrl);
        values.TryGetValue("scaleMode", out var scaleModeRaw);

        var sourceMode = string.Equals(sourceModeRaw, "file", StringComparison.OrdinalIgnoreCase) ? "file" : "url";
        var scaleMode = ParseScaleMode(scaleModeRaw);

        CancellationToken cancellationToken;
        try
        {
            cancellationToken = BeginRequest();
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        IsBusy = true;

        try
        {
            if (sourceMode == "file")
            {
                if (string.IsNullOrWhiteSpace(SessionFilePath))
                {
                    SetStatus("Modo arquivo ativo. Clique em 'Abrir arquivo GIF' para iniciar.");
                    return;
                }

                await gifRuntimeService.StartFromFileAsync(SessionFilePath, scaleMode, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (!Uri.TryCreate(gifUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                SetStatus("Modo URL ativo. Informe uma URL direta http/https e clique em Salvar.");
                return;
            }

            await gifRuntimeService.StartFromUrlAsync(uri.ToString(), scaleMode, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetStatus($"Erro no runtime GIF: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Stop()
    {
        CancelRequest();
        IsBusy = false;
        gifRuntimeService.Stop();
    }

    public RgbaColor[] GetLatestFrame() => gifRuntimeService.GetLatestFrame();

    public void SetStatus(string status)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            LastStatus = status;
        }
    }

    private CancellationToken BeginRequest()
    {
        CancelRequest();
        runtimeCts = new CancellationTokenSource();
        return runtimeCts.Token;
    }

    private void CancelRequest()
    {
        if (runtimeCts is null)
        {
            return;
        }

        runtimeCts.Cancel();
        runtimeCts.Dispose();
        runtimeCts = null;
    }

    private static GifScaleMode ParseScaleMode(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "fill" => GifScaleMode.Fill,
            "stretch" => GifScaleMode.Stretch,
            _ => GifScaleMode.Fit,
        };
    }
}
