using Device.Protocol.Models;

// DOCS: docs/wiki/architecture/05-device-session-and-reconnect.md#lifecycle-de-device-leve
namespace App.WinUI.Services.Devices;

internal static class DeviceLifecyclePolicy
{
    public static DeviceLifecyclePresentation Build(DeviceSnapshot snapshot, DeviceLifecycleThresholds thresholds, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        if (snapshot.Status == DeviceStatus.Pairing)
        {
            return new DeviceLifecyclePresentation(
                "Registrado",
                "Aguardando provisionamento",
                "Pareamento iniciado, aguardando primeira sessao",
                "Ultimo contato: desconhecido",
                false,
                true,
                false,
                DeviceLifecycleIcon.Clock,
                DeviceLifecycleTone.Muted);
        }

        if (snapshot.IsRegistered && snapshot.FirstSeenUtc is null)
        {
            return new DeviceLifecyclePresentation(
                "Registrado",
                "Nunca conectado",
                "Aguardando primeiro provisionamento",
                "Ultimo contato: desconhecido",
                false,
                true,
                false,
                DeviceLifecycleIcon.Clock,
                DeviceLifecycleTone.Muted);
        }

        if (snapshot.Status == DeviceStatus.Online)
        {
            return new DeviceLifecyclePresentation(
                "Online",
                "Configurado",
                "Telemetria ativa",
                "Ultimo contato: agora",
                true,
                false,
                false,
                DeviceLifecycleIcon.Accept,
                DeviceLifecycleTone.Success);
        }

        var lastSeen = DeviceRegistryPresenceNormalizer.NormalizeTimestamp(snapshot.LastSeenUtc);
        if (lastSeen is null)
        {
            return new DeviceLifecyclePresentation(
                "Offline",
                "Registro salvo",
                "Sem historico confiavel de contato",
                "Ultimo contato: desconhecido",
                false,
                false,
                false,
                DeviceLifecycleIcon.Help,
                DeviceLifecycleTone.Muted);
        }

        var elapsed = nowUtc - lastSeen.Value;
        var isConfigUncertain = snapshot.ConfigState == DeviceConfigState.DriftSuspected || elapsed >= thresholds.Dormant;

        return new DeviceLifecyclePresentation(
            "Offline",
            isConfigUncertain ? "Configuracao incerta" : "Configurado",
            isConfigUncertain ? "Registro salvo, sem contato ha muito tempo" : "Sem telemetria recente",
            BuildLastSeenLabel(elapsed),
            false,
            false,
            isConfigUncertain,
            isConfigUncertain ? DeviceLifecycleIcon.Important : DeviceLifecycleIcon.Pause,
            isConfigUncertain ? DeviceLifecycleTone.Warning : DeviceLifecycleTone.Normal);
    }

    private static string BuildLastSeenLabel(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.FromMinutes(1))
        {
            return "Ultimo contato: ha menos de 1 min";
        }

        if (elapsed < TimeSpan.FromHours(1))
        {
            return $"Ultimo contato: ha {Math.Max(1, (int)Math.Floor(elapsed.TotalMinutes))}m";
        }

        if (elapsed < TimeSpan.FromHours(24))
        {
            return $"Ultimo contato: ha {Math.Max(1, (int)Math.Floor(elapsed.TotalHours))}h";
        }

        return $"Ultimo contato: ha {Math.Max(1, (int)Math.Floor(elapsed.TotalDays))}d";
    }
}

