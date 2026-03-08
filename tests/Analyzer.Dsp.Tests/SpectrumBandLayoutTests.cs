using Analyzer.Dsp.Analysis;
using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Tests;

public class SpectrumBandLayoutTests
{
    [Fact]
    public void Constructor_ShouldBuildMode0LayoutWhenRequested()
    {
        var layout = new SpectrumBandLayout(new AnalyzerConfig
        {
            FftSize = 2048,
            SampleRate = 48_000,
            DisplayBandCount = 64,
            OutputBandCount = 64,
            DisplayMode = DisplayMode.AudioMotionMode0,
            DisplayViewportWidthPx = 96f,
        });

        Assert.NotEmpty(layout.DisplayRanges);
        Assert.NotNull(layout.DisplayBarX);
        Assert.NotNull(layout.DisplayBarWidth);
        Assert.Equal(layout.DisplayRanges.Length, layout.DisplayBarX!.Length);
        Assert.Equal(layout.DisplayRanges.Length, layout.DisplayBarWidth!.Length);
        Assert.Equal(64, layout.OutputRanges.Length);
    }

    [Fact]
    public void Constructor_ShouldBuildStandardLayoutWhenMode0IsNotEnabled()
    {
        var layout = new SpectrumBandLayout(new AnalyzerConfig
        {
            FftSize = 2048,
            SampleRate = 48_000,
            DisplayBandCount = 38,
            OutputBandCount = 64,
            DisplayMode = DisplayMode.FixedBands,
        });

        Assert.Equal(38, layout.DisplayRanges.Length);
        Assert.Null(layout.DisplayBarX);
        Assert.Null(layout.DisplayBarWidth);
        Assert.Equal(64, layout.OutputRanges.Length);
    }
}
