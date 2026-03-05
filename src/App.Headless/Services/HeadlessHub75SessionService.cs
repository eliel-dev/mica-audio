using Device.Protocol.Models;
using Device.Server.Hosting;

namespace App.Headless.Services;

internal sealed class HeadlessHub75SessionService : IHostedService
{
    private const string VisualizerAppId = "visualizer-hub75";
    private static readonly TimeSpan DispatchCooldown = TimeSpan.FromMilliseconds(900);

    private readonly object gate = new();
    private readonly SemaphoreSlim reconcileGate = new(1, 1);
    private readonly IDeviceServerHost serverHost;
    private readonly ILogger<HeadlessHub75SessionService> logger;

    private readonly Dictionary<string, DeviceSessionState> sessionsByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private bool enabled;
    private bool running;

    public HeadlessHub75SessionService(
        IDeviceServerHost serverHost,
        ILogger<HeadlessHub75SessionService> logger)
    {
        this.serverHost = serverHost;
        this.logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        running = true;
        serverHost.DevicesChanged += OnDevicesChanged;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        running = false;
        serverHost.DevicesChanged -= OnDevicesChanged;
        return Task.CompletedTask;
    }

    public async Task SetHub75ModeAsync(bool value, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            enabled = value;
        }

        await ReconcileAsync(cancellationToken).ConfigureAwait(false);
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        _ = ReconcileAsync(CancellationToken.None);
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        if (!running)
        {
            return;
        }

        await reconcileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var hubEnabled = enabled;
            var snapshot = serverHost.GetDevicesSnapshot();
            var onlineDevices = snapshot
                .Where(static item => item.Status == DeviceStatus.Online && !string.IsNullOrWhiteSpace(item.DeviceId))
                .ToArray();

            if (hubEnabled)
            {
                foreach (var device in onlineDevices)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var deviceId = device.DeviceId.Trim();

                    DeviceSessionState state;
                    lock (gate)
                    {
                        if (!sessionsByDeviceId.TryGetValue(deviceId, out state!))
                        {
                            state = new DeviceSessionState
                            {
                                DeviceId = deviceId,
                            };
                            sessionsByDeviceId[deviceId] = state;
                        }

                        if (string.IsNullOrWhiteSpace(state.PreviousAppId)
                            && !string.IsNullOrWhiteSpace(device.ActiveAppId)
                            && !string.Equals(device.ActiveAppId, VisualizerAppId, StringComparison.OrdinalIgnoreCase))
                        {
                            state.PreviousAppId = device.ActiveAppId.Trim();
                            state.PreviousAppName = string.IsNullOrWhiteSpace(device.ActiveAppName) ? null : device.ActiveAppName.Trim();
                        }
                    }

                    if (string.Equals(device.ActiveAppId, VisualizerAppId, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (now < state.NextAttemptUtc)
                    {
                        continue;
                    }

                    var result = await DispatchActivateAppAsync(deviceId, VisualizerAppId, cancellationToken).ConfigureAwait(false);
                    lock (gate)
                    {
                        state.NextAttemptUtc = now + DispatchCooldown;
                        state.LastErrorCode = result.Success ? null : result.ErrorCode;
                    }
                }

                return;
            }

            foreach (var device in onlineDevices)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var deviceId = device.DeviceId.Trim();
                DeviceSessionState? state;
                lock (gate)
                {
                    sessionsByDeviceId.TryGetValue(deviceId, out state);
                }

                if (state is null || string.IsNullOrWhiteSpace(state.PreviousAppId))
                {
                    continue;
                }

                if (string.Equals(device.ActiveAppId, state.PreviousAppId, StringComparison.OrdinalIgnoreCase))
                {
                    lock (gate)
                    {
                        sessionsByDeviceId.Remove(deviceId);
                    }
                    continue;
                }

                if (now < state.NextAttemptUtc)
                {
                    continue;
                }

                var result = await DispatchActivateAppAsync(deviceId, state.PreviousAppId, cancellationToken).ConfigureAwait(false);
                lock (gate)
                {
                    state.NextAttemptUtc = now + DispatchCooldown;
                    state.LastErrorCode = result.Success ? null : result.ErrorCode;

                    if (result.Success)
                    {
                        sessionsByDeviceId.Remove(deviceId);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao reconciliar sessao HUB75 headless.");
        }
        finally
        {
            reconcileGate.Release();
        }
    }

    private async Task<CommandDispatchResult> DispatchActivateAppAsync(
        string deviceId,
        string appId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await serverHost.SendCommandTrackedAsync(
                deviceId,
                DeviceCommandType.ActivateApp,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["appId"] = appId,
                },
                timeout: TimeSpan.FromSeconds(5),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao ativar app {AppId} para dispositivo {DeviceId}.", appId, deviceId);
            return new CommandDispatchResult
            {
                DeviceId = deviceId,
                Accepted = true,
                Completed = true,
                Success = false,
                ErrorCode = "dispatch_exception",
                Message = ex.Message,
            };
        }
    }

    private sealed class DeviceSessionState
    {
        public required string DeviceId { get; init; }

        public string? PreviousAppId { get; set; }

        public string? PreviousAppName { get; set; }

        public DateTimeOffset NextAttemptUtc { get; set; }

        public string? LastErrorCode { get; set; }
    }
}
