using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using App.WinUI.Services.Gif;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/guides/load-gif-hub75.md#gif-app-runtime-na-loja
[SupportedOSPlatform("windows")]
internal sealed class GifCatalogAppRuntimeService : IDisposable
{
    public const int TargetFps = 12;
    public const int MaxDownloadBytes = 25 * 1024 * 1024;

    private const string GifPresetId = "gifhub75";

    private readonly ILedOutput matrixOutput;
    private readonly ILedOutput simulatorOutput;
    private readonly Hub75GifDecoder decoder;
    private readonly Hub75FrameFormatter formatter;
    private readonly Hub75GifPlayer player;
    private readonly object gate = new();
    private readonly int maxDownloadBytes;
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
        int maxDownloadBytes = MaxDownloadBytes,
        float brightness = LedDefaults.Brightness)
    {
        this.matrixOutput = matrixOutput;
        this.simulatorOutput = simulatorOutput;
        this.decoder = decoder;
        this.formatter = formatter;
        this.player = player;
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

    public async Task StartFromFileAsync(string filePath, GifScaleMode scaleMode, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            throw new FileNotFoundException("Arquivo não encontrado.", filePath);
        }

        var info = new FileInfo(filePath);
        if (info.Length > maxDownloadBytes)
        {
            throw new InvalidDataException($"Arquivo acima de {maxDownloadBytes / (1024 * 1024)}MB.");
        }

        var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".gif")
        {
            await StartFromBytesCoreAsync(bytes, scaleMode, info.Name, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await StartFromStaticImageAsync(bytes, scaleMode, info.Name, cancellationToken).ConfigureAwait(false);
        }
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
    }

    private async Task StartFromStaticImageAsync(byte[] imageBytes, GifScaleMode scaleMode, string label, CancellationToken cancellationToken)
    {
        CancelPendingLoad();
        loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var ct = loadCts.Token;

        StatusChanged?.Invoke(this, $"Carregando imagem ({label})...");

        var frame = await Task.Run(() => DecodeStaticImageBytes(imageBytes), ct).ConfigureAwait(false);
        var formatted = await Task.Run(() => formatter.Format(frame, scaleMode), ct).ConfigureAwait(false);

        lock (gate)
        {
            loadedFrames = new RgbaColor[][] { formatted };
            latestFrame = formatted.ToArray();
        }

        player.SetFrames(new RgbaColor[][] { formatted });
        if (!player.Play())
        {
            throw new InvalidOperationException("Não foi possível iniciar o player de imagem.");
        }

        StatusChanged?.Invoke(this, $"Imagem carregada: {label}");
    }

    private static DecodedGifFrame DecodeStaticImageBytes(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var img = System.Drawing.Image.FromStream(ms);
        using var bmp = new System.Drawing.Bitmap(img);
        var lockData = bmp.LockBits(
            new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var stride = lockData.Stride;
            var raw = new byte[Math.Abs(stride) * bmp.Height];
            Marshal.Copy(lockData.Scan0, raw, 0, raw.Length);
            var pixels = new RgbaColor[bmp.Width * bmp.Height];
            for (var y = 0; y < bmp.Height; y++)
            {
                for (var x = 0; x < bmp.Width; x++)
                {
                    var o = (y * stride) + (x * 4);
                    pixels[(y * bmp.Width) + x] = new RgbaColor(raw[o + 2], raw[o + 1], raw[o], raw[o + 3]);
                }
            }

            return new DecodedGifFrame(bmp.Width, bmp.Height, pixels);
        }
        finally
        {
            bmp.UnlockBits(lockData);
        }
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
        var payload = LedPayloadFactory.CreateFramePayload(frame, GifPresetId);
        matrixOutput.Send(payload);
        simulatorOutput.Send(payload);
        NotifyFrameUpdated(frame);
    }

    private void SendSimulatorFrame(RgbaColor[] frame)
    {
        var payload = LedPayloadFactory.CreateFramePayload(frame, GifPresetId);
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
        matrixOutput.Send(LedPayloadFactory.CreateBinsPayload(new float[LedDefaults.MatrixWidth], GifPresetId, 0f));
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





