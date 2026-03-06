using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

// DOCS: docs/wiki/guides/visual-studio-web-headless-dev.md#referencias-de-codigo
// DOCS: docs/handoffs/2026-03-06-vs-green-button-web-dev.md#decisoes-tomadas

var repoRoot = RepositoryRootLocator.FindFrom(AppContext.BaseDirectory);
var runnerPath = Path.Combine(repoRoot, "scripts", "headless-web-run.ps1");
var webPackageJsonPath = Path.Combine(repoRoot, "src", "Web.Headless", "package.json");

EnsureFileExists(runnerPath, "scripts/headless-web-run.ps1");
EnsureFileExists(webPackageJsonPath, "src/Web.Headless/package.json");

Console.WriteLine($"[App.DevLauncher] Repo root: {repoRoot}");
Console.WriteLine("[App.DevLauncher] Starting Web Headless Dev...");

using var jobObject = WindowsJobObject.CreateKillOnClose();
using var runnerProcess = StartRunner(repoRoot, runnerPath);
jobObject.Assign(runnerProcess);

runnerProcess.WaitForExit();
return runnerProcess.ExitCode;

static void EnsureFileExists(string path, string label)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException($"Required file not found: {label}", path);
    }
}

static Process StartRunner(string repoRoot, string runnerPath)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        WorkingDirectory = repoRoot,
        UseShellExecute = false,
    };

    startInfo.ArgumentList.Add("-NoLogo");
    startInfo.ArgumentList.Add("-NoProfile");
    startInfo.ArgumentList.Add("-ExecutionPolicy");
    startInfo.ArgumentList.Add("Bypass");
    startInfo.ArgumentList.Add("-File");
    startInfo.ArgumentList.Add(runnerPath);
    startInfo.ArgumentList.Add("-Mode");
    startInfo.ArgumentList.Add("dev");

    return Process.Start(startInfo)
        ?? throw new InvalidOperationException("Failed to start headless web runner.");
}

static class RepositoryRootLocator
{
    public static string FindFrom(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "MicaAudio.sln");
            var agentsPath = Path.Combine(current.FullName, "AGENTS.md");
            if (File.Exists(solutionPath) && File.Exists(agentsPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from App.DevLauncher.");
    }
}

sealed class WindowsJobObject : IDisposable
{
    private readonly SafeJobHandle _handle;

    private WindowsJobObject(SafeJobHandle handle)
    {
        _handle = handle;
    }

    public static WindowsJobObject CreateKillOnClose()
    {
        SafeJobHandle? handle = null;
        var buffer = IntPtr.Zero;
        try
        {
            handle = NativeMethods.CreateJobObject(IntPtr.Zero, null);
            if (handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create the Windows job object.");
            }

            var info = new NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new NativeMethods.JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = NativeMethods.JobObjectLimitKillOnJobClose,
                },
            };

            var size = Marshal.SizeOf<NativeMethods.JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
            buffer = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(info, buffer, false);
            if (!NativeMethods.SetInformationJobObject(handle, NativeMethods.JobObjectExtendedLimitInformation, buffer, (uint)size))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to configure the Windows job object.");
            }

            var jobObject = new WindowsJobObject(handle);
            handle = null;
            return jobObject;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }

            handle?.Dispose();
        }
    }

    public void Assign(Process process)
    {
        if (!NativeMethods.AssignProcessToJobObject(_handle, process.Handle))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                $"Failed to assign process {process.Id} to the Windows job object.");
        }
    }

    public void Dispose()
    {
        _handle.Dispose();
    }
}

sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafeJobHandle()
        : base(ownsHandle: true)
    {
    }

    protected override bool ReleaseHandle()
    {
        return NativeMethods.CloseHandle(handle);
    }
}

static class NativeMethods
{
    internal const int JobObjectExtendedLimitInformation = 9;
    internal const uint JobObjectLimitKillOnJobClose = 0x00002000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern SafeJobHandle CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetInformationJobObject(
        SafeJobHandle hJob,
        int jobObjectInfoClass,
        IntPtr lpJobObjectInfo,
        uint cbJobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AssignProcessToJobObject(SafeJobHandle job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential)]
    internal struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public IntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}