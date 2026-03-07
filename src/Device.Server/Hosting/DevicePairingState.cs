using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#politicas-de-seguranca
internal sealed class DevicePairingState
{
    private readonly Dictionary<string, DateTimeOffset> pairingCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PairingAttemptWindow> pairingAttemptsByIp = new(StringComparer.OrdinalIgnoreCase);

    public PairingCodeInfo CreateCode(string code, TimeSpan ttl, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var now = timeProvider.GetUtcNow();
        var expiresAt = now.Add(ttl);
        CleanupExpiredCodes(now);
        pairingCodes[code] = expiresAt;
        return new PairingCodeInfo
        {
            Code = code,
            ExpiresAtUtc = expiresAt,
        };
    }

    public bool TryConsume(string code, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var now = timeProvider.GetUtcNow();
        CleanupExpiredCodes(now);
        if (!pairingCodes.Remove(code, out var expiresAt))
        {
            return false;
        }

        return expiresAt > now;
    }

    public bool TryRegisterAttempt(
        string remoteIpKey,
        DeviceServerRuntimeConfig config,
        TimeProvider timeProvider,
        out int retryAfterSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteIpKey);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var now = timeProvider.GetUtcNow();
        CleanupStaleAttempts(now, config.PairingAttemptWindow);

        if (!pairingAttemptsByIp.TryGetValue(remoteIpKey, out var current)
            || (now - current.WindowStartUtc) >= config.PairingAttemptWindow)
        {
            current = new PairingAttemptWindow(now, 0);
        }

        if (current.Attempts >= config.PairingAttemptsPerWindow)
        {
            var wait = (current.WindowStartUtc + config.PairingAttemptWindow) - now;
            retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
            return false;
        }

        pairingAttemptsByIp[remoteIpKey] = current with { Attempts = current.Attempts + 1 };
        retryAfterSeconds = 0;
        return true;
    }

    public void ResetAttempts(string remoteIpKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteIpKey);
        pairingAttemptsByIp.Remove(remoteIpKey);
    }

    public void Clear()
    {
        pairingCodes.Clear();
        pairingAttemptsByIp.Clear();
    }

    private void CleanupExpiredCodes(DateTimeOffset now)
    {
        foreach (var expired in pairingCodes.Where(kv => kv.Value <= now).ToArray())
        {
            pairingCodes.Remove(expired.Key);
        }
    }

    private void CleanupStaleAttempts(DateTimeOffset now, TimeSpan window)
    {
        foreach (var stale in pairingAttemptsByIp.Where(kv => (now - kv.Value.WindowStartUtc) > window).ToArray())
        {
            pairingAttemptsByIp.Remove(stale.Key);
        }
    }
}

internal readonly record struct PairingAttemptWindow(DateTimeOffset WindowStartUtc, int Attempts);
