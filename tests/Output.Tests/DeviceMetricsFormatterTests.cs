using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace Output.Tests;

public sealed class DeviceMetricsFormatterTests
{
    [Fact]
    public void Build_ShouldFormatOnlineMetrics()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-online",
            Name = "Online",
            Status = DeviceStatus.Online,
            IsRegistered = true,
            LastSeenUtc = DateTimeOffset.UtcNow,
            UptimeSeconds = 93_720,
            LoopLoadPercent = 64,
            FreeHeapBytes = 186_368,
            LargestHeapBlockBytes = 98_304,
            PsramAvailable = true,
            FreePsramBytes = 6_395_904,
            LargestPsramBlockBytes = 4_915_200,
            WifiConnected = true,
            LastKnownRssi = -56,
        };

        var result = DeviceMetricsFormatter.Build(snapshot);

        Assert.Equal("Online", result.StatusLabel);
        Assert.Equal("Uptime: 1d 02h 02m", result.UptimeLabel);
        Assert.Contains("Heap livre: 182 KB", result.HeapLabel, StringComparison.Ordinal);
        Assert.Contains("Maior bloco: 96 KB", result.HeapLabel, StringComparison.Ordinal);
        Assert.Contains("PSRAM livre: 6.1 MB", result.PsramLabel, StringComparison.Ordinal);
        Assert.Contains("Maior bloco: 4.7 MB", result.PsramLabel, StringComparison.Ordinal);
        Assert.Equal("Wi-Fi: conectado | RSSI -56 dBm", result.NetworkLabel);
        Assert.Equal(64, result.LoopLoadPercent);
        Assert.Equal(0.64d, result.LoopLoadProgress, 2);
        Assert.NotNull(result.HeapFragmentationProgress);
        Assert.NotNull(result.PsramFragmentationProgress);
        Assert.True(result.HasMetrics);
        Assert.False(result.IsOfflineSnapshot);
        Assert.True(result.IsPsramAvailable);
        Assert.Equal(string.Empty, result.PlaceholderMessage);
    }

    [Fact]
    public void Build_ShouldFormatOfflineSnapshot()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-offline",
            Name = "Offline",
            Status = DeviceStatus.Offline,
            IsRegistered = true,
            LastSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            UptimeSeconds = 2_000,
            LoopLoadPercent = 15,
            FreeHeapBytes = 120_000,
            PsramAvailable = false,
            WifiConnected = false,
        };

        var result = DeviceMetricsFormatter.Build(snapshot);

        Assert.Equal("Offline (ultimo snapshot)", result.StatusLabel);
        Assert.True(result.IsOfflineSnapshot);
        Assert.True(result.HasMetrics);
        Assert.Equal("PSRAM: indisponivel neste build", result.PsramLabel);
        Assert.Equal("Wi-Fi: sem conexao", result.NetworkLabel);
    }

    [Fact]
    public void Build_ShouldExposeLegacyControlPlaneStatus()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-legacy",
            Name = "Legacy",
            Status = DeviceStatus.Offline,
            ControlPlaneState = DeviceControlPlaneState.LegacyOnly,
            IsRegistered = true,
            LastSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            WifiState = "connected",
        };

        var result = DeviceMetricsFormatter.Build(snapshot);

        Assert.Equal("Firmware legado (sem MQTT)", result.StatusLabel);
        Assert.Equal("Control plane legado: WS-texto/HTTP | state connected", result.NetworkLabel);
        Assert.True(result.IsOfflineSnapshot);
    }

    [Fact]
    public void Build_ShouldNotShowRssi_WhenSnapshotIsOffline()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-offline-rssi",
            Name = "Offline with RSSI",
            Status = DeviceStatus.Offline,
            IsRegistered = true,
            LastSeenUtc = DateTimeOffset.UtcNow.AddMinutes(-2),
            LastKnownRssi = -55,
            WifiConnected = true,
        };

        var result = DeviceMetricsFormatter.Build(snapshot);

        Assert.DoesNotContain("RSSI", result.NetworkLabel, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Wi-Fi: indisponivel (offline)", result.NetworkLabel);
    }

    [Fact]
    public void Build_ShouldHandleMissingMetrics()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-empty",
            Name = "Empty",
            Status = DeviceStatus.Online,
            IsRegistered = true,
            LastSeenUtc = DateTimeOffset.UtcNow,
        };

        var result = DeviceMetricsFormatter.Build(snapshot);

        Assert.Equal("Sem metricas", result.StatusLabel);
        Assert.False(result.HasMetrics);
        Assert.Equal("Uptime: -", result.UptimeLabel);
        Assert.Equal("Heap livre: - | Maior bloco: -", result.HeapLabel);
        Assert.Equal("PSRAM: desconhecida", result.PsramLabel);
        Assert.Equal("Wi-Fi: -", result.NetworkLabel);
        Assert.Equal("Nenhuma metrica recebida ainda", result.PlaceholderMessage);
    }

    [Fact]
    public void Build_ShouldShowPsramUnavailable_WhenPsramAvailableIsFalse()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-no-psram",
            Status = DeviceStatus.Online,
            PsramAvailable = false,
        };

        var result = DeviceMetricsFormatter.Build(snapshot);

        Assert.Equal("PSRAM: indisponivel neste build", result.PsramLabel);
        Assert.False(result.IsPsramAvailable);
        Assert.Null(result.PsramFragmentationProgress);
    }

    [Fact]
    public void Build_ShouldShowPsramUnknown_WhenPsramAvailableIsNull()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-psram-unknown",
            Status = DeviceStatus.Online,
            PsramAvailable = null,
        };

        var result = DeviceMetricsFormatter.Build(snapshot);

        Assert.Equal("PSRAM: desconhecida", result.PsramLabel);
        Assert.False(result.IsPsramAvailable);
    }

    [Fact]
    public void Build_ShouldClampLoopLoadBetweenZeroAndOne()
    {
        var high = DeviceMetricsFormatter.Build(new DeviceSnapshot
        {
            DeviceId = "device-high",
            Status = DeviceStatus.Online,
            LoopLoadPercent = 135,
        });

        var low = DeviceMetricsFormatter.Build(new DeviceSnapshot
        {
            DeviceId = "device-low",
            Status = DeviceStatus.Online,
            LoopLoadPercent = -20,
        });

        Assert.Equal(1d, high.LoopLoadProgress, 3);
        Assert.Equal(0d, low.LoopLoadProgress, 3);
    }

    [Fact]
    public void Build_ShouldHideFragmentationProgress_WhenLargestBlockIsUnavailable()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-missing-largest",
            Status = DeviceStatus.Online,
            FreeHeapBytes = 4_096,
            LargestHeapBlockBytes = null,
            PsramAvailable = true,
            FreePsramBytes = 8_192,
            LargestPsramBlockBytes = null,
        };

        var result = DeviceMetricsFormatter.Build(snapshot);

        Assert.Null(result.HeapFragmentationProgress);
        Assert.Null(result.PsramFragmentationProgress);
    }

    [Fact]
    public void Build_ShouldHideFragmentationProgress_WhenLargestBlockIsGreaterThanFree()
    {
        var snapshot = new DeviceSnapshot
        {
            DeviceId = "device-invalid-largest",
            Status = DeviceStatus.Online,
            FreeHeapBytes = 4_096,
            LargestHeapBlockBytes = 8_192,
            PsramAvailable = true,
            FreePsramBytes = 12_000,
            LargestPsramBlockBytes = 20_000,
        };

        var result = DeviceMetricsFormatter.Build(snapshot);

        Assert.Null(result.HeapFragmentationProgress);
        Assert.Null(result.PsramFragmentationProgress);
    }
}
