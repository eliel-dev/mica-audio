using App.WinUI.Services.Devices;
using App.WinUI.Services.Devices.Onboarding;
using App.WinUI.Services.Firmware;
using Device.Protocol.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace Integration.Smoke;

public sealed class DeviceUsbOnboardingServiceTests
{
    [Fact]
    public async Task RunAsync_ShouldFlashAndReturnPairCode()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var firmwarePath = Path.Combine(tempRoot, "esp32s3-devkitc1-128x64-dma_exp_merged.bin");
        await File.WriteAllBytesAsync(firmwarePath, [0x01, 0x02, 0x03]);

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
        public event EventHandler? DevicesChanged;

        public event EventHandler<string>? LogMessage;

        public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged;

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
