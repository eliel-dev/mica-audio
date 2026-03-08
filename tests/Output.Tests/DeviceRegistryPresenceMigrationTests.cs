using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceRegistryPresenceMigrationTests
{
    [Fact]
    public void MigrateOldRecord_ShouldBackfillFirstSeenFromLastSeen()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-5);
        var lastSeen = DateTimeOffset.UtcNow.AddHours(-3);

        var result = DeviceRegistryPresenceNormalizer.NormalizeFirstSeenUtc(null, lastSeen, createdAt);

        Assert.Equal(lastSeen, result);
    }

    [Fact]
    public void MigrateOldRecord_ShouldBackfillFirstSeenFromCreatedAt_WhenLastSeenInvalid()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-2);

        var result = DeviceRegistryPresenceNormalizer.NormalizeFirstSeenUtc(null, DateTimeOffset.MinValue, createdAt);

        Assert.Equal(createdAt, result);
    }

    [Fact]
    public void MigrateOldRecord_ShouldBackfillLastAuthFromLastSeen()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-5);
        var lastSeen = DateTimeOffset.UtcNow.AddHours(-1);

        var result = DeviceRegistryPresenceNormalizer.NormalizeLastAuthUtc(null, lastSeen, createdAt);

        Assert.Equal(lastSeen, result);
    }

    [Fact]
    public void MigrateOldRecord_ShouldDefaultRegisteredAndUnknownConfig_WhenFieldsMissing()
    {
        Assert.True(DeviceRegistryPresenceNormalizer.NormalizeIsRegistered(null));
        Assert.Equal(DeviceConfigState.Unknown, DeviceRegistryPresenceNormalizer.NormalizeConfigState(null));
    }

    [Fact]
    public void MigrationHelpers_ShouldBeIdempotent()
    {
        var createdAt = DateTimeOffset.UtcNow.AddDays(-7);
        var lastSeen = DateTimeOffset.UtcNow.AddHours(-4);
        var firstPassFirstSeen = DeviceRegistryPresenceNormalizer.NormalizeFirstSeenUtc(null, lastSeen, createdAt);
        var firstPassLastAuth = DeviceRegistryPresenceNormalizer.NormalizeLastAuthUtc(null, lastSeen, createdAt);

        var secondPassFirstSeen = DeviceRegistryPresenceNormalizer.NormalizeFirstSeenUtc(firstPassFirstSeen, lastSeen, createdAt);
        var secondPassLastAuth = DeviceRegistryPresenceNormalizer.NormalizeLastAuthUtc(firstPassLastAuth, lastSeen, createdAt);

        Assert.Equal(firstPassFirstSeen, secondPassFirstSeen);
        Assert.Equal(firstPassLastAuth, secondPassLastAuth);
    }
}
