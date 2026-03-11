using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/device-operations-coordinator.md#render-estavel-na-devicespage
internal sealed class DeviceLogBook
{
    private readonly object gate = new();
    private readonly List<string> logs = new();
    private readonly Dictionary<string, List<string>> deviceLogsById = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> awaitingFirstTelemetryByDevice = new(StringComparer.OrdinalIgnoreCase);
    private readonly int maxLogEntries;
    private readonly int maxDeviceLogEntries;

    public DeviceLogBook(int maxLogEntries, int maxDeviceLogEntries)
    {
        this.maxLogEntries = maxLogEntries;
        this.maxDeviceLogEntries = maxDeviceLogEntries;
    }

    public IReadOnlyList<string> GetGlobalLogs()
    {
        lock (gate)
        {
            return logs.ToArray();
        }
    }

    public IReadOnlyList<string> GetDeviceLogs(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Array.Empty<string>();
        }

        lock (gate)
        {
            if (!deviceLogsById.TryGetValue(deviceId.Trim(), out var entries) || entries.Count == 0)
            {
                return Array.Empty<string>();
            }

            return entries.ToArray();
        }
    }

    public bool AppendGlobal(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        lock (gate)
        {
            logs.Add($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
            TrimToLimit(logs, maxLogEntries);
            return true;
        }
    }

    public bool AppendDevice(string deviceId, string message)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        lock (gate)
        {
            AppendDeviceLocked(deviceId.Trim(), message, DateTimeOffset.Now);
            return true;
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
                    AppendDeviceLocked(deviceId, "Firmware legado detectado; regrave para ativar controle MQTT.", now);
                }

                if (previousControlPlaneState == DeviceControlPlaneState.LegacyOnly
                    && currentControlPlaneState == DeviceControlPlaneState.MqttOnline)
                {
                    AppendDeviceLocked(deviceId, "Control plane MQTT ativo; firmware compativel confirmado.", now);
                }

                var isOnline = current.Status == DeviceStatus.Online;
                if (previousSnapshot is null)
                {
                    if (isOnline)
                    {
                        AppendDeviceLocked(deviceId, "Dispositivo autenticado e online.", now);
                        awaitingFirstTelemetryByDevice.Add(deviceId);
                    }
                }
                else
                {
                    var wasOnline = previousSnapshot.Status == DeviceStatus.Online;
                    if (!wasOnline && isOnline)
                    {
                        AppendDeviceLocked(deviceId, "Dispositivo autenticado e online.", now);
                        if (previousSnapshot.Status == DeviceStatus.Offline)
                        {
                            AppendDeviceLocked(deviceId, "Dispositivo voltou a aparecer apos ficar offline.", now);
                        }

                        awaitingFirstTelemetryByDevice.Add(deviceId);
                    }
                    else if (wasOnline && !isOnline)
                    {
                        AppendDeviceLocked(deviceId, "Dispositivo ficou offline.", now);
                        awaitingFirstTelemetryByDevice.Remove(deviceId);
                    }
                }

                if (isOnline
                    && awaitingFirstTelemetryByDevice.Contains(deviceId)
                    && HasFreshTelemetrySample(previousSnapshot, current))
                {
                    AppendDeviceLocked(deviceId, "Primeira telemetria recebida apos reconexao.", now);
                    awaitingFirstTelemetryByDevice.Remove(deviceId);
                }

                if (previousSnapshot is null)
                {
                    if (!string.IsNullOrWhiteSpace(current.WifiState))
                    {
                        AppendDeviceLocked(deviceId, $"Wi-Fi estado: {current.WifiState}.", now);
                    }

                    if (current.ProvisioningPortalActive == true)
                    {
                        AppendDeviceLocked(deviceId, "Portal de provisioning ativo.", now);
                    }

                    var currentConnectivityEvent = DeviceConnectivityEventClassifier.NormalizeForUi(current.LastWifiEvent);
                    if (DeviceConnectivityEventClassifier.ShouldSurfaceConnectivityEvent(currentConnectivityEvent))
                    {
                        AppendDeviceLocked(deviceId, $"Evento conectividade: {currentConnectivityEvent}.", now);
                    }
                }
                else
                {
                    if (!string.Equals(previousSnapshot.WifiState, current.WifiState, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(current.WifiState))
                    {
                        AppendDeviceLocked(deviceId, $"Wi-Fi estado: {current.WifiState}.", now);
                    }

                    if (previousSnapshot.ProvisioningPortalActive != current.ProvisioningPortalActive
                        && current.ProvisioningPortalActive.HasValue)
                    {
                        var portalState = current.ProvisioningPortalActive.Value ? "ativo" : "inativo";
                        AppendDeviceLocked(deviceId, $"Portal de provisioning: {portalState}.", now);
                    }

                    var previousConnectivityEvent = DeviceConnectivityEventClassifier.NormalizeForUi(previousSnapshot.LastWifiEvent);
                    var currentConnectivityEvent = DeviceConnectivityEventClassifier.NormalizeForUi(current.LastWifiEvent, previousConnectivityEvent);
                    if (!string.Equals(previousConnectivityEvent, currentConnectivityEvent, StringComparison.OrdinalIgnoreCase)
                        && DeviceConnectivityEventClassifier.ShouldSurfaceConnectivityEvent(currentConnectivityEvent))
                    {
                        AppendDeviceLocked(deviceId, $"Evento conectividade: {currentConnectivityEvent}.", now);
                    }
                }
            }

            awaitingFirstTelemetryByDevice.RemoveWhere(id => !seenIds.Contains(id));
        }
    }

    private void AppendDeviceLocked(string deviceId, string message, DateTimeOffset now)
    {
        if (!deviceLogsById.TryGetValue(deviceId, out var entries))
        {
            entries = new List<string>();
            deviceLogsById[deviceId] = entries;
        }

        entries.Add($"[{now:HH:mm:ss}] {message}");
        TrimToLimit(entries, maxDeviceLogEntries);
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

    private static void TrimToLimit(List<string> entries, int limit)
    {
        if (entries.Count <= limit)
        {
            return;
        }

        entries.RemoveRange(0, entries.Count - limit);
    }
}
