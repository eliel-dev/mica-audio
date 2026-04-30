namespace Device.Client.Embedded;

// DOCS: docs/wiki/modules/app-winui.md#referencias-de-codigo
// DOCS: docs/handoffs/2026-04-22-device-client-embedded-adapter.md
public sealed class EmbeddedDeviceServerSettings
{
    public int DeviceFreshThresholdSeconds { get; init; } = 15;

    public bool AllowLegacyWebSocketQueryToken { get; init; }
}
