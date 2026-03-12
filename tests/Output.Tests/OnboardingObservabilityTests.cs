using App.WinUI.Infrastructure.Observability;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Devices.Onboarding;
using App.WinUI.Services.Firmware;
using Device.Protocol.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;
using System.Text.Json;

namespace Output.Tests;

public sealed class OnboardingObservabilityTests
{
    [Fact]
    public async Task RunAsync_ShouldEmitOnboardingActivity()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-onboarding-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await CreateFirmwarePackageAsync(root, PrecompiledFirmwareService.RequiredControlPlane);

            using var loggerFactory = LoggerFactory.Create(builder => { });
            using var activityCapture = new ActivityCapture(AppObservability.ActivitySourceName);
            var firmwareService = new PrecompiledFirmwareService(
                Options.Create(new MicaAudioOptions
                {
                    PrecompiledFirmwareDirectory = root,
                }),
                loggerFactory.CreateLogger<PrecompiledFirmwareService>());
            var flashService = new SuccessfulFlashService();
            using var coordinator = new DeviceOperationsCoordinator(new PairingRuntime(), settingsRepository: null, settingsDomainService: null);
            var service = new DeviceUsbOnboardingService(
                coordinator,
                firmwareService,
                flashService,
                loggerFactory.CreateLogger<DeviceUsbOnboardingService>());

            var result = await service.RunAsync(new DeviceOnboardingRequest { PortName = "COM7" });

            Assert.True(result.Success);
            Assert.Equal("COM7", flashService.LastPortName);
            var activity = Assert.Single(activityCapture.CompletedActivities, item => item.OperationName == "device-onboarding.run");
            Assert.Equal(AppObservability.AppComponent, activity.GetTagItem(AppObservability.ComponentKey));
            Assert.Equal("COM7", activity.GetTagItem(AppObservability.PortNameKey));
            Assert.Equal(true, activity.GetTagItem("pairing.code.issued"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RunAsync_ShouldRejectIncompatibleFirmwareManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-onboarding-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            await CreateFirmwarePackageAsync(root, "ws-legacy");

            using var loggerFactory = LoggerFactory.Create(builder => { });
            var firmwareService = new PrecompiledFirmwareService(
                Options.Create(new MicaAudioOptions
                {
                    PrecompiledFirmwareDirectory = root,
                }),
                loggerFactory.CreateLogger<PrecompiledFirmwareService>());
            var flashService = new SuccessfulFlashService();
            using var coordinator = new DeviceOperationsCoordinator(new PairingRuntime(), settingsRepository: null, settingsDomainService: null);
            var service = new DeviceUsbOnboardingService(
                coordinator,
                firmwareService,
                flashService,
                loggerFactory.CreateLogger<DeviceUsbOnboardingService>());

            var result = await service.RunAsync(new DeviceOnboardingRequest { PortName = "COM8" });

            Assert.False(result.Success);
            Assert.Equal("firmware_incompatible", result.ErrorCode);
            Assert.Contains("control plane atual", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(flashService.LastPortName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task CreateFirmwarePackageAsync(string root, string controlPlane)
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
            ControlPlane = controlPlane,
            BuiltAtUtc = new DateTimeOffset(2026, 3, 9, 18, 0, 0, TimeSpan.Zero),
        };

        var manifestPath = Path.Combine(root, PrecompiledFirmwareService.GetManifestFileName(Path.GetFileName(firmwarePath)));
        await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(manifest));
    }

    private sealed class SuccessfulFlashService : IEspToolFlashService
    {
        public string? LastPortName { get; private set; }

        public Task<EspToolFlashResult> FlashAsync(
            string portName,
            string firmwarePath,
            IProgress<DeviceOnboardingProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastPortName = portName;
            return Task.FromResult(new EspToolFlashResult
            {
                Success = true,
                ExitCode = 0,
                Message = "ok",
            });
        }
    }

    private sealed class PairingRuntime : IDeviceOperationsRuntime
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
            => new() { Code = "123456", ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl) };

        public bool RemoveDevice(string deviceId) => true;

        public IReadOnlyList<DeviceSnapshot> GetDevices() => Array.Empty<DeviceSnapshot>();

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
