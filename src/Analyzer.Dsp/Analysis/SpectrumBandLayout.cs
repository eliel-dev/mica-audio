using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Analysis;

// DOCS: docs/wiki/modules/analyzer-dsp.md#modulo-analyzerdsp
internal sealed class SpectrumBandLayout
{
    public SpectrumBandLayout(AnalyzerConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.OutputMode == AnalyzerOutputMode.OutputOnly)
        {
            DisplayRanges = Array.Empty<BandRange>();
            DisplayAggregationRanges = Array.Empty<BandAggregationRange>();
            DisplayBarX = null;
            DisplayBarWidth = null;
        }
        else if (config.DisplayMode == DisplayMode.AudioMotionMode0 && config.DisplayViewportWidthPx > 1f)
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
            DisplayAggregationRanges = LogBandMapper.CreateAggregationRanges(mode0.Ranges);
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
            DisplayAggregationRanges = LogBandMapper.CreateAggregationRanges(DisplayRanges);
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
        OutputAggregationRanges = LogBandMapper.CreateAggregationRanges(OutputRanges);
    }

    public BandRange[] DisplayRanges { get; }

    public BandAggregationRange[] DisplayAggregationRanges { get; }

    public float[]? DisplayBarX { get; }

    public float[]? DisplayBarWidth { get; }

    public BandRange[] OutputRanges { get; }

    public BandAggregationRange[] OutputAggregationRanges { get; }
}
