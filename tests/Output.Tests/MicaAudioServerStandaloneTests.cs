using System.Xml.Linq;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;
using MicaAudio.Server;
using Microsoft.Extensions.Configuration;

namespace Output.Tests;

public sealed class MicaAudioServerStandaloneTests
{
    [Fact]
    public void ServerProject_ShouldBeExecutableWithoutWinUiOrEmbeddedClientReferences()
    {
        var solutionPath = GetRepoPath("MicaAudio.sln");
        var projectPath = GetRepoPath("src", "MicaAudio.Server", "MicaAudio.Server.csproj");

        Assert.Contains("src\\MicaAudio.Server\\MicaAudio.Server.csproj", File.ReadAllText(solutionPath));

        var project = XDocument.Load(projectPath);
        Assert.Equal("Exe", project.Descendants("OutputType").Single().Value);
        Assert.Equal("net10.0", project.Descendants("TargetFramework").Single().Value);

        var references = project
            .Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value.Replace('\\', '/'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        Assert.Contains("../Device.Server/Device.Server.csproj", references);
        Assert.Contains("../Device.Server.Abstractions/Device.Server.Abstractions.csproj", references);
        Assert.Contains("../Device.Protocol/Device.Protocol.csproj", references);
        Assert.DoesNotContain("../App.WinUI/App.WinUI.csproj", references);
        Assert.DoesNotContain("../Device.Client.Embedded/Device.Client.Embedded.csproj", references);
    }

    [Fact]
    public void ServerProject_ShouldPublishDashboardAssets()
    {
        var project = XDocument.Load(GetRepoPath("src", "MicaAudio.Server", "MicaAudio.Server.csproj"));
        var dashboardContent = project
            .Descendants("Content")
            .Select(element => element.Attribute("Include")?.Value.Replace('\\', '/'))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        Assert.Contains("../Device.Server/wwwroot/dashboard/**/*", dashboardContent);
    }

    [Fact]
    public void Options_ShouldUseRenderPortOverConfiguredPort()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Port"] = "5272",
                ["MqttPort"] = "5273",
            })
            .Build();

        var options = MicaAudioServerBootstrap.LoadOptions(configuration, renderPort: "8080");

        Assert.Equal(8080, options.Port);
    }

    [Fact]
    public void Options_ShouldMapPrefixedEnvironmentStyleValuesToServerConfig()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), "mica-audio-server-tests", Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ListenHost"] = "0.0.0.0",
                ["Port"] = "8181",
                ["MqttPort"] = "8182",
                ["MaxDevices"] = "7",
                ["MqttRootTopic"] = "mica/test/devices",
                ["AdminToken"] = "standalone-admin-token",
                ["MdnsServiceName"] = "_mica-test._tcp",
                ["PublicHttpBaseAddress"] = "http://192.168.15.10:5272/",
                ["RestrictToPrivateNetworks"] = "false",
                ["VisualUdpPort"] = "5274",
                ["PreferLanUdpVisualTransport"] = "true",
                ["DeviceFreshThresholdSeconds"] = "21",
                ["AllowLegacyWebSocketQueryToken"] = "true",
                ["StartupPairCodeTtlSeconds"] = "0",
                ["StorageRoot"] = storageRoot,
            })
            .Build();

        var options = MicaAudioServerBootstrap.LoadOptions(configuration, renderPort: string.Empty);
        var config = MicaAudioServerBootstrap.CreateServerConfig(options);

        Assert.Equal("0.0.0.0", config.ListenHost);
        Assert.Equal(8181, config.Port);
        Assert.Equal(8182, config.MqttPort);
        Assert.Equal(7, config.MaxDevices);
        Assert.Equal("mica/test/devices", config.MqttRootTopic);
        Assert.Equal("standalone-admin-token", config.AdminToken);
        Assert.Equal("_mica-test._tcp", config.MdnsServiceName);
        Assert.Equal("http://192.168.15.10:5272", config.PublicHttpBaseAddress);
        Assert.False(config.RestrictToPrivateNetworks);
        Assert.Equal(5274, config.VisualUdpPort);
        Assert.True(config.PreferLanUdpVisualTransport);
        Assert.Equal(21, config.DeviceFreshThresholdSeconds);
        Assert.True(config.AllowLegacyWebSocketQueryToken);
        Assert.Equal(0, options.StartupPairCodeTtlSeconds);
        Assert.Equal(storageRoot, options.StorageRoot);
    }

    [Fact]
    public async Task StandaloneRegistryStore_ShouldLoadAndSaveDeviceRecords()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-server-tests", Guid.NewGuid().ToString("N"));
        var store = new StandaloneDeviceRegistryStore(root);
        var record = new DeviceRecord
        {
            DeviceId = "mp-test",
            Token = "token",
            Name = "Test Panel",
            FirstSeenUtc = DateTimeOffset.Parse("2026-04-22T12:00:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
            LastSeenUtc = DateTimeOffset.Parse("2026-04-22T12:01:00Z", null, System.Globalization.DateTimeStyles.RoundtripKind),
            BoardModel = "esp32s3_devkitc1",
            PanelType = "hub75_128x64",
        };

        await store.SaveAsync(new[] { record });

        var loaded = await store.LoadAsync();
        var loadedRecord = Assert.Single(loaded);
        Assert.Equal(record.DeviceId, loadedRecord.DeviceId);
        Assert.Equal(record.Token, loadedRecord.Token);
        Assert.Equal(record.Name, loadedRecord.Name);
        Assert.Equal(record.BoardModel, loadedRecord.BoardModel);
        Assert.Equal(record.PanelType, loadedRecord.PanelType);
    }

    [Fact]
    public void FileServerPanelStore_ShouldPersistExplicitClearWithoutExistingPanel()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-server-tests", Guid.NewGuid().ToString("N"));
        var store = new FileServerPanelStore(root);

        var removed = store.Remove("mp-empty");

        Assert.False(removed);
        Assert.Equal(DevicePanelDisplayState.ExplicitlyCleared, store.GetDisplayState("mp-empty"));

        var reloaded = new FileServerPanelStore(root);
        Assert.Equal(DevicePanelDisplayState.ExplicitlyCleared, reloaded.GetDisplayState("mp-empty"));
    }

    [Fact]
    public void RenderBlueprint_ShouldUseDockerHealthCheckAndDataDisk()
    {
        var renderYaml = File.ReadAllText(GetRepoPath("render.yaml"));

        Assert.Contains("runtime: docker", renderYaml);
        Assert.Contains("dockerfilePath: ./src/MicaAudio.Server/Dockerfile", renderYaml);
        Assert.Contains("dockerContext: .", renderYaml);
        Assert.Contains("healthCheckPath: /api/v1/health", renderYaml);
        Assert.Contains("mountPath: /data", renderYaml);
        Assert.Contains("MICA_SERVER__STORAGEROOT", renderYaml);
        Assert.Contains("MICA_SERVER__RESTRICTTOPRIVATENETWORKS", renderYaml);
        Assert.Contains("MICA_SERVER__ADMINTOKEN", renderYaml);
        Assert.Contains("sync: false", renderYaml);
    }

    [Fact]
    public void Dockerignore_ShouldExcludeBuildOutputsAndKeepNuGetLocks()
    {
        var dockerignore = File.ReadAllText(GetRepoPath(".dockerignore"));

        Assert.Contains("**/bin/", dockerignore);
        Assert.Contains("**/obj/", dockerignore);
        Assert.Contains("!**/packages.lock.json", dockerignore);
    }

    [Fact]
    public void Dockerfile_ShouldExposeHttpLegacyMqttAndVisualUdpPorts()
    {
        var dockerfile = File.ReadAllText(GetRepoPath("src", "MicaAudio.Server", "Dockerfile"));

        Assert.Contains("EXPOSE 8080 5273 5274/udp", dockerfile);
    }

    [Theory]
    [InlineData("tests", "Integration.Smoke", "Integration.Smoke.csproj")]
    [InlineData("BenchmarkSuite1", "BenchmarkSuite1.csproj")]
    public void X64OnlyWindowsProjects_ShouldKeepRestoreLocksX64Only(params string[] projectPathParts)
    {
        var projectPath = GetRepoPath(projectPathParts);
        var project = XDocument.Load(projectPath);
        var projectDirectory = Path.GetDirectoryName(projectPath) ?? throw new InvalidOperationException("Project directory not found.");
        var lockFile = File.ReadAllText(Path.Combine(projectDirectory, "packages.lock.json"));

        Assert.Equal("win-x64", project.Descendants("RuntimeIdentifier").Single().Value);
        Assert.Equal("win-x64", project.Descendants("RuntimeIdentifiers").Single().Value);
        Assert.Contains("/win-x64", lockFile);
        Assert.DoesNotContain("/win-x86", lockFile);
        Assert.DoesNotContain("/win-arm64", lockFile);
    }

    private static string GetRepoPath(params string[] parts)
    {
        var pathParts = new List<string>
        {
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
        };
        pathParts.AddRange(parts);
        return Path.GetFullPath(Path.Combine(pathParts.ToArray()));
    }
}
