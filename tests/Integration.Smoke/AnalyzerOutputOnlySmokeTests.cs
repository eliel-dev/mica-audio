using Analyzer.Dsp.Analysis;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;
using MicaAudio.Core.Led;

namespace Integration.Smoke;

public class AnalyzerOutputOnlySmokeTests
{
    [Fact]
    public void OutputOnlyAnalyzer_ShouldStillProduceBinsForLedPayload()
    {
        var analyzer = new SpectrumAnalyzer(new AnalyzerConfig
        {
            SampleRate = 48_000,
            FftSize = 2048,
            HopSize = 2048,
            DisplayBandCount = 38,
            OutputBandCount = 64,
            OutputMode = AnalyzerOutputMode.OutputOnly,
        });

        var samples = new float[2048];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = 0.80f * MathF.Sin(2f * MathF.PI * 440f * i / 48_000f);
        }

        var frame = analyzer.Process(new PcmFrame(samples, 1));
        Assert.NotNull(frame);
        var resolvedFrame = frame!;
        var payload = LedPayloadFactory.CreateSpectrumPayload(resolvedFrame, "output-only");
        Assert.NotNull(payload.Bins128);
        var bins = payload.Bins128!;

        Assert.Empty(resolvedFrame.BandsDisplay);
        Assert.Equal(LedDefaults.MatrixWidth, bins.Length);
        Assert.Contains(bins, value => value > 0f);
    }
}
