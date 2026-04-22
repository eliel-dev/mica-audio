using Device.Protocol.Models;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#storage-de-pairing
// DOCS: docs/wiki/architecture/08-render-cloud-migration-plan.md#interfaces-e-contratos
// DOCS: docs/handoffs/2026-04-22-device-server-pairing-store.md
public sealed class InMemoryDevicePairingStore : IDevicePairingStore
{
    private readonly object gate = new();
    private readonly Dictionary<string, DateTimeOffset> pairingCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PairingAttemptWindow> pairingAttemptsByIp = new(StringComparer.OrdinalIgnoreCase);

    public PairingCodeInfo SaveCode(string code, TimeSpan ttl, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var expiresAt = now.Add(ttl);
        lock (gate)
        {
            CleanupExpiredCodes(now);
            pairingCodes[code] = expiresAt;
        }

        return new PairingCodeInfo
        {
            Code = code,
            ExpiresAtUtc = expiresAt,
        };
    }

    public bool TryConsumeCode(string code, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        lock (gate)
        {
            CleanupExpiredCodes(now);
            if (!pairingCodes.Remove(code, out var expiresAt))
            {
                return false;
            }

            return expiresAt > now;
        }
    }

    public bool TryRegisterAttempt(
        string remoteIpKey,
        int attemptsPerWindow,
        TimeSpan window,
        DateTimeOffset now,
        out int retryAfterSeconds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteIpKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(attemptsPerWindow, 1);
        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), window, "Pairing attempt window must be positive.");
        }

        lock (gate)
        {
            CleanupStaleAttempts(now, window);

            if (!pairingAttemptsByIp.TryGetValue(remoteIpKey, out var current)
                || (now - current.WindowStartUtc) >= window)
            {
                current = new PairingAttemptWindow(now, 0);
            }

            if (current.Attempts >= attemptsPerWindow)
            {
                var wait = (current.WindowStartUtc + window) - now;
                retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
                return false;
            }

            pairingAttemptsByIp[remoteIpKey] = current with { Attempts = current.Attempts + 1 };
        }

        retryAfterSeconds = 0;
        return true;
    }

    public void ResetAttempts(string remoteIpKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteIpKey);

        lock (gate)
        {
            pairingAttemptsByIp.Remove(remoteIpKey);
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            pairingCodes.Clear();
            pairingAttemptsByIp.Clear();
        }
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

    private readonly record struct PairingAttemptWindow(DateTimeOffset WindowStartUtc, int Attempts);
}
