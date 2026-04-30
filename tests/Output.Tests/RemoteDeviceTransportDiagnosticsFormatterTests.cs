using App.WinUI.Services.Devices;
using Device.Client.Remote;

namespace Output.Tests;

public sealed class RemoteDeviceTransportDiagnosticsFormatterTests
{
    [Fact]
    public void Format_ShouldExposeServerWebSocketFramesAndLastError()
    {
        var diagnostics = new RemoteDeviceFrameTransportDiagnostics(
            ServerWebSocketFramesQueued: 42,
            ServerWebSocketFramesSent: 40,
            ServerWebSocketReconnects: 2,
            LastWebSocketError: "admin ws 401");

        var text = RemoteDeviceTransportDiagnosticsFormatter.Format(diagnostics);

        Assert.Contains("servidor WS enfileirados: 42", text, StringComparison.Ordinal);
        Assert.Contains("enviados: 40", text, StringComparison.Ordinal);
        Assert.Contains("reconnects: 2", text, StringComparison.Ordinal);
        Assert.Contains("admin ws 401", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UDP direto", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Format_ShouldReturnEmbeddedMessage_WhenDiagnosticsAreUnavailable()
    {
        var text = RemoteDeviceTransportDiagnosticsFormatter.Format(null);

        Assert.Contains("Embedded", text, StringComparison.OrdinalIgnoreCase);
    }
}
