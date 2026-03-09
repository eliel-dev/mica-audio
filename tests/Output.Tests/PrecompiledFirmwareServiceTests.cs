using System.Text.Json;
using App.WinUI.Services.Firmware;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace Output.Tests;

public sealed class PrecompiledFirmwareServiceTests
{
    [Fact]
    public async Task TryResolveArtifact_ShouldRequireManifestAndParseMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-firmware-service", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var firmwarePath = Path.Combine(root, "esp32s3-devkitc1-128x64-dma_exp_merged.bin");
            await File.WriteAllBytesAsync(firmwarePath, [0x01, 0x02, 0x03]);

            var manifest = new FirmwareArtifactManifest
            {
                FirmwareVersion = "v2026.03.09-hotfix",
                GitSha = "abc1234",
                Profile = "dma_exp",
                BoardModel = PrecompiledFirmwareService.Esp32S3DevKitC1Board,
                PanelType = PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
                ControlPlane = PrecompiledFirmwareService.RequiredControlPlane,
                BuiltAtUtc = new DateTimeOffset(2026, 3, 9, 18, 0, 0, TimeSpan.Zero),
            };

            var manifestPath = Path.Combine(root, PrecompiledFirmwareService.GetManifestFileName(Path.GetFileName(firmwarePath)));
            await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));

            using var loggerFactory = LoggerFactory.Create(builder => { });
            var service = new PrecompiledFirmwareService(
                Options.Create(new MicaAudioOptions
                {
                    PrecompiledFirmwareDirectory = root,
                }),
                loggerFactory.CreateLogger<PrecompiledFirmwareService>());

            var resolved = service.TryResolveArtifact(
                PrecompiledFirmwareService.Esp32S3DevKitC1Board,
                PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
                "dma_exp",
                out var artifact,
                out var error);

            Assert.True(resolved, error);
            Assert.Equal(firmwarePath, artifact.FirmwarePath);
            Assert.Equal(manifestPath, artifact.ManifestPath);
            Assert.Equal("v2026.03.09-hotfix", artifact.Manifest.FirmwareVersion);
            Assert.Equal("abc1234", artifact.Manifest.GitSha);
            Assert.Equal(PrecompiledFirmwareService.RequiredControlPlane, artifact.Manifest.ControlPlane);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
