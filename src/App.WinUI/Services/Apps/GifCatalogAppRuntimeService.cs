using System.Net.Http;
using System.Runtime.InteropServices;
using App.WinUI.Services.Gif;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/guides/load-gif-hub75.md#gif-app-runtime-na-loja
internal sealed class GifCatalogAppRuntimeService : IDisposable
{
    public const int TargetFps = 12;
    public const int DownloadTimeoutSeconds = 10;
    public const int MaxDownloadBytes = 25 * 1024 * 1024;

    private const string GifPresetId = "gifhub75";

    private readonly ILedOutput matrixOutput;
    private readonly ILedOutput simulatorOutput;
    private readonly Hub75GifDecoder decoder;
    private readonly Hub75FrameFormatter formatter;
    private readonly Hub75GifPlayer player;
    private readonly HttpClient httpClient;
    private readonly object gate = new();
    private readonly int maxDownloadBytes;
    private readonly int downloadTimeoutSeconds;
    private readonly RgbaColor[] blackFrame = Enumerable.Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray();
    private CancellationTokenSource? loadCts;

    private IReadOnlyList<RgbaColor[]> loadedFrames = Array.Empty<RgbaColor[]>();
    private RgbaColor[] latestFrame;
    private bool disposed;

    public GifCatalogAppRuntimeService(
        ILedOutput matrixOutput,
        ILedOutput simulatorOutput,
        Hub75GifDecoder decoder,
        Hub75FrameFormatter formatter,
        Hub75GifPlayer player,
        HttpClient? httpClient = null,
        int downloadTimeoutSeconds = DownloadTimeoutSeconds,
        int maxDownloadBytes = MaxDownloadBytes,
        float brightness = LedDefaults.Brightness)
    {
        this.matrixOutput = matrixOutput;
        this.simulatorOutput = simulatorOutput;
        this.decoder = decoder;
        this.formatter = formatter;
        this.player = player;
        this.httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        this.downloadTimeoutSeconds = Math.Max(1, downloadTimeoutSeconds);
        this.maxDownloadBytes = Math.Max(1, maxDownloadBytes);
        latestFrame = blackFrame.ToArray();

        var config = new LedOutputConfig
        {
            Width = LedDefaults.MatrixWidth,
            Height = LedDefaults.MatrixHeight,
            Brightness = Math.Clamp(brightness, 0f, 1f),
        };

        matrixOutput.Start(config);
        matrixOutput.SetBrightness(config.Brightness);
        simulatorOutput.Start(config);
        simulatorOutput.SetBrightness(config.Brightness);
        player.FrameReady += OnPlayerFrameReady;
    }

    public event EventHandler<string>? StatusChanged;

    public event EventHandler<RgbaColor[]>? FrameUpdated;

    public int LoadedFrameCount
    {
        get
        {
            lock (gate)
            {
                return loadedFrames.Count;
            }
        }
    }

    public RgbaColor[] GetLatestFrame()
    {
        lock (gate)
        {
            return latestFrame.ToArray();
        }
    }

    public async Task StartFromUrlAsync(string url, GifScaleMode scaleMode, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidDataException("URL invalida: use http/https para um GIF direto.");
        }

        var bytes = await DownloadGifAsync(uri, cancellationToken).ConfigureAwait(false);
        await StartFromBytesCoreAsync(bytes, scaleMode, $"URL: {uri.Host}", cancellationToken).ConfigureAwait(false);
    }

    public async Task StartFromFileAsync(string filePath, GifScaleMode scaleMode, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("Arquivo GIF nao encontrado.", filePath);
        }

        var info = new FileInfo(filePath);
        if (info.Length > maxDownloadBytes)
        {
            throw new InvalidDataException($"Arquivo acima de {maxDownloadBytes / (1024 * 1024)}MB.");
        }

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        await StartFromBytesCoreAsync(bytes, scaleMode, $"Arquivo: {info.Name}", cancellationToken).ConfigureAwait(false);
    }

    public void Stop()
    {
        if (disposed)
        {
            return;
        }

        CancelPendingLoad();
        player.Stop();

        lock (gate)
        {
            loadedFrames = Array.Empty<RgbaColor[]>();
            latestFrame = blackFrame.ToArray();
        }

        SendLegacyBinsClear();
        SendSimulatorFrame(blackFrame);
        StatusChanged?.Invoke(this, "GIF runtime parado.");
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancelPendingLoad();
        player.FrameReady -= OnPlayerFrameReady;
        player.Dispose();
        matrixOutput.Stop();
        simulatorOutput.Stop();
        httpClient.Dispose();
    }

    private async Task StartFromBytesCoreAsync(byte[] gifBytes, GifScaleMode scaleMode, string sourceLabel, CancellationToken cancellationToken)
    {
        CancelPendingLoad();
        loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = loadCts.Token;

        if (gifBytes.Length == 0)
        {
            throw new InvalidDataException("Conteudo GIF vazio.");
        }

        if (gifBytes.Length > maxDownloadBytes)
        {
            throw new InvalidDataException($"Conteudo acima de {maxDownloadBytes / (1024 * 1024)}MB.");
        }

        StatusChanged?.Invoke(this, $"Carregando GIF ({sourceLabel})...");

        var decodedFrames = await Task.Run(() => decoder.Decode(gifBytes, ct), ct).ConfigureAwait(false);
        var formattedFrames = await Task.Run(() =>
        {
            var output = new List<RgbaColor[]>(decodedFrames.Count);
            foreach (var frame in decodedFrames)
            {
                ct.ThrowIfCancellationRequested();
                output.Add(formatter.Format(frame, scaleMode));
            }

            return (IReadOnlyList<RgbaColor[]>)output;
        }, ct).ConfigureAwait(false);

        if (formattedFrames.Count == 0)
        {
            throw new InvalidDataException("GIF sem frames validos.");
        }

        lock (gate)
        {
            loadedFrames = formattedFrames;
            latestFrame = formattedFrames[0].ToArray();
        }

        player.SetFrames(formattedFrames);
        if (!player.Play())
        {
            throw new InvalidOperationException("Nao foi possivel iniciar o player GIF.");
        }

        StatusChanged?.Invoke(this, $"GIF em reproducao ({formattedFrames.Count} frames, {TargetFps} FPS).");
    }

    private async Task<byte[]> DownloadGifAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(downloadTimeoutSeconds));

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                timeoutCts.Token).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long contentLength && contentLength > maxDownloadBytes)
            {
                throw new InvalidDataException($"Download acima de {maxDownloadBytes / (1024 * 1024)}MB.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token).ConfigureAwait(false);
            using var memory = new MemoryStream();
            var buffer = new byte[32 * 1024];
            var totalRead = 0L;
            while (true)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), timeoutCts.Token).ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                totalRead += read;
                if (totalRead > maxDownloadBytes)
                {
                    throw new InvalidDataException($"Download acima de {maxDownloadBytes / (1024 * 1024)}MB.");
                }

                memory.Write(buffer, 0, read);
            }

            return memory.ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Download de GIF excedeu {downloadTimeoutSeconds}s.");
        }
    }

    private void OnPlayerFrameReady(object? sender, RgbaColor[] frame)
    {
        if (frame.Length != LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
        {
            return;
        }

        lock (gate)
        {
            latestFrame = frame.ToArray();
        }

        SendFrame(frame);
    }

    private void SendFrame(RgbaColor[] frame)
    {
        var payload = new LedPayload
        {
            Frame64x32 = frame,
            Level = 1f,
            PresetId = GifPresetId,
        };

        matrixOutput.Send(payload);
        simulatorOutput.Send(payload);
        NotifyFrameUpdated(frame);
    }

    private void SendSimulatorFrame(RgbaColor[] frame)
    {
        var payload = new LedPayload
        {
            Frame64x32 = frame,
            Level = 1f,
            PresetId = GifPresetId,
        };

        simulatorOutput.Send(payload);
        NotifyFrameUpdated(frame);
    }


    private void NotifyFrameUpdated(RgbaColor[] frame)
    {
        var handlers = FrameUpdated;
        if (handlers is null)
        {
            return;
        }

        foreach (EventHandler<RgbaColor[]> callback in handlers.GetInvocationList())
        {
            try
            {
                callback(this, frame);
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
    private void SendLegacyBinsClear()
    {
        matrixOutput.Send(new LedPayload
        {
            Bins64 = new float[LedDefaults.MatrixWidth],
            Level = 0f,
            PresetId = GifPresetId,
        });
    }

    private void CancelPendingLoad()
    {
        loadCts?.Cancel();
        loadCts?.Dispose();
        loadCts = null;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}




