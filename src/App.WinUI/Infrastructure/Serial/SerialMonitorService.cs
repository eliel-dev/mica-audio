using System.IO.Ports;
using System.Text;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Infrastructure.Serial;

internal enum SerialMonitorConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Error = 3,
}

internal sealed record SerialMonitorLine
{
    public required string Text { get; init; }

    public required DateTimeOffset CapturedAt { get; init; }
}

internal sealed record SerialMonitorSessionState
{
    public string? SelectedPortName { get; init; }

    public SerialMonitorConnectionState ConnectionState { get; init; }

    public string StatusText { get; init; } = "Desconectado.";

    public string? ErrorText { get; init; }

    public IReadOnlyList<SerialPortDescriptor> AvailablePorts { get; init; } = Array.Empty<SerialPortDescriptor>();

    public IReadOnlyList<SerialMonitorLine> VisibleLines { get; init; } = Array.Empty<SerialMonitorLine>();

    public int LineCount { get; init; }
}

internal interface ISerialMonitorService : IAsyncDisposable
{
    event EventHandler? StateChanged;

    SerialMonitorSessionState GetStateSnapshot();

    void SetSelectedPort(string? portName);

    Task RefreshPortsAsync(CancellationToken cancellationToken = default);

    Task ConnectAsync(string portName, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    void Clear();

    string ExportAllText();
}

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitor-serial-em-configuracoes
internal sealed partial class SerialMonitorService : ISerialMonitorService
{
    internal const int DefaultBaudRate = 115200;
    internal const int MaxBufferedLines = 2000;
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);

    private readonly ISerialPortCatalogService serialPortCatalogService;
    private readonly ISerialMonitorPortFactory portFactory;
    private readonly ILogger<SerialMonitorService> logger;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private readonly object sync = new();
    private readonly SerialMonitorLineStore lineStore = new(MaxBufferedLines);

    private IReadOnlyList<SerialPortDescriptor> availablePorts = Array.Empty<SerialPortDescriptor>();
    private string? selectedPortName;
    private SerialMonitorConnectionState connectionState;
    private string statusText = "Desconectado.";
    private string? errorText;
    private ISerialMonitorPort? currentPort;
    private CancellationTokenSource? readLoopCts;
    private int readLoopGeneration;

    public SerialMonitorService(
        ISerialPortCatalogService serialPortCatalogService,
        ILogger<SerialMonitorService> logger)
        : this(serialPortCatalogService, new SerialMonitorPortFactory(), logger)
    {
    }

    internal SerialMonitorService(
        ISerialPortCatalogService serialPortCatalogService,
        ISerialMonitorPortFactory portFactory,
        ILogger<SerialMonitorService> logger)
    {
        this.serialPortCatalogService = serialPortCatalogService;
        this.portFactory = portFactory;
        this.logger = logger;
    }

    public event EventHandler? StateChanged;

    public SerialMonitorSessionState GetStateSnapshot()
    {
        lock (sync)
        {
            var lines = lineStore.Snapshot();
            return new SerialMonitorSessionState
            {
                SelectedPortName = selectedPortName,
                ConnectionState = connectionState,
                StatusText = statusText,
                ErrorText = errorText,
                AvailablePorts = availablePorts,
                VisibleLines = lines,
                LineCount = lines.Count,
            };
        }
    }

    public void SetSelectedPort(string? portName)
    {
        portName = NormalizePortName(portName);
        var changed = false;

        lock (sync)
        {
            if (string.Equals(selectedPortName, portName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            selectedPortName = portName;
            if (connectionState == SerialMonitorConnectionState.Disconnected)
            {
                statusText = BuildDisconnectedStatus(selectedPortName, availablePorts.Count);
                errorText = null;
            }

            changed = true;
        }

        if (changed)
        {
            RaiseStateChanged();
        }
    }

    public async Task RefreshPortsAsync(CancellationToken cancellationToken = default)
    {
        var ports = await serialPortCatalogService.ListAsync(includeAllPorts: true, cancellationToken).ConfigureAwait(false);
        var changed = false;

        lock (sync)
        {
            availablePorts = ports;
            selectedPortName = ResolveSelectedPortName(selectedPortName, ports, preserveCurrentWhenMissing: currentPort is not null);
            if (currentPort is null)
            {
                statusText = connectionState == SerialMonitorConnectionState.Error
                    ? errorText ?? BuildDisconnectedStatus(selectedPortName, availablePorts.Count)
                    : BuildDisconnectedStatus(selectedPortName, availablePorts.Count);
                if (connectionState != SerialMonitorConnectionState.Error)
                {
                    connectionState = SerialMonitorConnectionState.Disconnected;
                }
            }

            changed = true;
        }

        if (changed)
        {
            RaiseStateChanged();
        }
    }

    public async Task ConnectAsync(string portName, CancellationToken cancellationToken = default)
    {
        var normalizedPortName = NormalizePortName(portName);
        if (string.IsNullOrWhiteSpace(normalizedPortName))
        {
            ApplyDisconnectedError("Selecione uma porta COM antes de conectar.");
            return;
        }

        portName = normalizedPortName;

        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync(waitForGate: false).ConfigureAwait(false);

            ISerialMonitorPort port;
            try
            {
                port = portFactory.Create(portName);
            }
            catch (Exception ex)
            {
                LogSerialMonitorCreateFailed(logger, ex, portName);
                ApplyDisconnectedError($"Falha ao preparar {portName}: {ex.Message}");
                return;
            }

            var generation = 0;
            CancellationTokenSource? cts = null;

            try
            {
                lock (sync)
                {
                    selectedPortName = portName;
                    connectionState = SerialMonitorConnectionState.Connecting;
                    statusText = $"Conectando em {portName} a {DefaultBaudRate} baud...";
                    errorText = null;
                }
                RaiseStateChanged();

                port.Open();
                port.DiscardInBuffer();

                cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                lock (sync)
                {
                    currentPort = port;
                    readLoopCts = cts;
                    readLoopGeneration++;
                    generation = readLoopGeneration;
                    connectionState = SerialMonitorConnectionState.Connected;
                    statusText = $"Conectado em {portName} a {DefaultBaudRate} baud.";
                    errorText = null;
                }
                RaiseStateChanged();

                _ = Task.Run(() => RunReadLoopAsync(port, generation, cts.Token), CancellationToken.None);
            }
            catch (Exception ex)
            {
                cts?.Dispose();
                port.Dispose();
                LogSerialMonitorOpenFailed(logger, ex, portName);
                ApplyDisconnectedError($"Falha ao abrir {portName}: {ex.Message}", portName);
            }
        }
        finally
        {
            operationGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        await operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync(waitForGate: false).ConfigureAwait(false);
        }
        finally
        {
            operationGate.Release();
        }
    }

    public void Clear()
    {
        lock (sync)
        {
            lineStore.Clear();
        }

        RaiseStateChanged();
    }

    public string ExportAllText()
    {
        lock (sync)
        {
            var lines = lineStore.Snapshot();
            return string.Join(Environment.NewLine, lines.Select(l => l.Text));
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        operationGate.Dispose();
    }

    internal static string? ResolveSelectedPortName(
        string? currentPortName,
        IReadOnlyList<SerialPortDescriptor> ports,
        bool preserveCurrentWhenMissing = false)
    {
        if (!string.IsNullOrWhiteSpace(currentPortName))
        {
            foreach (var item in ports)
            {
                if (string.Equals(item.PortName, currentPortName, StringComparison.OrdinalIgnoreCase))
                {
                    return item.PortName;
                }
            }

            if (preserveCurrentWhenMissing)
            {
                return currentPortName.Trim();
            }
        }

        foreach (var item in ports)
        {
            if (item.IsPreferredDevice)
            {
                return item.PortName;
            }
        }

        return ports.Count > 0 ? ports[0].PortName : null;
    }

    internal static string BuildDisconnectedStatus(string? portName, int availablePortCount)
    {
        if (availablePortCount == 0)
        {
            return "Nenhuma porta COM detectada.";
        }

        return string.IsNullOrWhiteSpace(portName)
            ? "Selecione uma porta COM para iniciar o monitor serial."
            : $"Desconectado. Pronto para abrir {portName} a {DefaultBaudRate} baud.";
    }

    private async Task DisconnectCoreAsync(bool waitForGate)
    {
        if (waitForGate)
        {
            await operationGate.WaitAsync().ConfigureAwait(false);
        }

        try
        {
            ISerialMonitorPort? port;
            CancellationTokenSource? cts;
            string? portName;

            lock (sync)
            {
                port = currentPort;
                cts = readLoopCts;
                portName = selectedPortName;
                currentPort = null;
                readLoopCts = null;
                readLoopGeneration++;
                connectionState = SerialMonitorConnectionState.Disconnected;
                errorText = null;
                statusText = BuildDisconnectedStatus(portName, availablePorts.Count);
            }

            cts?.Cancel();

            if (port is not null)
            {
                try
                {
                    port.Close();
                }
                catch (Exception ex)
                {
                    LogSerialMonitorCloseFailed(logger, ex, port.PortName);
                }
                finally
                {
                    port.Dispose();
                }
            }

            cts?.Dispose();
            RaiseStateChanged();
        }
        finally
        {
            if (waitForGate)
            {
                operationGate.Release();
            }
        }
    }

    private async Task RunReadLoopAsync(ISerialMonitorPort port, int generation, CancellationToken cancellationToken)
    {
        var assembler = new SerialMonitorLineAssembler();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var chunk = port.ReadExisting();
                if (!string.IsNullOrEmpty(chunk))
                {
                    var lines = assembler.Append(chunk);
                    if (lines.Count > 0)
                    {
                        AppendLines(lines, generation);
                    }
                }

                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LogSerialMonitorReadFailed(logger, ex, port.PortName);
            ApplyReadLoopError(port.PortName, generation, ex.Message, assembler.FlushPendingLine());
            return;
        }

        var trailingLine = assembler.FlushPendingLine();
        if (!string.IsNullOrWhiteSpace(trailingLine))
        {
            AppendLines([trailingLine], generation);
        }
    }

    private void AppendLines(IReadOnlyList<string> lines, int generation)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var changed = false;

        lock (sync)
        {
            if (generation != readLoopGeneration)
            {
                return;
            }

            var now = DateTimeOffset.Now;
            foreach (var line in lines)
            {
                lineStore.Add(new SerialMonitorLine
                {
                    Text = line,
                    CapturedAt = now,
                });
            }

            changed = true;
        }

        if (changed)
        {
            RaiseStateChanged();
        }
    }

    private void ApplyReadLoopError(string portName, int generation, string message, string? trailingLine)
    {
        if (!string.IsNullOrWhiteSpace(trailingLine))
        {
            AppendLines([trailingLine], generation);
        }

        var changed = false;
        CancellationTokenSource? cts = null;

        lock (sync)
        {
            if (generation != readLoopGeneration)
            {
                return;
            }

            currentPort?.Dispose();
            currentPort = null;
            cts = readLoopCts;
            readLoopCts = null;
            readLoopGeneration++;
            connectionState = SerialMonitorConnectionState.Error;
            errorText = $"Falha ao ler {portName}: {message}";
            statusText = errorText;
            changed = true;
        }

        cts?.Cancel();
        cts?.Dispose();

        if (changed)
        {
            RaiseStateChanged();
        }
    }

    private void ApplyDisconnectedError(string message, string? portName = null)
    {
        lock (sync)
        {
            selectedPortName = NormalizePortName(portName) ?? selectedPortName;
            connectionState = SerialMonitorConnectionState.Error;
            errorText = message;
            statusText = message;
        }

        RaiseStateChanged();
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string? NormalizePortName(string? portName)
        => string.IsNullOrWhiteSpace(portName)
            ? null
            : portName.Trim();

    [LoggerMessage(EventId = 1110, Level = LogLevel.Warning, Message = "Nao foi possivel criar monitor serial. porta={PortName}")]
    private static partial void LogSerialMonitorCreateFailed(ILogger logger, Exception exception, string portName);

    [LoggerMessage(EventId = 1111, Level = LogLevel.Warning, Message = "Falha ao abrir monitor serial. porta={PortName}")]
    private static partial void LogSerialMonitorOpenFailed(ILogger logger, Exception exception, string portName);

    [LoggerMessage(EventId = 1112, Level = LogLevel.Debug, Message = "Falha ao fechar monitor serial. porta={PortName}")]
    private static partial void LogSerialMonitorCloseFailed(ILogger logger, Exception exception, string portName);

    [LoggerMessage(EventId = 1113, Level = LogLevel.Warning, Message = "Monitor serial perdeu a conexao. porta={PortName}")]
    private static partial void LogSerialMonitorReadFailed(ILogger logger, Exception exception, string portName);
}

internal interface ISerialMonitorPortFactory
{
    ISerialMonitorPort Create(string portName);
}

internal interface ISerialMonitorPort : IDisposable
{
    string PortName { get; }

    void Open();

    void Close();

    void DiscardInBuffer();

    string ReadExisting();
}

internal sealed class SerialMonitorPortFactory : ISerialMonitorPortFactory
{
    public ISerialMonitorPort Create(string portName)
        => new SerialMonitorPort(portName);
}

internal sealed class SerialMonitorPort : ISerialMonitorPort
{
    private readonly SerialPort port;

    public SerialMonitorPort(string portName)
    {
        PortName = portName;
        port = new SerialPort(portName, SerialMonitorService.DefaultBaudRate, Parity.None, 8, StopBits.One)
        {
            DtrEnable = false,
            RtsEnable = false,
            Handshake = Handshake.None,
            NewLine = "\n",
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false),
        };
    }

    public string PortName { get; }

    public void Open() => port.Open();

    public void Close()
    {
        if (port.IsOpen)
        {
            port.Close();
        }
    }

    public void DiscardInBuffer()
    {
        if (port.IsOpen)
        {
            port.DiscardInBuffer();
        }
    }

    public string ReadExisting() => port.ReadExisting();

    public void Dispose() => port.Dispose();
}

internal sealed class SerialMonitorLineAssembler
{
    private readonly StringBuilder pending = new();

    public IReadOnlyList<string> Append(string? chunk)
    {
        if (string.IsNullOrEmpty(chunk))
        {
            return Array.Empty<string>();
        }

        var lines = new List<string>();
        foreach (var character in chunk)
        {
            if (character is '\r' or '\n')
            {
                FlushLine(lines);
                continue;
            }

            pending.Append(character);
        }

        return lines;
    }

    public string? FlushPendingLine()
    {
        if (pending.Length == 0)
        {
            return null;
        }

        var line = pending.ToString();
        pending.Clear();
        return string.IsNullOrWhiteSpace(line) ? null : line;
    }

    public void Reset() => pending.Clear();

    private void FlushLine(List<string> lines)
    {
        if (pending.Length == 0)
        {
            return;
        }

        var line = pending.ToString();
        pending.Clear();
        if (!string.IsNullOrWhiteSpace(line))
        {
            lines.Add(line);
        }
    }
}

internal sealed class SerialMonitorLineStore
{
    private readonly List<SerialMonitorLine> lines = new();

    public SerialMonitorLineStore(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        Capacity = capacity;
    }

    public int Capacity { get; }

    public int Count => lines.Count;

    public void Add(SerialMonitorLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        lines.Add(line);
        TrimToCapacity();
    }

    public void Clear() => lines.Clear();

    public IReadOnlyList<SerialMonitorLine> Snapshot() => lines.ToArray();

    private void TrimToCapacity()
    {
        var overflow = lines.Count - Capacity;
        if (overflow > 0)
        {
            lines.RemoveRange(0, overflow);
        }
    }
}
