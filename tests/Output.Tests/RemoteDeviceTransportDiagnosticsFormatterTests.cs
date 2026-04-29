using App.WinUI.Services.Devices;
using Device.Client.Remote;

namespace Output.Tests;

public sealed class RemoteDeviceTransportDiagnosticsFormatterTests
{
    [Fact]
    public void Format_ShouldExposeDirectUdpFallbackMissingEndpointAndLastErrors()
    {
        var diagnostics = new RemoteDeviceFrameTransportDiagnostics(
            DirectUdpFramesSent: 42,
            WebSocketFallbackFramesQueued: 3,
            MissingEndpointFrames: 2,
            LastEndpointRefreshError: "admin 401",
            LastUdpError: "network unreachable");

        var text = RemoteDeviceTransportDiagnosticsFormatter.Format(diagnostics);

        Assert.Contains("UDP direto enviados: 42", text, StringComparison.Ordinal);
        Assert.Contains("fallback WS: 3", text, StringComparison.Ordinal);
        Assert.Contains("endpoint ausente: 2", text, StringComparison.Ordinal);
        Assert.Contains("endpoint: admin 401", text, StringComparison.Ordinal);
        Assert.Contains("UDP: network unreachable", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Format_ShouldReturnEmbeddedMessage_WhenDiagnosticsAreUnavailable()
    {
        var text = RemoteDeviceTransportDiagnosticsFormatter.Format(null);

        Assert.Contains("Embedded", text, StringComparison.OrdinalIgnoreCase);
    }
}
