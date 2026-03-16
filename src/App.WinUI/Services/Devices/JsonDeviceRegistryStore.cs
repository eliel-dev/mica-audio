using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Device.Protocol.Models;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#tokens-de-dispositivo-em-repouso
internal sealed class JsonDeviceRegistryStore : IDeviceRegistryStore
{
    private const string TokenCipherPrefix = "dpapi:v1:";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly byte[] TokenEntropy = Encoding.UTF8.GetBytes("MicaAudio.DeviceRegistry.Token.v1");

    private readonly string filePath;

    public JsonDeviceRegistryStore(IOptions<MicaAudioOptions> options)
    {
        var configuredPath = options.Value.DevicesFilePath;
        filePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(options.Value.AppDataRoot, "devices.json")
            : configuredPath;
    }

    public async Task<IReadOnlyList<DeviceRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return Array.Empty<DeviceRecord>();
        }

        try
        {
            await using var fs = File.OpenRead(filePath);
            var data = await JsonSerializer.DeserializeAsync<List<PersistedDeviceRecord>>(fs, JsonOptions, cancellationToken).ConfigureAwait(false);
            if (data is null || data.Count == 0)
            {
                return Array.Empty<DeviceRecord>();
            }

            return data.Select(MapToRuntimeRecord).ToArray();
        }
        catch (JsonException)
        {
            return Array.Empty<DeviceRecord>();
        }
        catch (IOException)
        {
            return Array.Empty<DeviceRecord>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<DeviceRecord>();
        }
    }

    public async Task SaveAsync(IReadOnlyList<DeviceRecord> devices, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        var persisted = devices
            .Select(MapToPersistedRecord)
            .ToArray();

        await using var fs = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fs, persisted, JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    private static DeviceRecord MapToRuntimeRecord(PersistedDeviceRecord record)
    {
        var decryptedToken = DecryptToken(record.TokenProtected);
        if (string.IsNullOrWhiteSpace(decryptedToken))
        {
            decryptedToken = record.Token ?? string.Empty;
        }

        return new DeviceRecord
        {
            DeviceId = record.DeviceId ?? string.Empty,
            Name = string.IsNullOrWhiteSpace(record.Name) ? "Matrix Portal" : record.Name,
            Profile = NormalizeFirmwareProfile(record.Profile),
            Token = decryptedToken,
            IsRegistered = DeviceRegistryPresenceNormalizer.NormalizeIsRegistered(record.IsRegistered),
            CreatedAtUtc = record.CreatedAtUtc,
            LastSeenUtc = record.LastSeenUtc,
            FirstSeenUtc = DeviceRegistryPresenceNormalizer.NormalizeFirstSeenUtc(record.FirstSeenUtc, record.LastSeenUtc, record.CreatedAtUtc),
            LastTelemetryUtc = DeviceRegistryPresenceNormalizer.NormalizeTimestamp(record.LastTelemetryUtc),
            LastAuthUtc = DeviceRegistryPresenceNormalizer.NormalizeLastAuthUtc(record.LastAuthUtc, record.LastSeenUtc, record.CreatedAtUtc),
            ConfigState = DeviceRegistryPresenceNormalizer.NormalizeConfigState(record.ConfigState),
            FirmwareVersion = record.FirmwareVersion,
            LastKnownIp = record.LastKnownIp,
            LastKnownRssi = record.LastKnownRssi,
            UptimeSeconds = record.UptimeSeconds,
            LoopHealthyPercent = record.LoopHealthyPercent,
            LoopLoadPercent = record.LoopLoadPercent,
            ChipTemperatureCelsius = record.ChipTemperatureCelsius,
            FreeHeapBytes = record.FreeHeapBytes,
            LargestHeapBlockBytes = record.LargestHeapBlockBytes,
            PsramAvailable = record.PsramAvailable,
            FreePsramBytes = record.FreePsramBytes,
            LargestPsramBlockBytes = record.LargestPsramBlockBytes,
            WifiConnected = record.WifiConnected,
            WifiState = record.WifiState,
            ProvisioningPortalActive = record.ProvisioningPortalActive,
            AuxLedAvailable = record.AuxLedAvailable,
            TestLedAvailable = record.TestLedAvailable,
            LastWifiEvent = record.LastWifiEvent,
            StreamFramesReceived = record.StreamFramesReceived,
            StreamFramesApplied = record.StreamFramesApplied,
            Hub75PresentFrames = record.Hub75PresentFrames,
            StreamSequenceGapCount = record.StreamSequenceGapCount,
            StreamInvalidFrameCount = record.StreamInvalidFrameCount,
            StreamLastSequence = record.StreamLastSequence,
            TelemetrySequence = record.TelemetrySequence,
            BrightnessCap = record.BrightnessCap,
            BrightnessRequested = record.BrightnessRequested,
            BrightnessApplied = record.BrightnessApplied,
            TestLedEnabled = record.TestLedEnabled,
            TestLedDuty = record.TestLedDuty,
            ActiveAppId = record.ActiveAppId,
            ActiveAppName = record.ActiveAppName,
            BoardModel = record.BoardModel,
            PanelType = record.PanelType,
            ChipModel = record.ChipModel,
            ChipRevision = record.ChipRevision,
            ChipCores = record.ChipCores,
            CpuFreqMHz = record.CpuFreqMHz,
            SdkVersion = record.SdkVersion,
            HeapTotalBytes = record.HeapTotalBytes,
            PsramTotalBytes = record.PsramTotalBytes,
            FlashTotalBytes = record.FlashTotalBytes,
            SketchSizeBytes = record.SketchSizeBytes,
            FreeSketchBytes = record.FreeSketchBytes,
        };
    }

    private static PersistedDeviceRecord MapToPersistedRecord(DeviceRecord record)
    {
        return new PersistedDeviceRecord
        {
            DeviceId = record.DeviceId,
            Name = record.Name,
            Profile = NormalizeFirmwareProfile(record.Profile),
            TokenProtected = EncryptToken(record.Token),
            Token = null,
            IsRegistered = record.IsRegistered,
            CreatedAtUtc = record.CreatedAtUtc,
            LastSeenUtc = record.LastSeenUtc,
            FirstSeenUtc = record.FirstSeenUtc,
            LastTelemetryUtc = record.LastTelemetryUtc,
            LastAuthUtc = record.LastAuthUtc,
            ConfigState = record.ConfigState,
            FirmwareVersion = record.FirmwareVersion,
            LastKnownIp = record.LastKnownIp,
            LastKnownRssi = record.LastKnownRssi,
            UptimeSeconds = record.UptimeSeconds,
            LoopHealthyPercent = record.LoopHealthyPercent,
            LoopLoadPercent = record.LoopLoadPercent,
            ChipTemperatureCelsius = record.ChipTemperatureCelsius,
            FreeHeapBytes = record.FreeHeapBytes,
            LargestHeapBlockBytes = record.LargestHeapBlockBytes,
            PsramAvailable = record.PsramAvailable,
            FreePsramBytes = record.FreePsramBytes,
            LargestPsramBlockBytes = record.LargestPsramBlockBytes,
            WifiConnected = record.WifiConnected,
            WifiState = record.WifiState,
            ProvisioningPortalActive = record.ProvisioningPortalActive,
            AuxLedAvailable = record.AuxLedAvailable,
            TestLedAvailable = record.TestLedAvailable,
            LastWifiEvent = record.LastWifiEvent,
            StreamFramesReceived = record.StreamFramesReceived,
            StreamFramesApplied = record.StreamFramesApplied,
            Hub75PresentFrames = record.Hub75PresentFrames,
            StreamSequenceGapCount = record.StreamSequenceGapCount,
            StreamInvalidFrameCount = record.StreamInvalidFrameCount,
            StreamLastSequence = record.StreamLastSequence,
            TelemetrySequence = record.TelemetrySequence,
            BrightnessCap = record.BrightnessCap,
            BrightnessRequested = record.BrightnessRequested,
            BrightnessApplied = record.BrightnessApplied,
            TestLedEnabled = record.TestLedEnabled,
            TestLedDuty = record.TestLedDuty,
            ActiveAppId = record.ActiveAppId,
            ActiveAppName = record.ActiveAppName,
            BoardModel = record.BoardModel,
            PanelType = record.PanelType,
            ChipModel = record.ChipModel,
            ChipRevision = record.ChipRevision,
            ChipCores = record.ChipCores,
            CpuFreqMHz = record.CpuFreqMHz,
            SdkVersion = record.SdkVersion,
            HeapTotalBytes = record.HeapTotalBytes,
            PsramTotalBytes = record.PsramTotalBytes,
            FlashTotalBytes = record.FlashTotalBytes,
            SketchSizeBytes = record.SketchSizeBytes,
            FreeSketchBytes = record.FreeSketchBytes,
        };
    }

    // DOCS: docs/wiki/reference/troubleshooting-matrix.md#token-criptografado-no-devicesjson
    private static string EncryptToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var clearBytes = Encoding.UTF8.GetBytes(token);
        var encryptedBytes = ProtectedData.Protect(clearBytes, TokenEntropy, DataProtectionScope.CurrentUser);
        return TokenCipherPrefix + Convert.ToBase64String(encryptedBytes);
    }

    private static string DecryptToken(string? tokenProtected)
    {
        if (string.IsNullOrWhiteSpace(tokenProtected))
        {
            return string.Empty;
        }

        if (!tokenProtected.StartsWith(TokenCipherPrefix, StringComparison.Ordinal))
        {
            return tokenProtected;
        }

        var cipherBase64 = tokenProtected[TokenCipherPrefix.Length..];
        if (string.IsNullOrWhiteSpace(cipherBase64))
        {
            return string.Empty;
        }

        try
        {
            var encryptedBytes = Convert.FromBase64String(cipherBase64);
            var clearBytes = ProtectedData.Unprotect(encryptedBytes, TokenEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clearBytes);
        }
        catch (FormatException)
        {
            return string.Empty;
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }

    private static string NormalizeFirmwareProfile(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return "dma_exp";
        }

        var normalized = profile.Trim();
        return string.Equals(normalized, "stable", StringComparison.OrdinalIgnoreCase)
            ? "dma_exp"
            : normalized;
    }

    private sealed class PersistedDeviceRecord
    {
        public string? DeviceId { get; init; }

        public string? Name { get; init; }

        public string? Profile { get; init; }

        public string? Token { get; init; }

        public string? TokenProtected { get; init; }

        public bool? IsRegistered { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;

        public DateTimeOffset LastSeenUtc { get; init; } = DateTimeOffset.MinValue;

        public DateTimeOffset? FirstSeenUtc { get; init; }

        public DateTimeOffset? LastTelemetryUtc { get; init; }

        public DateTimeOffset? LastAuthUtc { get; init; }

        public DeviceConfigState? ConfigState { get; init; }

        public string? FirmwareVersion { get; init; }

        public string? LastKnownIp { get; init; }

        public int? LastKnownRssi { get; init; }

        public int? UptimeSeconds { get; init; }

        public int? LoopHealthyPercent { get; init; }

        public int? LoopLoadPercent { get; init; }

        public double? ChipTemperatureCelsius { get; init; }

        public long? FreeHeapBytes { get; init; }

        public long? LargestHeapBlockBytes { get; init; }

        public bool? PsramAvailable { get; init; }

        public long? FreePsramBytes { get; init; }

        public long? LargestPsramBlockBytes { get; init; }

        public bool? WifiConnected { get; init; }

        public string? WifiState { get; init; }

        public bool? ProvisioningPortalActive { get; init; }

        public bool? AuxLedAvailable { get; init; }

        public bool? TestLedAvailable { get; init; }

        public string? LastWifiEvent { get; init; }

        public uint? StreamLastSequence { get; init; }

        public uint? StreamFramesReceived { get; init; }

        public uint? StreamFramesApplied { get; init; }

        public uint? Hub75PresentFrames { get; init; }

        public uint? StreamSequenceGapCount { get; init; }

        public uint? StreamInvalidFrameCount { get; init; }

        public uint? TelemetrySequence { get; init; }

        public int? BrightnessCap { get; init; }

        public int? BrightnessRequested { get; init; }

        public int? BrightnessApplied { get; init; }

        public bool? TestLedEnabled { get; init; }

        public int? TestLedDuty { get; init; }

        public string? ActiveAppId { get; init; }

        public string? ActiveAppName { get; init; }

        public string? BoardModel { get; init; }

        public string? PanelType { get; init; }

        public string? ChipModel { get; init; }

        public int? ChipRevision { get; init; }

        public int? ChipCores { get; init; }

        public int? CpuFreqMHz { get; init; }

        public string? SdkVersion { get; init; }

        public long? HeapTotalBytes { get; init; }

        public long? PsramTotalBytes { get; init; }

        public long? FlashTotalBytes { get; init; }

        public long? SketchSizeBytes { get; init; }

        public long? FreeSketchBytes { get; init; }
    }
}
