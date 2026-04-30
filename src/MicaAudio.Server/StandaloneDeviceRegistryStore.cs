using System.Text.Json;
using Device.Protocol.Models;

namespace MicaAudio.Server;

// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#baseline-atual
// DOCS: docs/handoffs/2026-04-22-micaudio-server-standalone.md
public sealed class StandaloneDeviceRegistryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string filePath;

    public StandaloneDeviceRegistryStore(string storageRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        filePath = Path.Combine(storageRoot, "devices.json");
    }

    public async Task<IReadOnlyList<DeviceRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return Array.Empty<DeviceRecord>();
        }

        try
        {
            await using var stream = File.OpenRead(filePath);
            var records = await JsonSerializer.DeserializeAsync<List<DeviceRecord>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return records?.Where(record => !string.IsNullOrWhiteSpace(record.DeviceId)).ToArray()
                   ?? Array.Empty<DeviceRecord>();
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
        ArgumentNullException.ThrowIfNull(devices);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, devices, JsonOptions, cancellationToken).ConfigureAwait(false);
    }
}
