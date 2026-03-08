using Analyzer.Dsp.Analysis;
using BenchmarkDotNet.Attributes;
using MicaAudio.Core.Audio;
using MicaAudio.Core.Config;

namespace BenchmarkSuite1;

// DOCS: docs/wiki/modules/analyzer-dsp.md#otimizacao-2026-03---fase-dsp-1-buffers-planos-e-output-only
[MemoryDiagnoser]
[InProcess]
public class SpectrumAnalyzerProcessBenchmark
{
    private const int SampleRate = 48_000;
    private const int ChunkSize = 256;

    private PcmFrame[] canonicalFrames = [];
    private PcmFrame[] stressFrames = [];
    private SpectrumAnalyzer canonicalAnalyzer = null!;
    private SpectrumAnalyzer stressAnalyzer = null!;
    private SpectrumAnalyzer outputOnlyAnalyzer = null!;
    private int canonicalIndex;
    private int stressIndex;
    private int outputOnlyIndex;

    [GlobalSetup]
    public void GlobalSetup()
    {
        canonicalFrames = CreateFrames(
            ChunkSize,
            96,
            static t =>
                (0.35f * MathF.Sin(2f * MathF.PI * 160f * t)) +
                (0.30f * MathF.Sin(2f * MathF.PI * 440f * t)) +
                (0.20f * MathF.Sin(2f * MathF.PI * 1200f * t)));

        stressFrames = CreateFrames(
            ChunkSize,
            160,
            static t =>
                (0.28f * MathF.Sin(2f * MathF.PI * 40f * t)) +
                (0.22f * MathF.Sin(2f * MathF.PI * 160f * t)) +
                (0.18f * MathF.Sin(2f * MathF.PI * 440f * t)) +
                (0.14f * MathF.Sin(2f * MathF.PI * 1000f * t)) +
                (0.10f * MathF.Sin(2f * MathF.PI * 4000f * t)));
    }

    [IterationSetup(Target = nameof(ProcessCanonical))]
    public void SetupCanonical()
    {
        canonicalAnalyzer = CreateAnalyzer(
            fftSize: 2048,
            hopSize: 256,
            displayBandCount: 38,
            outputMode: AnalyzerOutputMode.DisplayAndOutput);
        Warm(canonicalAnalyzer, canonicalFrames, 7);
        canonicalIndex = 0;
    }

    [IterationSetup(Target = nameof(ProcessStress))]
    public void SetupStress()
    {
        stressAnalyzer = CreateAnalyzer(
            fftSize: 2048,
            hopSize: 256,
            displayBandCount: 128,
            outputMode: AnalyzerOutputMode.DisplayAndOutput);
        Warm(stressAnalyzer, stressFrames, 31);
        stressIndex = 0;
    }

    [IterationSetup(Target = nameof(ProcessOutputOnly))]
    public void SetupOutputOnly()
    {
        outputOnlyAnalyzer = CreateAnalyzer(
            fftSize: 2048,
            hopSize: 256,
            displayBandCount: 38,
            outputMode: AnalyzerOutputMode.OutputOnly);
        Warm(outputOnlyAnalyzer, canonicalFrames, 7);
        outputOnlyIndex = 0;
    }

    [Benchmark(Baseline = true)]
    public SpectrumFrame? ProcessCanonical()
    {
        return RunNext(canonicalAnalyzer, canonicalFrames, ref canonicalIndex);
    }

    [Benchmark]
    public SpectrumFrame? ProcessStress()
    {
        return RunNext(stressAnalyzer, stressFrames, ref stressIndex);
    }

    [Benchmark]
    public SpectrumFrame? ProcessOutputOnly()
    {
        return RunNext(outputOnlyAnalyzer, canonicalFrames, ref outputOnlyIndex);
    }

    private static SpectrumAnalyzer CreateAnalyzer(int fftSize, int hopSize, int displayBandCount, AnalyzerOutputMode outputMode)
    {
        return new SpectrumAnalyzer(new AnalyzerConfig
        {
            SampleRate = SampleRate,
            FftSize = fftSize,
            HopSize = hopSize,
            DisplayBandCount = displayBandCount,
            OutputBandCount = 64,
            MinHz = 20f,
            MaxHz = 1000f,
            ScaleMode = ScaleMode.Linear,
            FrequencyScale = FrequencyScale.Bark,
            FftSmoothing = 0.75f,
            WeightingFilter = WeightingFilter.B,
            LinearBoost = 1.3f,
            OutputMode = outputMode,
        });
    }

    private static void Warm(SpectrumAnalyzer analyzer, PcmFrame[] frames, int chunkCount)
    {
        for (var i = 0; i < chunkCount; i++)
        {
            _ = analyzer.Process(frames[i % frames.Length]);
        }
    }

    private static SpectrumFrame? RunNext(SpectrumAnalyzer analyzer, PcmFrame[] frames, ref int index)
    {
        var frame = analyzer.Process(frames[index]);
        index = (index + 1) % frames.Length;
        return frame;
    }

    private static PcmFrame[] CreateFrames(int chunkSize, int chunkCount, Func<float, float> sampleFactory)
    {
        var frames = new PcmFrame[chunkCount];
        for (var chunk = 0; chunk < chunkCount; chunk++)
        {
            var samples = new float[chunkSize];
            for (var i = 0; i < samples.Length; i++)
            {
                var sampleIndex = (chunk * chunkSize) + i;
                var t = sampleIndex / (float)SampleRate;
                samples[i] = sampleFactory(t);
            }

            frames[chunk] = new PcmFrame(samples, chunk);
        }

        return frames;
    }
}
