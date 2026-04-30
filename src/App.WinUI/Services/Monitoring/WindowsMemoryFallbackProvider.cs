using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Services.Monitoring;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
[SupportedOSPlatform("windows")]
internal sealed partial class WindowsMemoryFallbackProvider
{
    private readonly ILogger<WindowsMemoryFallbackProvider>? logger;

    public WindowsMemoryFallbackProvider(ILogger<WindowsMemoryFallbackProvider> logger)
        : this((Func<MonitoringSystemMemoryFallback?>?)null, logger)
    {
    }

    internal WindowsMemoryFallbackProvider(
        Func<MonitoringSystemMemoryFallback?>? systemMemoryReader = null,
        ILogger<WindowsMemoryFallbackProvider>? logger = null)
    {
        this.logger = logger;
        readSystemMemory = systemMemoryReader ?? ReadSystemMemoryFallback;
    }

    private readonly Func<MonitoringSystemMemoryFallback?> readSystemMemory;

    public MonitoringMemoryFallbackSnapshot ReadSnapshot()
    {
        var systemMemory = SafeRead(readSystemMemory);
        var gpuMemory = SafeRead(ReadGpuMemoryFallback);
        return new MonitoringMemoryFallbackSnapshot(systemMemory, gpuMemory);
    }

    private T? SafeRead<T>(Func<T?> reader)
        where T : class
    {
        try
        {
            return reader();
        }
        catch (Exception ex)
        {
            if (logger is not null)
            {
                LogWindowsMemoryFallbackFailure(logger, ex);
            }

            return null;
        }
    }

    private static MonitoringSystemMemoryFallback? ReadSystemMemoryFallback()
    {
        var status = new MemoryStatusEx();
        status.dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>();
        if (!GlobalMemoryStatusEx(ref status))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Falha ao consultar a memoria do sistema.");
        }

        var totalBytes = status.ullTotalPhys;
        var availableBytes = status.ullAvailPhys;
        var usedBytes = totalBytes > availableBytes ? totalBytes - availableBytes : 0UL;
        return new MonitoringSystemMemoryFallback(usedBytes, availableBytes, totalBytes);
    }

    private static MonitoringGpuMemoryFallback? ReadGpuMemoryFallback()
    {
        var controllers = QueryVideoControllers();
        if (controllers.Count != 1)
        {
            return null;
        }

        var selectedController = controllers[0];
        var usageSamples = QueryGpuAdapterMemorySamples();
        if (usageSamples.Count == 0)
        {
            return null;
        }

        usageSamples.Sort(static (left, right) => right.DedicatedUsageBytes.CompareTo(left.DedicatedUsageBytes));
        var primary = usageSamples[0];
        var secondaryUsage = usageSamples.Count > 1 ? usageSamples[1].DedicatedUsageBytes : 0UL;
        var isReliable = usageSamples.Count == 1 ||
                         secondaryUsage == 0UL ||
                         (primary.DedicatedUsageBytes > 0UL &&
                          secondaryUsage < 256UL * 1024UL * 1024UL &&
                          secondaryUsage < (primary.DedicatedUsageBytes / 5UL));
        if (!isReliable)
        {
            return null;
        }

        var usedBytes = Math.Min(primary.DedicatedUsageBytes, selectedController.TotalBytes);
        return new MonitoringGpuMemoryFallback(selectedController.Name, usedBytes, selectedController.TotalBytes);
    }

    private static List<VideoControllerInfo> QueryVideoControllers()
    {
        using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
        using var results = searcher.Get();
        var controllers = new List<VideoControllerInfo>();
        foreach (ManagementObject controller in results)
        {
            var name = controller["Name"] as string;
            if (string.IsNullOrWhiteSpace(name) ||
                MonitoringTextNormalization.ContainsAny(name, "MICROSOFT BASIC", "REMOTE DISPLAY"))
            {
                continue;
            }

            var totalBytes = ConvertToUInt64(controller["AdapterRAM"]);
            if (totalBytes == 0UL)
            {
                continue;
            }

            controllers.Add(new VideoControllerInfo(name.Trim(), totalBytes));
        }

        return controllers;
    }

    private static List<GpuAdapterMemorySample> QueryGpuAdapterMemorySamples()
    {
        using var searcher = new ManagementObjectSearcher("SELECT Name, DedicatedUsage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUAdapterMemory");
        using var results = searcher.Get();
        var samples = new List<GpuAdapterMemorySample>();
        foreach (ManagementObject sample in results)
        {
            var name = sample["Name"] as string;
            if (string.IsNullOrWhiteSpace(name) || !name.Contains("_phys_", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            samples.Add(new GpuAdapterMemorySample(
                name.Trim(),
                ConvertToUInt64(sample["DedicatedUsage"])));
        }

        return samples;
    }

    private static ulong ConvertToUInt64(object? value)
    {
        if (value is null)
        {
            return 0UL;
        }

        return value switch
        {
            ulong ulongValue => ulongValue,
            uint uintValue => uintValue,
            long longValue when longValue > 0 => (ulong)longValue,
            int intValue when intValue > 0 => (ulong)intValue,
            _ => ulong.TryParse(value.ToString(), out var parsed) ? parsed : 0UL,
        };
    }

    [LoggerMessage(EventId = 1703, Level = LogLevel.Warning, Message = "Falha ao consultar fallback de memoria do Windows.")]
    private static partial void LogWindowsMemoryFallbackFailure(ILogger logger, Exception exception);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    private sealed record VideoControllerInfo(string Name, ulong TotalBytes);

    private sealed record GpuAdapterMemorySample(string Name, ulong DedicatedUsageBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
