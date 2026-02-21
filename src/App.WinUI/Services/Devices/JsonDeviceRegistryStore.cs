using System.Text.Json;
using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

internal sealed class JsonDeviceRegistryStore : IDeviceRegistryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string filePath;

    public JsonDeviceRegistryStore(string appDataRoot)
    {
        filePath = Path.Combine(appDataRoot, "devices.json");
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
            var data = await JsonSerializer.DeserializeAsync<List<DeviceRecord>>(fs, JsonOptions, cancellationToken).ConfigureAwait(false);
            return data is null ? Array.Empty<DeviceRecord>() : data;
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
        await using var fs = File.Create(filePath);
        await JsonSerializer.SerializeAsync(fs, devices, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
