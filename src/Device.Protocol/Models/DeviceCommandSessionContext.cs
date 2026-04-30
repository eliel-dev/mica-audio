namespace Device.Protocol.Models;

// DOCS: docs/wiki/modules/device-server-protocol.md#ownership-shadow-e-lock-lease
// DOCS: docs/wiki/reference/ws-protocol-v2.md#streamframev3-e-owner-epoch
// DOCS: docs/handoffs/2026-04-24-windows-client-session-ownership.md
public sealed record DeviceCommandSessionContext(
    string ClientId,
    uint OwnerEpoch,
    string? LockToken);
