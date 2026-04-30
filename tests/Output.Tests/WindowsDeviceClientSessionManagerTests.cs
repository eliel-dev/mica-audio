using App.WinUI.Services.Devices;
using Device.Client;
using Device.Protocol.Models;
using MicaAudio.Core.Config;
using Microsoft.Extensions.Options;

namespace Output.Tests;

public sealed class WindowsDeviceClientSessionManagerTests
{
    [Fact]
    public void ClientId_ShouldBeStableForAppDataRoot()
    {
        var root = CreateTempRoot();
        using var first = new WindowsDeviceClientSessionManager(new CaptureDeviceServerClient(), Options.Create(CreateOptions(root)));
        using var second = new WindowsDeviceClientSessionManager(new CaptureDeviceServerClient(), Options.Create(CreateOptions(root)));

        Assert.StartsWith("win-", first.ClientId, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(first.ClientId, second.ClientId);
        Assert.DoesNotContain(" ", first.ClientId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AssumeOwnerAsync_ShouldPredictEpochWithoutSendingCommand()
    {
        var root = CreateTempRoot();
        var client = new CaptureDeviceServerClient();
        client.SetDevices([
            new DeviceSnapshot
            {
                DeviceId = "device-1",
                Name = "device-1",
                Profile = "dma_exp",
                Status = DeviceStatus.Online,
                SessionActiveClientId = "other-client",
                SessionActiveOwnerEpoch = 4,
            },
        ]);
        using var manager = new WindowsDeviceClientSessionManager(client, Options.Create(CreateOptions(root)));
        manager.ObserveDevices(client.GetDevices());

        var context = await manager.AssumeOwnerAsync("device-1", "visualizer");

        Assert.Equal(manager.ClientId, context.ClientId);
        Assert.Equal((uint)5, context.OwnerEpoch);
        Assert.Null(client.LastCommandType);
        Assert.Equal((uint)5, manager.GetOwnerEpoch("device-1"));
    }

    [Fact]
    public async Task StartHeartbeat_ShouldSendImmediateHeartbeatWithCurrentOwnerEpoch()
    {
        var root = CreateTempRoot();
        var client = new CaptureDeviceServerClient();
        using var manager = new WindowsDeviceClientSessionManager(
            client,
            Options.Create(CreateOptions(root)),
            heartbeatInterval: TimeSpan.FromMilliseconds(50));
        await manager.AssumeOwnerAsync("device-1", "visualizer");

        manager.StartHeartbeat("device-1", "visualizer");

        var sent = await DeviceServerTestHarness.WaitForConditionAsync(
            () => client.LastCommandType == DeviceCommandType.SessionHeartbeat,
            TimeSpan.FromSeconds(1));

        Assert.True(sent, "Heartbeat inicial nao foi enviado.");
        Assert.Equal("visualizer", client.LastParameters!["mode"]);
        Assert.NotNull(client.LastSessionContext);
        Assert.Equal(manager.ClientId, client.LastSessionContext!.ClientId);
        Assert.Equal((uint)1, client.LastSessionContext.OwnerEpoch);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-session-manager-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static MicaAudioOptions CreateOptions(string root)
        => new()
        {
            AppDataRoot = root,
        };

    private sealed class CaptureDeviceServerClient : IDeviceServerClient
    {
        private IReadOnlyList<DeviceSnapshot> devices = Array.Empty<DeviceSnapshot>();

        public event EventHandler? DevicesChanged;

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

        public DeviceCommandType? LastCommandType { get; private set; }

        public IReadOnlyDictionary<string, string>? LastParameters { get; private set; }

        public DeviceCommandSessionContext? LastSessionContext { get; private set; }

        public string GetServerBaseAddress() => "http://127.0.0.1:5272";

        public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
            => new() { Code = "123456", ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl) };

        public IReadOnlyList<DeviceSnapshot> GetDevices() => devices;

        public bool RemoveDevice(string deviceId) => true;

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            TimeSpan timeout,
            CancellationToken cancellationToken)
            => SendCommandTrackedAsync(deviceId, commandType, parameters, sessionContext: null, timeout, cancellationToken);

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            DeviceCommandSessionContext? sessionContext,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            LastCommandType = commandType;
            LastParameters = parameters;
            LastSessionContext = sessionContext;
            return Task.FromResult(new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = "cmd-session",
                Accepted = true,
                Completed = true,
                Success = true,
                ProgressPercent = 100,
                Stage = "done",
                Message = "ok",
            });
        }

        public PanelsBatchRegistration RegisterPanelsBatch(
            string deviceId,
            string panelsSessionId,
            ulong batchSequence,
            byte[] payload,
            int frameCount,
            int durationMs,
            string contentType = "image/webp")
            => new(panelsSessionId, batchSequence, payload.LongLength, string.Empty, contentType, frameCount, durationMs, string.Empty);

        public void ClearPanelsBatches(string deviceId, string? panelsSessionId = null)
        {
        }

        public void SetDevices(IReadOnlyList<DeviceSnapshot> snapshots)
        {
            devices = snapshots;
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
