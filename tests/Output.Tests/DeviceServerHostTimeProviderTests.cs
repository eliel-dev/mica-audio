using System.Net;
using System.Net.Http.Json;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

public class DeviceServerHostTimeProviderTests
{
    [Fact]
    public async Task PairingCode_ShouldExpireAccordingToInjectedTimeProvider()
    {
        var timeProvider = new ManualTimeProvider(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));
        var port = GetFreeTcpPort();

        await using var host = new DeviceServerHost(timeProvider);
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
        });

        using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var pairing = host.CreatePairingCode(TimeSpan.FromSeconds(30));

        timeProvider.Advance(TimeSpan.FromSeconds(31));

        var response = await client.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest
        {
            PairingCode = pairing.Code,
            DeviceName = "expired-device",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static int GetFreeTcpPort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
