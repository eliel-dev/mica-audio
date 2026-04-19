using System.Security.Cryptography;
using System.Text.Json;
using App.WinUI.Services.Firmware;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace Output.Tests;

public sealed class PrecompiledFirmwareServiceTests
{
    [Fact]
    public async Task TryResolveArtifact_ShouldRequireManifestAndParseMetadata()
    {
        var root = CreateTempRoot();
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

            var service = CreateService(precompiledFirmwareDirectory: root);

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
            Assert.Equal(3L, artifact.Manifest.FileSizeBytes);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData([0x01, 0x02, 0x03])).ToLowerInvariant(),
                artifact.Manifest.Sha256);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task TryResolveOfficialArtifact_ShouldHideStaleWorkspaceRelease()
    {
        var workspaceRoot = CreateWorkspace();
        var outputRoot = Path.Combine(workspaceRoot, "artifacts");
        try
        {
            await WriteArtifactAsync(
                outputRoot,
                version: "v2026.03.14-stale",
                builtAtUtc: new DateTimeOffset(2026, 3, 14, 3, 2, 28, TimeSpan.Zero),
                bytes: [0x01, 0x02, 0x03]);
            TouchWorkspaceInputs(workspaceRoot, new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
            await WriteBuildScriptAsync(workspaceRoot, "exit 0");

            var service = CreateService(outputRoot, workspaceRoot);

            Assert.True(service.TryResolveArtifact(
                PrecompiledFirmwareService.Esp32S3DevKitC1Board,
                PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
                "dma_exp",
                out _,
                out var rawError), rawError);

            Assert.False(service.TryResolveOfficialArtifact(
                PrecompiledFirmwareService.Esp32S3DevKitC1Board,
                PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
                "dma_exp",
                out _,
                out var freshError));
            Assert.Contains("stale", freshError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureOfficialFirmwareFreshAsync_ShouldRegenerateMissingRelease()
    {
        var workspaceRoot = CreateWorkspace();
        var outputRoot = Path.Combine(workspaceRoot, "artifacts");
        try
        {
            TouchWorkspaceInputs(workspaceRoot, new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
            await WriteBuildScriptAsync(
                workspaceRoot,
                @"
$binPath = Join-Path $OutputRoot 'esp32s3-devkitc1-128x64-dma_exp_merged.bin'
$manifestPath = Join-Path $OutputRoot 'esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json'
[System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
[System.IO.File]::WriteAllBytes($binPath, [byte[]](1,2,3,4))
$manifest = [ordered]@{
    schemaVersion   = 2
    firmwareVersion = 'v2026.03.20-generated'
    gitSha          = 'feedbee'
    profile         = 'dma_exp'
    boardModel      = 'esp32s3_devkitc1'
    panelType       = 'hub75_p2_5_128x64_smd2121_scan32'
    controlPlane    = 'mqtt'
    builtAtUtc      = '2026-03-20T12:01:00Z'
    sha256          = ''
    fileSizeBytes   = 0
}
$manifest | ConvertTo-Json | Set-Content -Path $manifestPath -Encoding utf8
");

            var service = CreateService(outputRoot, workspaceRoot);
            var result = await service.EnsureOfficialFirmwareFreshAsync("esp32s3_devkitc1_128x64_dma_exp");

            Assert.True(result.IsFresh, result.FailureReason);
            Assert.True(result.WasRegenerated);
            Assert.NotNull(result.ResolvedArtifact);
            Assert.Equal("v2026.03.20-generated", result.ResolvedArtifact!.Manifest.FirmwareVersion);
            Assert.True(service.TryResolveOfficialArtifact(
                PrecompiledFirmwareService.Esp32S3DevKitC1Board,
                PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
                "dma_exp",
                out var artifact,
                out var error), error);
            Assert.Equal("v2026.03.20-generated", artifact.Manifest.FirmwareVersion);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureOfficialFirmwareFreshAsync_ShouldSkipBuildWhenReleaseIsFresh()
    {
        var workspaceRoot = CreateWorkspace();
        var outputRoot = Path.Combine(workspaceRoot, "artifacts");
        try
        {
            TouchWorkspaceInputs(workspaceRoot, new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
            await WriteArtifactAsync(
                outputRoot,
                version: "v2026.03.20-fresh",
                builtAtUtc: new DateTimeOffset(2026, 3, 20, 12, 5, 0, TimeSpan.Zero),
                bytes: [0x01, 0x02, 0x03]);

            var markerPath = Path.Combine(workspaceRoot, "script-called.txt");
            await WriteBuildScriptAsync(
                workspaceRoot,
                $@"
Set-Content -Path '{markerPath}' -Value 'called' -Encoding ascii
exit 0
");

            var service = CreateService(outputRoot, workspaceRoot);
            var result = await service.EnsureOfficialFirmwareFreshAsync("esp32s3_devkitc1_128x64_dma_exp");

            Assert.True(result.IsFresh, result.FailureReason);
            Assert.False(result.WasRegenerated);
            Assert.False(File.Exists(markerPath));
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task TryResolveOfficialArtifact_ShouldIgnoreDirectoryTimestampAndTemporaryFirmwareVersionHeader()
    {
        var workspaceRoot = CreateWorkspace();
        var outputRoot = Path.Combine(workspaceRoot, "artifacts");
        try
        {
            var sourceTimestamp = new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
            var artifactTimestamp = new DateTimeOffset(2026, 3, 20, 12, 5, 0, TimeSpan.Zero);
            var temporaryTimestamp = artifactTimestamp.AddMinutes(10);

            TouchWorkspaceInputs(workspaceRoot, sourceTimestamp);
            await WriteArtifactAsync(
                outputRoot,
                version: "v2026.03.20-fresh",
                builtAtUtc: artifactTimestamp,
                bytes: [0x01, 0x02, 0x03]);

            var firmwareSrcRoot = Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "src");
            var temporaryHeaderPath = Path.Combine(firmwareSrcRoot, "firmware_version.auto.h");
            await File.WriteAllTextAsync(temporaryHeaderPath, "#pragma once");
            File.SetLastWriteTimeUtc(temporaryHeaderPath, temporaryTimestamp.UtcDateTime);
            Directory.SetLastWriteTimeUtc(firmwareSrcRoot, temporaryTimestamp.UtcDateTime);

            var service = CreateService(outputRoot, workspaceRoot);

            Assert.True(service.TryResolveOfficialArtifact(
                PrecompiledFirmwareService.Esp32S3DevKitC1Board,
                PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
                "dma_exp",
                out var artifact,
                out var error), error);
            Assert.Equal("v2026.03.20-fresh", artifact.Manifest.FirmwareVersion);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureOfficialFirmwareFreshAsync_ShouldFailWhenOfficialBuildScriptFails()
    {
        var workspaceRoot = CreateWorkspace();
        var outputRoot = Path.Combine(workspaceRoot, "artifacts");
        try
        {
            TouchWorkspaceInputs(workspaceRoot, new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
            await WriteBuildScriptAsync(
                workspaceRoot,
                @"
Write-Error 'simulated build failure'
exit 7
");

            var service = CreateService(outputRoot, workspaceRoot);
            var result = await service.EnsureOfficialFirmwareFreshAsync("esp32s3_devkitc1_128x64_dma_exp");

            Assert.False(result.IsFresh);
            Assert.False(result.WasRegenerated);
            Assert.Contains("falhou", result.FailureReason, StringComparison.OrdinalIgnoreCase);
            Assert.False(service.TryResolveOfficialArtifact(
                PrecompiledFirmwareService.Esp32S3DevKitC1Board,
                PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
                "dma_exp",
                out _,
                out var error));
            Assert.Contains("workspace-local indisponivel", error, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareOfficialFirmwareExportAsync_ShouldReturnVersionedSuggestedFileName()
    {
        var workspaceRoot = CreateWorkspace();
        var outputRoot = Path.Combine(workspaceRoot, "artifacts");
        try
        {
            TouchWorkspaceInputs(workspaceRoot, new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
            await WriteArtifactAsync(
                outputRoot,
                version: "v2026.03.20-fresh",
                builtAtUtc: new DateTimeOffset(2026, 3, 20, 12, 5, 0, TimeSpan.Zero),
                bytes: [0x01, 0x02, 0x03]);

            var service = CreateService(outputRoot, workspaceRoot);
            var export = await service.PrepareOfficialFirmwareExportAsync("esp32s3_devkitc1_128x64_dma_exp");

            Assert.True(export.Success, export.FailureReason);
            Assert.NotNull(export.ResolvedArtifact);
            Assert.Equal("esp32s3-devkitc1-128x64-dma_exp_merged_v2026.03.20-fresh.bin", export.SuggestedFileName);
            Assert.Equal("esp32s3-devkitc1-128x64-dma_exp_merged.bin", export.ResolvedArtifact!.Option.FileName);
            Assert.Equal("v2026.03.20-fresh", export.ResolvedArtifact.Manifest.FirmwareVersion);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Fact]
    public async Task PrepareOfficialFirmwareExportAsync_ShouldFailStrictlyWhenFreshOfficialReleaseIsUnavailable()
    {
        var workspaceRoot = CreateWorkspace();
        var outputRoot = Path.Combine(workspaceRoot, "artifacts");
        try
        {
            await WriteArtifactAsync(
                outputRoot,
                version: "v2026.03.14-stale",
                builtAtUtc: new DateTimeOffset(2026, 3, 14, 3, 2, 28, TimeSpan.Zero),
                bytes: [0x01, 0x02, 0x03]);
            TouchWorkspaceInputs(workspaceRoot, new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero));
            await WriteBuildScriptAsync(
                workspaceRoot,
                @"
Write-Error 'simulated build failure'
exit 7
");

            var service = CreateService(outputRoot, workspaceRoot);
            var export = await service.PrepareOfficialFirmwareExportAsync("esp32s3_devkitc1_128x64_dma_exp");

            Assert.False(export.Success);
            Assert.Null(export.ResolvedArtifact);
            Assert.Equal(string.Empty, export.SuggestedFileName);
            Assert.Contains("falhou", export.FailureReason, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static PrecompiledFirmwareService CreateService(string precompiledFirmwareDirectory, string? workspaceRoot = null)
    {
        return new PrecompiledFirmwareService(
            Options.Create(new MicaAudioOptions
            {
                PrecompiledFirmwareDirectory = precompiledFirmwareDirectory,
                WorkspaceRoot = workspaceRoot ?? string.Empty,
            }),
            NullLogger<PrecompiledFirmwareService>.Instance);
    }

    private static string CreateTempRoot()
        => Path.Combine(Path.GetTempPath(), "mica-audio-firmware-service", Guid.NewGuid().ToString("N"));

    private static string CreateWorkspace()
    {
        var root = CreateTempRoot();
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "scripts"));
        Directory.CreateDirectory(Path.Combine(root, "firmware", "esp32s3-devkitc1", "src"));
        Directory.CreateDirectory(Path.Combine(root, "firmware", "esp32s3-devkitc1", "boards"));
        Directory.CreateDirectory(Path.Combine(root, "firmware", "esp32s3-devkitc1", "partitions"));
        Directory.CreateDirectory(Path.Combine(root, "firmware", "esp32s3-devkitc1", "scripts"));
        File.WriteAllText(Path.Combine(root, "MicaAudio.sln"), "Microsoft Visual Studio Solution File");
        return root;
    }

    private static void TouchWorkspaceInputs(string workspaceRoot, DateTimeOffset timestampUtc)
    {
        var files = new Dictionary<string, string>
        {
            [Path.Combine("firmware", "esp32s3-devkitc1", "platformio.ini")] = "[env:esp32s3_devkitc1_dma_exp]",
            [Path.Combine("firmware", "esp32s3-devkitc1", "src", "main.cpp")] = "int main() { return 0; }",
            [Path.Combine("firmware", "esp32s3-devkitc1", "src", "firmware_version.h")] = "#define MICA_FIRMWARE_VERSION \"test\"",
            [Path.Combine("firmware", "esp32s3-devkitc1", "boards", "mica_esp32_s3_devkitc1_n16r8.json")] = "{}",
            [Path.Combine("firmware", "esp32s3-devkitc1", "partitions", "mica_app3M_fat9M_16MB.csv")] = "# partitions",
            [Path.Combine("firmware", "esp32s3-devkitc1", "scripts", "patch.py")] = "# patch",
        };

        foreach (var file in files)
        {
            var fullPath = Path.Combine(workspaceRoot, file.Key);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            File.WriteAllText(fullPath, file.Value);
            File.SetLastWriteTimeUtc(fullPath, timestampUtc.UtcDateTime);
        }

        var buildScriptPath = Path.Combine(workspaceRoot, "scripts", "build-precompiled-firmware.ps1");
        if (!File.Exists(buildScriptPath))
        {
            File.WriteAllText(buildScriptPath, "param([string]$OutputRoot)");
        }

        File.SetLastWriteTimeUtc(buildScriptPath, timestampUtc.UtcDateTime);
        TouchDirectoryTree(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "boards"), timestampUtc.UtcDateTime);
        TouchDirectoryTree(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "partitions"), timestampUtc.UtcDateTime);
        TouchDirectoryTree(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "scripts"), timestampUtc.UtcDateTime);
    }

    private static async Task WriteBuildScriptAsync(string workspaceRoot, string scriptBody)
    {
        var path = Path.Combine(workspaceRoot, "scripts", "build-precompiled-firmware.ps1");
        var content = $"""
param(
    [string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

{scriptBody}
""";
        await File.WriteAllTextAsync(path, content);
        var timestampUtc = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, timestampUtc);
        TouchDirectoryTree(Path.Combine(workspaceRoot, "scripts"), timestampUtc);
    }

    private static async Task WriteArtifactAsync(
        string outputRoot,
        string version,
        DateTimeOffset builtAtUtc,
        byte[] bytes)
    {
        Directory.CreateDirectory(outputRoot);
        var firmwarePath = Path.Combine(outputRoot, "esp32s3-devkitc1-128x64-dma_exp_merged.bin");
        await File.WriteAllBytesAsync(firmwarePath, bytes);

        var manifestPath = Path.Combine(outputRoot, "esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json");
        var manifest = new FirmwareArtifactManifest
        {
            SchemaVersion = 2,
            FirmwareVersion = version,
            GitSha = "deadbee",
            Profile = "dma_exp",
            BoardModel = PrecompiledFirmwareService.Esp32S3DevKitC1Board,
            PanelType = PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
            ControlPlane = PrecompiledFirmwareService.RequiredControlPlane,
            BuiltAtUtc = builtAtUtc,
        };

        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
    }

    private static void TouchDirectoryTree(string root, DateTime timestampUtc)
    {
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .Append(root)
                     .OrderByDescending(path => path.Length))
        {
            Directory.SetLastWriteTimeUtc(directory, timestampUtc);
        }
    }
}
