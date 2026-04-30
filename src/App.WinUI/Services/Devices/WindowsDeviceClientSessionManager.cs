using System.Text.Json;
using System.Text.RegularExpressions;
using Device.Client;
using Device.Protocol.Models;
using MicaAudio.Core.Config;
using Microsoft.Extensions.Options;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/app-winui.md#client-owned-data-plane-lan
// DOCS: docs/wiki/modules/device-server-protocol.md#ownership-shadow-e-lock-lease
// DOCS: docs/handoffs/2026-04-24-windows-client-session-ownership.md
internal sealed class WindowsDeviceClientSessionManager : IDeviceClientSessionManager
{
    private static readonly TimeSpan DefaultHeartbeatInterval = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(2);
    private static readonly Regex UnsafeClientIdChars = new("[^a-z0-9-]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly IDeviceServerClient serverClient;
    private readonly TimeSpan heartbeatInterval;
    private readonly object gate = new();
    private readonly Dictionary<string, DeviceOwnerState> ownerByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CancellationTokenSource> heartbeatCtsByDeviceId = new(StringComparer.OrdinalIgnoreCase);

    private bool disposed;

    public WindowsDeviceClientSessionManager(
        IDeviceServerClient serverClient,
        IOptions<MicaAudioOptions> options,
        TimeSpan? heartbeatInterval = null)
    {
        this.serverClient = serverClient ?? throw new ArgumentNullException(nameof(serverClient));
        ArgumentNullException.ThrowIfNull(options);

        ClientId = LoadOrCreateClientId(options.Value);
        this.heartbeatInterval = heartbeatInterval.GetValueOrDefault(DefaultHeartbeatInterval);
        if (this.heartbeatInterval <= TimeSpan.Zero)
        {
            this.heartbeatInterval = DefaultHeartbeatInterval;
        }
    }

    public string ClientId { get; }

    public uint? GetOwnerEpoch(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        lock (gate)
        {
            return ownerByDeviceId.TryGetValue(deviceId.Trim(), out var state)
                ? state.OwnerEpoch
                : null;
        }
    }

    public IReadOnlyList<DeviceClientFrameTarget> GetFrameTargets(string? deviceId, string mode)
    {
        lock (gate)
        {
            if (!string.IsNullOrWhiteSpace(deviceId))
            {
                return ownerByDeviceId.TryGetValue(deviceId.Trim(), out var state)
                    ? [new DeviceClientFrameTarget(state.DeviceId, state.OwnerEpoch)]
                    : Array.Empty<DeviceClientFrameTarget>();
            }

            var normalizedMode = NormalizeMode(mode);
            return ownerByDeviceId.Values
                .Where(state => string.Equals(state.Mode, normalizedMode, StringComparison.OrdinalIgnoreCase))
                .Select(state => new DeviceClientFrameTarget(state.DeviceId, state.OwnerEpoch))
                .ToArray();
        }
    }

    public DeviceCommandSessionContext? CreateCommandContext(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        lock (gate)
        {
            var normalizedDeviceId = deviceId.Trim();
            if (!ownerByDeviceId.TryGetValue(normalizedDeviceId, out var state) || state.IsObservedOnly)
            {
                var predictedEpoch = PredictOwnerEpochLocked(normalizedDeviceId);
                state = new DeviceOwnerState(normalizedDeviceId, predictedEpoch, "idle");
                ownerByDeviceId[normalizedDeviceId] = state;
            }

            return new DeviceCommandSessionContext(ClientId, state.OwnerEpoch, state.LockToken);
        }
    }

    public Task<DeviceCommandSessionContext> AssumeOwnerAsync(
        string deviceId,
        string mode,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        lock (gate)
        {
            var normalizedDeviceId = deviceId.Trim();
            var normalizedMode = NormalizeMode(mode);
            var epoch = ownerByDeviceId.TryGetValue(normalizedDeviceId, out var existing)
                ? existing.IsObservedOnly ? existing.OwnerEpoch + 1 : existing.OwnerEpoch
                : PredictOwnerEpochLocked(normalizedDeviceId);
            var state = existing is null
                ? new DeviceOwnerState(normalizedDeviceId, epoch, normalizedMode)
                : existing with { OwnerEpoch = epoch, Mode = normalizedMode, IsObservedOnly = false };
            ownerByDeviceId[normalizedDeviceId] = state;
            return Task.FromResult(new DeviceCommandSessionContext(ClientId, state.OwnerEpoch, state.LockToken));
        }
    }

    public void ObserveDevices(IReadOnlyList<DeviceSnapshot> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);

        lock (gate)
        {
            foreach (var device in devices)
            {
                if (string.IsNullOrWhiteSpace(device.DeviceId))
                {
                    continue;
                }

                var deviceId = device.DeviceId.Trim();
                var observedEpoch = device.SessionActiveOwnerEpoch.GetValueOrDefault();
                var observedMode = NormalizeMode(device.SessionMode);
                if (observedEpoch > 0
                    && string.Equals(device.SessionActiveClientId, ClientId, StringComparison.OrdinalIgnoreCase))
                {
                    ownerByDeviceId[deviceId] = new DeviceOwnerState(deviceId, observedEpoch, observedMode);
                    continue;
                }

                if (!ownerByDeviceId.TryGetValue(deviceId, out var localState))
                {
                    ownerByDeviceId[deviceId] = new DeviceOwnerState(deviceId, observedEpoch, observedMode, IsObservedOnly: true);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(device.SessionActiveClientId) && observedEpoch >= localState.OwnerEpoch)
                {
                    ownerByDeviceId[deviceId] = new DeviceOwnerState(deviceId, observedEpoch, observedMode, IsObservedOnly: true);
                }
            }
        }
    }

    public void StartHeartbeat(string deviceId, string mode)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        CancellationTokenSource cts;
        var normalizedDeviceId = deviceId.Trim();
        lock (gate)
        {
            StopHeartbeatLocked(normalizedDeviceId);
            cts = new CancellationTokenSource();
            heartbeatCtsByDeviceId[normalizedDeviceId] = cts;
        }

        _ = RunHeartbeatAsync(normalizedDeviceId, NormalizeMode(mode), cts.Token);
    }

    public void StopHeartbeat(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        lock (gate)
        {
            StopHeartbeatLocked(deviceId.Trim());
        }
    }

    public async Task<DeviceClientLockLease> AcquireLockAsync(
        string deviceId,
        string reason,
        TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        var normalizedDeviceId = deviceId.Trim();
        var lockToken = Guid.NewGuid().ToString("N");
        var context = EnsureContextWithLock(normalizedDeviceId, lockToken);
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["reason"] = string.IsNullOrWhiteSpace(reason) ? "settings" : reason.Trim(),
            ["leaseMs"] = Math.Clamp((int)Math.Ceiling(ttl.TotalMilliseconds), 1, 15_000).ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        var result = await serverClient.SendCommandTrackedAsync(
                normalizedDeviceId,
                DeviceCommandType.SessionLockAcquire,
                parameters,
                context,
                CommandTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        if (!result.Accepted || !result.Success)
        {
            throw new InvalidOperationException(result.ErrorCode ?? result.Message ?? "lock_acquire_failed");
        }

        return new DeviceClientLockLease(normalizedDeviceId, lockToken, parameters["reason"]);
    }

    public async Task ReleaseLockAsync(string deviceId, string lockToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(lockToken))
        {
            return;
        }

        var normalizedDeviceId = deviceId.Trim();
        var context = EnsureContextWithLock(normalizedDeviceId, lockToken.Trim());
        await serverClient.SendCommandTrackedAsync(
                normalizedDeviceId,
                DeviceCommandType.SessionLockRelease,
                parameters: null,
                context,
                CommandTimeout,
                cancellationToken)
            .ConfigureAwait(false);

        lock (gate)
        {
            if (ownerByDeviceId.TryGetValue(normalizedDeviceId, out var state)
                && string.Equals(state.LockToken, lockToken, StringComparison.Ordinal))
            {
                ownerByDeviceId[normalizedDeviceId] = state with { LockToken = null };
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        CancellationTokenSource[] sources;
        lock (gate)
        {
            sources = heartbeatCtsByDeviceId.Values.ToArray();
            heartbeatCtsByDeviceId.Clear();
        }

        foreach (var source in sources)
        {
            source.Cancel();
            source.Dispose();
        }
    }

    private async Task RunHeartbeatAsync(string deviceId, string mode, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var context = CreateCommandContext(deviceId);
                if (context is not null)
                {
                    await serverClient.SendCommandTrackedAsync(
                            deviceId,
                            DeviceCommandType.SessionHeartbeat,
                            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["mode"] = mode,
                            },
                            context,
                            CommandTimeout,
                            cancellationToken)
                        .ConfigureAwait(false);
                }

                await Task.Delay(heartbeatInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                try
                {
                    await Task.Delay(heartbeatInterval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private DeviceCommandSessionContext EnsureContextWithLock(string deviceId, string lockToken)
    {
        lock (gate)
        {
            var epoch = ownerByDeviceId.TryGetValue(deviceId, out var state)
                ? state.OwnerEpoch
                : PredictOwnerEpochLocked(deviceId);
            ownerByDeviceId[deviceId] = new DeviceOwnerState(deviceId, epoch, state?.Mode ?? "idle", lockToken);
            return new DeviceCommandSessionContext(ClientId, epoch, lockToken);
        }
    }

    private uint PredictOwnerEpochLocked(string deviceId)
    {
        if (ownerByDeviceId.TryGetValue(deviceId, out var state) && state.OwnerEpoch > 0)
        {
            return state.IsObservedOnly
                ? state.OwnerEpoch + 1
                : state.OwnerEpoch;
        }

        return 1;
    }

    private void StopHeartbeatLocked(string deviceId)
    {
        if (!heartbeatCtsByDeviceId.Remove(deviceId, out var cts))
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    private static string LoadOrCreateClientId(MicaAudioOptions options)
    {
        var path = ResolveClientIdPath(options);
        var existing = TryLoadClientId(path);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var created = CreateClientId();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        JsonSerializer.Serialize(fs, new ClientSessionDocument { ClientId = created }, JsonOptions);
        return created;
    }

    private static string? TryLoadClientId(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var fs = File.OpenRead(path);
            var document = JsonSerializer.Deserialize<ClientSessionDocument>(fs, JsonOptions);
            return NormalizeClientId(document?.ClientId);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string ResolveClientIdPath(MicaAudioOptions options)
        => Path.Combine(
            string.IsNullOrWhiteSpace(options.AppDataRoot)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MicaAudio")
                : options.AppDataRoot,
            "client-session.json");

    private static string CreateClientId()
    {
        var machineName = NormalizeMachineName(Environment.MachineName);
        return $"win-{machineName}-{Guid.NewGuid():N}"[..^24];
    }

    private static string NormalizeMachineName(string? machineName)
    {
        var normalized = UnsafeClientIdChars.Replace((machineName ?? "windows").Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(normalized) ? "windows" : normalized;
    }

    private static string? NormalizeClientId(string? clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
        {
            return null;
        }

        var normalized = UnsafeClientIdChars.Replace(clientId.Trim().ToLowerInvariant(), "-").Trim('-');
        return normalized.StartsWith("win-", StringComparison.OrdinalIgnoreCase)
            ? normalized
            : null;
    }

    private static string NormalizeMode(string? mode)
        => string.IsNullOrWhiteSpace(mode) ? "idle" : mode.Trim().ToLowerInvariant();

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed record DeviceOwnerState(
        string DeviceId,
        uint OwnerEpoch,
        string Mode,
        string? LockToken = null,
        bool IsObservedOnly = false);

    private sealed class ClientSessionDocument
    {
        public string? ClientId { get; init; }
    }
}
