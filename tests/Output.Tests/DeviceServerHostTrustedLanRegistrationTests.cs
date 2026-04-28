using System.Net;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class DeviceServerHostTrustedLanRegistrationTests
{
    [Fact]
    public async Task TrustedLanRegistration_ShouldCreateAndReuseDeviceByMac()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "192.168.15.10",
            Port = port,
            MqttPort = mqttPort,
            MqttRootTopic = "mica/test/devices",
            TrustedLanAutoRegistration = true,
            DiscoveryUdpPort = 5275,
            VisualUdpPort = 5274,
        });

        var request = new MicaDiscoveryRequestV1
        {
            DeviceMac = "AA:BB:CC:DD:EE:FF",
            DeviceIp = "192.168.15.51",
            DeviceName = "Painel bancada",
            FirmwareVersion = "0.7.0-test",
            BoardModel = "esp32s3_devkitc1",
            PanelType = "hub75_128x64",
            Profile = "dma_exp",
        };

        var first = host.TryRegisterTrustedLanDevice(request, IPAddress.Parse("192.168.15.51"));
        var second = host.TryRegisterTrustedLanDevice(request, IPAddress.Parse("192.168.15.51"));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.DeviceId, second!.DeviceId);
        Assert.Equal(first.Token, second.Token);
        Assert.Equal($"http://192.168.15.10:{port}", first.HttpBase);
        Assert.Equal("192.168.15.10", first.MqttHost);
        Assert.Equal(mqttPort, first.MqttPort);
        Assert.Equal("mica/test/devices", first.MqttRootTopic);
        Assert.Equal("/ws/v1/stream", first.WsPath);
        Assert.Equal(5274, first.VisualUdpPort);

        var record = Assert.Single(host.GetDeviceRecords());
        Assert.Equal("aa:bb:cc:dd:ee:ff", record.DeviceMac);
        Assert.Equal("Painel bancada", record.Name);
        Assert.Equal("esp32s3_devkitc1", record.BoardModel);
        Assert.Equal("hub75_128x64", record.PanelType);
        Assert.Equal("192.168.15.51", record.LastKnownIp);
        Assert.Equal("192.168.15.51", record.LanIpAddress);
    }

    [Fact]
    public async Task TrustedLanRegistration_ShouldReuseSameDeviceAfterCleanFlashAndUpdateFirmware()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "192.168.15.10",
            Port = port,
            MqttPort = mqttPort,
            TrustedLanAutoRegistration = true,
        });

        var first = host.TryRegisterTrustedLanDevice(new MicaDiscoveryRequestV1
        {
            DeviceMac = "AA:BB:CC:DD:EE:01",
            DeviceIp = "192.168.15.51",
            DeviceName = "Painel bancada",
            FirmwareVersion = "v2026.04.28-210000Z-untagged-aac170d",
            BoardModel = "esp32s3_devkitc1",
            PanelType = "hub75_128x64",
            Profile = "dma_exp",
        }, IPAddress.Parse("172.17.0.1"));

        var second = host.TryRegisterTrustedLanDevice(new MicaDiscoveryRequestV1
        {
            DeviceMac = "aa-bb-cc-dd-ee-01",
            DeviceIp = "192.168.15.52",
            DeviceName = "Painel bancada novo flash",
            FirmwareVersion = "v2026.04.28-222740Z-untagged-aac170d",
            BoardModel = "esp32s3_devkitc1",
            PanelType = "hub75_128x64",
            Profile = "dma_exp",
        }, IPAddress.Parse("172.17.0.1"));

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first!.DeviceId, second!.DeviceId);
        Assert.Equal(first.Token, second.Token);

        var record = Assert.Single(host.GetDeviceRecords());
        Assert.Equal(first.DeviceId, record.DeviceId);
        Assert.Equal("aa:bb:cc:dd:ee:01", record.DeviceMac);
        Assert.Equal("v2026.04.28-222740Z-untagged-aac170d", record.FirmwareVersion);
        Assert.Equal("172.17.0.1", record.LastKnownIp);
        Assert.Equal("192.168.15.52", record.LanIpAddress);
    }

    [Fact]
    public async Task TrustedLanRegistration_ShouldRejectWhenDisabledOrDeviceLimitReached()
    {
        var disabledPort = DeviceServerTestHarness.GetFreeTcpPort();
        var disabledMqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        await using var disabledHost = new DeviceServerHost();
        await disabledHost.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = disabledPort,
            MqttPort = disabledMqttPort,
            TrustedLanAutoRegistration = false,
        });

        var request = new MicaDiscoveryRequestV1
        {
            DeviceMac = "10:20:30:40:50:60",
            DeviceName = "Blocked",
        };

        Assert.Null(disabledHost.TryRegisterTrustedLanDevice(request, IPAddress.Loopback));

        var limitedPort = DeviceServerTestHarness.GetFreeTcpPort();
        var limitedMqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        await using var limitedHost = new DeviceServerHost();
        await limitedHost.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = limitedPort,
            MqttPort = limitedMqttPort,
            MaxDevices = 1,
            TrustedLanAutoRegistration = true,
        });

        var accepted = limitedHost.TryRegisterTrustedLanDevice(request, IPAddress.Loopback);
        Assert.NotNull(accepted);

        var rejected = limitedHost.TryRegisterTrustedLanDevice(new MicaDiscoveryRequestV1
        {
            DeviceMac = "10:20:30:40:50:61",
            DeviceName = "Over limit",
        }, IPAddress.Loopback);

        Assert.Null(rejected);
    }
}
