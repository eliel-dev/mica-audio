using Analyzer.Dsp.Analysis;

namespace Analyzer.Dsp.Tests;

public class SpectrumSampleWindowTests
{
    private static readonly float[] ExpectedShiftedSamples = [4f, 5f, 6f, 7f, 8f];

    [Fact]
    public void Append_ShouldResizeAndPreserveBufferedSamples()
    {
        var window = new SpectrumSampleWindow(4);
        var samples = new float[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f, 9f };
        var copy = new float[9];
        var hann = Enumerable.Repeat(1f, 9).ToArray();

        window.Append(samples);
        window.CopyWindowTo(copy, 1f, hann);

        Assert.Equal(samples.Length, window.SampleCount);
        Assert.Equal(samples, copy);
    }

    [Fact]
    public void Slide_ShouldDropHopAndKeepRemainingSamples()
    {
        var window = new SpectrumSampleWindow(8);
        var output = new float[5];
        var hann = Enumerable.Repeat(1f, 5).ToArray();

        window.Append([1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f]);
        window.Slide(3);
        window.CopyWindowTo(output, 1f, hann);

        Assert.Equal(5, window.SampleCount);
        Assert.Equal(ExpectedShiftedSamples, output);
    }
}
