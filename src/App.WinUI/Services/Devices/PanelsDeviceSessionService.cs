using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/paineis.md#carga-direcionada-por-device
internal sealed class PanelsDeviceSessionService : IDisposable
{
    public const string PanelsAppId = "panels-hub75";
    public const string PanelsAppName = "Paineis HUB75";

    private static readonly TimeSpan DispatchCooldown = TimeSpan.FromMilliseconds(900);
    private static readonly TimeSpan BusyRetryDelay = TimeSpan.FromMilliseconds(800);
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(8);

    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly SemaphoreSlim reconcileGate = new(1, 1);
    private readonly object stateGate = new();

    private CancellationTokenSource? delayedReconcileCts;
    private DateTimeOffset delayedReconcileAtUtc = DateTimeOffset.MinValue;
    private string? targetDeviceId;
    private string? previousAppId;
    private string? previousAppName;
    private DateTimeOffset nextAttemptUtc = DateTimeOffset.MinValue;
    private int retryCount;
    private string? lastErrorCode;
    private DeviceSessionStatus status;
    private bool targetEnabled;
    private bool disposed;
    private int reconcileRequested;

    public PanelsDeviceSessionService(DeviceOperationsCoordinator deviceOps)
    {
        this.deviceOps = deviceOps;
        deviceOps.DeviceListChanged += OnDeviceListChanged;
    }

    public string? ActiveDeviceId
    {
        get
        {
            lock (stateGate)
            {
                return targetEnabled ? targetDeviceId : null;
            }
        }
    }

    public async Task StartAsync(string deviceId, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);

        lock (stateGate)
        {
            targetDeviceId = deviceId.Trim();
            targetEnabled = true;
            nextAttemptUtc = DateTimeOffset.MinValue;
            retryCount = 0;
            lastErrorCode = null;
            status = DeviceSessionStatus.ActivationPending;
        }

        await ReconcileAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        var shouldRestore = false;
        lock (stateGate)
        {
            targetEnabled = false;
            nextAttemptUtc = DateTimeOffset.MinValue;
            retryCount = 0;
            lastErrorCode = null;
            shouldRestore = !string.IsNullOrWhiteSpace(targetDeviceId) && !string.IsNullOrWhiteSpace(previousAppId);
            status = shouldRestore ? DeviceSessionStatus.RestorePending : DeviceSessionStatus.Idle;
            if (!shouldRestore)
            {
                ClearStateLocked();
            }
        }

        if (shouldRestore)
        {
            await ReconcileAsync(cancellationToken).ConfigureAwait(false);
        }
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
            ClearStateLocked();
            CancelDelayedReconcileLocked();
        }

        reconcileGate.Dispose();
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
            DeviceCommandPlan? plan;
            DateTimeOffset? nextRetryUtc;

            lock (stateGate)
            {
                plan = BuildCommandLocked(snapshot, nowUtc);
                nextRetryUtc = GetNextRetryUtcLocked(nowUtc);
            }

            UpdateDelayedReconcileSchedule(nextRetryUtc, nowUtc);

            if (plan is null)
            {
                return;
            }

            CommandDispatchResult result;
            try
            {
                result = await deviceOps
                    .ActivateAppAsync(plan.Value.DeviceId, plan.Value.TargetAppId, plan.Value.TargetAppName, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch
            {
                lock (stateGate)
                {
                    RegisterFailureLocked(plan.Value.Action, nowUtc, errorCode: null);
                }

                return;
            }

            lock (stateGate)
            {
                ApplyResultLocked(plan.Value, result, nowUtc);
                nextRetryUtc = GetNextRetryUtcLocked(DateTimeOffset.UtcNow);
            }

            if (result.Accepted && result.Success)
            {
                deviceOps.RequestRefresh();
            }

            UpdateDelayedReconcileSchedule(nextRetryUtc, DateTimeOffset.UtcNow);
        }
        finally
        {
            reconcileGate.Release();
        }
    }

    private DeviceCommandPlan? BuildCommandLocked(IReadOnlyList<DeviceSnapshot> snapshot, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(targetDeviceId))
        {
            return null;
        }

        var deviceId = targetDeviceId;
        var onlineDevice = snapshot.FirstOrDefault(device =>
            device.Status == DeviceStatus.Online
            && string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

        if (targetEnabled)
        {
            if (onlineDevice is null)
            {
                status = DeviceSessionStatus.ActivationPending;
                return null;
            }

            CapturePreviousAppIfNeededLocked(onlineDevice);
            if (string.Equals(Normalize(onlineDevice.ActiveAppId), PanelsAppId, StringComparison.OrdinalIgnoreCase))
            {
                status = DeviceSessionStatus.Idle;
                retryCount = 0;
                nextAttemptUtc = DateTimeOffset.MinValue;
                lastErrorCode = null;
                return null;
            }

            if (nowUtc < nextAttemptUtc)
            {
                status = DeviceSessionStatus.ActivationPending;
                return null;
            }

            status = DeviceSessionStatus.ActivationPending;
            nextAttemptUtc = nowUtc + DispatchCooldown;
            return new DeviceCommandPlan(deviceId, PanelsAppId, PanelsAppName, DeviceSessionAction.ActivatePanels);
        }

        if (string.IsNullOrWhiteSpace(previousAppId))
        {
            ClearStateLocked();
            return null;
        }

        if (onlineDevice is null)
        {
            status = DeviceSessionStatus.RestorePending;
            return null;
        }

        if (string.Equals(Normalize(onlineDevice.ActiveAppId), previousAppId, StringComparison.OrdinalIgnoreCase))
        {
            ClearStateLocked();
            return null;
        }

        if (nowUtc < nextAttemptUtc)
        {
            status = DeviceSessionStatus.RestorePending;
            return null;
        }

        status = DeviceSessionStatus.RestorePending;
        nextAttemptUtc = nowUtc + DispatchCooldown;
        return new DeviceCommandPlan(deviceId, previousAppId, previousAppName, DeviceSessionAction.RestorePrevious);
    }

    private void CapturePreviousAppIfNeededLocked(DeviceSnapshot snapshot)
    {
        if (!string.IsNullOrWhiteSpace(previousAppId))
        {
            return;
        }

        var currentAppId = Normalize(snapshot.ActiveAppId);
        if (string.IsNullOrWhiteSpace(currentAppId)
            || string.Equals(currentAppId, PanelsAppId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        previousAppId = currentAppId;
        previousAppName = Normalize(snapshot.ActiveAppName);
    }

    private void ApplyResultLocked(DeviceCommandPlan plan, CommandDispatchResult result, DateTimeOffset nowUtc)
    {
        if (result.Accepted && result.Success)
        {
            retryCount = 0;
            nextAttemptUtc = nowUtc + DispatchCooldown;
            status = DeviceSessionStatus.Idle;
            lastErrorCode = null;

            if (plan.Action == DeviceSessionAction.RestorePrevious)
            {
                ClearStateLocked();
            }

            return;
        }

        RegisterFailureLocked(plan.Action, nowUtc, result.ErrorCode);
    }

    private void RegisterFailureLocked(DeviceSessionAction action, DateTimeOffset nowUtc, string? errorCode)
    {
        retryCount = Math.Clamp(retryCount + 1, 1, 32);
        nextAttemptUtc = nowUtc + ComputeRetryDelay(retryCount, errorCode);
        lastErrorCode = errorCode;
        status = action == DeviceSessionAction.RestorePrevious
            ? DeviceSessionStatus.RestoreFailed
            : DeviceSessionStatus.ActivationFailed;
    }

    private DateTimeOffset? GetNextRetryUtcLocked(DateTimeOffset nowUtc)
    {
        if (nextAttemptUtc <= nowUtc)
        {
            return null;
        }

        return status is DeviceSessionStatus.ActivationPending
            or DeviceSessionStatus.ActivationFailed
            or DeviceSessionStatus.RestorePending
            or DeviceSessionStatus.RestoreFailed
            ? nextAttemptUtc
            : null;
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

            if (!nextRetryUtc.HasValue || nextRetryUtc.Value <= nowUtc)
            {
                CancelDelayedReconcileLocked();
                return;
            }

            if (delayedReconcileCts is not null && delayedReconcileAtUtc <= nextRetryUtc.Value)
            {
                return;
            }

            CancelDelayedReconcileLocked();
            delayedReconcileAtUtc = nextRetryUtc.Value;
            delayedReconcileCts = new CancellationTokenSource();
            scheduledCts = delayedReconcileCts;
            due = delayedReconcileAtUtc - nowUtc;
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

    private void ClearStateLocked()
    {
        targetDeviceId = null;
        previousAppId = null;
        previousAppName = null;
        nextAttemptUtc = DateTimeOffset.MinValue;
        retryCount = 0;
        lastErrorCode = null;
        status = DeviceSessionStatus.Idle;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

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

    private readonly record struct DeviceCommandPlan(
        string DeviceId,
        string TargetAppId,
        string? TargetAppName,
        DeviceSessionAction Action);

    private enum DeviceSessionAction
    {
        ActivatePanels,
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
}
