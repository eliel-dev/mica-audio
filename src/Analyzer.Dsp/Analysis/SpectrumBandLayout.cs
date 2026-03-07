using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Analysis;

// DOCS: docs/wiki/modules/analyzer-dsp.md#modulo-analyzerdsp
internal sealed class SpectrumBandLayout
{
    public SpectrumBandLayout(AnalyzerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.DisplayMode == DisplayMode.AudioMotionMode0 && config.DisplayViewportWidthPx > 1f)
        {
            var mode0 = LogBandMapper.CreateMode0Ranges(
                config.FftSize,
                config.SampleRate,
                config.MinHz,
                config.MaxHz,
                config.FrequencyScale,
                config.DisplayViewportWidthPx,
                config.BarSpace);

            DisplayRanges = mode0.Ranges;
            DisplayBarX = mode0.BarX;
            DisplayBarWidth = mode0.BarWidth;
        }
        else
        {
            DisplayRanges = LogBandMapper.CreateRanges(
                config.DisplayBandCount,
                config.FftSize,
                config.SampleRate,
                config.MinHz,
                config.MaxHz,
                config.FrequencyScale);
            DisplayBarX = null;
            DisplayBarWidth = null;
        }

        OutputRanges = LogBandMapper.CreateRanges(
            config.OutputBandCount,
            config.FftSize,
            config.SampleRate,
            config.MinHz,
            config.MaxHz,
            config.FrequencyScale);
    }

    public BandRange[] DisplayRanges { get; }

    public float[]? DisplayBarX { get; }

    public float[]? DisplayBarWidth { get; }

    public BandRange[] OutputRanges { get; }
}
