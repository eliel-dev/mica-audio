using System.Net.Http;
using Analyzer.Dsp.Analysis;
using App.Headless.Services.Gif;
using Audio.Loopback.Capture;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace App.Headless.Services;

/// <summary>
/// Audio capture + DSP pipeline for headless mode with mutable visualizer settings.
/// </summary>
internal sealed class HeadlessAudioPipeline : IAsyncDisposable
{
    private const string VisualizerPresetId = "audiomotion-clone";
    private const int TargetFps = 60;
    private const int GifTargetFps = 12;
    private const int GifDownloadTimeoutSeconds = 10;
    private const int GifMaxDownloadBytes = 25 * 1024 * 1024;

    private readonly object gate = new();
    private readonly SimulatorLedOutput simulatorOutput;
    private readonly Esp32S3LedOutput esp32Output;
    private readonly NullLedOutput nullOutput;
    private readonly HeadlessVisualizerSettingsStore settingsStore;
    private readonly HeadlessVisualizerSettingsDomainService settingsDomainService;
    private readonly HeadlessHub75SessionService hub75SessionService;
    private readonly ILogger<HeadlessAudioPipeline> logger;
    private readonly HttpClient gifHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly Hub75GifDecoder gifDecoder = new(Hub75GifDecoder.DefaultMaxGifFrames);
    private readonly Hub75FrameFormatter gifFrameFormatter = new();
    private readonly Hub75GifPlayer gifPlayer = new(TimeSpan.FromMilliseconds(1000d / GifTargetFps));

    private WasapiLoopbackCaptureService? capture;
    private CancellationTokenSource? cts;
    private Task? loopTask;
    private bool running;
    private int settingsVersion;
    private string statusText = "Parado";
    private VisualizerSpectrumSnapshot latestSpectrum = new();
    private DateTimeOffset lastSpectrumBroadcastUtc = DateTimeOffset.MinValue;

    // Settings that mirror the WinUI visualizer controls.
    private float brightness = LedDefaults.Brightness;
    private float linearBoost = 1.6f;
    private int barCount = 38;
    private int fftSize = 2048;
    private float fftSmoothing = 0.75f;
    private WeightingFilter weightingFilter = WeightingFilter.B;
    private FrequencyScale frequencyScale = FrequencyScale.Bark;
    private float frequencyMinHz = 20f;
    private float frequencyMaxHz = 1000f;
    private bool hub75Enabled;
    private string contentMode = "audio";
    private string? gifSourceUrl;
    private string? gifError;
    private GifScaleMode gifScaleMode = GifScaleMode.Fit;
    private IReadOnlyList<RgbaColor[]> gifFrames = Array.Empty<RgbaColor[]>();

    public HeadlessAudioPipeline(
        SimulatorLedOutput simulatorOutput,
        Esp32S3LedOutput esp32Output,
        NullLedOutput nullOutput,
        HeadlessVisualizerSettingsStore settingsStore,
        HeadlessVisualizerSettingsDomainService settingsDomainService,
        HeadlessHub75SessionService hub75SessionService,
        ILogger<HeadlessAudioPipeline> logger)
    {
        this.simulatorOutput = simulatorOutput;
        this.esp32Output = esp32Output;
        this.nullOutput = nullOutput;
        this.settingsStore = settingsStore;
        this.settingsDomainService = settingsDomainService;
        this.hub75SessionService = hub75SessionService;
        this.logger = logger;

        var persisted = settingsDomainService
            .Migrate(settingsStore.LoadAsync().GetAwaiter().GetResult());

        lock (gate)
        {
            ApplyPersistedSettingsUnsafe(persisted);
            settingsVersion = 1;
            statusText = "Pronto";
        }

        gifPlayer.FrameReady += OnGifFrameReady;
    }

    public bool IsRunning
    {
        get
        {
            lock (gate)
            {
                return running;
            }
        }
    }

    public event EventHandler<string>? StatusChanged;

    public event EventHandler<VisualizerSpectrumSnapshot>? SpectrumUpdated;

    public event EventHandler<VisualizerRuntimeStateSnapshot>? VisualizerStatusUpdated;

    public event EventHandler<VisualizerRuntimeStateSnapshot>? VisualizerSettingsUpdated;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        bool alreadyRunning;
        lock (gate)
        {
            alreadyRunning = running;
        }

        if (alreadyRunning)
        {
            return;
        }

        capture = new WasapiLoopbackCaptureService();
        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            await capture.StartAsync(new CaptureConfig
            {
                TargetSampleRate = 48_000,
                TargetChannels = 1,
                ChannelCapacity = 8,
                BufferMilliseconds = 12,
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Erro de audio: {ex.Message}");
            cts.Dispose();
            cts = null;
            return;
        }

        lock (gate)
        {
            running = true;
            statusText = contentMode == "gif"
                ? BuildGifStatusUnsafe()
                : $"Executando a {TargetFps} FPS";
        }

        ConfigureOutputs();
        await hub75SessionService.SetHub75ModeAsync(GetHub75Enabled(), cancellationToken).ConfigureAwait(false);

        loopTask = Task.Run(() => PipelineLoopAsync(cts.Token));
        PublishStatusSnapshot();
        StatusChanged?.Invoke(this, statusText);
    }

    public async Task StopAsync()
    {
        bool wasRunning;
        lock (gate)
        {
            wasRunning = running;
        }

        if (!wasRunning)
        {
            simulatorOutput.Stop();
            esp32Output.Stop();
            nullOutput.Stop();
            return;
        }

        cts?.Cancel();

        try
        {
            if (capture is not null)
            {
                await capture.StopAsync().ConfigureAwait(false);
            }

            if (loopTask is not null)
            {
                await loopTask.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            cts?.Dispose();
            cts = null;
            loopTask = null;

            lock (gate)
            {
                running = false;
                statusText = "Parado";
            }

            gifPlayer.Stop();
            simulatorOutput.Stop();
            esp32Output.Stop();
            nullOutput.Stop();

            if (capture is not null)
            {
                await capture.DisposeAsync().ConfigureAwait(false);
                capture = null;
            }
        }

        PublishStatusSnapshot();
        StatusChanged?.Invoke(this, "Parado");
    }

    public VisualizerRuntimeStateSnapshot GetStateSnapshot()
    {
        lock (gate)
        {
            return BuildStateSnapshotUnsafe();
        }
    }

    public void SetBrightness(float value)
    {
        var persisted = default(AppSettings);
        var snapshot = default(VisualizerRuntimeStateSnapshot);

        lock (gate)
        {
            brightness = Math.Clamp(value, 0f, 1f);
            settingsVersion++;
            persisted = BuildPersistedSettingsUnsafe();
            snapshot = BuildStateSnapshotUnsafe();
        }

        ConfigureOutputs();
        _ = SaveSettingsSafeAsync(persisted);
        VisualizerSettingsUpdated?.Invoke(this, snapshot);
    }

    public async Task<VisualizerRuntimeStateSnapshot> UpdateSettingsAsync(
        VisualizerSettingsUpdate update,
        CancellationToken cancellationToken = default)
    {
        bool syncHubSession;
        var persisted = default(AppSettings);
        var snapshot = default(VisualizerRuntimeStateSnapshot);

        lock (gate)
        {
            var oldHub = hub75Enabled;

            if (update.LinearBoost.HasValue)
            {
                linearBoost = CoerceLinearBoost(update.LinearBoost.Value);
            }

            if (update.BarCount.HasValue)
            {
                barCount = Math.Clamp(update.BarCount.Value, 8, 128);
            }

            if (update.FftSize.HasValue)
            {
                fftSize = FftSizePolicy.CoerceUiSize(update.FftSize.Value);
            }

            if (update.FftSmoothing.HasValue)
            {
                fftSmoothing = Math.Clamp(update.FftSmoothing.Value, 0f, 0.99f);
            }

            if (!string.IsNullOrWhiteSpace(update.WeightingFilter)
                && TryParseWeightingFilter(update.WeightingFilter, out var weighting))
            {
                weightingFilter = weighting;
            }

            if (!string.IsNullOrWhiteSpace(update.FrequencyScale)
                && TryParseFrequencyScale(update.FrequencyScale, out var scale))
            {
                frequencyScale = scale;
            }

            if (update.FrequencyMinHz.HasValue)
            {
                frequencyMinHz = Math.Clamp(update.FrequencyMinHz.Value, 16f, 10_000f);
            }

            if (update.FrequencyMaxHz.HasValue)
            {
                frequencyMaxHz = Math.Clamp(update.FrequencyMaxHz.Value, 250f, 20_000f);
            }

            EnsureFrequencyRangeOrderUnsafe();

            if (update.Hub75Enabled.HasValue)
            {
                hub75Enabled = update.Hub75Enabled.Value;
            }

            if (update.Brightness.HasValue)
            {
                brightness = Math.Clamp(update.Brightness.Value, 0f, 1f);
            }

            settingsVersion++;
            syncHubSession = oldHub != hub75Enabled;
            persisted = BuildPersistedSettingsUnsafe();
            snapshot = BuildStateSnapshotUnsafe();
        }

        ConfigureOutputs();
        await SaveSettingsSafeAsync(persisted, cancellationToken).ConfigureAwait(false);

        if (syncHubSession)
        {
            await hub75SessionService.SetHub75ModeAsync(GetHub75Enabled(), cancellationToken).ConfigureAwait(false);
        }

        VisualizerSettingsUpdated?.Invoke(this, snapshot);
        PublishStatusSnapshot();
        return snapshot;
    }

    public async Task<VisualizerRuntimeStateSnapshot> SetHub75ModeAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var persisted = default(AppSettings);
        var snapshot = default(VisualizerRuntimeStateSnapshot);

        lock (gate)
        {
            hub75Enabled = enabled;
            settingsVersion++;
            persisted = BuildPersistedSettingsUnsafe();
            snapshot = BuildStateSnapshotUnsafe();
        }

        ConfigureOutputs();
        await SaveSettingsSafeAsync(persisted, cancellationToken).ConfigureAwait(false);
        await hub75SessionService.SetHub75ModeAsync(enabled, cancellationToken).ConfigureAwait(false);

        VisualizerSettingsUpdated?.Invoke(this, snapshot);
        PublishStatusSnapshot();
        return snapshot;
    }

    public async Task<VisualizerRuntimeStateSnapshot> SetContentModeAsync(string mode, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(mode, "audio", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "gif", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Modo invalido. Use audio ou gif.");
        }

        var snapshot = default(VisualizerRuntimeStateSnapshot);
        lock (gate)
        {
            contentMode = string.Equals(mode, "gif", StringComparison.OrdinalIgnoreCase) ? "gif" : "audio";
            statusText = contentMode == "gif"
                ? BuildGifStatusUnsafe()
                : $"Executando a {TargetFps} FPS";

            if (contentMode == "audio")
            {
                gifPlayer.Stop();
            }

            snapshot = BuildStateSnapshotUnsafe();
        }

        ConfigureOutputs();
        if (contentMode == "gif")
        {
            await hub75SessionService.SetHub75ModeAsync(GetHub75Enabled(), cancellationToken).ConfigureAwait(false);
        }

        PublishStatusSnapshot();
        return snapshot;
    }

    public async Task<VisualizerRuntimeStateSnapshot> LoadGifFromUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Informe uma URL http/https valida.");
        }

        var gifBytes = await DownloadGifFromUrlAsync(uri, cancellationToken).ConfigureAwait(false);
        if (gifBytes.Length == 0)
        {
            throw new InvalidDataException("Conteudo GIF vazio.");
        }

        if (gifBytes.Length > GifMaxDownloadBytes)
        {
            throw new InvalidDataException("Arquivo GIF acima de 25MB.");
        }

        var decoded = await Task.Run(
            () => gifDecoder.Decode(gifBytes, cancellationToken),
            cancellationToken).ConfigureAwait(false);

        if (decoded.Count == 0)
        {
            throw new InvalidDataException("GIF sem frames validos.");
        }

        var formatted = await Task.Run(() =>
        {
            var frames = new List<RgbaColor[]>(decoded.Count);
            foreach (var frame in decoded)
            {
                frames.Add(gifFrameFormatter.Format(frame, gifScaleMode));
            }

            return (IReadOnlyList<RgbaColor[]>)frames;
        }, cancellationToken).ConfigureAwait(false);

        var firstFrame = formatted[0];
        var snapshot = default(VisualizerRuntimeStateSnapshot);
        lock (gate)
        {
            gifFrames = formatted;
            gifSourceUrl = uri.ToString();
            gifError = null;
            statusText = contentMode == "gif"
                ? $"GIF carregado: {gifFrames.Count} frames, {GifTargetFps} FPS."
                : statusText;
            snapshot = BuildStateSnapshotUnsafe();
        }

        gifPlayer.SetFrames(formatted);
        if (string.Equals(contentMode, "gif", StringComparison.OrdinalIgnoreCase))
        {
            SendHubFrame(firstFrame, presetId: "gif-hub75", forceSimulator: true);
        }

        PublishStatusSnapshot();
        return snapshot;
    }

    public VisualizerRuntimeStateSnapshot PlayGif()
    {
        var snapshot = default(VisualizerRuntimeStateSnapshot);
        if (gifFrames.Count == 0)
        {
            lock (gate)
            {
                gifError = "Nenhum GIF carregado.";
                statusText = "Modo GIF ativo. Carregue um GIF para iniciar.";
                snapshot = BuildStateSnapshotUnsafe();
            }

            PublishStatusSnapshot();
            return snapshot;
        }

        _ = gifPlayer.Play();
        lock (gate)
        {
            gifError = null;
            statusText = $"Modo GIF ativo ({GifTargetFps} FPS).";
            snapshot = BuildStateSnapshotUnsafe();
        }

        PublishStatusSnapshot();
        return snapshot;
    }

    public VisualizerRuntimeStateSnapshot PauseGif()
    {
        gifPlayer.Pause();

        VisualizerRuntimeStateSnapshot snapshot;
        lock (gate)
        {
            statusText = "GIF pausado.";
            snapshot = BuildStateSnapshotUnsafe();
        }

        PublishStatusSnapshot();
        return snapshot;
    }

    public VisualizerRuntimeStateSnapshot StopGif()
    {
        gifPlayer.Stop();

        VisualizerRuntimeStateSnapshot snapshot;
        lock (gate)
        {
            statusText = "GIF parado.";
            snapshot = BuildStateSnapshotUnsafe();
        }

        PublishStatusSnapshot();
        return snapshot;
    }

    private async Task PipelineLoopAsync(CancellationToken cancellationToken)
    {
        if (capture is null)
        {
            return;
        }

        var reader = capture.Frames;
        IAnalyzer? analyzer = null;
        var analyzerVersion = -1;

        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var pcmFrame))
            {
                AnalyzerConfig config;
                int currentVersion;
                string mode;
                bool sendToSimulator;

                lock (gate)
                {
                    currentVersion = settingsVersion;
                    config = BuildAnalyzerConfigUnsafe();
                    mode = contentMode;
                    sendToSimulator = hub75Enabled;
                }

                if (!string.Equals(mode, "audio", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (analyzer is null || analyzerVersion != currentVersion)
                {
                    analyzer = new SpectrumAnalyzer(config);
                    analyzerVersion = currentVersion;
                }

                var spectrum = analyzer.Process(in pcmFrame);
                if (spectrum is null)
                {
                    continue;
                }

                var bins128 = GetOutputBins128(spectrum);
                var payload = new LedPayload
                {
                    Bins128 = bins128,
                    Level = spectrum.Level,
                    PresetId = VisualizerPresetId,
                };

                esp32Output.Send(payload);
                if (sendToSimulator)
                {
                    simulatorOutput.Send(payload);
                }
                else
                {
                    nullOutput.Send(payload);
                }

                MaybeBroadcastSpectrum(spectrum);
            }
        }
    }

    private void MaybeBroadcastSpectrum(SpectrumFrame spectrum)
    {
        var now = DateTimeOffset.UtcNow;
        VisualizerSpectrumSnapshot snapshot;

        lock (gate)
        {
            if (now - lastSpectrumBroadcastUtc < TimeSpan.FromMilliseconds(30))
            {
                return;
            }

            lastSpectrumBroadcastUtc = now;
            snapshot = new VisualizerSpectrumSnapshot
            {
                Bands = spectrum.BandsDisplay.ToArray(),
                Level = spectrum.Level,
                TimestampUtc = now,
            };
            latestSpectrum = snapshot;
        }

        SpectrumUpdated?.Invoke(this, snapshot);
    }

    private void OnGifFrameReady(object? sender, RgbaColor[] frame)
    {
        if (frame.Length != LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
        {
            return;
        }

        if (!string.Equals(GetContentMode(), "gif", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SendHubFrame(frame, presetId: "gif-hub75", forceSimulator: true);
    }

    private void SendHubFrame(RgbaColor[] frame128x64, string presetId, bool forceSimulator)
    {
        if (frame128x64.Length != (LedDefaults.MatrixWidth * LedDefaults.MatrixHeight))
        {
            return;
        }

        var payload = new LedPayload
        {
            Frame128x64 = frame128x64,
            Level = 1f,
            PresetId = presetId,
        };

        esp32Output.Send(payload);

        if (forceSimulator || GetHub75Enabled())
        {
            simulatorOutput.Send(payload);
        }
        else
        {
            nullOutput.Send(payload);
        }
    }

    private void ConfigureOutputs()
    {
        float localBrightness;
        bool showSimulator;

        lock (gate)
        {
            localBrightness = brightness;
            showSimulator = hub75Enabled || string.Equals(contentMode, "gif", StringComparison.OrdinalIgnoreCase);
        }

        var config = new LedOutputConfig
        {
            Width = LedDefaults.MatrixWidth,
            Height = LedDefaults.MatrixHeight,
            Brightness = localBrightness,
        };

        esp32Output.Start(config);
        esp32Output.SetBrightness(localBrightness);

        if (showSimulator)
        {
            simulatorOutput.Start(config);
            simulatorOutput.SetBrightness(localBrightness);
        }
        else
        {
            simulatorOutput.Stop();
        }

        nullOutput.Start(config);
        nullOutput.SetBrightness(localBrightness);
    }

    private void ApplyPersistedSettingsUnsafe(AppSettings settings)
    {
        brightness = Math.Clamp(settings.Brightness, 0f, 1f);
        linearBoost = CoerceLinearBoost(settings.LinearBoost);
        barCount = Math.Clamp(settings.BarCount, 8, 128);
        fftSize = FftSizePolicy.CoerceUiSize(settings.FftSize);
        fftSmoothing = Math.Clamp(settings.FftSmoothing, 0f, 0.99f);
        weightingFilter = Enum.IsDefined(typeof(WeightingFilter), settings.WeightingFilter) ? settings.WeightingFilter : WeightingFilter.Off;
        frequencyScale = Enum.IsDefined(typeof(FrequencyScale), settings.FrequencyScale) ? settings.FrequencyScale : FrequencyScale.Bark;
        frequencyMinHz = Math.Clamp(settings.FrequencyMinHz, 16f, 10_000f);
        frequencyMaxHz = Math.Clamp(settings.FrequencyMaxHz, 250f, 20_000f);
        hub75Enabled = settings.Hub75PreviewEnabled;
        EnsureFrequencyRangeOrderUnsafe();
    }

    private AppSettings BuildPersistedSettingsUnsafe()
    {
        return settingsDomainService.Migrate(new AppSettings
        {
            ActivePresetId = VisualizerPresetId,
            SelectedRendererId = VisualizerPresetId,
            Hub75PreviewEnabled = hub75Enabled,
            Brightness = brightness,
            LinearBoost = linearBoost,
            BarCount = barCount,
            FftSize = fftSize,
            FftSmoothing = fftSmoothing,
            WeightingFilter = weightingFilter,
            FrequencyScale = frequencyScale,
            FrequencyMinHz = frequencyMinHz,
            FrequencyMaxHz = frequencyMaxHz,
        });
    }

    private VisualizerRuntimeStateSnapshot BuildStateSnapshotUnsafe()
    {
        return new VisualizerRuntimeStateSnapshot
        {
            AudioCapturing = running && string.Equals(contentMode, "audio", StringComparison.OrdinalIgnoreCase),
            TargetFps = TargetFps,
            Status = statusText,
            ContentMode = contentMode,
            Settings = new VisualizerSettingsSnapshot
            {
                LinearBoost = linearBoost,
                BarCount = barCount,
                FftSize = fftSize,
                FftSmoothing = fftSmoothing,
                WeightingFilter = weightingFilter,
                FrequencyScale = frequencyScale,
                FrequencyMinHz = frequencyMinHz,
                FrequencyMaxHz = frequencyMaxHz,
                Hub75Enabled = hub75Enabled,
                Brightness = brightness,
            },
            Gif = new VisualizerGifStateSnapshot
            {
                SourceUrl = gifSourceUrl,
                ScaleMode = gifScaleMode.ToString().ToLowerInvariant(),
                IsPlaying = gifPlayer.IsPlaying,
                FrameCount = gifFrames.Count,
                Error = gifError,
            },
            Spectrum = latestSpectrum,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private AnalyzerConfig BuildAnalyzerConfigUnsafe()
    {
        return new AnalyzerConfig
        {
            SampleRate = 48_000,
            FftSize = fftSize,
            DisplayBandCount = barCount,
            DisplayMode = DisplayMode.FixedBands,
            MinHz = frequencyMinHz,
            MaxHz = frequencyMaxHz,
            FrequencyScale = frequencyScale,
            FftSmoothing = fftSmoothing,
            WeightingFilter = weightingFilter,
            UseLinearAmplitude = true,
            LinearBoost = linearBoost,
            OutputBandCount = 64,
            MinDecibels = -85f,
            MaxDecibels = -25f,
        };
    }

    private string BuildGifStatusUnsafe()
    {
        if (gifFrames.Count == 0)
        {
            return "Modo GIF ativo. Carregue um GIF para iniciar.";
        }

        if (gifPlayer.IsPlaying)
        {
            return $"Modo GIF ativo ({GifTargetFps} FPS).";
        }

        return $"GIF carregado: {gifFrames.Count} frames.";
    }

    private static float[] GetOutputBins128(SpectrumFrame spectrum)
    {
        if (spectrum.BandsDisplay.Length == LedDefaults.MatrixWidth)
        {
            return spectrum.BandsDisplay.ToArray();
        }

        var source = spectrum.BandsDisplay.Length > 0 ? spectrum.BandsDisplay : spectrum.Bands64;
        if (source.Length == LedDefaults.MatrixWidth)
        {
            return source.ToArray();
        }

        var bins = new float[LedDefaults.MatrixWidth];
        if (source.Length == 0)
        {
            return bins;
        }

        for (var i = 0; i < bins.Length; i++)
        {
            var t = bins.Length == 1 ? 0f : i / (float)(bins.Length - 1);
            var scaled = t * (source.Length - 1);
            var left = (int)MathF.Floor(scaled);
            var right = Math.Min(source.Length - 1, left + 1);
            var blend = scaled - left;
            bins[i] = (source[left] * (1f - blend)) + (source[right] * blend);
        }

        return bins;
    }

    private static bool TryParseWeightingFilter(string value, out WeightingFilter filter)
    {
        var normalized = value.Trim().ToUpperInvariant();
        filter = normalized switch
        {
            "A" => WeightingFilter.A,
            "B" => WeightingFilter.B,
            "C" => WeightingFilter.C,
            "D" => WeightingFilter.D,
            "468" => WeightingFilter.Filter468,
            "OFF" => WeightingFilter.Off,
            _ => WeightingFilter.Off,
        };

        return normalized is "A" or "B" or "C" or "D" or "468" or "OFF";
    }

    private static bool TryParseFrequencyScale(string value, out FrequencyScale scale)
    {
        if (Enum.TryParse<FrequencyScale>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(typeof(FrequencyScale), parsed))
        {
            scale = parsed;
            return true;
        }

        scale = FrequencyScale.Bark;
        return false;
    }

    private static float CoerceLinearBoost(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 1.6f;
        }

        return Math.Clamp(value, 1f, 3f);
    }

    private void EnsureFrequencyRangeOrderUnsafe()
    {
        if (frequencyMaxHz > frequencyMinHz)
        {
            return;
        }

        frequencyMinHz = 20f;
        frequencyMaxHz = 1000f;
    }

    private bool GetHub75Enabled()
    {
        lock (gate)
        {
            return hub75Enabled;
        }
    }

    private string GetContentMode()
    {
        lock (gate)
        {
            return contentMode;
        }
    }

    private void PublishStatusSnapshot()
    {
        VisualizerRuntimeStateSnapshot snapshot;
        lock (gate)
        {
            snapshot = BuildStateSnapshotUnsafe();
        }

        VisualizerStatusUpdated?.Invoke(this, snapshot);
    }

    private async Task SaveSettingsSafeAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            await settingsStore.SaveAsync(settings, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao persistir configuracoes do visualizador headless.");
        }
    }

    private async Task<byte[]> DownloadGifFromUrlAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(GifDownloadTimeoutSeconds));

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await gifHttpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCts.Token).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long contentLength && contentLength > GifMaxDownloadBytes)
        {
            throw new InvalidDataException("Download acima de 25MB.");
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
            if (totalRead > GifMaxDownloadBytes)
            {
                throw new InvalidDataException("Download acima de 25MB.");
            }

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        gifPlayer.FrameReady -= OnGifFrameReady;
        gifPlayer.Dispose();
        gifHttpClient.Dispose();
        await StopAsync().ConfigureAwait(false);
    }
}
