namespace Device.Client.Embedded;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/handoffs/2026-04-22-device-client-embedded-adapter.md
public interface IEmbeddedDevicePublicHostResolver
{
    string ResolvePublicHost();
}
