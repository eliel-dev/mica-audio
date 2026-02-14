using Analyzer.Dsp.Analysis;

namespace Analyzer.Dsp.Tests;

public class BandMappingTests
{
    [Fact]
    public void CreateRanges_ShouldProduceAscendingRanges()
    {
        var ranges = LogBandMapper.CreateRanges(128, 4096, 48_000, 20f, 20_000f);

        Assert.Equal(128, ranges.Length);
        for (var i = 1; i < ranges.Length; i++)
        {
            Assert.True(ranges[i].StartBin >= ranges[i - 1].StartBin);
            Assert.True(ranges[i].EndBin >= ranges[i].StartBin + 1);
        }
    }

    [Fact]
    public void Downmix128To64_ShouldPreservePairEnergyShape()
    {
        var input = Enumerable.Range(0, 128)
            .Select(i => i / 127f)
            .ToArray();

        var output = SpectrumDownmixer.DownmixTo64(input);

        Assert.Equal(64, output.Length);
        Assert.True(output[10] > output[4]);
        Assert.True(output[63] > output[20]);
    }

    [Fact]
    public void AggregateBandsPeak_ShouldHighlightTransientsMoreThanRms()
    {
        var fftSize = 4096;
        var ranges = LogBandMapper.CreateRanges(38, fftSize, 48_000, 30f, 16_000f);
        var spectrum = new float[(fftSize / 2) + 1];

        var targetBand = Array.FindIndex(ranges, range => (range.EndBin - range.StartBin) >= 4);
        if (targetBand < 0)
        {
            targetBand = ranges.Length / 2;
        }

        var targetRange = ranges[targetBand];
        var spikeBin = (targetRange.StartBin + targetRange.EndBin) / 2;
        spectrum[spikeBin] = 0.01f;

        var peak = LogBandMapper.AggregateBandsPeak(spectrum, ranges, -85f, -10f, useDb: false, useLinearAmplitude: false);
        var rms = LogBandMapper.AggregateBandsRms(spectrum, ranges, -85f, -10f, useDb: false, useLinearAmplitude: false);

        Assert.True(peak[targetBand] > rms[targetBand]);
    }
}
