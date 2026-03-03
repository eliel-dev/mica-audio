using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceListVisibilityPolicyTests
{
    [Fact]
    public void BuildVisibleList_ShouldKeepOfflineDevicesVisible()
    {
        var now = DateTimeOffset.UtcNow;
        var devices = new[]
        {
            CreateDevice("device-offline", "Offline Unit", DeviceStatus.Offline, now.AddHours(-1), firmwareVersion: null),
        };

        var result = DeviceListVisibilityPolicy.BuildVisibleList(devices, DeviceLifecycleThresholds.Default, now);

        Assert.Single(result);
        Assert.Equal("device-offline", result[0].DeviceId);
        Assert.Equal(DeviceStatus.Offline, result[0].Status);
    }

    [Fact]
    public void BuildVisibleList_ShouldOrderOnlineBeforeOffline()
    {
        var now = DateTimeOffset.UtcNow;
        var devices = new[]
        {
            CreateDevice("device-b", "Zulu", DeviceStatus.Offline, now.AddHours(-1)),
            CreateDevice("device-a", "Alpha", DeviceStatus.Online, now.AddMinutes(-1)),
        };

        var result = DeviceListVisibilityPolicy.BuildVisibleList(devices, DeviceLifecycleThresholds.Default, now);

        Assert.Equal(new[] { "device-a", "device-b" }, result.Select(static device => device.DeviceId));
    }

    [Fact]
    public void BuildVisibleList_ShouldIgnoreOnlyDevicesWithoutDeviceId()
    {
        var now = DateTimeOffset.UtcNow;
        var devices = new[]
        {
            CreateDevice(string.Empty, "Sem Id", DeviceStatus.Online, now.AddMinutes(-1)),
            CreateDevice("device-ok", "Com Id", DeviceStatus.Offline, now.AddHours(-1), firmwareVersion: null),
        };

        var result = DeviceListVisibilityPolicy.BuildVisibleList(devices, DeviceLifecycleThresholds.Default, now);

        Assert.Single(result);
        Assert.Equal("device-ok", result[0].DeviceId);
    }

    [Fact]
    public void BuildVisibleList_ShouldSortByNameInsideSameStatus()
    {
        var now = DateTimeOffset.UtcNow;
        var devices = new[]
        {
            CreateDevice("device-b", "Zulu", DeviceStatus.Online, now.AddMinutes(-1)),
            CreateDevice("device-a", "alpha", DeviceStatus.Online, now.AddMinutes(-1)),
        };

        var result = DeviceListVisibilityPolicy.BuildVisibleList(devices, DeviceLifecycleThresholds.Default, now);

        Assert.Equal(new[] { "device-a", "device-b" }, result.Select(static device => device.DeviceId));
    }

    private static DeviceSnapshot CreateDevice(
        string deviceId,
        string name,
        DeviceStatus status,
        DateTimeOffset lastSeenUtc,
        string? firmwareVersion = "1.0.0")
    {
        return new DeviceSnapshot
        {
            DeviceId = deviceId,
            Name = name,
            Status = status,
            IsRegistered = true,
            FirstSeenUtc = lastSeenUtc.AddDays(-1),
            LastSeenUtc = lastSeenUtc,
            FirmwareVersion = firmwareVersion,
            Profile = "dma_exp",
        };
    }
}
