using App.WinUI.Services.Devices;
using Device.Protocol.Contracts;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class RemoteDeviceServerConnectionTesterTests
{
    private const string AdminToken = "settings-test-token";

    [Fact]
    public async Task TestAsync_ShouldValidateHealthAdminFramesWebSocketAndVisualEndpoints()
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
}
