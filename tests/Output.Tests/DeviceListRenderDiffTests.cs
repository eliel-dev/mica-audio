using App.WinUI.Services.Devices;

namespace Output.Tests;

public sealed class DeviceListRenderDiffTests
{
    private static readonly string[] StableSignatureTokens = ["a|online", "b|offline"];
    private static readonly string[] ChangedSignatureTokens = ["a|online", "b|warning"];
    private static readonly string[] OrderedDeviceIds = ["device-a", "device-b"];
    private static readonly string[] ReversedDeviceIds = ["device-b", "device-a"];
    private static readonly string[] SingleDeviceId = ["device-a"];

    [Fact]
    public void BuildSignature_ShouldBeStable_ForEquivalentSnapshots()
    {
        var first = DeviceListRenderDiff.BuildSignature(StableSignatureTokens);
        var second = DeviceListRenderDiff.BuildSignature(StableSignatureTokens);

        Assert.Equal(first, second);
    }

    [Fact]
    public void BuildSignature_ShouldChange_WhenTokenSequenceChanges()
    {
        var first = DeviceListRenderDiff.BuildSignature(StableSignatureTokens);
        var second = DeviceListRenderDiff.BuildSignature(ChangedSignatureTokens);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void HasSameOrder_ShouldReturnTrue_ForEquivalentSequences()
    {
        var result = DeviceListRenderDiff.HasSameOrder(
            OrderedDeviceIds,
            OrderedDeviceIds);

        Assert.True(result);
    }

    [Fact]
    public void HasSameOrder_ShouldReturnFalse_ForDifferentOrder()
    {
        var result = DeviceListRenderDiff.HasSameOrder(
            OrderedDeviceIds,
            ReversedDeviceIds);

        Assert.False(result);
    }

    [Fact]
    public void HasSameOrder_ShouldReturnFalse_ForDifferentCounts()
    {
        var result = DeviceListRenderDiff.HasSameOrder(
            SingleDeviceId,
            OrderedDeviceIds);

        Assert.False(result);
    }
}
