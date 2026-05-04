using System.Numerics;
using Analyzer.Dsp.Analysis;
using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Tests;

public class SpectrumPowerProcessorTests
{
    [Fact]
    public void BuildPowerSpectrum_WithFftSmoothing_ShouldRetainMoreEnergyAfterSignalStops()
    {
        var noSmoothing = new SpectrumPowerProcessor(new AnalyzerConfig
        {
            FftSize = 2048,
            SampleRate = 48_000,
            FftSmoothing = 0f,
        });
        var withSmoothing = new SpectrumPowerProcessor(new AnalyzerConfig
        {
            FftSize = 2048,
            SampleRate = 48_000,
            FftSmoothing = 0.8f,
        });

        var powerBuffer = new float[1025];
        noSmoothing.BuildPowerSpectrum(CreateSineWindow(2048, 440f, 48_000), powerBuffer);
        var noSmoothingAfterStop = new float[1025];
        noSmoothing.BuildPowerSpectrum(new Complex[2048], noSmoothingAfterStop);

        withSmoothing.BuildPowerSpectrum(CreateSineWindow(2048, 440f, 48_000), new float[1025]);
        var withSmoothingAfterStop = new float[1025];
        withSmoothing.BuildPowerSpectrum(new Complex[2048], withSmoothingAfterStop);

        Assert.True(withSmoothingAfterStop.Max() > noSmoothingAfterStop.Max());
    }

    [Fact]
    public void ComputeLevel_ShouldClampIntoZeroToOne()
    {
        var processor = new SpectrumPowerProcessor(new AnalyzerConfig
        {
            FftSize = 2048,
            SampleRate = 48_000,
            LevelCompression = 1f,
        });

        var level = processor.ComputeLevel([0f, 100f, 100f, 100f]);

        Assert.InRange(level, 0f, 1f);
    }

    private static Complex[] CreateSineWindow(int size, float frequencyHz, int sampleRate)
    {
        var output = new Complex[size];
        for (var i = 0; i < size; i++)
        {
            output[i] = new Complex(MathF.Sin(2f * MathF.PI * frequencyHz * i / sampleRate), 0d);
        }

        return output;
    }
}
