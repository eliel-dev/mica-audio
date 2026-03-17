using App.WinUI.Infrastructure.Serial;
using Microsoft.Extensions.Logging;

namespace Output.Tests;

public sealed class SerialMonitorServiceTests
{
    [Fact]
    public void ResolveSelectedPortName_ShouldPreserveCurrentThenPreferPreferredPort()
    {
        var ports = new[]
        {
            new SerialPortDescriptor { PortName = "COM4", DisplayName = "USB Serial", IsPreferredDevice = false, PriorityRank = 100 },
            new SerialPortDescriptor { PortName = "COM7", DisplayName = "ESP32-S3", IsPreferredDevice = true, PriorityRank = 0 },
        };

        var current = SerialMonitorService.ResolveSelectedPortName("COM4", ports);
        var preferred = SerialMonitorService.ResolveSelectedPortName("COM9", ports);
        var none = SerialMonitorService.ResolveSelectedPortName(null, Array.Empty<SerialPortDescriptor>());

        Assert.Equal("COM4", current);
        Assert.Equal("COM7", preferred);
        Assert.Null(none);
    }

    [Fact]
    public void LineAssembler_ShouldHandleCrLfAndFragments()
    {
        var assembler = new SerialMonitorLineAssembler();

        var first = assembler.Append("boot\r\nready");
        var second = assembler.Append("\npartial");
        var trailing = assembler.FlushPendingLine();

        Assert.Equal(["boot"], first);
        Assert.Equal(["ready"], second);
        Assert.Equal("partial", trailing);
    }

    [Fact]
    public void LineStore_ShouldTrimOldestEntriesAtCapacity()
    {
        var store = new SerialMonitorLineStore(capacity: 3);

        store.Add(new SerialMonitorLine { Text = "line-1", CapturedAt = DateTimeOffset.Now });
        store.Add(new SerialMonitorLine { Text = "line-2", CapturedAt = DateTimeOffset.Now });
        store.Add(new SerialMonitorLine { Text = "line-3", CapturedAt = DateTimeOffset.Now });
        store.Add(new SerialMonitorLine { Text = "line-4", CapturedAt = DateTimeOffset.Now });

        var snapshot = store.Snapshot();

        Assert.Equal(3, snapshot.Count);
        Assert.Equal(["line-2", "line-3", "line-4"], snapshot.Select(static item => item.Text).ToArray());
    }

    [Fact]
    public async Task ConnectAsync_ShouldTransitionThroughConnectingAndConnectedAndCaptureLines()
    {
        var fakePort = new FakeSerialMonitorPort("COM7");
        fakePort.EnqueueChunk("boot\r\nready\n");
        var catalog = new FakeSerialPortCatalogService([
            new SerialPortDescriptor { PortName = "COM7", DisplayName = "ESP32-S3", IsPreferredDevice = true, PriorityRank = 0 },
        ]);
        var factory = new FakeSerialMonitorPortFactory(fakePort);
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        await using var service = new SerialMonitorService(catalog, factory, loggerFactory.CreateLogger<SerialMonitorService>());

        var states = new List<SerialMonitorConnectionState>();
        service.StateChanged += (_, _) => states.Add(service.GetStateSnapshot().ConnectionState);

        service.SetSelectedPort("COM7");
        await service.ConnectAsync("COM7");
        await WaitForAsync(() => service.GetStateSnapshot().LineCount == 2);

        var snapshot = service.GetStateSnapshot();

        Assert.Contains(SerialMonitorConnectionState.Connecting, states);
        Assert.Contains(SerialMonitorConnectionState.Connected, states);
        Assert.Equal(SerialMonitorConnectionState.Connected, snapshot.ConnectionState);
        Assert.Equal(["boot", "ready"], snapshot.VisibleLines.Select(static item => item.Text).ToArray());

        await service.DisconnectAsync();
    }

    [Fact]
    public async Task ConnectAsync_ShouldExposeOpenFailureAsError()
    {
        var fakePort = new FakeSerialMonitorPort("COM8")
        {
            OpenException = new UnauthorizedAccessException("Acesso negado"),
        };
        var catalog = new FakeSerialPortCatalogService([
            new SerialPortDescriptor { PortName = "COM8", DisplayName = "Busy device", IsPreferredDevice = true, PriorityRank = 0 },
        ]);
        var factory = new FakeSerialMonitorPortFactory(fakePort);
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        await using var service = new SerialMonitorService(catalog, factory, loggerFactory.CreateLogger<SerialMonitorService>());

        await service.ConnectAsync("COM8");

        var snapshot = service.GetStateSnapshot();

        Assert.Equal(SerialMonitorConnectionState.Error, snapshot.ConnectionState);
        Assert.Contains("Falha ao abrir COM8", snapshot.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clear_ShouldRemoveBufferedLinesWithoutDroppingConnection()
    {
        var fakePort = new FakeSerialMonitorPort("COM7");
        fakePort.EnqueueChunk("one\n");
        var catalog = new FakeSerialPortCatalogService([
            new SerialPortDescriptor { PortName = "COM7", DisplayName = "ESP32-S3", IsPreferredDevice = true, PriorityRank = 0 },
        ]);
        var factory = new FakeSerialMonitorPortFactory(fakePort);
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        await using var service = new SerialMonitorService(catalog, factory, loggerFactory.CreateLogger<SerialMonitorService>());

        await service.ConnectAsync("COM7");
        await WaitForAsync(() => service.GetStateSnapshot().LineCount == 1);

        service.Clear();

        var snapshot = service.GetStateSnapshot();

        Assert.Equal(SerialMonitorConnectionState.Connected, snapshot.ConnectionState);
        Assert.Empty(snapshot.VisibleLines);
        Assert.Equal(0, snapshot.LineCount);
    }

    [Fact]
    public async Task ReadLoop_ShouldTransitionToErrorWhenPortThrowsWhileConnected()
    {
        var fakePort = new FakeSerialMonitorPort("COM7");
        fakePort.EnqueueChunk("hello\n");
        fakePort.ReadException = new IOException("Porta removida");
        var catalog = new FakeSerialPortCatalogService([
            new SerialPortDescriptor { PortName = "COM7", DisplayName = "ESP32-S3", IsPreferredDevice = true, PriorityRank = 0 },
        ]);
        var factory = new FakeSerialMonitorPortFactory(fakePort);
        using var loggerFactory = LoggerFactory.Create(static _ => { });
        await using var service = new SerialMonitorService(catalog, factory, loggerFactory.CreateLogger<SerialMonitorService>());

        await service.ConnectAsync("COM7");
        await WaitForAsync(() => service.GetStateSnapshot().ConnectionState == SerialMonitorConnectionState.Error);

        var snapshot = service.GetStateSnapshot();

        Assert.Equal(SerialMonitorConnectionState.Error, snapshot.ConnectionState);
        Assert.Contains("Falha ao ler COM7", snapshot.StatusText, StringComparison.Ordinal);
        Assert.Contains("hello", snapshot.VisibleLines.Select(static item => item.Text));
    }

    private static async Task WaitForAsync(Func<bool> predicate, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (predicate())
            {
                return;
            }

            await Task.Delay(20);
        }

        Assert.True(predicate(), "Condition was not met before timeout.");
    }

    private sealed class FakeSerialPortCatalogService : ISerialPortCatalogService
    {
        private readonly IReadOnlyList<SerialPortDescriptor> ports;

        public FakeSerialPortCatalogService(IReadOnlyList<SerialPortDescriptor> ports)
        {
            this.ports = ports;
        }

        public Task<IReadOnlyList<SerialPortDescriptor>> ListAsync(bool includeAllPorts, CancellationToken cancellationToken = default)
            => Task.FromResult(ports);
    }

    private sealed class FakeSerialMonitorPortFactory : ISerialMonitorPortFactory
    {
        private readonly FakeSerialMonitorPort port;

        public FakeSerialMonitorPortFactory(FakeSerialMonitorPort port)
        {
            this.port = port;
        }

        public ISerialMonitorPort Create(string portName)
        {
            Assert.Equal(port.PortName, portName);
            return port;
        }
    }

    private sealed class FakeSerialMonitorPort : ISerialMonitorPort
    {
        private readonly Queue<string> chunks = new();

        public FakeSerialMonitorPort(string portName)
        {
            PortName = portName;
        }

        public string PortName { get; }

        public Exception? OpenException { get; init; }

        public Exception? ReadException { get; set; }

        public bool IsOpen { get; private set; }

        public void EnqueueChunk(string chunk)
        {
            chunks.Enqueue(chunk);
        }

        public void Open()
        {
            if (OpenException is not null)
            {
                throw OpenException;
            }

            IsOpen = true;
        }

        public void Close()
        {
            IsOpen = false;
        }

        public void DiscardInBuffer()
        {
        }

        public string ReadExisting()
        {
            if (!IsOpen)
            {
                return string.Empty;
            }

            if (chunks.Count > 0)
            {
                return chunks.Dequeue();
            }

            if (ReadException is not null)
            {
                throw ReadException;
            }

            return string.Empty;
        }

        public void Dispose()
        {
            IsOpen = false;
        }
    }
}
