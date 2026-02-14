using MicaAudio.Core.Config;

namespace Analyzer.Dsp.Tests;

public class FftSizePolicyTests
{
    [Theory]
    [InlineData(16384, 8192)]
    [InlineData(32768, 8192)]
    [InlineData(8192, 8192)]
    [InlineData(4096, 4096)]
    [InlineData(2048, 2048)]
    [InlineData(1024, 1024)]
    [InlineData(0, 2048)]
    public void CoerceUiSize_ShouldMatchPolicy(int input, int expected)
    {
        var coerced = FftSizePolicy.CoerceUiSize(input);
        Assert.Equal(expected, coerced);
    }
}
