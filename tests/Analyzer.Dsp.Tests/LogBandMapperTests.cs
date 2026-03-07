using Analyzer.Dsp.Analysis;
using MicaAudio.Core.Audio;

namespace Analyzer.Dsp.Tests;

public class LogBandMapperTests
{
    [Fact]
    public void CreateRanges_ShouldBeMonotonicAndNonLinear()
    {
        const int bandCount = 38;
        const int fftSize = 1024;
        const int sampleRate = 48_000;
        const float minHz = 30f;
        const float maxHz = 16_000f;

        var ranges = LogBandMapper.CreateRanges(bandCount, fftSize, sampleRate, minHz, maxHz);

        Assert.Equal(bandCount, ranges.Length);

        for (var i = 1; i < ranges.Length; i++)
        {
            Assert.True(ranges[i].StartBin >= ranges[i - 1].EndBin);
            Assert.True(ranges[i].EndBin > ranges[i].StartBin);
        }

        var centersHz = ranges
            .Select(r => (((r.StartBin + r.EndBin) * 0.5f) * sampleRate) / fftSize)
            .ToArray();

        for (var i = 1; i < centersHz.Length; i++)
        {
            Assert.True(centersHz[i] > centersHz[i - 1]);
        }

        var firstStepHz = centersHz[1] - centersHz[0];
        var lastStepHz = centersHz[^1] - centersHz[^2];

        // Linear spacing would produce nearly constant steps.
        Assert.True(lastStepHz > firstStepHz * 5f);
    }

    [Theory]
    [InlineData(FrequencyScale.Mel)]
    [InlineData(FrequencyScale.Bark)]
    public void CreateRanges_ShouldSupportPsychoacousticScales(FrequencyScale scale)
    {
        var ranges = LogBandMapper.CreateRanges(38, 1024, 48_000, 30f, 16_000f, scale);

        Assert.Equal(38, ranges.Length);

        for (var i = 1; i < ranges.Length; i++)
        {
            Assert.True(ranges[i].StartBin >= ranges[i - 1].EndBin);
            Assert.True(ranges[i].EndBin > ranges[i].StartBin);
        }
    }

    [Fact]
    public void CreateRanges_MelAndBark_ShouldDifferFromLogarithmicSpacing()
    {
        var logRanges = LogBandMapper.CreateRanges(38, 1024, 48_000, 30f, 16_000f, FrequencyScale.Logarithmic);
        var melRanges = LogBandMapper.CreateRanges(38, 1024, 48_000, 30f, 16_000f, FrequencyScale.Mel);
        var barkRanges = LogBandMapper.CreateRanges(38, 1024, 48_000, 30f, 16_000f, FrequencyScale.Bark);

        Assert.NotEqual(logRanges[10].StartBinExact, melRanges[10].StartBinExact);
        Assert.NotEqual(logRanges[10].StartBinExact, barkRanges[10].StartBinExact);
    }

    [Fact]
    public void CreateMode0Ranges_ShouldProduceMonotonicGeometry()
    {
        const float viewport = 1600f;
        var layout = LogBandMapper.CreateMode0Ranges(
            fftSize: 2048,
            sampleRate: 48_000,
            minHz: 20f,
            maxHz: 1000f,
            frequencyScale: FrequencyScale.Bark,
            viewportWidthPx: viewport,
            barSpace: 0.10f);

        Assert.True(layout.Ranges.Length > 8);
        Assert.Equal(layout.Ranges.Length, layout.BarX.Length);
        Assert.Equal(layout.Ranges.Length, layout.BarWidth.Length);

        var firstPixel = layout.BarX[0] * viewport;
        var lastPixel = layout.BarX[^1] * viewport;
        Assert.True(firstPixel >= 0.5f); // not on x=0
        Assert.True(lastPixel <= viewport - 1.5f); // not on x=width-1

        for (var i = 0; i < layout.Ranges.Length; i++)
        {
            Assert.True(layout.BarWidth[i] > 0f);
            Assert.InRange(layout.BarX[i], 0f, 1f);
            Assert.InRange(layout.BarX[i] + layout.BarWidth[i], 0f, 1.0001f);

            if (i == 0)
            {
                continue;
            }

            Assert.True(layout.Ranges[i].StartBin >= layout.Ranges[i - 1].StartBin);
            Assert.True(layout.BarX[i] > layout.BarX[i - 1]);
        }
    }

    [Fact]
    public void CreateMode0Ranges_ShouldUseSinglePixelLikeBarWidth()
    {
        const float viewport = 1600f;
        var layout = LogBandMapper.CreateMode0Ranges(
            fftSize: 2048,
            sampleRate: 48_000,
            minHz: 20f,
            maxHz: 1000f,
            frequencyScale: FrequencyScale.Bark,
            viewportWidthPx: viewport,
            barSpace: 0.75f);

        var expectedWidth = 1f / viewport;
        Assert.All(layout.BarWidth, width => Assert.InRange(width, expectedWidth * 0.95f, expectedWidth * 1.05f));
    }

    [Fact]
    public void CreateMode0Ranges_BarSpaceShouldNotChangeCountOrGeometry()
    {
        var tight = LogBandMapper.CreateMode0Ranges(
            fftSize: 2048,
            sampleRate: 48_000,
            minHz: 20f,
            maxHz: 1000f,
            frequencyScale: FrequencyScale.Bark,
            viewportWidthPx: 1600f,
            barSpace: 0f);
        var loose = LogBandMapper.CreateMode0Ranges(
            fftSize: 2048,
            sampleRate: 48_000,
            minHz: 20f,
            maxHz: 1000f,
            frequencyScale: FrequencyScale.Bark,
            viewportWidthPx: 1600f,
            barSpace: 0.9f);

        Assert.Equal(tight.Ranges.Length, loose.Ranges.Length);
        Assert.Equal(tight.BarX, loose.BarX);
        Assert.Equal(tight.BarWidth, loose.BarWidth);
    }

    [Theory]
    [InlineData(FrequencyScale.Logarithmic)]
    [InlineData(FrequencyScale.Mel)]
    [InlineData(FrequencyScale.Bark)]
    public void CreateMode0Ranges_ShouldMatchMode0ReferenceBarCount(FrequencyScale scale)
    {
        var layout = LogBandMapper.CreateMode0Ranges(
            fftSize: 2048,
            sampleRate: 48_000,
            minHz: 20f,
            maxHz: 1000f,
            frequencyScale: scale,
            viewportWidthPx: 1600f,
            barSpace: 0.10f);

        var expected = ComputeMode0ReferenceBarCount(
            fftSize: 2048,
            sampleRate: 48_000,
            minHz: 20f,
            maxHz: 1000f,
            frequencyScale: scale,
            viewportWidthPx: 1600f);

        Assert.Equal(expected, layout.Ranges.Length);
    }

    [Fact]
    public void CreateMode0Ranges_WiderViewport_ShouldNotReduceBarCount()
    {
        var narrow = LogBandMapper.CreateMode0Ranges(
            fftSize: 2048,
            sampleRate: 48_000,
            minHz: 20f,
            maxHz: 1000f,
            frequencyScale: FrequencyScale.Bark,
            viewportWidthPx: 900f,
            barSpace: 0.10f);
        var wide = LogBandMapper.CreateMode0Ranges(
            fftSize: 2048,
            sampleRate: 48_000,
            minHz: 20f,
            maxHz: 1000f,
            frequencyScale: FrequencyScale.Bark,
            viewportWidthPx: 1800f,
            barSpace: 0.10f);

        Assert.True(wide.Ranges.Length >= narrow.Ranges.Length);
    }

    [Fact]
    public void AggregationRanges_ShouldPreservePeakAndRmsResults()
    {
        var spectrum = new float[513];
        for (var i = 0; i < spectrum.Length; i++)
        {
            spectrum[i] = i / (float)(spectrum.Length - 1);
        }

        var ranges = LogBandMapper.CreateRanges(38, 1024, 48_000, 30f, 16_000f, FrequencyScale.Bark);
        var aggregationRanges = LogBandMapper.CreateAggregationRanges(ranges);

        var peakFromRanges = LogBandMapper.AggregateBandsPeak(spectrum, ranges, -85f, -25f, useDb: false);
        var rmsFromRanges = LogBandMapper.AggregateBandsRms(spectrum, ranges, -85f, -25f, useDb: false);
        var peakFromAggregation = new float[ranges.Length];
        var rmsFromAggregation = new float[ranges.Length];

        LogBandMapper.AggregateBandsPeak(spectrum, aggregationRanges, -85f, -25f, useDb: false, useLinearAmplitude: true, linearBoost: 1f, peakFromAggregation);
        LogBandMapper.AggregateBandsRms(spectrum, aggregationRanges, -85f, -25f, useDb: false, useLinearAmplitude: true, linearBoost: 1f, rmsFromAggregation);

        Assert.Equal(peakFromRanges, peakFromAggregation);
        Assert.Equal(rmsFromRanges, rmsFromAggregation);
    }

    private static int ComputeMode0ReferenceBarCount(
        int fftSize,
        int sampleRate,
        float minHz,
        float maxHz,
        FrequencyScale frequencyScale,
        float viewportWidthPx)
    {
        var width = MathF.Max(1f, viewportWidthPx);
        var widthInt = global::System.Math.Max(1, (int)MathF.Round(width));
        var maxAnalyserBin = (fftSize / 2) - 1;
        if (maxAnalyserBin < 1)
        {
            return 0;
        }

        var edgeInsetPx = widthInt > 2 ? 1 : 0;
        var xMin = edgeInsetPx;
        var xMax = global::System.Math.Max(xMin, widthInt - 1 - edgeInsetPx);
        var minBin = global::System.Math.Clamp((int)MathF.Floor(minHz * fftSize / sampleRate), 1, maxAnalyserBin);
        var maxBinExclusive = global::System.Math.Clamp(
            (int)MathF.Ceiling(maxHz * fftSize / sampleRate),
            minBin + 1,
            maxAnalyserBin + 1);
        var minScale = ToScale(minHz, frequencyScale);
        var maxScale = ToScale(maxHz, frequencyScale);
        var unitWidth = width / MathF.Max(1e-6f, maxScale - minScale);

        var previousX = int.MinValue;
        var count = 0;

        for (var bin = minBin; bin < maxBinExclusive; bin++)
        {
            var frequencyHz = MathF.Max(1f, (bin + 0.5f) * sampleRate / (float)fftSize);
            var x = (int)MathF.Round((ToScale(frequencyHz, frequencyScale) - minScale) * unitWidth);
            x = global::System.Math.Clamp(x, xMin, xMax);

            if (x > previousX)
            {
                count++;
                previousX = x;
            }
        }

        return count;
    }

    private static float ToScale(float hz, FrequencyScale scale)
    {
        return scale switch
        {
            FrequencyScale.Mel => MathF.Log2(1f + (hz / 700f)),
            FrequencyScale.Bark => ((26.81f * hz) / (1960f + hz)) - 0.53f,
            _ => MathF.Log2(hz),
        };
    }
}
