using Analyzer.Dsp.Analysis;
using Audio.Loopback.Capture;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;
using Visual.Win2D.Engine;

namespace App.WinUI.Services;

// DOCS: docs/wiki/modules/app-winui.md#responsabilidades
// DOCS: docs/wiki/modules/output-led.md#fluxo-de-execucao
internal sealed class AudioPipelineCoordinator
{
    private readonly ILoopbackCapture capture;
    private readonly AudioPipelineOutputRouter outputRouter;
    private readonly AudioPipelineFrameProcessor frameProcessor;

    private CancellationTokenSource? cts;
    private Task? loopTask;
    private bool running;

    public AudioPipelineCoordinator(
        ILoopbackCapture capture,
        ILedOutput simulatorLedOutput,
        ILedOutput esp32s3LedOutput,
        ILedOutput nullLedOutput,
        IAnalyzer initialAnalyzer)
    {
        this.capture = capture;
        outputRouter = new AudioPipelineOutputRouter(simulatorLedOutput, esp32s3LedOutput, nullLedOutput);
        frameProcessor = new AudioPipelineFrameProcessor(outputRouter, initialAnalyzer);
    }

    public SpectrumFrame? LatestFrame => frameProcessor.LatestFrame;

    public event EventHandler<string>? StatusChanged;

    public async Task StartAsync(bool hubPreviewEnabled, float brightness, string presetId, CancellationToken cancellationToken = default)
    {
        if (running)
        {
            return;
        }

        cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        frameProcessor.SetCurrentPreset(presetId);
        frameProcessor.Reset();
        outputRouter.Configure(hubPreviewEnabled, brightness);

        try
        {
            await capture.StartAsync(AudioPipelineCaptureProfile.CreateDefault(), cts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Erro de audio: {ex.Message}");
            cts.Dispose();
            cts = null;
            outputRouter.StopAll();
            return;
        }

        loopTask = Task.Run(() => PipelineLoopAsync(cts.Token), CancellationToken.None);
        running = true;
        StatusChanged?.Invoke(this, "Executando a 60 FPS");
    }

    public async Task StopAsync()
    {
        if (!running)
        {
            outputRouter.StopAll();
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
            frameProcessor.Reset();
            outputRouter.StopAll();
        }

        StatusChanged?.Invoke(this, "Parado");
    }

    public void SetHubPreview(bool enabled, float brightness)
    {
        outputRouter.Configure(enabled, brightness);
    }

    public void ConfigureHubOutputs(bool enableSimulator, float brightness)
    {
        outputRouter.Configure(enableSimulator, brightness);
    }

    public void SendHubFrame(RgbaColor[] frame128x64, bool forceSimulator = false, string presetId = "gif-hub75")
    {
        if (frame128x64.Length != (LedDefaults.MatrixWidth * LedDefaults.MatrixHeight))
        {
            return;
        }

        frameProcessor.SendHubFrame(frame128x64, forceSimulator, presetId);
    }

    public void SetCurrentPreset(string presetId) => frameProcessor.SetCurrentPreset(presetId);

    public void SetHubTransportMode(RendererHubTransportMode mode)
    {
        frameProcessor.SetHubTransportMode(mode);
    }

    public void SetAnalyzer(IAnalyzer analyzer)
    {
        frameProcessor.SetAnalyzer(analyzer);
    }

    private async Task PipelineLoopAsync(CancellationToken cancellationToken)
    {
        var reader = capture.Frames;

        try
        {
            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out var pcmFrame))
                {
                    frameProcessor.Process(in pcmFrame);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }
}


