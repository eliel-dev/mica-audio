using Analyzer.Dsp.Analysis;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace App.WinUI.Services;

// DOCS: docs/wiki/modules/app-winui.md#responsabilidades
internal sealed class AudioPipelineFrameProcessor
{
    private readonly AudioPipelineOutputRouter outputRouter;
    private readonly object stateGate = new();

    private IAnalyzer analyzer;
    private SpectrumFrame? latestFrame;
    private string currentPresetId = VisualizerRuntimeDefaults.DefaultPresetId;
    private RendererHubTransportMode hubTransportMode = RendererHubTransportMode.Bins128;

    public AudioPipelineFrameProcessor(AudioPipelineOutputRouter outputRouter, IAnalyzer initialAnalyzer)
    {
        this.outputRouter = outputRouter;
        analyzer = initialAnalyzer ?? throw new ArgumentNullException(nameof(initialAnalyzer));
    }

    public SpectrumFrame? LatestFrame
    {
        get
        {
            lock (stateGate)
            {
                return latestFrame;
            }
        }
    }

    public void SetAnalyzer(IAnalyzer value)
    {
        ArgumentNullException.ThrowIfNull(value);

        lock (stateGate)
        {
            analyzer = value;
        }
    }

    public void SetCurrentPreset(string presetId)
    {
        lock (stateGate)
        {
            currentPresetId = VisualizerRuntimeSettings.NormalizePresetId(presetId);
        }
    }

    public void SetHubTransportMode(RendererHubTransportMode mode)
    {
        lock (stateGate)
        {
            hubTransportMode = mode;
        }
    }

    public void Reset()
    {
        lock (stateGate)
        {
            latestFrame = null;
        }
    }

    public void SendHubFrame(RgbaColor[] frame128x64, bool forceSimulator = false, string presetId = "gif-hub75")
    {
        ArgumentNullException.ThrowIfNull(frame128x64);
        outputRouter.Dispatch(LedPayloadFactory.CreateFramePayload(frame128x64, presetId), forceSimulator);
    }

    public void Process(in PcmFrame pcmFrame)
    {
        IAnalyzer currentAnalyzer;
        RendererHubTransportMode activeTransportMode;
        string presetId;

        lock (stateGate)
        {
            currentAnalyzer = analyzer;
            activeTransportMode = hubTransportMode;
            presetId = currentPresetId;
        }

        var spectrum = currentAnalyzer.Process(in pcmFrame);
        if (spectrum is null)
        {
            return;
        }

        lock (stateGate)
        {
            latestFrame = spectrum;
        }

        if (activeTransportMode == RendererHubTransportMode.Frame128x64)
        {
            return;
        }

        outputRouter.Dispatch(LedPayloadFactory.CreateSpectrumPayload(spectrum, presetId));
    }
}
