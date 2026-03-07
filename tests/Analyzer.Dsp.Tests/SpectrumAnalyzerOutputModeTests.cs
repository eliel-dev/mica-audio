using Analyzer.Dsp.Analysis;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Tests;

public class SpectrumAnalyzerOutputModeTests
{
    [Fact]
    public void OutputOnly_ShouldSkipDisplayBandsAndGeometry()
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
            samples[i] = 0.75f * MathF.Sin(2f * MathF.PI * 220f * i / 48_000f);
        }

        var frame = analyzer.Process(new PcmFrame(samples, 123));

        Assert.NotNull(frame);
        Assert.Empty(frame!.BandsDisplay);
        Assert.Equal(64, frame.Bands64.Length);
        Assert.Null(frame.DisplayBarX);
        Assert.Null(frame.DisplayBarWidth);
    }
}
