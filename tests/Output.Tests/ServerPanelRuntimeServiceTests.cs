using System.Security.Cryptography;
using Device.Client;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Server.Hosting;
using MicaAudio.Server;
using MicaAudio.Panels;
using Microsoft.Extensions.Logging.Abstractions;

namespace Output.Tests;

public sealed class ServerPanelRuntimeServiceTests
{
    [Fact]
    public async Task BatchTransport_ShouldNotSendFallbackStreamFrames()
    {
        await using var host = new FakeDeviceServerHost(animatedWebpBatchSupported: true);
        var libraryStore = new FakePanelLibraryStore(CreateLibrary());
        var stateStore = new FakePanelRuntimeStateStore(CreateState());
        var statusStore = new FakePanelRuntimeStatusStore();
        await using var service = CreateService(host, libraryStore, stateStore, statusStore);

        await service.StartAsync();
        Assert.True(
            await WaitForConditionAsync(() => host.CountCommands(DeviceCommandType.QueuePanelsBatch) >= 1, TimeSpan.FromSeconds(2)),
            "Timed out waiting for the first panels batch command.");

        await service.StopAsync();

        Assert.Empty(host.SentFrames);
    }

    [Fact]
    public async Task BatchTransport_ShouldAwaitActivateAppBeforeQueueingBatches()
    {
        await using var host = new FakeDeviceServerHost(animatedWebpBatchSupported: true, blockActivateApp: true);
        var libraryStore = new FakePanelLibraryStore(CreateLibrary());
        var stateStore = new FakePanelRuntimeStateStore(CreateState());
        var statusStore = new FakePanelRuntimeStatusStore();
        await using var service = CreateService(host, libraryStore, stateStore, statusStore);

        await service.StartAsync();
        Assert.True(
            await WaitForConditionAsync(() => host.CountCommands(DeviceCommandType.ActivateApp) >= 1, TimeSpan.FromSeconds(2)),
            "Timed out waiting for activate_app to be dispatched.");

        var progressedBeforeActivation = await WaitForConditionAsync(
            () => host.CountCommands(DeviceCommandType.QueuePanelsBatch) > 0 || host.SentFrames.Length > 0,
            TimeSpan.FromSeconds(1));

        host.ReleaseActivateApp();
        Assert.True(
            await WaitForConditionAsync(() => host.CountCommands(DeviceCommandType.ActivateApp) >= 1, TimeSpan.FromSeconds(2)),
            "Timed out waiting for activate_app.");
        await service.StopAsync();

        Assert.False(progressedBeforeActivation);
    }

    [Fact]
    public async Task BatchTransport_ShouldPrimeTwoBatchesBeforeAnySessionHeartbeat()
    {
        await using var host = new FakeDeviceServerHost(animatedWebpBatchSupported: true, blockHeartbeat: true);
        var libraryStore = new FakePanelLibraryStore(CreateLibrary());
        var stateStore = new FakePanelRuntimeStateStore(CreateState());
        var statusStore = new FakePanelRuntimeStatusStore();
        await using var service = CreateService(host, libraryStore, stateStore, statusStore);

        await service.StartAsync();
        Assert.True(
            await WaitForConditionAsync(() => host.CountCommands(DeviceCommandType.QueuePanelsBatch) >= 2, TimeSpan.FromSeconds(2)),
            "Timed out waiting for the two primed panels batch commands.");

        await service.StopAsync();

        var commandTypes = host.CommandTypes;
        var secondBatchIndex = IndexOfNth(commandTypes, DeviceCommandType.QueuePanelsBatch, 2);
        var firstHeartbeatIndex = Array.IndexOf(commandTypes, DeviceCommandType.SessionHeartbeat);

        Assert.True(secondBatchIndex >= 0, "Expected two primed batch commands.");
        Assert.True(
            firstHeartbeatIndex < 0 || secondBatchIndex < firstHeartbeatIndex,
            "Batch transport must prime two batches before session heartbeat traffic.");
    }

    [Fact]
    public async Task BatchTransport_ShouldSendActivateAndBatchCommandsWithoutSessionContext()
    {
        await using var host = new FakeDeviceServerHost(animatedWebpBatchSupported: true);
        var libraryStore = new FakePanelLibraryStore(CreateLibrary());
        var stateStore = new FakePanelRuntimeStateStore(CreateState());
        var statusStore = new FakePanelRuntimeStatusStore();
        await using var service = CreateService(host, libraryStore, stateStore, statusStore);

        await service.StartAsync();
        Assert.True(
            await WaitForConditionAsync(() => host.CountCommands(DeviceCommandType.QueuePanelsBatch) >= 2, TimeSpan.FromSeconds(2)),
            "Timed out waiting for the two primed panels batch commands.");

        await service.StopAsync();

        Assert.All(host.SessionContextsFor(DeviceCommandType.ActivateApp), Assert.Null);
        Assert.All(host.SessionContextsFor(DeviceCommandType.QueuePanelsBatch), Assert.Null);
        Assert.Equal(0, host.CountCommands(DeviceCommandType.SessionHeartbeat));
    }

    private static ServerPanelRuntimeService CreateService(
        FakeDeviceServerHost host,
        FakePanelLibraryStore libraryStore,
        FakePanelRuntimeStateStore stateStore,
        FakePanelRuntimeStatusStore statusStore)
    {
        return new ServerPanelRuntimeService(
            host,
            libraryStore,
            stateStore,
            statusStore,
            new PanelFrameComposer(),
            NullLogger<ServerPanelRuntimeService>.Instance);
    }

    private static PanelLibraryDocument CreateLibrary()
    {
        return new PanelLibraryDocument
        {
            Panels =
            [
                new PanelLibraryItem
                {
                    PanelId = "panel-clock",
                    Name = "Clock",
                    Width = 128,
                    Height = 64,
                    IsEnabled = true,
                    Widgets =
                    [
                        new PanelWidgetItem
                        {
                            WidgetId = "widget-clock",
                            AppId = "analogclock",
                            X = 0,
                            Y = 0,
                            Width = 128,
                            Height = 64,
                            ZIndex = 0,
                            ConfigValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            {
                                ["format24h"] = "true",
                            },
                        },
                    ],
                },
            ],
        };
    }

    private static PanelRuntimeStateDocument CreateState()
    {
        return new PanelRuntimeStateDocument
        {
            Enabled = true,
            PanelId = "panel-clock",
            TargetDeviceId = "device-1",
            UpdatedAtUtc = new DateTimeOffset(2026, 5, 6, 17, 0, 0, TimeSpan.Zero),
        };
    }

    private static async Task<bool> WaitForConditionAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (predicate())
            {
                return true;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }

        return predicate();
    }

    private static int IndexOfNth(DeviceCommandType[] commandTypes, DeviceCommandType commandType, int occurrence)
    {
        var seen = 0;
        for (var i = 0; i < commandTypes.Length; i++)
        {
            if (commandTypes[i] != commandType)
            {
                continue;
            }

            seen++;
            if (seen == occurrence)
            {
                return i;
            }
        }

        return -1;
    }

    private sealed class FakePanelLibraryStore(PanelLibraryDocument document) : IPanelLibraryStore
    {
        private PanelLibraryDocument current = document;

        public Task<PanelLibraryDocument> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(current);

        public Task SaveAsync(PanelLibraryDocument document, CancellationToken cancellationToken = default)
        {
            current = document;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePanelRuntimeStateStore(PanelRuntimeStateDocument document) : IPanelRuntimeStateStore
    {
        private PanelRuntimeStateDocument current = document;

        public Task<PanelRuntimeStateDocument> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(current);

        public Task SaveAsync(PanelRuntimeStateDocument document, CancellationToken cancellationToken = default)
        {
            current = document;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePanelRuntimeStatusStore : IPanelRuntimeStatusStore
    {
        public PanelRuntimeStatusDocument Current { get; private set; } = new();

        public Task<PanelRuntimeStatusDocument> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Current);

        public Task SaveAsync(PanelRuntimeStatusDocument document, CancellationToken cancellationToken = default)
        {
            Current = document;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDeviceServerHost : IDeviceServerHost
    {
        private readonly object gate = new();
        private readonly DeviceSnapshot device;
        private readonly TaskCompletionSource activateAppGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource heartbeatGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool blockActivateApp;
        private readonly bool blockHeartbeat;
        private readonly List<TrackedCommand> commands = [];
        private readonly List<byte[]> sentFrames = [];
        private readonly List<PanelsBatchRegistration> registrations = [];

        public FakeDeviceServerHost(bool animatedWebpBatchSupported, bool blockActivateApp = false, bool blockHeartbeat = false)
        {
            this.blockActivateApp = blockActivateApp;
            this.blockHeartbeat = blockHeartbeat;
            if (!blockActivateApp)
            {
                activateAppGate.TrySetResult();
            }

            if (!blockHeartbeat)
            {
                heartbeatGate.TrySetResult();
            }

            device = new DeviceSnapshot
            {
                DeviceId = "device-1",
                Name = "device-1",
                Profile = "dma_exp",
                Status = DeviceStatus.Online,
                ControlPlaneState = DeviceControlPlaneState.MqttOnline,
                IsRegistered = true,
                LastSeenUtc = DateTimeOffset.UtcNow,
                LastTelemetryUtc = DateTimeOffset.UtcNow,
                AnimatedWebpBatchSupported = animatedWebpBatchSupported,
            };
        }

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

        public event EventHandler<DeviceCommandProgressMessage>? CommandProgressChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<DeviceLogMessage>? DeviceLogReceived
        {
            add { }
            remove { }
        }

        public byte[][] SentFrames
        {
            get
            {
                lock (gate)
                {
                    return sentFrames.Select(static frame => frame.ToArray()).ToArray();
                }
            }
        }

        public int CountCommands(DeviceCommandType commandType)
        {
            lock (gate)
            {
                return commands.Count(command => command.CommandType == commandType);
            }
        }

        public DeviceCommandType[] CommandTypes
        {
            get
            {
                lock (gate)
                {
                    return commands.Select(command => command.CommandType).ToArray();
                }
            }
        }

        public DeviceCommandSessionContext?[] SessionContextsFor(DeviceCommandType commandType)
        {
            lock (gate)
            {
                return commands
                    .Where(command => command.CommandType == commandType)
                    .Select(command => command.SessionContext)
                    .ToArray();
            }
        }

        public void ReleaseActivateApp() => activateAppGate.TrySetResult();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public void BroadcastFrame(byte[] framePayload)
        {
        }

        public void SendFrame(string deviceId, byte[] framePayload)
        {
            lock (gate)
            {
                sentFrames.Add(framePayload.ToArray());
            }
        }

        public Task StartAsync(ServerConfig config, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task StopAsync() => Task.CompletedTask;

        public PairingCodeInfo CreatePairingCode(TimeSpan ttl)
            => new() { Code = "123456", ExpiresAtUtc = DateTimeOffset.UtcNow.Add(ttl) };

        public IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot() => [device];

        public IReadOnlyList<DeviceRecord> GetDeviceRecords() => Array.Empty<DeviceRecord>();

        public void SeedDevices(IEnumerable<DeviceRecord> devices)
        {
        }

        public Task<bool> SendCommandAsync(string deviceId, DeviceCommandType commandType, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
            => SendCommandTrackedAsync(deviceId, commandType, parameters: null, sessionContext: null, timeout, cancellationToken);

        public Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
            => SendCommandTrackedAsync(deviceId, commandType, parameters, sessionContext: null, timeout, cancellationToken);

        public async Task<CommandDispatchResult> SendCommandTrackedAsync(
            string deviceId,
            DeviceCommandType commandType,
            IReadOnlyDictionary<string, string>? parameters,
            DeviceCommandSessionContext? sessionContext,
            TimeSpan? timeout = null,
            CancellationToken cancellationToken = default)
        {
            lock (gate)
            {
                commands.Add(new TrackedCommand(commandType, sessionContext, parameters));
            }

            if (blockActivateApp && commandType == DeviceCommandType.ActivateApp)
            {
                await activateAppGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            if (blockHeartbeat && commandType == DeviceCommandType.SessionHeartbeat)
            {
                await heartbeatGate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = Guid.NewGuid().ToString("N"),
                Accepted = true,
                Completed = true,
                Success = true,
                ProgressPercent = 100,
                Stage = "done",
                Message = "ok",
            };
        }

        public bool RemoveDevice(string deviceId) => false;

        public PanelsBatchRegistration RegisterPanelsBatch(
            string deviceId,
            string panelsSessionId,
            ulong batchSequence,
            byte[] payload,
            int frameCount,
            int durationMs,
            string contentType = "image/webp")
        {
            var registration = new PanelsBatchRegistration(
                panelsSessionId,
                batchSequence,
                payload.LongLength,
                Convert.ToHexStringLower(SHA256.HashData(payload)),
                contentType,
                frameCount,
                durationMs,
                $"http://localhost/batches/{batchSequence}.webp");

            lock (gate)
            {
                registrations.Add(registration);
            }

            return registration;
        }

        public void ClearPanelsBatches(string deviceId, string? panelsSessionId = null)
        {
        }

        private sealed record TrackedCommand(
            DeviceCommandType CommandType,
            DeviceCommandSessionContext? SessionContext,
            IReadOnlyDictionary<string, string>? Parameters);
    }
}
