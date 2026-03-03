using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceLifecyclePolicyTests
{
    [Fact]
    public void Build_ShouldReturnAwaitingProvisioning_WhenStatusIsPairing()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-pairing",
            Name = "Pairing Unit",
            Status = DeviceStatus.Pairing,
            IsRegistered = true,
        };

        var result = DeviceLifecyclePolicy.Build(snapshot, DeviceLifecycleThresholds.Default, DateTimeOffset.UtcNow);

        Assert.Equal("Registrado", result.PrimaryStateLabel);
        Assert.Equal("Aguardando provisionamento", result.SecondaryStateLabel);
        Assert.Equal(DeviceLifecycleIcon.Clock, result.Icon);
        Assert.Equal(DeviceLifecycleTone.Muted, result.Tone);
        Assert.True(result.IsNeverSeen);
        Assert.False(result.CanRunCommands);
    }

    [Fact]
    public void Build_ShouldReturnRegisteredNeverConnected_WhenFirstSeenIsNull()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-never-seen",
            Name = "Never Seen",
            Status = DeviceStatus.Offline,
            IsRegistered = true,
            FirstSeenUtc = null,
        };

        var result = DeviceLifecyclePolicy.Build(snapshot, DeviceLifecycleThresholds.Default, DateTimeOffset.UtcNow);

        Assert.Equal("Registrado", result.PrimaryStateLabel);
        Assert.Equal("Nunca conectado", result.SecondaryStateLabel);
        Assert.Equal(DeviceLifecycleIcon.Clock, result.Icon);
        Assert.Equal(DeviceLifecycleTone.Muted, result.Tone);
        Assert.True(result.IsNeverSeen);
    }

    [Fact]
    public void Build_ShouldReturnOnlineConfigured_WhenDeviceIsOnline()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-online",
            Name = "Online Unit",
            Status = DeviceStatus.Online,
            IsRegistered = true,
            FirstSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
        };

        var result = DeviceLifecyclePolicy.Build(snapshot, DeviceLifecycleThresholds.Default, DateTimeOffset.UtcNow);

        Assert.Equal("Online", result.PrimaryStateLabel);
        Assert.Equal("Configurado", result.SecondaryStateLabel);
        Assert.Equal(DeviceLifecycleIcon.Accept, result.Icon);
        Assert.Equal(DeviceLifecycleTone.Success, result.Tone);
        Assert.True(result.CanRunCommands);
    }

    [Fact]
    public void Build_ShouldReturnOfflineConfigured_WhenOfflineWithinDormantThreshold()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-offline",
            Name = "Offline Unit",
            Status = DeviceStatus.Offline,
            IsRegistered = true,
            FirstSeenUtc = now.AddDays(-1),
            LastSeenUtc = now.AddHours(-1),
        };

        var result = DeviceLifecyclePolicy.Build(snapshot, DeviceLifecycleThresholds.Default, now);

        Assert.Equal("Offline", result.PrimaryStateLabel);
        Assert.Equal("Configurado", result.SecondaryStateLabel);
        Assert.Equal(DeviceLifecycleIcon.Pause, result.Icon);
        Assert.Equal(DeviceLifecycleTone.Normal, result.Tone);
        Assert.False(result.IsConfigUncertain);
        Assert.False(result.CanRunCommands);
    }

    [Fact]
    public void Build_ShouldReturnOfflineConfigUncertain_WhenOfflinePastDormantThreshold()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-dormant",
            Name = "Dormant Unit",
            Status = DeviceStatus.Offline,
            IsRegistered = true,
            FirstSeenUtc = now.AddDays(-10),
            LastSeenUtc = now.AddDays(-3),
        };

        var result = DeviceLifecyclePolicy.Build(snapshot, DeviceLifecycleThresholds.Default, now);

        Assert.Equal("Offline", result.PrimaryStateLabel);
        Assert.Equal("Configuracao incerta", result.SecondaryStateLabel);
        Assert.Equal(DeviceLifecycleIcon.Important, result.Icon);
        Assert.Equal(DeviceLifecycleTone.Warning, result.Tone);
        Assert.True(result.IsConfigUncertain);
    }

    [Fact]
    public void Build_ShouldHandleUnknownLastSeen_WhenTimestampIsDefault()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-unknown-last-seen",
            Name = "Unknown Last Seen",
            Status = DeviceStatus.Offline,
            IsRegistered = true,
            FirstSeenUtc = DateTimeOffset.UtcNow.AddDays(-1),
            LastSeenUtc = default,
        };

        var result = DeviceLifecyclePolicy.Build(snapshot, DeviceLifecycleThresholds.Default, DateTimeOffset.UtcNow);

        Assert.Equal("Offline", result.PrimaryStateLabel);
        Assert.Equal("Registro salvo", result.SecondaryStateLabel);
        Assert.Equal("Ultimo contato: desconhecido", result.LastSeenLabel);
        Assert.Equal(DeviceLifecycleIcon.Help, result.Icon);
        Assert.Equal(DeviceLifecycleTone.Muted, result.Tone);
    }

    [Fact]
    public void Build_ShouldUseThresholdsFromAppSettings()
    {
        var now = DateTimeOffset.UtcNow;
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-custom-thresholds",
            Name = "Custom Thresholds",
            Status = DeviceStatus.Offline,
            IsRegistered = true,
            FirstSeenUtc = now.AddHours(-2),
            LastSeenUtc = now.AddMinutes(-20),
        };

        var strictThresholds = new DeviceLifecycleThresholds(
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(10));

        var result = DeviceLifecyclePolicy.Build(snapshot, strictThresholds, now);

        Assert.True(result.IsConfigUncertain);
        Assert.Equal("Configuracao incerta", result.SecondaryStateLabel);
    }
}
