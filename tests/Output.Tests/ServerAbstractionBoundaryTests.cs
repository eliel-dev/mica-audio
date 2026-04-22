using System.Xml.Linq;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class ServerAbstractionBoundaryTests
{
    [Fact]
    public void DeviceServerContracts_ShouldLiveInAbstractionsAssembly()
    {
        Assert.Equal("Device.Server.Abstractions", typeof(IDeviceServerHost).Assembly.GetName().Name);
        Assert.Equal("Device.Server.Abstractions", typeof(IDeviceOfficialFirmwareCatalog).Assembly.GetName().Name);
    }

    [Fact]
    public void OutputProject_ShouldReferenceServerAbstractionsWithoutConcreteServer()
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

        Assert.Contains("../Device.Server.Abstractions/Device.Server.Abstractions.csproj", references);
        Assert.DoesNotContain("../Device.Server/Device.Server.csproj", references);
    }
}
