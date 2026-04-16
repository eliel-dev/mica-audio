using App.WinUI.Infrastructure.Serial;

namespace Output.Tests;

public sealed class MicaSerialProtocolTests
{
    [Fact]
    public void TryReadHello_ShouldReturnDeviceIdWhenPresent()
    {
        const string line = """{"type":"hello","protocol":"mica.serial.v1","deviceId":"mp-device-01"}""";

        var parsed = MicaSerialProtocol.TryParseDocument(line, out var type, out var document);

        Assert.True(parsed);
        Assert.Equal("hello", type);

        using (document)
        {
            Assert.True(MicaSerialProtocol.TryReadHello(document!.RootElement, out var hello));
            Assert.Equal(MicaSerialProtocol.ProtocolId, hello.Protocol);
            Assert.Equal("mp-device-01", hello.DeviceId);
        }
    }

    [Fact]
    public void TryReadHello_ShouldAllowEmptyDeviceIdBeforePairing()
    {
        const string line = """{"type":"hello","protocol":"mica.serial.v1","deviceId":null}""";

        var parsed = MicaSerialProtocol.TryParseDocument(line, out _, out var document);

        Assert.True(parsed);

        using (document)
        {
            Assert.True(MicaSerialProtocol.TryReadHello(document!.RootElement, out var hello));
            Assert.Equal(MicaSerialProtocol.ProtocolId, hello.Protocol);
            Assert.Null(hello.DeviceId);
        }
    }

    [Fact]
    public void TryParseDocument_ShouldRejectNonJsonLines()
    {
        var parsed = MicaSerialProtocol.TryParseDocument("boot: ready", out var type, out var document);

        Assert.False(parsed);
        Assert.Equal(string.Empty, type);
        Assert.Null(document);
    }
}
