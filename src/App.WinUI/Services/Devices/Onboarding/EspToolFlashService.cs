using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Services.Devices.Onboarding;

// DOCS: docs/wiki/guides/setup-new-device.md#passos
internal sealed class EspToolFlashService : IEspToolFlashService
{
    private const int MaxCapturedOutputLines = 12;
    private static readonly Regex PercentRegex = new(@"(\d{1,3})\s*%", RegexOptions.Compiled);
    private readonly ILogger<EspToolFlashService> logger;

    public EspToolFlashService(ILogger<EspToolFlashService> logger)
    {
        this.logger = logger;
    }

    public async Task<EspToolFlashResult> FlashAsync(
        string portName,
        string firmwarePath,
        IProgress<DeviceOnboardingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return new EspToolFlashResult
            {
                Success = false,
                ExitCode = -1,
                Message = "Porta COM invalida para gravacao.",
            };
        }

        if (!File.Exists(firmwarePath))
        {
            return new EspToolFlashResult
            {
                Success = false,
                ExitCode = -1,
                Message = "Arquivo de firmware nao encontrado.",
            };
        }

        var invocation = ResolveEsptoolInvocation();
        if (invocation is null)
        {
            return new EspToolFlashResult
            {
                Success = false,
                ExitCode = -1,
                Message = "Esptool nao encontrado em tools/esptool/win-x64 e python -m esptool indisponivel.",
            };
        }

        progress?.Report(new DeviceOnboardingProgress
        {
            Stage = DeviceOnboardingStage.Flashing,
            Message = $"Iniciando gravacao do firmware em {portName}...",
            Percent = 0,
        });

        var startInfo = new ProcessStartInfo
        {
            FileName = invocation.FileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var arg in invocation.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        foreach (var arg in BuildWriteFlashArguments(portName, firmwarePath))
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        var latestPercent = 0;
        var capturedOutput = new Queue<string>();
        var capturedOutputGate = new object();
        void HandleOutput(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            logger.LogInformation("[esptool] {Line}", line);

            lock (capturedOutputGate)
            {
                capturedOutput.Enqueue(line.Trim());
                while (capturedOutput.Count > MaxCapturedOutputLines)
                {
                    capturedOutput.Dequeue();
                }
            }

            var percent = ParseProgressPercent(line);
            if (!percent.HasValue)
            {
                return;
            }

            if (percent.Value <= latestPercent)
            {
                return;
            }

            latestPercent = percent.Value;
            progress?.Report(new DeviceOnboardingProgress
            {
                Stage = DeviceOnboardingStage.Flashing,
                Message = $"Gravando firmware ({percent.Value}%)",
                Percent = percent.Value,
            });
        }

        process.OutputDataReceived += (_, args) => HandleOutput(args.Data ?? string.Empty);
        process.ErrorDataReceived += (_, args) => HandleOutput(args.Data ?? string.Empty);

        try
        {
            if (!process.Start())
            {
                return new EspToolFlashResult
                {
                    Success = false,
                    ExitCode = -1,
                    Message = "Falha ao iniciar esptool.",
                };
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var _ = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                    // No-op: process may already have finished.
                }
            });

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new EspToolFlashResult
            {
                Success = false,
                ExitCode = -1,
                Message = "Gravacao de firmware cancelada.",
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao executar esptool.");
            return new EspToolFlashResult
            {
                Success = false,
                ExitCode = -1,
                Message = $"Falha ao executar esptool: {ex.Message}",
            };
        }

        if (process.ExitCode != 0)
        {
            string[] outputSnapshot;
            lock (capturedOutputGate)
            {
                outputSnapshot = capturedOutput.ToArray();
            }

            return new EspToolFlashResult
            {
                Success = false,
                ExitCode = process.ExitCode,
                Message = BuildFailureMessage(process.ExitCode, outputSnapshot),
            };
        }

        progress?.Report(new DeviceOnboardingProgress
        {
            Stage = DeviceOnboardingStage.Flashing,
            Message = "Firmware gravado com sucesso.",
            Percent = 100,
        });

        return new EspToolFlashResult
        {
            Success = true,
            ExitCode = 0,
            Message = "Firmware gravado com sucesso.",
        };
    }

    private static EsptoolInvocation? ResolveEsptoolInvocation()
    {
        var bundleExe = Path.Combine(AppContext.BaseDirectory, "tools", "esptool", "win-x64", "esptool.exe");
        if (File.Exists(bundleExe))
        {
            return new EsptoolInvocation(bundleExe, Array.Empty<string>());
        }

        var bundleCmd = Path.Combine(AppContext.BaseDirectory, "tools", "esptool", "win-x64", "esptool.cmd");
        if (File.Exists(bundleCmd))
        {
            return new EsptoolInvocation(bundleCmd, Array.Empty<string>());
        }

        return new EsptoolInvocation("python", ["-m", "esptool"]);
    }

    internal static IReadOnlyList<string> BuildWriteFlashArguments(string portName, string firmwarePath)
    {
        return
        [
            "--chip", "esp32s3",
            "--port", portName,
            "--baud", "115200",
            "--before", "default_reset",
            "--after", "hard_reset",
            "write_flash",
            "--no-compress",
            "0x0",
            firmwarePath,
        ];
    }

    internal static int? ParseProgressPercent(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var match = PercentRegex.Match(line);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var rawPercent))
        {
            return null;
        }

        return Math.Clamp(rawPercent, 0, 100);
    }

    internal static string BuildFailureMessage(int exitCode, IReadOnlyList<string> outputLines)
    {
        var normalizedLines = outputLines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Trim())
            .ToArray();

        if (normalizedLines.Any(line => line.Contains("No module named esptool", StringComparison.OrdinalIgnoreCase)))
        {
            return "Esptool indisponivel no ambiente. Python foi encontrado, mas o modulo 'esptool' nao esta instalado.";
        }

        if (exitCode == 9009 || normalizedLines.Any(IsPythonNotFoundMessage))
        {
            return "Esptool indisponivel no ambiente. Nem esptool.exe nem Python com esptool estao disponiveis.";
        }

        var detail = normalizedLines.LastOrDefault();
        if (!string.IsNullOrWhiteSpace(detail))
        {
            return $"Esptool finalizou com erro (exit code {exitCode}): {detail}";
        }

        return $"Esptool finalizou com erro (exit code {exitCode}).";
    }

    private static bool IsPythonNotFoundMessage(string line)
    {
        return line.Contains("python", StringComparison.OrdinalIgnoreCase)
            && (line.Contains("is not recognized", StringComparison.OrdinalIgnoreCase)
                || line.Contains("nao e reconhecido", StringComparison.OrdinalIgnoreCase));
    }

    private sealed record EsptoolInvocation(string FileName, IReadOnlyList<string> Arguments);
}
