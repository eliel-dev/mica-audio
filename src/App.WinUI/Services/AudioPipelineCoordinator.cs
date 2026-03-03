using Analyzer.Dsp.Analysis;
using Audio.Loopback.Capture;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace App.WinUI.Services;

// DOCS: docs/wiki/modules/app-winui.md#responsabilidades
// DOCS: docs/wiki/modules/output-led.md#fluxo-de-execucao
internal sealed class AudioPipelineCoordinator
{
    private readonly ILoopbackCapture capture;
    private readonly ILedOutput simulatorLedOutput;
    private readonly ILedOutput esp32s3LedOutput;
    private readonly ILedOutput nullLedOutput;
    private readonly Func<IAnalyzer> analyzerFactory;

    private CancellationTokenSource? cts;
    private Task? loopTask;
    private bool running;
    private bool hubPreviewEnabled;
    private float brightness = LedDefaults.Brightness;
    private string currentPresetId = "audiomotion-clone";

    public AudioPipelineCoordinator(
        ILoopbackCapture capture,
        ILedOutput simulatorLedOutput,
        ILedOutput esp32s3LedOutput,
        ILedOutput nullLedOutput,
        Func<IAnalyzer> analyzerFactory)
    {
        this.capture = capture;
        this.simulatorLedOutput = simulatorLedOutput;
        this.esp32s3LedOutput = esp32s3LedOutput;
        this.nullLedOutput = nullLedOutput;
        this.analyzerFactory = analyzerFactory;
    }

    public SpectrumFrame? LatestFrame { get; private set; }

    public event EventHandler<string>? StatusChanged;

    public async Task StartAsync(bool hubPreviewEnabled, float brightness, string presetId, CancellationToken cancellationToken = default)
    {
        if (running)
        {
            return;
        }

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

        currentPresetId = presetId;
        LatestFrame = null;
        ConfigureOutputs(hubPreviewEnabled, brightness);

        loopTask = Task.Run(() => PipelineLoopAsync(cts.Token));
        running = true;
        StatusChanged?.Invoke(this, "Executando a 60 FPS");
    }

    public async Task StopAsync()
    {
        if (!running)
        {
            simulatorLedOutput.Stop();
            esp32s3LedOutput.Stop();
            nullLedOutput.Stop();
            return;
        }

        cts?.Cancel();

        try
        {
            await capture.StopAsync().ConfigureAwait(false);
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
            running = false;
            simulatorLedOutput.Stop();
            esp32s3LedOutput.Stop();
            nullLedOutput.Stop();
        }

        StatusChanged?.Invoke(this, "Parado");
    }

    public void SetHubPreview(bool enabled, float brightness)
    {
        ConfigureOutputs(enabled, brightness);
    }

    public void ConfigureHubOutputs(bool enableSimulator, float brightness)
    {
        ConfigureOutputs(enableSimulator, brightness);
    }

    public void SendHubFrame(RgbaColor[] frame128x64, bool forceSimulator = false, string presetId = "gif-hub75")
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

        esp32s3LedOutput.Send(payload);

        if (forceSimulator || hubPreviewEnabled)
        {
            simulatorLedOutput.Send(payload);
        }
        else
        {
            nullLedOutput.Send(payload);
        }
    }

    public void SetCurrentPreset(string presetId) => currentPresetId = presetId;

    private void ConfigureOutputs(bool enableSimulator, float brightness)
    {
        hubPreviewEnabled = enableSimulator;
        this.brightness = Math.Clamp(brightness, 0f, 1f);

        var ledConfig = new LedOutputConfig
        {
            Width = LedDefaults.MatrixWidth,
            Height = LedDefaults.MatrixHeight,
            Brightness = this.brightness,
        };

        esp32s3LedOutput.Start(ledConfig);
        esp32s3LedOutput.SetBrightness(this.brightness);

        if (hubPreviewEnabled)
        {
            simulatorLedOutput.Start(ledConfig);
            simulatorLedOutput.SetBrightness(this.brightness);
        }
        else
        {
            simulatorLedOutput.Stop();
        }

        nullLedOutput.Start(ledConfig);
        nullLedOutput.SetBrightness(this.brightness);
    }

    private async Task PipelineLoopAsync(CancellationToken cancellationToken)
    {
        var reader = capture.Frames;

        while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
        {
            while (reader.TryRead(out var pcmFrame))
            {
                var analyzer = analyzerFactory();
                var spectrum = analyzer.Process(in pcmFrame);
                if (spectrum is null)
                {
                    continue;
                }

                LatestFrame = spectrum;

                var bins128 = GetOutputBins128(spectrum);
                var payload = new LedPayload
                {
                    Bins128 = bins128,
                    Level = spectrum.Level,
                    PresetId = currentPresetId,
                };

                esp32s3LedOutput.Send(payload);

                if (hubPreviewEnabled)
                {
                    simulatorLedOutput.Send(payload);
                }
                else
                {
                    nullLedOutput.Send(payload);
                }
            }
        }
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
}


