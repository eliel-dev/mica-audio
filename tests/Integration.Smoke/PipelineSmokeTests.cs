using Analyzer.Dsp.Analysis;
using Device.Protocol.Stream;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Led;
using Output.Led;

namespace Integration.Smoke;

public class PipelineSmokeTests
{
    [Fact]
    public void AnalyzerToOutput_ShouldPass128Bands()
    {
        var analyzer = new SpectrumAnalyzer(new AnalyzerConfig());
        var simulator = new SimulatorLedOutput();
        simulator.Start(new LedOutputConfig { Width = LedDefaults.MatrixWidth, Height = LedDefaults.MatrixHeight, Brightness = 0.8f });

        var samples = new float[4096];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = MathF.Sin(2f * MathF.PI * 220f * i / 48_000f);
        }

        var frame = analyzer.Process(new PcmFrame(samples, 456));
        Assert.NotNull(frame);

        simulator.Send(LedPayloadFactory.CreateSpectrumPayload(frame!, "smoke"));

        var snapshot = simulator.GetFrameSnapshot();
        Assert.Equal(128 * 64, snapshot.Length);
    }

    [Fact]
    public void SimulatorPreview_ShouldReflectBinsFlagsFamilies()
    {
        var simulator = new SimulatorLedOutput();
        simulator.Start(new LedOutputConfig { Width = LedDefaults.MatrixWidth, Height = LedDefaults.MatrixHeight, Brightness = 1f });

        var bins = Enumerable.Repeat(0.7f, LedDefaults.MatrixWidth).ToArray();

        simulator.Send(new LedPayload
        {
            Bins128 = bins,
            Level = 0.6f,
            BinsFlags = Bins128VisualFlags.Create(Bins128VisualStyle.WaveMirror, Bins128PaletteFamily.Canonical),
        });
        var waveSnapshot = simulator.GetFrameSnapshot();

        simulator.Send(new LedPayload
        {
            Bins128 = bins,
            Level = 0.6f,
            BinsFlags = Bins128VisualFlags.Create(Bins128VisualStyle.LaunchpadGrid, Bins128PaletteFamily.Canonical),
        });
        var gridSnapshot = simulator.GetFrameSnapshot();

        Assert.NotEqual(waveSnapshot, gridSnapshot);
    }

}
