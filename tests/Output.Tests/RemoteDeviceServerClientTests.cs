using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Device.Client.Remote;
using Device.Protocol.Contracts;
using Device.Protocol.Models;
using Device.Protocol.Stream;
using Device.Server.Hosting;

namespace Output.Tests;

public sealed class RemoteDeviceServerClientTests
{
    private const string AdminToken = "remote-client-test-token";

    [Fact]
    public async Task SendCommandTrackedAsync_WithSessionContext_ShouldSerializeSessionInAdminRequest()
    {
        using var handler = new CaptureAdminCommandHandler();
        using var httpClient = new HttpClient(handler);
        await using var remote = new RemoteDeviceServerClient(
            httpClient,
            new RemoteDeviceServerClientOptions
            {
                BaseAddress = "http://127.0.0.1:5272",
                AdminToken = AdminToken,
            });

        var result = await remote.SendCommandTrackedAsync(
            "device-remote",
            DeviceCommandType.SetBrightness,
            new Dictionary<string, string> { ["brightness"] = "90" },
            new DeviceCommandSessionContext("win-eliel-1234abcd", 12, "lock-1"),
            TimeSpan.FromSeconds(5),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("/api/v1/admin/devices/device-remote/commands/tracked", handler.LastRequestPath);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal(DeviceCommandType.SetBrightness, handler.LastRequest!.CommandType);
        Assert.Equal("90", handler.LastRequest.Parameters!["brightness"]);
        Assert.NotNull(handler.LastRequest.Session);
        Assert.Equal("win-eliel-1234abcd", handler.LastRequest.Session!.ClientId);
        Assert.Equal((uint)12, handler.LastRequest.Session.OwnerEpoch);
        Assert.Equal("lock-1", handler.LastRequest.Session.LockToken);
    }

    [Fact]
    public async Task RemoteDeviceServerClient_ShouldCreatePairingCodeListAndRemoveDevices()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        await using var remote = new RemoteDeviceServerClient(
            httpClient,
            new RemoteDeviceServerClientOptions
            {
                BaseAddress = $"http://127.0.0.1:{port}",
                AdminToken = AdminToken,
            });

        var pairing = await remote.CreatePairingCodeAsync(TimeSpan.FromMinutes(5), CancellationToken.None);
        Assert.False(string.IsNullOrWhiteSpace(pairing.Code));

        var pairedResponse = await httpClient.PostAsJsonAsync("/api/v1/pair", new PairDeviceRequest
        {
            PairingCode = pairing.Code,
            DeviceName = "remote-client-device",
        });
        pairedResponse.EnsureSuccessStatusCode();
        var paired = await pairedResponse.Content.ReadFromJsonAsync<PairDeviceResponse>();
        Assert.NotNull(paired);

        var devices = await remote.GetDevicesAsync(CancellationToken.None);
        Assert.Contains(devices, device => string.Equals(device.DeviceId, paired!.DeviceId, StringComparison.OrdinalIgnoreCase));

        Assert.True(await remote.RemoveDeviceAsync(paired!.DeviceId, CancellationToken.None));
        var afterRemove = await remote.GetDevicesAsync(CancellationToken.None);
        Assert.DoesNotContain(afterRemove, device => string.Equals(device.DeviceId, paired.DeviceId, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RemoteDeviceServerClient_ShouldRegisterAndClearPanelsBatch()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        await using var remote = new RemoteDeviceServerClient(
            httpClient,
            new RemoteDeviceServerClientOptions
            {
                BaseAddress = $"http://127.0.0.1:{port}",
                AdminToken = AdminToken,
            });

        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, httpClient, "remote-batch-device");
        var payload = "RIFFclientWEBP"u8.ToArray();

        var registration = await remote.RegisterPanelsBatchAsync(
            paired.DeviceId,
            "session-client",
            3,
            payload,
            frameCount: 20,
            durationMs: 800,
            cancellationToken: CancellationToken.None);

        Assert.Equal(payload.LongLength, registration.FileSizeBytes);
        Assert.Equal("image/webp", registration.ContentType);
        Assert.Equal(20, registration.FrameCount);
        Assert.Equal(800, registration.DurationMs);

        await remote.ClearPanelsBatchesAsync(paired.DeviceId, "session-client", CancellationToken.None);
        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, registration.DownloadUrl);
        downloadRequest.Headers.Add("X-Device-Id", paired.DeviceId);
        downloadRequest.Headers.Add("X-Device-Token", paired.Token);
        using var cleared = await httpClient.SendAsync(downloadRequest);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, cleared.StatusCode);
    }

    [Fact]
    public async Task RemoteDeviceServerClient_ShouldRoundTripPanelLibraryAndMedia()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = DeviceServerTestHarness.GetFreeTcpPort(),
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
            MaxMediaUploadBytes = 1024,
        });

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        await using var remote = new RemoteDeviceServerClient(
            httpClient,
            new RemoteDeviceServerClientOptions
            {
                BaseAddress = $"http://127.0.0.1:{port}",
                AdminToken = AdminToken,
            });

        var document = new PanelLibraryDocument
        {
            LastSelectedPanelId = "remote-panel",
            ActivePanels =
            [
                new PanelDeviceState
                {
                    DeviceId = "remote-device",
                    ActivePanelId = "remote-panel",
                    ActiveAppId = "panels-hub75",
                    LastServerOwnedPanelId = "remote-panel",
                    UpdatedAtUtc = new DateTimeOffset(2026, 4, 29, 14, 0, 0, TimeSpan.Zero),
                },
            ],
            Panels =
            [
                new PanelLibraryItem
                {
                    PanelId = "remote-panel",
                    Name = "Remoto",
                    Width = 128,
                    Height = 64,
                    IsEnabled = true,
                    Widgets =
                    [
                        new PanelWidgetItem
                        {
                            WidgetId = "visualizer-widget",
                            AppId = "visualizer-hub75",
                            DataSource = PanelWidgetDataSources.WindowsClient,
                        },
                    ],
                },
            ],
        };

        await remote.SavePanelLibraryAsync(document, CancellationToken.None);
        var loaded = await remote.GetPanelLibraryAsync(CancellationToken.None);
        Assert.Equal("remote-panel", loaded.LastSelectedPanelId);
        Assert.Equal("remote-device", Assert.Single(loaded.ActivePanels).DeviceId);
        var loadedPanel = Assert.Single(loaded.Panels);
        Assert.Equal("Remoto", loadedPanel.Name);
        Assert.Equal(PanelWidgetDataSources.WindowsClient, Assert.Single(loadedPanel.Widgets).DataSource);

        var payload = "GIF89a-remote"u8.ToArray();
        var media = await remote.UploadMediaAsync("remote.gif", "image/gif", payload, CancellationToken.None);
        Assert.Equal(payload.LongLength, media.SizeBytes);

        var downloaded = await remote.DownloadMediaAsync(media.MediaId, CancellationToken.None);
        Assert.Equal(payload, downloaded);

        Assert.True(await remote.DeleteMediaAsync(media.MediaId, CancellationToken.None));
        Assert.Null(await remote.DownloadMediaAsync(media.MediaId, CancellationToken.None));
    }

    [Fact]
    public async Task RemoteDeviceFrameTransport_ShouldForwardFramesThroughAdminWebSocket()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        using var httpClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };
        var paired = await DeviceServerTestHarness.PairDeviceAsync(host, httpClient, "remote-frame-device");

        using var deviceWs = new ClientWebSocket();
        deviceWs.Options.SetRequestHeader("X-Device-Id", paired.DeviceId);
        deviceWs.Options.SetRequestHeader("X-Device-Token", paired.Token);
        await deviceWs.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        await using var transport = new RemoteDeviceFrameTransport(new RemoteDeviceServerClientOptions
        {
            BaseAddress = $"http://127.0.0.1:{port}",
            AdminToken = AdminToken,
            FrameQueueCapacity = 4,
        });
        await transport.StartAsync(CancellationToken.None);

        var payload = new byte[] { 9, 8, 7, 6 };
        transport.SendFrame(paired.DeviceId, payload);

        Assert.Equal(payload, await ReceiveBinaryFrameAsync(deviceWs));

        await transport.StopAsync();
        await CloseWebSocketQuietlyAsync(deviceWs);
    }

    [Fact]
    public async Task RemoteDeviceFrameTransport_ShouldSendBins128ThroughServerVisualUdp()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        var visualUdpPort = DeviceServerTestHarness.GetFreeUdpPort();
        var udpSender = new FakeVisualUdpSender();
        await using var host = new DeviceServerHost(
            TimeProvider.System,
            firmwareCatalog: null,
            panelsBatchStore: null,
            pairingStore: null,
            commandStateStore: null,
            sessionStateStore: null,
            visualUdpSender: udpSender);
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = mqttPort,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
            PreferLanUdpVisualTransport = true,
            VisualUdpPort = visualUdpPort,
        });

        const string deviceToken = "direct-udp-device-token";
        host.SeedDevices(
        [
            new DeviceRecord
            {
                DeviceId = "direct-udp-device",
                Token = deviceToken,
                Name = "Server UDP",
                LastSeenUtc = DateTimeOffset.UtcNow,
                LanIpAddress = "127.0.0.1",
                VisualUdpSupported = true,
                VisualUdpPort = visualUdpPort,
                VisualUdpMode = "bins128",
            },
        ]);

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            "direct-udp-device",
            deviceToken);
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, "direct-udp-device", "online");
        var online = await DeviceServerTestHarness.WaitForConditionAsync(
            () => DeviceServerTestHarness.GetDeviceStatus(host, "direct-udp-device") == DeviceStatus.Online,
            TimeSpan.FromSeconds(5));
        Assert.True(online, "Device seedado nao entrou em online pelo MQTT.");

        await using var transport = new RemoteDeviceFrameTransport(new RemoteDeviceServerClientOptions
        {
            BaseAddress = $"http://127.0.0.1:{port}",
            AdminToken = AdminToken,
            FrameQueueCapacity = 4,
        });
        await transport.StartAsync(CancellationToken.None);

        var payload = CreateBinsPayload(sequence: 77);
        transport.SendFrame("direct-udp-device", payload);

        var sentByServer = await DeviceServerTestHarness.WaitForConditionAsync(
            () => udpSender.Sends.Count > 0,
            TimeSpan.FromSeconds(5));
        Assert.True(sentByServer, "O servidor nao enviou o frame visual por UDP.");

        var received = Assert.Single(udpSender.Sends);
        Assert.Equal("127.0.0.1", received.Address.ToString());
        Assert.Equal(visualUdpPort, received.Port);
        Assert.True(VisualUdpFrameV1.TryValidateDatagram(
            received.Datagram,
            deviceToken,
            lastAcceptedSequence: null,
            out var sequence,
            out var payloadOffset,
            out var payloadLength));
        Assert.Equal(77u, sequence);
        Assert.Equal(payload, received.Datagram.AsSpan(payloadOffset, payloadLength).ToArray());

        var diagnostics = transport.GetDiagnosticsSnapshot();
        Assert.True(diagnostics.ServerWebSocketFramesQueued > 0);
        Assert.True(diagnostics.ServerWebSocketFramesSent > 0);
    }

    [Fact]
    public async Task RemoteDeviceFrameTransport_ShouldNotRequireVisualEndpointsRoute()
    {
        using var legacyServer = new LegacyVisualEndpointServer();
        await using var transport = new RemoteDeviceFrameTransport(new RemoteDeviceServerClientOptions
        {
            BaseAddress = legacyServer.BaseAddress,
            AdminToken = AdminToken,
            FrameQueueCapacity = 4,
        });

        await transport.StartAsync(CancellationToken.None);
        transport.BroadcastFrame(CreateBinsPayload(sequence: 12));
        await Task.Delay(100);

        var diagnostics = transport.GetDiagnosticsSnapshot();
        Assert.True(diagnostics.ServerWebSocketFramesQueued > 0);
        Assert.DoesNotContain("visual-endpoints", diagnostics.LastWebSocketError ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RemoteDeviceFrameTransport_ShouldFallbackToAdminWebSocketForFrame128x64()
    {
        var port = DeviceServerTestHarness.GetFreeTcpPort();
        var mqttPort = DeviceServerTestHarness.GetFreeTcpPort();
        var visualUdpPort = DeviceServerTestHarness.GetFreeUdpPort();
        await using var host = new DeviceServerHost();
        await host.StartAsync(new ServerConfig
        {
            ListenHost = "127.0.0.1",
            PublicHost = "127.0.0.1",
            Port = port,
            MqttPort = mqttPort,
            RestrictToPrivateNetworks = true,
            AdminToken = AdminToken,
        });

        const string deviceToken = "full-frame-device-token";
        host.SeedDevices(
        [
            new DeviceRecord
            {
                DeviceId = "full-frame-device",
                Token = deviceToken,
                Name = "Full frame",
                LastSeenUtc = DateTimeOffset.UtcNow,
                LanIpAddress = "127.0.0.1",
                VisualUdpSupported = true,
                VisualUdpPort = visualUdpPort,
                VisualUdpMode = "bins128",
            },
        ]);

        using var mqttClient = await DeviceServerTestHarness.ConnectMqttClientAsync(
            "127.0.0.1",
            mqttPort,
            "full-frame-device",
            deviceToken);
        await DeviceServerTestHarness.PublishPresenceAsync(mqttClient, "full-frame-device", "online");

        using var deviceWs = new ClientWebSocket();
        deviceWs.Options.SetRequestHeader("X-Device-Id", "full-frame-device");
        deviceWs.Options.SetRequestHeader("X-Device-Token", deviceToken);
        await deviceWs.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/ws/v1/stream"), CancellationToken.None);

        await using var transport = new RemoteDeviceFrameTransport(new RemoteDeviceServerClientOptions
        {
            BaseAddress = $"http://127.0.0.1:{port}",
            AdminToken = AdminToken,
            FrameQueueCapacity = 4,
        });
        await transport.StartAsync(CancellationToken.None);

        var payload = StreamFrameV2.CreateFrame128x64Rgb565(
            sequence: 88,
            timestampQpc: 1234,
            pixels128x64Rgb565: new ushort[StreamFrameV2.PixelCount128x64],
            brightness0To255: 160);
        transport.SendFrame("full-frame-device", payload);

        Assert.Equal(payload, await ReceiveBinaryFrameAsync(deviceWs));

        var diagnostics = transport.GetDiagnosticsSnapshot();
        Assert.True(diagnostics.ServerWebSocketFramesQueued > 0);
        Assert.True(diagnostics.ServerWebSocketFramesSent > 0);

        await transport.StopAsync();
        await CloseWebSocketQuietlyAsync(deviceWs);
    }

    private static async Task<byte[]> ReceiveBinaryFrameAsync(ClientWebSocket ws)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var buffer = new byte[4096];
        using var ms = new MemoryStream();
        while (true)
        {
            var result = await ws.ReceiveAsync(buffer, timeoutCts.Token);
            Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
            if (result.Count > 0)
            {
                ms.Write(buffer, 0, result.Count);
            }

            if (result.EndOfMessage)
            {
                return ms.ToArray();
            }
        }
    }

    private static byte[] CreateBinsPayload(uint sequence)
    {
        Span<byte> bins = stackalloc byte[StreamFrameV2.BinCount128];
        for (var index = 0; index < bins.Length; index++)
        {
            bins[index] = (byte)index;
        }

        return StreamFrameV2.CreateBins128(
            sequence,
            timestampQpc: 123,
            level0To255: 200,
            bins,
            brightness0To255: 128);
    }

    private static async Task CloseWebSocketQuietlyAsync(ClientWebSocket ws)
    {
        if (ws.State == WebSocketState.Open || ws.State == WebSocketState.CloseReceived)
        {
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                return;
            }
            catch (WebSocketException)
            {
            }
        }

        ws.Abort();
    }

    private sealed class CaptureAdminCommandHandler : HttpMessageHandler
    {
        public string? LastRequestPath { get; private set; }

        public AdminTrackedCommandRequest? LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestPath = request.RequestUri?.PathAndQuery;
            LastRequest = await request.Content!.ReadFromJsonAsync<AdminTrackedCommandRequest>(
                new JsonSerializerOptions(JsonSerializerDefaults.Web),
                cancellationToken);

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new CommandDispatchResult
                {
                    DeviceId = "device-remote",
                    CommandId = "cmd-remote",
                    Accepted = true,
                    Completed = true,
                    Success = true,
                    ProgressPercent = 100,
                    Stage = "done",
                    Message = "ok",
                }),
            };
        }
    }

    private sealed class FakeVisualUdpSender : IVisualUdpSender
    {
        public List<(IPAddress Address, int Port, byte[] Datagram)> Sends { get; } = [];

        public bool TrySend(IPAddress address, int port, ReadOnlySpan<byte> datagram)
        {
            Sends.Add((address, port, datagram.ToArray()));
            return true;
        }
    }

    private sealed class LegacyVisualEndpointServer : IDisposable
    {
        private readonly TcpListener listener;
        private readonly CancellationTokenSource cts = new();
        private readonly Task worker;

        public LegacyVisualEndpointServer()
        {
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            BaseAddress = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}";
            worker = Task.Run(RunAsync);
        }

        public string BaseAddress { get; }

        public void Dispose()
        {
            cts.Cancel();
            listener.Stop();
            try
            {
                worker.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }

            cts.Dispose();
        }

        private async Task RunAsync()
        {
            while (!cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await listener.AcceptTcpClientAsync(cts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (SocketException) when (cts.IsCancellationRequested)
                {
                    return;
                }

                _ = Task.Run(() => HandleClientAsync(client), CancellationToken.None);
            }
        }

        private static async Task HandleClientAsync(TcpClient client)
        {
            using (client)
            {
                await using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                _ = await reader.ReadLineAsync().ConfigureAwait(false);
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync().ConfigureAwait(false)))
                {
                }

                var payload = """{"error":"not_found"}"""u8.ToArray();
                var header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 404 Not Found\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header).ConfigureAwait(false);
                await stream.WriteAsync(payload).ConfigureAwait(false);
            }
        }
    }
}
