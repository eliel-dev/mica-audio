namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/reference/device-telemetry-v2-fields.md#shadow-retained-de-sessao
// DOCS: docs/handoffs/2026-04-23-client-owned-lan-data-plane-and-session-ownership.md
public sealed class DeviceSessionShadowMessage
{
    public string DeviceId { get; init; } = string.Empty;

    public uint ShadowVersion { get; init; }

    public string? Mode { get; init; }

    public string? ActiveClientId { get; init; }

    public uint? ActiveOwnerEpoch { get; init; }

    public int? OwnerLeaseRemainingMs { get; init; }

    public bool? LockHeld { get; init; }

    public string? LockClientId { get; init; }

    public string? LockReason { get; init; }

    public int? LockLeaseRemainingMs { get; init; }

    public string? ActiveAppId { get; init; }

    public string? FallbackState { get; init; }
}
