namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/wiki/reference/ws-protocol-v2.md#estrutura-streamframev2
// DOCS: docs/handoffs/2026-04-22-device-server-client-boundary.md
public interface IDeviceFrameTransport
{
    void SendFrame(string deviceId, byte[] framePayload);

    void BroadcastFrame(byte[] framePayload);
}
