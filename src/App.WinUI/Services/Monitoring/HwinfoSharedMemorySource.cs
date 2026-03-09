using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Linq;
using App.WinUI.Infrastructure.Observability;
using App.WinUI.Services.Logging;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Services.Monitoring;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
internal sealed partial class HwinfoSharedMemorySource
{
    private const string MapName = "Global\\HWiNFO_SENS_SM2";
    private const string MutexName = "Global\\HWiNFO_SM2_MUTEX";
    private const uint FileMapRead = 0x0004;
    private const uint SynchronizeAccess = 0x00100000;
    private const uint WaitObject0 = 0x00000000;
    private const uint WaitAbandoned = 0x00000080;
    private const uint WaitTimeout = 0x00000102;
    private const uint WaitFailed = 0xFFFFFFFF;
    private const uint MutexWaitTimeoutMs = 250;

    private readonly AppLogStore? appLogStore;
    private readonly ILogger<HwinfoSharedMemorySource>? logger;
    private readonly WindowsMemoryFallbackProvider? memoryFallbackProvider;
    private readonly Func<DateTimeOffset> nowProvider;

    private readonly object logGate = new();
    private string lastStateSignature = string.Empty;
    private string lastMemorySignature = string.Empty;

    public HwinfoSharedMemorySource(
        AppLogStore appLogStore,
        ILogger<HwinfoSharedMemorySource> logger,
        WindowsMemoryFallbackProvider memoryFallbackProvider)
        : this(appLogStore, logger, memoryFallbackProvider, null)
    {
    }

    internal HwinfoSharedMemorySource(
        AppLogStore? appLogStore = null,
        ILogger<HwinfoSharedMemorySource>? logger = null,
        WindowsMemoryFallbackProvider? memoryFallbackProvider = null,
        Func<DateTimeOffset>? nowProvider = null)
    {
        this.appLogStore = appLogStore;
        this.logger = logger;
        this.memoryFallbackProvider = memoryFallbackProvider;
        this.nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
    }

    public MonitoringSnapshot ReadSnapshot()
    {
        using var activity = AppObservability.StartActivity("monitoring.refresh", AppObservability.AppComponent);
        activity?.SetTag(AppObservability.FeatureKey, "hwinfo-monitoring");
        using var scope = AppObservability.BeginScope(
            logger,
            activity,
            (AppObservability.ComponentKey, AppObservability.AppComponent),
            (AppObservability.MicaComponentKey, AppObservability.AppComponent),
            (AppObservability.OperationKey, "monitoring.refresh"),
            (AppObservability.FeatureKey, "hwinfo-monitoring"));

        try
        {
            var buffer = ReadSharedMemoryBuffer();
            if (buffer is null)
            {
                var unavailable = MonitoringSnapshotProjector.CreateUnavailableSnapshot(
                    "HWiNFO64 indisponivel",
                    "Abra o HWiNFO64, habilite Shared Memory Support e mantenha a janela de sensores ativa.");
                LogStateTransition(unavailable);
                return unavailable;
            }

            var fallbackSnapshot = OperatingSystem.IsWindows()
                ? memoryFallbackProvider?.ReadSnapshot() ?? MonitoringMemoryFallbackSnapshot.Empty
                : MonitoringMemoryFallbackSnapshot.Empty;
            var snapshot = MonitoringSnapshotProjector.Project(HwinfoSharedMemoryBinaryParser.Parse(buffer), nowProvider(), fallbackSnapshot);
            LogStateTransition(snapshot);
            LogMemoryTransition(snapshot);
            return snapshot;
        }
        catch (Exception ex)
        {
            AppObservability.SetException(activity, ex);
            var errorSnapshot = MonitoringSnapshotProjector.CreateErrorSnapshot(
                "Falha ao ler o HWiNFO64",
                BuildErrorHint(ex));
            LogStateTransition(errorSnapshot);
            if (logger is not null)
            {
                LogHwinfoReadFailure(logger, ex);
            }

            return errorSnapshot;
        }
    }

    internal static MonitoringSnapshot ReadSnapshotFromPayload(HwinfoSharedMemoryPayload payload, DateTimeOffset nowUtc)
        => MonitoringSnapshotProjector.Project(payload, nowUtc);

    private static byte[]? ReadSharedMemoryBuffer()
    {
        var mappingHandle = OpenFileMapping(FileMapRead, false, MapName);
        if (mappingHandle == IntPtr.Zero)
        {
            return null;
        }

        var mutexHandle = OpenMutex(SynchronizeAccess, false, MutexName);
        var mutexAcquired = false;
        var view = IntPtr.Zero;

        try
        {
            if (mutexHandle != IntPtr.Zero)
            {
                var waitResult = WaitForSingleObject(mutexHandle, MutexWaitTimeoutMs);
                if (waitResult is WaitFailed or WaitTimeout)
                {
                    throw new IOException("Nao foi possivel sincronizar a leitura do Shared Memory do HWiNFO64.");
                }

                mutexAcquired = waitResult is WaitObject0 or WaitAbandoned;
            }

            view = MapViewOfFile(mappingHandle, FileMapRead, 0, 0, UIntPtr.Zero);
            if (view == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Falha ao mapear o Shared Memory do HWiNFO64.");
            }

            var headerBuffer = new byte[HwinfoSharedMemoryBinaryParser.HeaderSize];
            Marshal.Copy(view, headerBuffer, 0, headerBuffer.Length);
            var header = HwinfoSharedMemoryBinaryParser.ParseHeaderOnly(headerBuffer);
            var requiredLength = ResolveRequiredLength(header);
            var fullBuffer = new byte[requiredLength];
            Marshal.Copy(view, fullBuffer, 0, fullBuffer.Length);
            return fullBuffer;
        }
        finally
        {
            if (view != IntPtr.Zero)
            {
                _ = UnmapViewOfFile(view);
            }

            if (mutexAcquired && mutexHandle != IntPtr.Zero)
            {
                _ = ReleaseMutex(mutexHandle);
            }

            if (mutexHandle != IntPtr.Zero)
            {
                _ = CloseHandle(mutexHandle);
            }

            _ = CloseHandle(mappingHandle);
        }
    }

    private static int ResolveRequiredLength(HwinfoSharedMemoryHeader header)
    {
        var sensorLength = (long)header.SensorOffset + ((long)header.SensorElementSize * header.SensorCount);
        var readingLength = (long)header.ReadingOffset + ((long)header.ReadingElementSize * header.ReadingCount);
        var requiredLength = Math.Max(HwinfoSharedMemoryBinaryParser.HeaderSize, Math.Max(sensorLength, readingLength));
        if (requiredLength > int.MaxValue)
        {
            throw new InvalidDataException("O Shared Memory do HWiNFO64 excedeu o limite suportado pelo app.");
        }

        return (int)requiredLength;
    }

    private void LogStateTransition(MonitoringSnapshot snapshot)
    {
        var signature = snapshot.State + "|" + snapshot.HintText;
        lock (logGate)
        {
            if (string.Equals(lastStateSignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            lastStateSignature = signature;
        }

        var severity = snapshot.State switch
        {
            MonitoringSourceState.Stale => LogSeverity.Warning,
            MonitoringSourceState.Error => LogSeverity.Error,
            _ => LogSeverity.Info,
        };

        appLogStore?.Append(LogCategory.System, severity, "HWiNFO64: " + snapshot.StateText);

        if (logger is null)
        {
            return;
        }

        switch (snapshot.State)
        {
            case MonitoringSourceState.Stale:
                LogHwinfoStateWarning(logger, snapshot.StateText);
                break;
            default:
                LogHwinfoState(logger, snapshot.StateText);
                break;
        }
    }

    private void LogMemoryTransition(MonitoringSnapshot snapshot)
    {
        var ramCard = snapshot.Kpis.FirstOrDefault(static kpi => string.Equals(kpi.Id, "ram-usage", StringComparison.Ordinal));
        var vramCard = snapshot.Kpis.FirstOrDefault(static kpi => string.Equals(kpi.Id, "gpu-vram", StringComparison.Ordinal));

        var signature = string.Join(
            "|",
            BuildCardSignature(ramCard),
            BuildCardSignature(vramCard));

        lock (logGate)
        {
            if (string.Equals(lastMemorySignature, signature, StringComparison.Ordinal))
            {
                return;
            }

            lastMemorySignature = signature;
        }

        LogMemoryCard(ramCard, "Memoria RAM", CollectCandidateLabels(snapshot.Readings, gpuOnly: false));
        LogMemoryCard(vramCard, "VRAM GPU", CollectCandidateLabels(snapshot.Readings, gpuOnly: true));
    }

    private void LogMemoryCard(MonitoringKpi? card, string title, string candidateLabels)
    {
        if (card is null)
        {
            return;
        }

        var usesFallback = card.Metrics.Any(static metric => metric.DataOrigin == MonitoringDataOrigin.WindowsFallback);
        var allUnavailable = card.Metrics.All(static metric => !metric.IsAvailable);
        if (!usesFallback && !allUnavailable)
        {
            return;
        }

        var message = usesFallback
            ? $"{title}: fallback do Windows ativo. Candidatos HWiNFO: {candidateLabels}"
            : $"{title}: indisponivel no HWiNFO. Candidatos vistos: {candidateLabels}";
        var severity = usesFallback ? LogSeverity.Warning : LogSeverity.Warning;
        appLogStore?.Append(LogCategory.System, severity, message);
    }

    private static string BuildCardSignature(MonitoringKpi? card)
    {
        if (card is null)
        {
            return "none";
        }

        return card.Id + ":" + string.Join(",", card.Metrics.Select(static metric => $"{metric.DataOrigin}:{metric.IsAvailable}:{metric.ValueText}"));
    }

    private static string CollectCandidateLabels(IReadOnlyList<MonitoringReading> readings, bool gpuOnly)
    {
        var labels = readings
            .Where(reading => gpuOnly ? IsGpuMemoryCandidate(reading) : IsMemoryCandidate(reading))
            .Select(static reading => reading.EffectiveLabel)
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToArray();

        return labels.Length == 0 ? "nenhum" : string.Join(", ", labels);
    }

    private static bool IsMemoryCandidate(MonitoringReading reading)
        => MonitoringTextNormalization.ContainsAny(reading.EffectiveSensorName, "MEMORIA", "MEMORY", "RAM", "SYSTEM") ||
           MonitoringTextNormalization.ContainsAny(reading.EffectiveLabel, "MEMORIA", "MEMORY", "RAM");

    private static bool IsGpuMemoryCandidate(MonitoringReading reading)
        => MonitoringTextNormalization.ContainsAny(reading.EffectiveSensorName, "GPU", "GEFORCE", "RADEON", "RTX", "GTX") &&
           MonitoringTextNormalization.ContainsAny(reading.EffectiveLabel, "MEMORIA", "MEMORY", "VRAM", "VIDEO");

    private static string BuildErrorHint(Exception exception)
    {
        if (exception is Win32Exception win32 && win32.NativeErrorCode == 5)
        {
            return "A leitura foi bloqueada por permissao. Execute o app e o HWiNFO64 no mesmo nivel de privilegio.";
        }

        if (exception.Message.Contains("sincronizar", StringComparison.OrdinalIgnoreCase))
        {
            return "O mutex do HWiNFO64 nao liberou a leitura a tempo. Tente novamente em alguns segundos.";
        }

        return "Verifique se o HWiNFO64 esta aberto, com Shared Memory Support habilitado e sem conflito de permissao.";
    }

    [LoggerMessage(EventId = 1700, Level = LogLevel.Information, Message = "HWiNFO monitoramento: {Message}")]
    private static partial void LogHwinfoState(ILogger logger, string message);

    [LoggerMessage(EventId = 1701, Level = LogLevel.Warning, Message = "HWiNFO monitoramento: {Message}")]
    private static partial void LogHwinfoStateWarning(ILogger logger, string message);

    [LoggerMessage(EventId = 1702, Level = LogLevel.Warning, Message = "Falha ao ler o Shared Memory do HWiNFO64.")]
    private static partial void LogHwinfoReadFailure(ILogger logger, Exception exception);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenFileMapping(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, string name);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenMutex(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, string name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr MapViewOfFile(IntPtr mappingHandle, uint desiredAccess, uint fileOffsetHigh, uint fileOffsetLow, UIntPtr bytesToMap);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnmapViewOfFile(IntPtr baseAddress);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseMutex(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
