namespace App.WinUI.Services.Firmware;

// DOCS: docs/wiki/modules/server-build-and-artifacts.md#manifesto-oficial
internal sealed record OfficialFirmwareRefreshResult(
    bool WasRegenerated,
    bool IsFresh,
    string FailureReason,
    ResolvedFirmwareArtifact? ResolvedArtifact);
