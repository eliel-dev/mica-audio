using App.WinUI.Services.Devices;
using App.WinUI.Services.Devices.Onboarding;
using App.WinUI.Services.Firmware;
using Device.Protocol.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;
using System.Text.Json;

namespace Integration.Smoke;

public sealed class DeviceUsbOnboardingServiceTests
{
    [Fact]
    public async Task RunAsync_ShouldFlashAndReturnPairCode()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var firmwarePath = Path.Combine(tempRoot, "esp32s3-devkitc1-128x64-dma_exp_merged.bin");
        var manifestPath = Path.Combine(tempRoot, "esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json");
        await File.WriteAllBytesAsync(firmwarePath, [0x01, 0x02, 0x03]);
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(new FirmwareArtifactManifest
            {
                SchemaVersion = 1,
                FirmwareVersion = "1.0.0-test",
                GitSha = "deadbeef",
                Profile = "dma_exp",
                BoardModel = PrecompiledFirmwareService.Esp32S3DevKitC1Board,
                PanelType = PrecompiledFirmwareService.Hub75PanelP25_128x64_Smd2121_Scan32,
                ControlPlane = PrecompiledFirmwareService.RequiredControlPlane,
                BuiltAtUtc = DateTimeOffset.UtcNow,
            }));

        try
        {
            var runtime = new FakeDeviceOperationsRuntime();
            using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
            var firmwareService = new PrecompiledFirmwareService(
                Options.Create(new MicaAudioOptions { PrecompiledFirmwareDirectory = tempRoot }),
                NullLogger<PrecompiledFirmwareService>.Instance);
            var flashService = new FakeFlashService
            {
                NextResult = new EspToolFlashResult
                {
                    Success = true,
                    ExitCode = 0,
                    Message = "ok",
                },
            };
            var sut = new DeviceUsbOnboardingService(
                coordinator,
                firmwareService,
                flashService,
                NullLogger<DeviceUsbOnboardingService>.Instance);

            var result = await sut.RunAsync(new DeviceOnboardingRequest
            {
                PortName = "COM7",
            });

            Assert.True(result.Success);
            Assert.Equal("PAIR-TEST-123", result.PairCode);
            Assert.Equal("COM7", flashService.LastPortName);
            Assert.Equal(firmwarePath, flashService.LastFirmwarePath);
            Assert.Equal(1, flashService.CallCount);
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldFailWhenPortIsMissing()
    {
        var runtime = new FakeDeviceOperationsRuntime();
        using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
        var firmwareService = new PrecompiledFirmwareService(
            Options.Create(new MicaAudioOptions { PrecompiledFirmwareDirectory = Path.GetTempPath() }),
            NullLogger<PrecompiledFirmwareService>.Instance);
        var flashService = new FakeFlashService();
        var sut = new DeviceUsbOnboardingService(
            coordinator,
            firmwareService,
            flashService,
            NullLogger<DeviceUsbOnboardingService>.Instance);

        var result = await sut.RunAsync(new DeviceOnboardingRequest
        {
            PortName = " ",
        });

        Assert.False(result.Success);
        Assert.Equal("port_required", result.ErrorCode);
        Assert.Equal(0, flashService.CallCount);
    }

    [Fact]
    public async Task RunAsync_ShouldUseRegeneratedOfficialReleaseWhenWorkspaceArtifactIsMissing()
    {
        var workspaceRoot = Path.Combine(Path.GetTempPath(), "mica-audio-onboarding-tests", Guid.NewGuid().ToString("N"));
        var outputRoot = Path.Combine(workspaceRoot, "artifacts");
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "scripts"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "src"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "boards"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "partitions"));
        Directory.CreateDirectory(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "scripts"));
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "MicaAudio.sln"), "Microsoft Visual Studio Solution File");
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "platformio.ini"), "[env:esp32s3_devkitc1_dma_exp]");
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "src", "main.cpp"), "int main() { return 0; }");
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "src", "firmware_version.h"), "#define MICA_FIRMWARE_VERSION \"test\"");
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "boards", "board.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "partitions", "partitions.csv"), "# partitions");
        await File.WriteAllTextAsync(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "scripts", "patch.py"), "# patch");
        var sourceTimestamp = new DateTime(2026, 3, 20, 12, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "platformio.ini"), sourceTimestamp);
        File.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "src", "main.cpp"), sourceTimestamp);
        File.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "src", "firmware_version.h"), sourceTimestamp);
        File.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "boards", "board.json"), sourceTimestamp);
        File.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "partitions", "partitions.csv"), sourceTimestamp);
        File.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "scripts", "patch.py"), sourceTimestamp);
        Directory.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "boards"), sourceTimestamp);
        Directory.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "partitions"), sourceTimestamp);
        Directory.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "firmware", "esp32s3-devkitc1", "scripts"), sourceTimestamp);
        await File.WriteAllTextAsync(
            Path.Combine(workspaceRoot, "scripts", "build-precompiled-firmware.ps1"),
            """
param(
    [string]$OutputRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

[System.IO.Directory]::CreateDirectory($OutputRoot) | Out-Null
$binPath = Join-Path $OutputRoot 'esp32s3-devkitc1-128x64-dma_exp_merged.bin'
$manifestPath = Join-Path $OutputRoot 'esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json'
[System.IO.File]::WriteAllBytes($binPath, [byte[]](1,2,3,4,5))
$manifest = [ordered]@{
    schemaVersion   = 2
    firmwareVersion = '2.0.0-regenerated'
    gitSha          = 'regen123'
    profile         = 'dma_exp'
    boardModel      = 'esp32s3_devkitc1'
    panelType       = 'hub75_p2_5_128x64_smd2121_scan32'
    controlPlane    = 'mqtt'
    builtAtUtc      = '2026-03-20T12:05:00Z'
    sha256          = ''
    fileSizeBytes   = 0
}
$manifest | ConvertTo-Json | Set-Content -Path $manifestPath -Encoding utf8
""");
        File.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "scripts", "build-precompiled-firmware.ps1"), sourceTimestamp);
        Directory.SetLastWriteTimeUtc(Path.Combine(workspaceRoot, "scripts"), sourceTimestamp);

        try
        {
            var runtime = new FakeDeviceOperationsRuntime();
            using var coordinator = new DeviceOperationsCoordinator(runtime, settingsRepository: null, settingsDomainService: null);
            var firmwareService = new PrecompiledFirmwareService(
                Options.Create(new MicaAudioOptions
                {
                    PrecompiledFirmwareDirectory = outputRoot,
                    WorkspaceRoot = workspaceRoot,
                }),
                NullLogger<PrecompiledFirmwareService>.Instance);
            var flashService = new FakeFlashService
            {
                NextResult = new EspToolFlashResult
                {
                    Success = true,
                    ExitCode = 0,
                    Message = "ok",
                },
            };
            var sut = new DeviceUsbOnboardingService(
                coordinator,
                firmwareService,
                flashService,
                NullLogger<DeviceUsbOnboardingService>.Instance);

            var progressItems = new List<DeviceOnboardingProgress>();
            var progress = new Progress<DeviceOnboardingProgress>(item => progressItems.Add(item));

            var result = await sut.RunAsync(new DeviceOnboardingRequest
            {
                PortName = "COM9",
            }, progress);

            Assert.True(result.Success);
            Assert.Equal("COM9", flashService.LastPortName);
            Assert.NotNull(flashService.LastFirmwarePath);
            Assert.StartsWith(outputRoot, flashService.LastFirmwarePath!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(progressItems, item => item.Message.Contains("2.0.0-regenerated", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private sealed class FakeFlashService : IEspToolFlashService
    {
        public EspToolFlashResult NextResult { get; set; } = new()
        {
            Success = true,
            ExitCode = 0,
            Message = "ok",
        };

        public int CallCount { get; private set; }

        public string? LastPortName { get; private set; }

        public string? LastFirmwarePath { get; private set; }

        public Task<EspToolFlashResult> FlashAsync(
            string portName,
            string firmwarePath,
            IProgress<DeviceOnboardingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastPortName = portName;
            LastFirmwarePath = firmwarePath;
            return Task.FromResult(NextResult);
        }
    }

    private sealed class FakeDeviceOperationsRuntime : IDeviceOperationsRuntime
    {
        public event EventHandler? DevicesChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<string>? LogMessage
        {
            add { }
            remove { }
        }

        public event EventHandler<DeviceLogMessage>? DeviceLogReceived
        {
            add { }
            remove { }
        }

        public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged
        {
            add { }
            remove { }
        }

        public string GetServerBaseAddress() => "http://127.0.0.1:5272";

        public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
        {
            return new PairingCodeInfo
            {
                Code = "PAIR-TEST-123",
                ExpiresAtUtc = DateTimeOffset.UtcNow + ttl,
            };
        }

        public bool RemoveDevice(string deviceId) => true;

        public IReadOnlyList<DeviceSnapshot> GetDevices() => Array.Empty<DeviceSnapshot>();

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new CommandDispatchResult
            {
                Accepted = true,
                Completed = true,
                Success = true,
                Message = "ok",
            });
        }
    }
}
