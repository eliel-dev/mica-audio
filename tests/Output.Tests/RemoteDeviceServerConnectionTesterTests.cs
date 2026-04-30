using App.WinUI.Services.Devices;
using Device.Protocol.Contracts;
using Device.Server.Hosting;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Output.Tests;

public sealed class RemoteDeviceServerConnectionTesterTests
{
    private const string AdminToken = "settings-test-token";

    [Fact]
    public async Task TestAsync_ShouldValidateHealthAdminDevicesAndFramesWebSocket()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = mqttPort,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        var tester = new RemoteDeviceServerConnectionTester();

        var result = await tester.TestAsync($"http://127.0.0.1:{port}", AdminToken);

        Assert.True(result.Success, result.Message);
        Assert.True(result.HealthOk);
        Assert.True(result.AdminOk);
        Assert.True(result.FramesWebSocketOk);
    }

    [Fact]
    public async Task TestAsync_ShouldReturnVisibleFailureForWrongAdminToken()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = mqttPort,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        var tester = new RemoteDeviceServerConnectionTester();

        var result = await tester.TestAsync($"http://127.0.0.1:{port}", "wrong-token");

        Assert.False(result.Success);
        Assert.True(result.HealthOk);
        Assert.False(result.AdminOk);
        Assert.Contains("Admin token", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestAsync_ShouldExplainMissingAdminRouteWithoutVisualEndpoints()
    {
        using var legacyServer = new LegacyHttpServer();
        var tester = new RemoteDeviceServerConnectionTester(TimeSpan.FromSeconds(3));

        var result = await tester.TestAsync(legacyServer.BaseAddress, AdminToken);

        Assert.False(result.Success);
        Assert.True(result.HealthOk);
        Assert.False(result.AdminOk);
        Assert.Contains("admin", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("visual-endpoints", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("servidor remoto", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class LegacyHttpServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cts = new();
        private readonly Task worker;

        public LegacyHttpServer()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            BaseAddress = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
            worker = Task.Run(RunAsync);
        }

        public string BaseAddress { get; }

        public void Dispose()
        {
            cts.Cancel();
            listener.Stop();
            try
            {
                worker.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }

            cts.Dispose();
        }

        private async Task RunAsync()
        {
            while (!cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (SocketException) when (cts.IsCancellationRequested)
                {
                    return;
                }

                _ = Task.Run(() => HandleClientAsync(client), CancellationToken.None);
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync().ConfigureAwait(false) ?? string.Empty;
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync().ConfigureAwait(false)))
                {
                }

                var isHealth = requestLine.Contains(" /api/v1/health ", StringComparison.OrdinalIgnoreCase);
                var status = isHealth ? "200 OK" : "404 Not Found";
                var body = isHealth ? """{"status":"ok"}""" : """{"error":"not_found"}""";
                var payload = Encoding.UTF8.GetBytes(body);
                var header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 {status}\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header).ConfigureAwait(false);
                await stream.WriteAsync(payload).ConfigureAwait(false);
            }
        }
    }
}
