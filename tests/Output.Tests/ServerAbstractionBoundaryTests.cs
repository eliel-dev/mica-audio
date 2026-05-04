using System.Xml.Linq;
using System.Net.WebSockets;
using System.Threading.Channels;
using Device.Client;
using Device.Client.Remote;
using Device.Server.Hosting;
using Output.Led;

namespace Output.Tests;

public sealed class ServerAbstractionBoundaryTests
{
    [Fact]
    public void DeviceServerContracts_ShouldLiveInAbstractionsAssembly()
    {
        Assert.Equal("Device.Server.Abstractions", typeof(IDeviceServerHost).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(DeviceOfficialFirmwarePackage).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(IPanelsBatchStore).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(PanelsBatchWrite).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(PanelsBatchEntry).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(IDevicePairingStore).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(ICommandStateStore).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(TrackedCommandState).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(ISessionStateStore).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(DeviceSessionState).Assembly.GetName().Name);
        Assert.DoesNotContain(typeof(DeviceSessionState).GetMembers(), member => ExposesTransportType(member));
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
        Assert.Equal("Device.Server", typeof(InMemorySessionStateStore).Assembly.GetName().Name);
        Assert.True(typeof(ISessionStateStore).IsAssignableFrom(typeof(InMemorySessionStateStore)));
    }

    [Fact]
    public void DeviceClientContracts_ShouldLiveInClientAbstractionsAssembly()
    {
        Assert.Equal("Device.Client.Abstractions", typeof(IDeviceServerClient).Assembly.GetName().Name);
        Assert.Equal("Device.Client.Abstractions", typeof(IDeviceServerClientRuntime).Assembly.GetName().Name);
        Assert.Equal("Device.Client.Abstractions", typeof(IDeviceFrameTransport).Assembly.GetName().Name);
        Assert.Equal("Device.Client.Abstractions", typeof(IDeviceClientSessionManager).Assembly.GetName().Name);
        Assert.Equal("Device.Client.Abstractions", typeof(DeviceClientFrameTarget).Assembly.GetName().Name);
        Assert.Equal("Device.Client.Abstractions", typeof(PanelsBatchRegistration).Assembly.GetName().Name);
        Assert.True(typeof(IDeviceFrameTransport).IsAssignableFrom(typeof(IDeviceServerHost)));
    }

    [Fact]
    public void EmbeddedClientAssembly_ShouldBeRemoved()
    {
        Assert.Null(Type.GetType("Device.Client.Embedded.EmbeddedDeviceServerClient, Device.Client.Embedded"));
        Assert.Null(Type.GetType("Device.Client.Embedded.IEmbeddedDeviceServerClientRuntime, Device.Client.Embedded"));
    }

    [Fact]
    public void DeviceRemoteClient_ShouldLiveInRemoteAssembly()
    {
        Assert.Equal("Device.Client.Remote", typeof(RemoteDeviceServerClient).Assembly.GetName().Name);
        Assert.True(typeof(IDeviceServerClient).IsAssignableFrom(typeof(RemoteDeviceServerClient)));
        Assert.True(typeof(IDeviceServerClientRuntime).IsAssignableFrom(typeof(RemoteDeviceServerClient)));
        Assert.True(typeof(IDeviceFrameTransport).IsAssignableFrom(typeof(RemoteDeviceFrameTransport)));
        Assert.True(typeof(IDeviceServerClientRuntime).IsAssignableFrom(typeof(RemoteDeviceServerRuntime)));
    }

    [Fact]
    public void Esp32Output_ShouldDependOnFrameTransportAndOptionalSessionManagerOnly()
    {
        var constructor = Assert.Single(typeof(Esp32S3LedOutput).GetConstructors());

        Assert.Equal(typeof(IDeviceFrameTransport), constructor.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(IDeviceClientSessionManager), constructor.GetParameters()[1].ParameterType);
        Assert.True(constructor.GetParameters()[1].HasDefaultValue);
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
    public void Solution_ShouldNotContainEmbeddedClientProject()
    {
        var solutionPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "MicaAudio.sln"));

        var solution = File.ReadAllText(solutionPath);
        Assert.DoesNotContain("Device.Client.Embedded", solution, StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(solutionPath)!,
            "src",
            "Device.Client.Embedded"))));
    }

    [Fact]
    public void RemoteClientProject_ShouldNotReferenceAppWinUiOrDeviceServer()
    {
        var projectPath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "Device.Client.Remote",
            "Device.Client.Remote.csproj"));

        var project = XDocument.Load(projectPath);
        var references = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value.Replace('\\', '/'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        Assert.Contains("../Device.Client.Abstractions/Device.Client.Abstractions.csproj", references);
        Assert.Contains("../Device.Protocol/Device.Protocol.csproj", references);
        Assert.DoesNotContain("../App.WinUI/App.WinUI.csproj", references);
        Assert.DoesNotContain("../Device.Server/Device.Server.csproj", references);
        Assert.DoesNotContain("../Device.Client.Embedded/Device.Client.Embedded.csproj", references);
    }

    private static bool ExposesTransportType(System.Reflection.MemberInfo member)
    {
        static bool IsTransportType(Type type)
            => type == typeof(WebSocket)
               || type.Namespace?.StartsWith("System.Net.WebSockets", StringComparison.Ordinal) == true
               || type == typeof(Channel<byte[]>)
               || type.Namespace?.StartsWith("System.Threading.Channels", StringComparison.Ordinal) == true;

        return member switch
        {
            System.Reflection.PropertyInfo property => IsTransportType(property.PropertyType),
            System.Reflection.MethodInfo method => IsTransportType(method.ReturnType)
                || method.GetParameters().Any(parameter => IsTransportType(parameter.ParameterType)),
            _ => false,
        };
    }
}
