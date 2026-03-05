using System.Diagnostics;
using System.Text.RegularExpressions;

namespace App.Headless.Services;

internal interface IEspToolFlashService
{
    Task<HeadlessOnboardingResult> FlashAsync(
        string portName,
        string firmwarePath,
        IProgress<HeadlessOnboardingProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

internal sealed class EspToolFlashService : IEspToolFlashService
{
    private static readonly Regex PercentRegex = new(@"(\d{1,3})\s*%", RegexOptions.Compiled);
    private readonly ILogger<EspToolFlashService> logger;

    public EspToolFlashService(ILogger<EspToolFlashService> logger)
    {
        this.logger = logger;
    }

    public async Task<HeadlessOnboardingResult> FlashAsync(
        string portName,
        string firmwarePath,
        IProgress<HeadlessOnboardingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return Fail("port_required", "Porta COM invalida para gravacao.");
        }

        if (!File.Exists(firmwarePath))
        {
            return Fail("firmware_missing", "Arquivo de firmware nao encontrado.");
        }

        var invocation = ResolveEsptoolInvocation();
        if (invocation is null)
        {
            return Fail("esptool_missing", "Esptool nao encontrado em tools/esptool/win-x64 e python -m esptool indisponivel.");
        }

        progress?.Report(new HeadlessOnboardingProgress
        {
            Stage = HeadlessOnboardingStage.Flashing,
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
        void HandleOutput(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            logger.LogInformation("[esptool] {Line}", line);
            var percent = ParseProgressPercent(line);
            if (!percent.HasValue || percent.Value <= latestPercent)
            {
                return;
            }

            latestPercent = percent.Value;
            progress?.Report(new HeadlessOnboardingProgress
            {
                Stage = HeadlessOnboardingStage.Flashing,
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
                return Fail("esptool_start_failed", "Falha ao iniciar esptool.");
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
                }
            });

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Fail("cancelled", "Gravacao de firmware cancelada.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao executar esptool.");
            return Fail("esptool_error", $"Falha ao executar esptool: {ex.Message}");
        }

        if (process.ExitCode != 0)
        {
            return Fail("flash_failed", $"Esptool finalizou com erro (exit code {process.ExitCode}).");
        }

        progress?.Report(new HeadlessOnboardingProgress
        {
            Stage = HeadlessOnboardingStage.Flashing,
            Message = "Firmware gravado com sucesso.",
            Percent = 100,
        });

        return new HeadlessOnboardingResult
        {
            Success = true,
            Message = "Firmware gravado com sucesso.",
        };
    }

    private static EsptoolInvocation? ResolveEsptoolInvocation()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "esptool", "win-x64", "esptool.exe"),
            Path.Combine(AppContext.BaseDirectory, "tools", "esptool", "win-x64", "esptool.cmd"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "esptool", "win-x64", "esptool.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "esptool", "win-x64", "esptool.cmd")),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return new EsptoolInvocation(candidate, Array.Empty<string>());
            }
        }

        return new EsptoolInvocation("python", ["-m", "esptool"]);
    }

    private static IReadOnlyList<string> BuildWriteFlashArguments(string portName, string firmwarePath)
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

    private static int? ParseProgressPercent(string line)
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

    private static HeadlessOnboardingResult Fail(string code, string message)
    {
        return new HeadlessOnboardingResult
        {
            Success = false,
            ErrorCode = code,
            Message = message,
        };
    }

    private sealed record EsptoolInvocation(string FileName, IReadOnlyList<string> Arguments);
}
