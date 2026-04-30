using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#render-estavel-na-devicespage
internal sealed class DeviceLogBook
{
    private readonly object gate = new();
    private readonly List<DeviceLogEntry> logs = new();
    private readonly Dictionary<string, List<DeviceLogEntry>> deviceLogsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, uint> lastDeviceSequenceById = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> awaitingFirstTelemetryByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly int maxLogEntries;
    private readonly int maxDeviceLogEntries;

    public DeviceLogBook(int maxLogEntries, int maxDeviceLogEntries)
    {
        this.maxLogEntries = maxLogEntries;
        this.maxDeviceLogEntries = maxDeviceLogEntries;
    }

    public IReadOnlyList<DeviceLogEntry> GetGlobalLogs()
    {
        lock (gate)
        {
            return logs.ToArray();
        }
    }

    public IReadOnlyList<DeviceLogEntry> GetDeviceLogs(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Array.Empty<DeviceLogEntry>();
        }

        lock (gate)
        {
            if (!deviceLogsById.TryGetValue(deviceId.Trim(), out var entries) || entries.Count == 0)
            {
                return Array.Empty<DeviceLogEntry>();
            }

            return entries.ToArray();
        }
    }

    public bool AppendGlobal(
        string message,
        DeviceLogSeverity severity = DeviceLogSeverity.Info,
        string category = "server",
        string? code = null,
        DeviceLogSource source = DeviceLogSource.Server)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        lock (gate)
        {
            logs.Add(new DeviceLogEntry(DateTimeOffset.Now, deviceId: null, source, severity, category, code, message));
            TrimToLimit(logs, maxLogEntries);
            return true;
        }
    }

    public bool AppendDevice(
        string deviceId,
        string message,
        DeviceLogSeverity severity = DeviceLogSeverity.Info,
        string category = "app",
        string? code = null,
        DeviceLogSource source = DeviceLogSource.App)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        lock (gate)
        {
            AppendDeviceLocked(deviceId.Trim(), source, severity, category, code, message, DateTimeOffset.Now);
            return true;
        }
    }

    public bool AppendDeviceFirmware(DeviceLogMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (string.IsNullOrWhiteSpace(message.DeviceId) || string.IsNullOrWhiteSpace(message.Message))
        {
            return false;
        }

        lock (gate)
        {
            var deviceId = message.DeviceId.Trim();
            if (lastDeviceSequenceById.TryGetValue(deviceId, out var lastSequence)
                && lastSequence == message.Sequence)
            {
                return false;
            }

            lastDeviceSequenceById[deviceId] = message.Sequence;
            AppendDeviceLocked(
                deviceId,
                DeviceLogSource.Device,
                MapSeverity(message.Level),
                NormalizeToken(message.Category, "device"),
                NormalizeOptional(message.EventCode),
                message.Message,
                DateTimeOffset.Now);
            return true;
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
            var normalized = deviceId.Trim();
            deviceLogsById.Remove(normalized);
            lastDeviceSequenceById.Remove(normalized);
            awaitingFirstTelemetryByDevice.Remove(normalized);
        }
    }

    public void RecordLifecycleEvents(
        IReadOnlyList<DeviceSnapshot> previous,
        IReadOnlyList<DeviceSnapshot> next,
        DateTimeOffset now)
    {
        lock (gate)
        {
            var previousById = new Dictionary<string, DeviceSnapshot>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in previous)
            {
                if (!string.IsNullOrWhiteSpace(item.DeviceId))
                {
                    previousById[item.DeviceId] = item;
                }
            }

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var current in next)
            {
                if (string.IsNullOrWhiteSpace(current.DeviceId))
                {
                    continue;
                }

                var deviceId = current.DeviceId;
                seenIds.Add(deviceId);
                previousById.TryGetValue(deviceId, out var previousSnapshot);
                var previousControlPlaneState = previousSnapshot?.ControlPlaneState ?? DeviceControlPlaneState.Offline;
                var currentControlPlaneState = current.ControlPlaneState;

                if (previousControlPlaneState != DeviceControlPlaneState.LegacyOnly
                    && currentControlPlaneState == DeviceControlPlaneState.LegacyOnly)
                {
                    AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Warning, "lifecycle", "legacy_detected", "Firmware legado detectado; regrave para ativar controle MQTT.", now);
                }

                if (previousControlPlaneState == DeviceControlPlaneState.LegacyOnly
                    && currentControlPlaneState == DeviceControlPlaneState.MqttOnline)
                {
                    AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Info, "lifecycle", "mqtt_online", "Control plane MQTT ativo; firmware compativel confirmado.", now);
                }

                var isOnline = current.Status == DeviceStatus.Online;
                if (previousSnapshot is null)
                {
                    if (isOnline)
                    {
                        AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Info, "lifecycle", "online", "Dispositivo autenticado e online.", now);
                        awaitingFirstTelemetryByDevice.Add(deviceId);
                    }
                }
                else
                {
                    var wasOnline = previousSnapshot.Status == DeviceStatus.Online;
                    if (!wasOnline && isOnline)
                    {
                        AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Info, "lifecycle", "online", "Dispositivo autenticado e online.", now);
                        if (previousSnapshot.Status == DeviceStatus.Offline)
                        {
                            AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Info, "lifecycle", "back_online", "Dispositivo voltou a aparecer apos ficar offline.", now);
                        }

                        awaitingFirstTelemetryByDevice.Add(deviceId);
                    }
                    else if (wasOnline && !isOnline)
                    {
                        AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Warning, "lifecycle", "offline", "Dispositivo ficou offline.", now);
                        awaitingFirstTelemetryByDevice.Remove(deviceId);
                    }
                }

                if (isOnline
                    && awaitingFirstTelemetryByDevice.Contains(deviceId)
                    && HasFreshTelemetrySample(previousSnapshot, current))
                {
                    AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Info, "telemetry", "first_after_reconnect", "Primeira telemetria recebida apos reconexao.", now);
                    awaitingFirstTelemetryByDevice.Remove(deviceId);
                }

                if (previousSnapshot is null)
                {
                    if (!string.IsNullOrWhiteSpace(current.WifiState))
                    {
                        AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Info, "wifi", "state", $"Wi-Fi estado: {current.WifiState}.", now);
                    }

                    if (current.ProvisioningPortalActive == true)
                    {
                        AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Warning, "portal", "active", "Portal de provisioning ativo.", now);
                    }

                    var currentConnectivityEvent = DeviceConnectivityEventClassifier.NormalizeForUi(current.LastWifiEvent);
                    if (DeviceConnectivityEventClassifier.ShouldSurfaceConnectivityEvent(currentConnectivityEvent))
                    {
                        AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Info, "wifi", "event", $"Evento conectividade: {currentConnectivityEvent}.", now);
                    }
                }
                else
                {
                    if (!string.Equals(previousSnapshot.WifiState, current.WifiState, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(current.WifiState))
                    {
                        AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Info, "wifi", "state", $"Wi-Fi estado: {current.WifiState}.", now);
                    }

                    if (previousSnapshot.ProvisioningPortalActive != current.ProvisioningPortalActive
                        && current.ProvisioningPortalActive.HasValue)
                    {
                        var portalState = current.ProvisioningPortalActive.Value ? "ativo" : "inativo";
                        AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Info, "portal", "state", $"Portal de provisioning: {portalState}.", now);
                    }

                    var previousConnectivityEvent = DeviceConnectivityEventClassifier.NormalizeForUi(previousSnapshot.LastWifiEvent);
                    var currentConnectivityEvent = DeviceConnectivityEventClassifier.NormalizeForUi(current.LastWifiEvent, previousConnectivityEvent);
                    if (!string.Equals(previousConnectivityEvent, currentConnectivityEvent, StringComparison.OrdinalIgnoreCase)
                        && DeviceConnectivityEventClassifier.ShouldSurfaceConnectivityEvent(currentConnectivityEvent))
                    {
                        AppendDeviceLocked(deviceId, DeviceLogSource.App, DeviceLogSeverity.Info, "wifi", "event", $"Evento conectividade: {currentConnectivityEvent}.", now);
                    }
                }
            }

            awaitingFirstTelemetryByDevice.RemoveWhere(id => !seenIds.Contains(id));
        }
    }

    private void AppendDeviceLocked(
        string deviceId,
        DeviceLogSource source,
        DeviceLogSeverity severity,
        string category,
        string? code,
        string message,
        DateTimeOffset now)
    {
        if (!deviceLogsById.TryGetValue(deviceId, out var entries))
        {
            entries = new List<DeviceLogEntry>();
            deviceLogsById[deviceId] = entries;
        }

        entries.Add(new DeviceLogEntry(now, deviceId, source, severity, category, code, message));
        TrimToLimit(entries, maxDeviceLogEntries);
    }

    private static DeviceLogSeverity MapSeverity(string? rawLevel)
    {
        return NormalizeToken(rawLevel, "info") switch
        {
            "warning" => DeviceLogSeverity.Warning,
            "error" => DeviceLogSeverity.Error,
            _ => DeviceLogSeverity.Info,
        };
    }

    private static bool HasFreshTelemetrySample(DeviceSnapshot? previous, DeviceSnapshot current)
    {
        if (!current.LastTelemetryUtc.HasValue)
        {
            return false;
        }

        if (previous is null)
        {
            return true;
        }

        return !previous.LastTelemetryUtc.HasValue || current.LastTelemetryUtc > previous.LastTelemetryUtc;
    }

    private static string NormalizeToken(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void TrimToLimit<T>(List<T> entries, int limit)
    {
        if (entries.Count <= limit)
        {
            return;
        }

        entries.RemoveRange(0, entries.Count - limit);
    }
}
