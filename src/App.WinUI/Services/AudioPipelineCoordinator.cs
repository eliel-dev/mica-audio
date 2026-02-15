using Analyzer.Dsp.Analysis;
using Audio.Loopback.Capture;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Led;
using Output.Led;

namespace App.WinUI.Services;

internal sealed class AudioPipelineCoordinator
{
    private readonly ILoopbackCapture capture;
    private readonly ILedOutput simulatorLedOutput;
    private readonly ILedOutput nullLedOutput;
    private readonly Func<IAnalyzer> analyzerFactory;

    private CancellationTokenSource? cts;
    private Task? loopTask;
    private bool running;
    private ILedOutput activeLedOutput;
    private string currentPresetId = "audiomotion-clone";

    public AudioPipelineCoordinator(
        ILoopbackCapture capture,
        ILedOutput simulatorLedOutput,
        ILedOutput nullLedOutput,
        Func<IAnalyzer> analyzerFactory)
    {
        this.capture = capture;
        this.simulatorLedOutput = simulatorLedOutput;
        this.nullLedOutput = nullLedOutput;
        this.analyzerFactory = analyzerFactory;
        activeLedOutput = nullLedOutput;
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
        SetHubPreview(hubPreviewEnabled, brightness);

        loopTask = Task.Run(() => PipelineLoopAsync(cts.Token));
        running = true;
        StatusChanged?.Invoke(this, "Executando a 60 FPS");
    }

    public async Task StopAsync()
    {
        if (!running)
        {
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
            activeLedOutput.Stop();
        }

        StatusChanged?.Invoke(this, "Parado");
    }

    public void SetHubPreview(bool enabled, float brightness)
    {
        activeLedOutput = enabled ? simulatorLedOutput : nullLedOutput;
        activeLedOutput.Start(new LedOutputConfig { Width = 64, Height = 32, Brightness = brightness });
    }

    public void SetCurrentPreset(string presetId) => currentPresetId = presetId;

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
                activeLedOutput.Send(new LedPayload
                {
                    Bins64 = spectrum.Bands64,
                    Level = spectrum.Level,
                    PresetId = currentPresetId,
                });
            }
        }
    }
}
