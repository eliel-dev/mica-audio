using Visual.Win2D.Engine;

namespace Output.Tests;

public sealed class ReactiveBandSamplerTests
{
    [Fact]
    public void Sample_ShouldReturnZeroedBands_ForEmptyInput()
    {
        var previous = Array.Empty<float>();
        var snapshot = ReactiveBandSampler.Sample(
            ReadOnlySpan<float>.Empty,
            0.75f,
            1f / 60f,
            12,
            RendererBarCountMode.Resampled,
            ref previous);

        Assert.All(snapshot.Bands.ToArray(), static value => Assert.Equal(0f, value));
        Assert.Equal(0f, snapshot.Low);
        Assert.Equal(0f, snapshot.Mid);
        Assert.Equal(0f, snapshot.High);
        Assert.Equal(0f, snapshot.GlobalLevel);
    }

    [Fact]
    public void Sample_ShouldBeDeterministic_ForTheSameInput()
    {
        var input = CreateBands(16, 0.65f, 0.35f);
        var previousA = Array.Empty<float>();
        var previousB = Array.Empty<float>();

        var snapshotA = ReactiveBandSampler.Sample(input, 0.65f, 0.25f, 8, RendererBarCountMode.Resampled, ref previousA);
        var valuesA = snapshotA.Bands.ToArray();
        var snapshotB = ReactiveBandSampler.Sample(input, 0.65f, 0.25f, 8, RendererBarCountMode.Resampled, ref previousB);
        var valuesB = snapshotB.Bands.ToArray();

        Assert.Equal(valuesA, valuesB);
        Assert.Equal(snapshotA.Low, snapshotB.Low);
        Assert.Equal(snapshotA.Mid, snapshotB.Mid);
        Assert.Equal(snapshotA.High, snapshotB.High);
        Assert.Equal(snapshotA.GlobalLevel, snapshotB.GlobalLevel);

    }

    [Fact]
    public void Sample_ShouldIncreaseAverageBandEnergy_ForHigherInput()
    {
        var lowInput = Enumerable.Repeat(0.10f, 24).ToArray();
        var highInput = Enumerable.Repeat(1.00f, 24).ToArray();
        var previousLow = Array.Empty<float>();
        var previousHigh = Array.Empty<float>();

        var lowSnapshot = ReactiveBandSampler.Sample(lowInput, 0.10f, 0.25f, 12, RendererBarCountMode.Resampled, ref previousLow);
        var highSnapshot = ReactiveBandSampler.Sample(highInput, 1.00f, 0.25f, 12, RendererBarCountMode.Resampled, ref previousHigh);

        var lowAverage = lowSnapshot.Bands.ToArray().Average();
        var highAverage = highSnapshot.Bands.ToArray().Average();

        Assert.True((highAverage - lowAverage) >= 0.15f, $"Delta medio insuficiente: {highAverage - lowAverage:0.000}");
    }

    [Theory]
    [InlineData(0, 2.0f)]
    [InlineData(4, 2.0f)]
    [InlineData(9, 2.0f)]
    public void Sample_ShouldFavorTheDominantSpectralRegion(int emphasizedIndex, float emphasizedValue)
    {
        var input = new float[12];
        for (var i = 0; i < input.Length; i++)
        {
            input[i] = i == emphasizedIndex ? emphasizedValue : 0f;
        }

        var previous = Array.Empty<float>();
        var snapshot = ReactiveBandSampler.Sample(input, emphasizedValue, 0.25f, 12, RendererBarCountMode.Native, ref previous);

        if (emphasizedIndex < 3)
        {
            Assert.True(snapshot.Low >= snapshot.Mid + 0.10f && snapshot.Low >= snapshot.High + 0.10f);
        }
        else if (emphasizedIndex < 8)
        {
            Assert.True(snapshot.Mid >= snapshot.Low + 0.10f && snapshot.Mid >= snapshot.High + 0.10f);
        }
        else
        {
            Assert.True(snapshot.High >= snapshot.Low + 0.10f && snapshot.High >= snapshot.Mid + 0.10f);
        }
    }

    [Fact]
    public void Sample_ShouldHonorBandCount_ByBarCountMode()
    {
        var input = CreateBands(10, 0.80f, 0.20f);
        var previousNative = Array.Empty<float>();
        var previousResampled = Array.Empty<float>();
        var previousFixed = Array.Empty<float>();

        var nativeSnapshot = ReactiveBandSampler.Sample(input, 0.8f, 0.25f, 6, RendererBarCountMode.Native, ref previousNative);
        var resampledSnapshot = ReactiveBandSampler.Sample(input, 0.8f, 0.25f, 6, RendererBarCountMode.Resampled, ref previousResampled);
        var fixedSnapshot = ReactiveBandSampler.Sample(input, 0.8f, 0.25f, 6, RendererBarCountMode.Fixed, ref previousFixed);

        Assert.Equal(10, nativeSnapshot.Bands.Length);
        Assert.Equal(6, resampledSnapshot.Bands.Length);
        Assert.Equal(6, fixedSnapshot.Bands.Length);
        Assert.InRange(resampledSnapshot.GlobalLevel, 0f, 1f);
        Assert.InRange(fixedSnapshot.GlobalLevel, 0f, 1f);
    }

    private static float[] CreateBands(int count, float highValue, float lowValue)
    {
        var values = new float[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = i % 2 == 0 ? highValue : lowValue;
        }

        return values;
    }
}
