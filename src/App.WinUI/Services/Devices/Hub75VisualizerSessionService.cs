using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
internal sealed class Hub75VisualizerSessionService : IDisposable
{
    public const string VisualizerAppId = "visualizer-hub75";
    public const string VisualizerAppName = "Visualizer HUB75";

    private static readonly TimeSpan DispatchCooldown = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(8);

    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly SemaphoreSlim reconcileGate = new(1, 1);
    private readonly object stateGate = new();
    private readonly Dictionary<string, DeviceSessionState> sessionsByDeviceId = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? delayedReconcileCts;
    private DateTimeOffset delayedReconcileAtUtc = DateTimeOffset.MinValue;

    private bool hub75Enabled;
    private bool disposed;
    private int reconcileRequested;

    public Hub75VisualizerSessionService(DeviceOperationsCoordinator deviceOps)
    {
        this.deviceOps = deviceOps;
        deviceOps.DeviceListChanged += OnDeviceListChanged;
    }

    public bool IsHub75Enabled
    {
        get
        {
            lock (stateGate)
            {
                return hub75Enabled;
            }
        }
    }

    public async Task SetHub75ModeAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var disableTransition = false;
        lock (stateGate)
        {
            disableTransition = hub75Enabled && !enabled;
            hub75Enabled = enabled;

            if (disableTransition)
            {
                foreach (var state in sessionsByDeviceId.Values)
                {
                    if (string.IsNullOrWhiteSpace(state.PreviousAppId))
                    {
                        continue;
                    }

                    state.NextAttemptUtc = DateTimeOffset.MinValue;
                    state.RetryCount = 0;
                    state.LastErrorCode = null;
                    state.Status = DeviceSessionStatus.RestorePending;
                }
            }
        }

        await ReconcileAsync(cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        deviceOps.DeviceListChanged -= OnDeviceListChanged;

        lock (stateGate)
        {
            sessionsByDeviceId.Clear();
            CancelDelayedReconcileLocked();
        }
    }

    private void OnDeviceListChanged(object? sender, EventArgs e)
    {
        RequestReconcile();
    }

    private void RequestReconcile()
    {
        if (disposed)
        {
            return;
        }

        if (Interlocked.Exchange(ref reconcileRequested, 1) == 1)
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            while (!disposed && Interlocked.Exchange(ref reconcileRequested, 0) == 1)
            {
                try
                {
                    await ReconcileAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Hub75VisualizerSessionService] Falha na reconciliacao: {ex.Message}");
                }
            }
        });
    }

    private async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await reconcileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (disposed)
            {
                return;
            }

            var snapshot = deviceOps.GetStateSnapshot().DeviceListSnapshot;
            var nowUtc = DateTimeOffset.UtcNow;
            List<DeviceCommandPlan> commands;
            DateTimeOffset? nextRetryUtc;

            lock (stateGate)
            {
                var reconcilePlan = BuildCommandsLocked(snapshot, nowUtc);
                commands = reconcilePlan.Commands;
                nextRetryUtc = reconcilePlan.NextRetryUtc;
            }

            UpdateDelayedReconcileSchedule(nextRetryUtc, nowUtc);

            var shouldRequestRefresh = false;

            foreach (var command in commands)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CommandDispatchResult result;

                try
                {
                    result = await deviceOps
                        .ActivateAppAsync(command.DeviceId, command.TargetAppId, command.TargetAppName, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lock (stateGate)
                    {
                        RegisterFailureLocked(command.DeviceId, command.Action, nowUtc, errorCode: null);
                    }

                    System.Diagnostics.Debug.WriteLine($"[Hub75VisualizerSessionService] Falha ao enviar comando para {command.DeviceId}: {ex.Message}");
                    continue;
                }

                lock (stateGate)
                {
                    ApplyResultLocked(command, result, nowUtc);
                }

                if (result.Accepted && result.Success)
                {
                    shouldRequestRefresh = true;
                }
            }

            if (shouldRequestRefresh)
            {
                deviceOps.RequestRefresh();
            }

            var finalNowUtc = DateTimeOffset.UtcNow;
            lock (stateGate)
            {
                nextRetryUtc = GetNextRetryUtcLocked(finalNowUtc);
            }

            UpdateDelayedReconcileSchedule(nextRetryUtc, finalNowUtc);
        }
        finally
        {
            reconcileGate.Release();
        }
    }

    private ReconcilePlan BuildCommandsLocked(IReadOnlyList<DeviceSnapshot> snapshot, DateTimeOffset nowUtc)
    {
        var plans = new List<DeviceCommandPlan>();
        var onlineByDeviceId = new Dictionary<string, DeviceSnapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var device in snapshot)
        {
            if (device.Status != DeviceStatus.Online || string.IsNullOrWhiteSpace(device.DeviceId))
            {
                continue;
            }

            onlineByDeviceId[device.DeviceId.Trim()] = device;
        }

        if (hub75Enabled)
        {
            foreach (var (deviceId, device) in onlineByDeviceId)
            {
                var state = GetOrCreateStateLocked(deviceId);
                CapturePreviousAppIfNeeded(state, device);

                if (IsVisualizerApp(device.ActiveAppId))
                {
                    state.RetryCount = 0;
                    state.NextAttemptUtc = DateTimeOffset.MinValue;
                    state.Status = DeviceSessionStatus.Idle;
                    state.LastErrorCode = null;
                    continue;
                }

                if (nowUtc < state.NextAttemptUtc)
                {
                    state.Status = DeviceSessionStatus.ActivationPending;
                    continue;
                }

                state.Status = DeviceSessionStatus.ActivationPending;
                plans.Add(new DeviceCommandPlan(deviceId, VisualizerAppId, VisualizerAppName, DeviceSessionAction.ActivateVisualizer));
                state.NextAttemptUtc = nowUtc + DispatchCooldown;
            }

            return new ReconcilePlan(plans, GetNextRetryUtcLocked(nowUtc));
        }

        foreach (var (deviceId, state) in sessionsByDeviceId.ToArray())
        {
            if (string.IsNullOrWhiteSpace(state.PreviousAppId))
            {
                sessionsByDeviceId.Remove(deviceId);
                continue;
            }

            if (!onlineByDeviceId.TryGetValue(deviceId, out var device))
            {
                state.Status = DeviceSessionStatus.RestorePending;
                continue;
            }

            if (string.Equals(Normalize(device.ActiveAppId), state.PreviousAppId, StringComparison.OrdinalIgnoreCase))
            {
                sessionsByDeviceId.Remove(deviceId);
                continue;
            }

            if (nowUtc < state.NextAttemptUtc)
            {
                state.Status = DeviceSessionStatus.RestorePending;
                continue;
            }

            state.Status = DeviceSessionStatus.RestorePending;
            plans.Add(new DeviceCommandPlan(deviceId, state.PreviousAppId, state.PreviousAppName, DeviceSessionAction.RestorePrevious));
            state.NextAttemptUtc = nowUtc + DispatchCooldown;
        }

        return new ReconcilePlan(plans, GetNextRetryUtcLocked(nowUtc));
    }

    private DeviceSessionState GetOrCreateStateLocked(string deviceId)
    {
        if (sessionsByDeviceId.TryGetValue(deviceId, out var state))
        {
            return state;
        }

        state = new DeviceSessionState
        {
            DeviceId = deviceId,
        };

        sessionsByDeviceId[deviceId] = state;
        return state;
    }

    private static void CapturePreviousAppIfNeeded(DeviceSessionState state, DeviceSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(state.PreviousAppId))
        {
            return;
        }

        var currentAppId = Normalize(snapshot.ActiveAppId);
        if (string.IsNullOrWhiteSpace(currentAppId)
            || string.Equals(currentAppId, VisualizerAppId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        state.PreviousAppId = currentAppId;
        state.PreviousAppName = Normalize(snapshot.ActiveAppName);
    }

    private void ApplyResultLocked(DeviceCommandPlan command, CommandDispatchResult result, DateTimeOffset nowUtc)
    {
        if (!sessionsByDeviceId.TryGetValue(command.DeviceId, out var state))
        {
            return;
        }

        if (result.Accepted && result.Success)
        {
            state.RetryCount = 0;
            state.NextAttemptUtc = nowUtc + DispatchCooldown;
            state.Status = DeviceSessionStatus.Idle;
            state.LastErrorCode = null;

            if (command.Action == DeviceSessionAction.RestorePrevious)
            {
                sessionsByDeviceId.Remove(command.DeviceId);
            }

            return;
        }

        RegisterFailureLocked(command.DeviceId, command.Action, nowUtc, result.ErrorCode);
    }

    private void RegisterFailureLocked(
        string deviceId,
        DeviceSessionAction action,
        DateTimeOffset nowUtc,
        string? errorCode)
    {
        if (!sessionsByDeviceId.TryGetValue(deviceId, out var state))
        {
            return;
        }

        state.RetryCount = Math.Clamp(state.RetryCount + 1, 1, 32);
        state.NextAttemptUtc = nowUtc + ComputeRetryDelay(state.RetryCount, errorCode);
        state.LastErrorCode = errorCode;
        state.Status = action == DeviceSessionAction.RestorePrevious
            ? DeviceSessionStatus.RestoreFailed
            : DeviceSessionStatus.ActivationFailed;
    }

    private static TimeSpan ComputeRetryDelay(int retryCount, string? errorCode)
    {
        if (string.Equals(errorCode, "busy", StringComparison.OrdinalIgnoreCase))
        {
            return BusyRetryDelay;
        }

        var exponent = Math.Clamp(retryCount - 1, 0, 6);
        var factor = 1 << exponent;
        var delayMs = BaseRetryDelay.TotalMilliseconds * factor;
        return TimeSpan.FromMilliseconds(Math.Min(delayMs, MaxRetryDelay.TotalMilliseconds));
    }

    private static bool IsVisualizerApp(string? appId)
        => string.Equals(Normalize(appId), VisualizerAppId, StringComparison.OrdinalIgnoreCase);

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private DateTimeOffset? GetNextRetryUtcLocked(DateTimeOffset nowUtc)
    {
        DateTimeOffset? nextRetryUtc = null;
        foreach (var state in sessionsByDeviceId.Values)
        {
            if (state.NextAttemptUtc <= nowUtc)
            {
                continue;
            }

            if (state.Status is not DeviceSessionStatus.ActivationPending
                and not DeviceSessionStatus.ActivationFailed
                and not DeviceSessionStatus.RestorePending
                and not DeviceSessionStatus.RestoreFailed)
            {
                continue;
            }

            if (!nextRetryUtc.HasValue || state.NextAttemptUtc < nextRetryUtc.Value)
            {
                nextRetryUtc = state.NextAttemptUtc;
            }
        }

        return nextRetryUtc;
    }

    private void UpdateDelayedReconcileSchedule(DateTimeOffset? nextRetryUtc, DateTimeOffset nowUtc)
    {
        CancellationTokenSource? scheduledCts = null;
        TimeSpan due = TimeSpan.Zero;

        lock (stateGate)
        {
            if (disposed)
            {
                CancelDelayedReconcileLocked();
                return;
            }

            if (!nextRetryUtc.HasValue)
            {
                CancelDelayedReconcileLocked();
                return;
            }

            var scheduledAtUtc = nextRetryUtc.Value;
            if (scheduledAtUtc <= nowUtc)
            {
                CancelDelayedReconcileLocked();
                return;
            }

            if (delayedReconcileCts is not null && delayedReconcileAtUtc <= scheduledAtUtc)
            {
                return;
            }

            CancelDelayedReconcileLocked();

            delayedReconcileAtUtc = scheduledAtUtc;
            delayedReconcileCts = new CancellationTokenSource();
            scheduledCts = delayedReconcileCts;
            due = scheduledAtUtc - nowUtc;
        }

        _ = RunDelayedReconcileAsync(scheduledCts!, due);
    }

    private async Task RunDelayedReconcileAsync(CancellationTokenSource scheduledCts, TimeSpan due)
    {
        try
        {
            await Task.Delay(due, scheduledCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (stateGate)
        {
            if (disposed || !ReferenceEquals(delayedReconcileCts, scheduledCts))
            {
                return;
            }

            delayedReconcileCts = null;
            delayedReconcileAtUtc = DateTimeOffset.MinValue;
        }

        scheduledCts.Dispose();
        RequestReconcile();
    }

    private void CancelDelayedReconcileLocked()
    {
        var cts = delayedReconcileCts;
        delayedReconcileCts = null;
        delayedReconcileAtUtc = DateTimeOffset.MinValue;

        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private readonly record struct DeviceCommandPlan(
        string DeviceId,
        string TargetAppId,
        string? TargetAppName,
        DeviceSessionAction Action);

    private readonly record struct ReconcilePlan(List<DeviceCommandPlan> Commands, DateTimeOffset? NextRetryUtc);

    private enum DeviceSessionAction
    {
        ActivateVisualizer,
        RestorePrevious,
    }

    private enum DeviceSessionStatus
    {
        Idle,
        ActivationPending,
        ActivationFailed,
        RestorePending,
        RestoreFailed,
    }

    private sealed class DeviceSessionState
    {
        public required string DeviceId { get; init; }

        public string? PreviousAppId { get; set; }

        public string? PreviousAppName { get; set; }

        public DateTimeOffset NextAttemptUtc { get; set; }

        public int RetryCount { get; set; }

        public DeviceSessionStatus Status { get; set; }

        public string? LastErrorCode { get; set; }
    }
}
