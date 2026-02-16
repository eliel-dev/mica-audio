using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/server-build-and-artifacts.md#modulo-server-build-and-artifacts
internal sealed class FirmwareBuildService : IFirmwareBuildService
{
    private const string MergedFirmwareFileName = "matrixportal-s3_merged.bin";

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git",
        ".pio",
        ".vs",
        "bin",
        "obj",
        "__pycache__",
    };

    private sealed record CommandRunner(string FileName, string PrefixArguments = "")
    {
        public string BuildArguments(string commandArguments)
        {
            if (string.IsNullOrWhiteSpace(PrefixArguments))
            {
                return commandArguments;
            }

            if (string.IsNullOrWhiteSpace(commandArguments))
            {
                return PrefixArguments;
            }

            return $"{PrefixArguments} {commandArguments}";
        }
    }

    private CommandRunner? platformIoRunner;
    private CommandRunner? pythonRunner;
    private CommandRunner? esptoolRunner;

    public async Task EnsureToolchainAsync(CancellationToken cancellationToken = default)
    {
        platformIoRunner ??= await ResolvePlatformIoRunnerAsync(cancellationToken).ConfigureAwait(false);
        if (platformIoRunner is null)
        {
            throw new InvalidOperationException(
                "PlatformIO (pio) nao encontrado. Instale PlatformIO Core para habilitar build de firmware.");
        }

        pythonRunner ??= await ResolvePythonRunnerAsync(cancellationToken).ConfigureAwait(false);
        if (pythonRunner is null)
        {
            throw new InvalidOperationException("Python Launcher (py) ou python nao encontrado. Instale Python para build de firmware.");
        }

        esptoolRunner ??= await ResolveEsptoolRunnerAsync(pythonRunner, cancellationToken).ConfigureAwait(false);
        if (esptoolRunner is null)
        {
            throw new InvalidOperationException(
                "esptool nao encontrado no Python atual. Execute: `py -m pip install --upgrade esptool`.");
        }
    }

    // DOCS: docs/wiki/guides/build-export-firmware.md#passos
    public async Task<FirmwareArtifactSet> BuildAsync(
        FirmwareBuildRequest request,
        IProgress<BuildProgressUpdate>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkingDirectory) || !Directory.Exists(request.WorkingDirectory))
        {
            throw new DirectoryNotFoundException("Diretorio de firmware nao encontrado.");
        }

        Report(progress, 5, "toolchain", "Validando toolchain...");
        await EnsureToolchainAsync(cancellationToken).ConfigureAwait(false);

        var logs = new List<string>();
        var profile = request.Profile;

        Report(progress, 10, "workspace", "Preparando workspace...");
        var buildWorkspace = PrepareBuildWorkspace(request.WorkingDirectory, logs);

        var compilePercent = 20;
        Report(progress, compilePercent, "compile", $"Compilando firmware ({profile})...");
        var buildCmd = await RunWithRunnerAsync(
            platformIoRunner!,
            $"run -d \"{buildWorkspace}\" -e {profile}",
            buildWorkspace,
            cancellationToken,
            onOutputLine: line =>
            {
                logs.Add(line);
                if (TryMapCompileProgress(line, ref compilePercent, out var mappedPercent, out var stage))
                {
                    Report(progress, mappedPercent, stage, line);
                }
            }).ConfigureAwait(false);

        if (buildCmd.ExitCode != 0)
        {
            Report(progress, compilePercent, "compile", "Falha no build.", isTerminal: true);
            throw new InvalidOperationException($"Falha no build do firmware ({profile}).\n{buildCmd.Output}");
        }

        Report(progress, 85, "artifacts", "Coletando artefatos do build...");
        var buildDir = Path.Combine(buildWorkspace, ".pio", "build", profile);
        var firmwareBin = GetIfExists(Path.Combine(buildDir, "firmware.bin"));
        var bootloaderBin = ResolveBootloaderBinary(buildDir, logs);
        var partitionsBin = GetIfExists(Path.Combine(buildDir, "partitions.bin"));

        if (firmwareBin is null || bootloaderBin is null || partitionsBin is null)
        {
            Report(progress, 85, "artifacts", "Artefatos obrigatorios nao encontrados.", isTerminal: true);
            throw new InvalidOperationException(
                "Build concluido, mas nao encontrou os binarios esperados (bootloader.bin, partitions.bin e firmware.bin).");
        }

        var mergedPath = Path.Combine(buildDir, MergedFirmwareFileName);
        Report(progress, 90, "merge", $"Gerando {MergedFirmwareFileName}...");

        var merge = await RunWithRunnerAsync(
            esptoolRunner!,
            $"-m esptool --chip esp32s3 merge-bin -o \"{mergedPath}\" --flash-mode dio --flash-freq 80m --flash-size 8MB 0x0 \"{bootloaderBin}\" 0x8000 \"{partitionsBin}\" 0x10000 \"{firmwareBin}\"",
            buildWorkspace,
            cancellationToken,
            onOutputLine: line =>
            {
                logs.Add(line);
                Report(progress, 92, "merge", line);
            }).ConfigureAwait(false);

        if (merge.ExitCode != 0 || !File.Exists(mergedPath))
        {
            Report(progress, 92, "merge", $"Falha ao gerar {MergedFirmwareFileName}.", isTerminal: true);
            throw new InvalidOperationException($"Falha ao gerar {MergedFirmwareFileName}.\n{merge.Output}");
        }

        Report(progress, 95, "merge", $"{MergedFirmwareFileName} pronto.");

        return new FirmwareArtifactSet
        {
            Profile = profile,
            BuildDirectory = buildDir,
            MergedBinPath = mergedPath,
            FirmwareBinPath = firmwareBin,
            BootloaderBinPath = bootloaderBin,
            PartitionsBinPath = partitionsBin,
            Logs = logs,
        };
    }

    public async Task<string> ExportAsync(FirmwareArtifactSet artifactSet, string targetRootDirectory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(artifactSet.MergedBinPath) || !File.Exists(artifactSet.MergedBinPath))
        {
            throw new InvalidOperationException($"Arquivo consolidado ausente: {MergedFirmwareFileName}");
        }

        var ts = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var exportDir = Path.Combine(targetRootDirectory, "MicaAudio", "firmware", ts);
        Directory.CreateDirectory(exportDir);

        CopyIfExists(artifactSet.MergedBinPath, exportDir, MergedFirmwareFileName);

        var report = new
        {
            profile = artifactSet.Profile,
            exportedAt = DateTimeOffset.Now,
            mergedBin = MergedFirmwareFileName,
            logs = artifactSet.Logs,
        };

        var reportPath = Path.Combine(exportDir, "build-report.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), cancellationToken).ConfigureAwait(false);

        var flashGuide = Path.Combine(exportDir, "flash-guide-ptbr.md");
        var guideText = "# Flash manual Matrix Portal S3\n\n"
            + "Este pacote exporta um unico binario consolidado:\n"
            + $"- `{MergedFirmwareFileName}` (bootloader + particoes + firmware)\n\n"
            + "1. Conecte o device por USB.\n"
            + "2. Ajuste a porta COM em `flash-manual.bat`.\n"
            + $"3. Execute o script para gravar `{MergedFirmwareFileName}`.\n";

        await File.WriteAllTextAsync(flashGuide, guideText, cancellationToken).ConfigureAwait(false);

        var bat = Path.Combine(exportDir, "flash-manual.bat");
        var batScript = BuildFlashBatchScript(MergedFirmwareFileName);
        await File.WriteAllTextAsync(bat, batScript, cancellationToken).ConfigureAwait(false);

        return exportDir;
    }

    private static bool TryMapCompileProgress(string line, ref int compilePercent, out int mappedPercent, out string stage)
    {
        mappedPercent = compilePercent;
        stage = "compile";

        var normalized = line.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.Contains("Compiling", StringComparison.OrdinalIgnoreCase))
        {
            compilePercent = Math.Min(70, compilePercent + 1);
            mappedPercent = compilePercent;
            return true;
        }

        if (normalized.Contains("Linking", StringComparison.OrdinalIgnoreCase))
        {
            mappedPercent = Math.Max(compilePercent, 75);
            stage = "link";
            return true;
        }

        if (normalized.Contains("Building", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Retrieving maximum program size", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Checking size", StringComparison.OrdinalIgnoreCase))
        {
            mappedPercent = Math.Max(compilePercent, 80);
            stage = "finalize";
            return true;
        }

        if (normalized.Contains("Tool Manager:", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Library Manager:", StringComparison.OrdinalIgnoreCase))
        {
            mappedPercent = Math.Max(compilePercent, 25);
            stage = "deps";
            return true;
        }

        if (normalized.StartsWith("Processing", StringComparison.OrdinalIgnoreCase))
        {
            mappedPercent = Math.Max(compilePercent, 22);
            stage = "compile";
            return true;
        }

        return false;
    }

    private static string? ResolveBootloaderBinary(string buildDir, List<string> logs)
    {
        var fromBuildDir = GetIfExists(Path.Combine(buildDir, "bootloader.bin"));
        if (!string.IsNullOrWhiteSpace(fromBuildDir))
        {
            return fromBuildDir;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var packageRoot = Path.Combine(userProfile, ".platformio", "packages", "framework-arduinoespressif32", "variants", "adafruit_matrixportal_esp32s3");

        var candidates = new[]
        {
            Path.Combine(packageRoot, "bootloader-tinyuf2.bin"),
            Path.Combine(packageRoot, "bootloader.bin"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                logs.Add($"Bootloader externo detectado: {candidate}");
                return candidate;
            }
        }

        return null;
    }

    private static string PrepareBuildWorkspace(string sourceDirectory, List<string> logs)
    {
        var workspaceDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MicaAudio",
            "fw-work",
            "matrixportal-s3");

        if (Directory.Exists(workspaceDirectory))
        {
            Directory.Delete(workspaceDirectory, true);
        }

        Directory.CreateDirectory(workspaceDirectory);
        MirrorDirectory(sourceDirectory, workspaceDirectory);
        logs.Add($"Workspace de build: {workspaceDirectory}");

        return workspaceDirectory;
    }

    private static void MirrorDirectory(string sourceDirectory, string destinationDirectory)
    {
        foreach (var dir in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, dir);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => IgnoredDirectories.Contains(part)))
            {
                continue;
            }

            Directory.CreateDirectory(Path.Combine(destinationDirectory, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            if (relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => IgnoredDirectories.Contains(part)))
            {
                continue;
            }

            var target = Path.Combine(destinationDirectory, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string BuildFlashBatchScript(string mergedName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("set PORT=COM3");
        sb.AppendLine("where python >nul 2>nul");
        sb.AppendLine("if %ERRORLEVEL%==0 (set TOOL=python) else (set TOOL=py)");
        sb.AppendLine();
        sb.AppendLine($"%TOOL% -m esptool --chip esp32s3 --port %PORT% --baud 921600 write_flash 0x0 {mergedName}");

        return sb.ToString();
    }

    private static async Task<CommandRunner?> ResolvePlatformIoRunnerAsync(CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            new CommandRunner("pio"),
            new CommandRunner("py", "-m platformio"),
            new CommandRunner("python", "-m platformio"),
        };

        foreach (var candidate in candidates)
        {
            var check = await RunWithRunnerAsync(candidate, "--version", Directory.GetCurrentDirectory(), cancellationToken).ConfigureAwait(false);
            if (check.ExitCode == 0)
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task<CommandRunner?> ResolvePythonRunnerAsync(CancellationToken cancellationToken)
    {
        var candidates = new[]
        {
            new CommandRunner("python"),
            new CommandRunner("py"),
        };

        foreach (var candidate in candidates)
        {
            var check = await RunWithRunnerAsync(candidate, "--version", Directory.GetCurrentDirectory(), cancellationToken).ConfigureAwait(false);
            if (check.ExitCode == 0)
            {
                return candidate;
            }
        }

        return null;
    }

    private static async Task<CommandRunner?> ResolveEsptoolRunnerAsync(CommandRunner preferredPythonRunner, CancellationToken cancellationToken)
    {
        var candidates = new List<CommandRunner> { preferredPythonRunner };

        if (!string.Equals(preferredPythonRunner.FileName, "python", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(new CommandRunner("python"));
        }

        if (!string.Equals(preferredPythonRunner.FileName, "py", StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(new CommandRunner("py"));
        }

        foreach (var candidate in candidates)
        {
            var check = await RunWithRunnerAsync(candidate, "-m esptool version", Directory.GetCurrentDirectory(), cancellationToken).ConfigureAwait(false);
            if (check.ExitCode == 0)
            {
                return candidate;
            }
        }

        return null;
    }

    private static void CopyIfExists(string? sourcePath, string targetDir, string? targetName = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return;
        }

        var destination = Path.Combine(targetDir, targetName ?? Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destination, overwrite: true);
    }

    private static string? GetIfExists(string path) => File.Exists(path) ? path : null;

    private static Task<(int ExitCode, string Output)> RunWithRunnerAsync(
        CommandRunner runner,
        string commandArguments,
        string workingDirectory,
        CancellationToken cancellationToken,
        Action<string>? onOutputLine = null)
    {
        return TryRunAndCaptureAsync(
            runner.FileName,
            runner.BuildArguments(commandArguments),
            workingDirectory,
            cancellationToken,
            onOutputLine);
    }

    private static async Task<(int ExitCode, string Output)> TryRunAndCaptureAsync(
        string fileName,
        string arguments,
        string workingDirectory,
        CancellationToken cancellationToken,
        Action<string>? onOutputLine = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = psi };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                stdout.AppendLine(e.Data);
                onOutputLine?.Invoke(e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null)
                {
                    return;
                }

                stderr.AppendLine(e.Data);
                onOutputLine?.Invoke(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var output = stdout.AppendLine(stderr.ToString()).ToString();
            return (process.ExitCode, output);
        }
        catch (Exception ex)
        {
            return (-1, ex.ToString());
        }
    }

    private static void Report(
        IProgress<BuildProgressUpdate>? progress,
        int percent,
        string stage,
        string message,
        bool isTerminal = false)
    {
        progress?.Report(new BuildProgressUpdate
        {
            Percent = Math.Clamp(percent, 0, 100),
            Stage = stage,
            Message = message,
            IsTerminal = isTerminal,
        });
    }
}
