using App.WinUI.Services.Devices;

namespace Output.Tests;

public sealed class DeviceListRenderDiffTests
{
    [Fact]
    public void BuildSignature_ShouldBeStable_ForEquivalentSnapshots()
    {
        var tokens = new[] { "a|online", "b|offline" };

        var first = DeviceListRenderDiff.BuildSignature(tokens);
        var second = DeviceListRenderDiff.BuildSignature(tokens);

        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildSignature_ShouldChange_WhenTokenSequenceChanges()
    {
        var first = DeviceListRenderDiff.BuildSignature(new[] { "a|online", "b|offline" });
        var second = DeviceListRenderDiff.BuildSignature(new[] { "a|online", "b|warning" });

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HasSameOrder_ShouldReturnTrue_ForEquivalentSequences()
    {
        var result = DeviceListRenderDiff.HasSameOrder(
            new[] { "device-a", "device-b" },
            new[] { "device-a", "device-b" });

        Assert.True(result);
    }

    [Fact]
    public void HasSameOrder_ShouldReturnFalse_ForDifferentOrder()
    {
        var result = DeviceListRenderDiff.HasSameOrder(
            new[] { "device-a", "device-b" },
            new[] { "device-b", "device-a" });

        Assert.False(result);
    }

    [Fact]
    public void HasSameOrder_ShouldReturnFalse_ForDifferentCounts()
    {
        var result = DeviceListRenderDiff.HasSameOrder(
            new[] { "device-a" },
            new[] { "device-a", "device-b" });

        Assert.False(result);
    }
}
