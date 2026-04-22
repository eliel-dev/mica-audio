using System.Xml.Linq;
using Device.Client;
using Device.Client.Embedded;
using Device.Server.Hosting;
using Output.Led;

namespace Output.Tests;

public sealed class ServerAbstractionBoundaryTests
{
    [Fact]
    public void DeviceServerContracts_ShouldLiveInAbstractionsAssembly()
    {
        Assert.Equal("Device.Server.Abstractions", typeof(IDeviceServerHost).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(IDeviceOfficialFirmwareCatalog).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(IPanelsBatchStore).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(PanelsBatchWrite).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(PanelsBatchEntry).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(IDevicePairingStore).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(ICommandStateStore).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(TrackedCommandState).Assembly.GetName().Name);
    }

    [Fact]
    public void DeviceServerInMemoryStores_ShouldLiveInServerAssembly()
    {
        Assert.Equal("Device.Server", typeof(InMemoryPanelsBatchStore).Assembly.GetName().Name);
        Assert.True(typeof(IPanelsBatchStore).IsAssignableFrom(typeof(InMemoryPanelsBatchStore)));
        Assert.Equal("Device.Server", typeof(InMemoryDevicePairingStore).Assembly.GetName().Name);
        Assert.True(typeof(IDevicePairingStore).IsAssignableFrom(typeof(InMemoryDevicePairingStore)));
        Assert.Equal("Device.Server", typeof(InMemoryCommandStateStore).Assembly.GetName().Name);
        Assert.True(typeof(ICommandStateStore).IsAssignableFrom(typeof(InMemoryCommandStateStore)));
    }

    [Fact]
    public void DeviceClientContracts_ShouldLiveInClientAbstractionsAssembly()
    {
        Assert.Equal("Device.Client.Abstractions", typeof(IDeviceServerClient).Assembly.GetName().Name);
        Assert.Equal("Device.Client.Abstractions", typeof(IDeviceFrameTransport).Assembly.GetName().Name);
        Assert.Equal("Device.Client.Abstractions", typeof(PanelsBatchRegistration).Assembly.GetName().Name);
        Assert.True(typeof(IDeviceFrameTransport).IsAssignableFrom(typeof(IDeviceServerHost)));
    }

    [Fact]
    public void DeviceEmbeddedClient_ShouldLiveInEmbeddedAssembly()
    {
        Assert.Equal("Device.Client.Embedded", typeof(EmbeddedDeviceServerClient).Assembly.GetName().Name);
        Assert.True(typeof(IDeviceServerClient).IsAssignableFrom(typeof(EmbeddedDeviceServerClient)));
        Assert.True(typeof(IEmbeddedDeviceServerClientRuntime).IsAssignableFrom(typeof(EmbeddedDeviceServerClient)));
    }

    [Fact]
    public void Esp32Output_ShouldDependOnFrameTransportOnly()
    {
        var constructor = Assert.Single(typeof(Esp32S3LedOutput).GetConstructors());

        Assert.Equal(typeof(IDeviceFrameTransport), constructor.GetParameters()[0].ParameterType);
    }

    [Fact]
    public void OutputProject_ShouldReferenceClientAbstractionsWithoutServerContracts()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Output",
            "Output.csproj"));

        var project = XDocument.Load(projectPath);
        var references = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value.Replace('\\', '/'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        Assert.Contains("../Device.Client.Abstractions/Device.Client.Abstractions.csproj", references);
        Assert.DoesNotContain("../Device.Server.Abstractions/Device.Server.Abstractions.csproj", references);
        Assert.DoesNotContain("../Device.Server/Device.Server.csproj", references);
    }

    [Fact]
    public void EmbeddedClientProject_ShouldNotReferenceAppWinUI()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Device.Client.Embedded",
            "Device.Client.Embedded.csproj"));

        var project = XDocument.Load(projectPath);
        var references = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value.Replace('\\', '/'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        Assert.Contains("../Device.Client.Abstractions/Device.Client.Abstractions.csproj", references);
        Assert.Contains("../Device.Server.Abstractions/Device.Server.Abstractions.csproj", references);
        Assert.Contains("../Device.Protocol/Device.Protocol.csproj", references);
        Assert.DoesNotContain("../App.WinUI/App.WinUI.csproj", references);
    }
}
