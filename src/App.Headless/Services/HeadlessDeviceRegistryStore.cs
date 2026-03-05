using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Device.Protocol.Models;

namespace App.Headless.Services;

/// <summary>
/// Persists devices in %AppData%/MicaAudio/devices.headless.json with DPAPI for token.
/// Separate from the WinUI store intentionally.
/// </summary>
internal sealed class HeadlessDeviceRegistryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private static readonly byte[] TokenEntropy = Encoding.UTF8.GetBytes("MicaAudio.Headless.DeviceRegistry.Token.v1");

    private readonly string filePath;

    public HeadlessDeviceRegistryStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        filePath = Path.Combine(appData, "MicaAudio", "devices.headless.json");
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
        return new DeviceRecord
        {
            DeviceId = record.DeviceId,
            Name = record.Name,
            Token = decryptedToken ?? string.Empty,
            CreatedAtUtc = record.CreatedAtUtc,
        };
    }

    private static PersistedDeviceRecord MapToPersistedRecord(DeviceRecord record)
    {
        return new PersistedDeviceRecord
        {
            DeviceId = record.DeviceId,
            Name = record.Name,
            TokenProtected = EncryptToken(record.Token),
            CreatedAtUtc = record.CreatedAtUtc,
        };
    }

    private static string EncryptToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return string.Empty;
        }

        var plain = Encoding.UTF8.GetBytes(token);
        var cipher = ProtectedData.Protect(plain, TokenEntropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(cipher);
    }

    private static string? DecryptToken(string? protectedBase64)
    {
        if (string.IsNullOrWhiteSpace(protectedBase64))
        {
            return null;
        }

        try
        {
            var cipher = Convert.FromBase64String(protectedBase64);
            var plain = ProtectedData.Unprotect(cipher, TokenEntropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private sealed class PersistedDeviceRecord
    {
        public string DeviceId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string? TokenProtected { get; init; }
        public DateTimeOffset CreatedAtUtc { get; init; }
    }
}
