using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/reference/device-observability-dashboard.md#aba-estatisticas
internal sealed class DeviceTelemetryHistorySnapshot
{
    public static DeviceTelemetryHistorySnapshot Empty { get; } = new();

    public IReadOnlyList<int?> RssiSamples { get; init; } = Array.Empty<int?>();

    public IReadOnlyList<int?> LoopLoadSamples { get; init; } = Array.Empty<int?>();

    public IReadOnlyList<long?> FreeHeapSamples { get; init; } = Array.Empty<long?>();

    public IReadOnlyList<long?> FreePsramSamples { get; init; } = Array.Empty<long?>();

    public uint? LastTelemetrySequence { get; init; }
}

internal sealed class DeviceTelemetryHistoryBook
{
    private readonly object gate = new();
    private readonly Dictionary<string, DeviceTelemetryHistoryBuffer> buffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly int maxSamples;

    public DeviceTelemetryHistoryBook(int maxSamples)
    {
        this.maxSamples = Math.Max(1, maxSamples);
    }

    public void RecordSnapshots(IReadOnlyList<DeviceSnapshot> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        lock (gate)
        {
            foreach (var snapshot in snapshots)
            {
                RecordSnapshotLocked(snapshot);
            }
        }
    }

    public DeviceTelemetryHistorySnapshot GetHistory(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return DeviceTelemetryHistorySnapshot.Empty;
        }

        lock (gate)
        {
            if (!buffers.TryGetValue(deviceId.Trim(), out var buffer))
            {
                return DeviceTelemetryHistorySnapshot.Empty;
            }

            return buffer.CreateSnapshot();
        }
    }

    public void RemoveDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        lock (gate)
        {
            buffers.Remove(deviceId.Trim());
        }
    }

    private void RecordSnapshotLocked(DeviceSnapshot snapshot)
    {
        if (snapshot is null
            || string.IsNullOrWhiteSpace(snapshot.DeviceId)
            || !snapshot.TelemetrySequence.HasValue)
        {
            return;
        }

        var deviceId = snapshot.DeviceId.Trim();
        if (!buffers.TryGetValue(deviceId, out var buffer))
        {
            buffer = new DeviceTelemetryHistoryBuffer(maxSamples);
            buffers[deviceId] = buffer;
        }

        buffer.Append(snapshot);
    }

    private sealed class DeviceTelemetryHistoryBuffer
    {
        private readonly int maxSamples;
        private readonly List<int?> rssiSamples = new();
        private readonly List<int?> loopLoadSamples = new();
        private readonly List<long?> freeHeapSamples = new();
        private readonly List<long?> freePsramSamples = new();

        public DeviceTelemetryHistoryBuffer(int maxSamples)
        {
            this.maxSamples = maxSamples;
        }

        public uint? LastTelemetrySequence { get; private set; }

        public void Append(DeviceSnapshot snapshot)
        {
            var sequence = snapshot.TelemetrySequence!.Value;
            if (LastTelemetrySequence.HasValue)
            {
                if (sequence == LastTelemetrySequence.Value)
                {
                    return;
                }

                if (sequence < LastTelemetrySequence.Value)
                {
                    rssiSamples.Clear();
                    loopLoadSamples.Clear();
                    freeHeapSamples.Clear();
                    freePsramSamples.Clear();
                }
            }

            LastTelemetrySequence = sequence;
            AppendSample(rssiSamples, snapshot.LastKnownRssi);
            AppendSample(loopLoadSamples, snapshot.LoopLoadPercent);
            AppendSample(freeHeapSamples, snapshot.FreeHeapBytes);
            AppendSample(freePsramSamples, snapshot.FreePsramBytes);
        }

        public DeviceTelemetryHistorySnapshot CreateSnapshot()
        {
            return new DeviceTelemetryHistorySnapshot
            {
                RssiSamples = rssiSamples.ToArray(),
                LoopLoadSamples = loopLoadSamples.ToArray(),
                FreeHeapSamples = freeHeapSamples.ToArray(),
                FreePsramSamples = freePsramSamples.ToArray(),
                LastTelemetrySequence = LastTelemetrySequence,
            };
        }

        private void AppendSample<T>(List<T?> entries, T? value)
        {
            entries.Add(value);
            if (entries.Count > maxSamples)
            {
                entries.RemoveRange(0, entries.Count - maxSamples);
            }
        }
    }
}
