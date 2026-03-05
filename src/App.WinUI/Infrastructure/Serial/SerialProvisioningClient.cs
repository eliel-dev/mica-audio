using System.IO.Ports;
using System.Text.Json;
using App.WinUI.Services.Devices.Onboarding;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Infrastructure.Serial;

internal interface ISerialProvisioningClient
{
    Task<SerialProvisioningResult> ProvisionAsync(
        string portName,
        SerialProvisioningRequest request,
        IProgress<DeviceOnboardingProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

internal sealed record SerialProvisioningRequest
{
    public required string RequestId { get; init; }

    public required string Ssid { get; init; }

    public required string Password { get; init; }

    public required string ServerBaseUrl { get; init; }

    public required string PairCode { get; init; }
}

internal sealed record SerialProvisioningResult
{
    public bool Success { get; init; }

    public string? DeviceId { get; init; }

    public string? ErrorCode { get; init; }

    public string Message { get; init; } = string.Empty;
}

// DOCS: docs/wiki/guides/setup-new-device.md#passos
internal sealed class SerialProvisioningClient : ISerialProvisioningClient
{
    private static readonly TimeSpan HelloTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ResultTimeout = TimeSpan.FromSeconds(90);
    private readonly ILogger<SerialProvisioningClient> logger;

    public SerialProvisioningClient(ILogger<SerialProvisioningClient> logger)
    {
        this.logger = logger;
    }

    public async Task<SerialProvisioningResult> ProvisionAsync(
        string portName,
        SerialProvisioningRequest request,
        IProgress<DeviceOnboardingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return new SerialProvisioningResult
            {
                Success = false,
                ErrorCode = "port_invalid",
                Message = "Porta serial invalida.",
            };
        }

        using var serial = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
        {
            DtrEnable = true,
            RtsEnable = false,
            Handshake = Handshake.None,
            NewLine = "\n",
            ReadTimeout = 1000,
            WriteTimeout = 1000,
        };

        try
        {
            serial.Open();
        }
        catch (Exception ex)
        {
            return new SerialProvisioningResult
            {
                Success = false,
                ErrorCode = "port_open_failed",
                Message = $"Falha ao abrir {portName}: {ex.Message}",
            };
        }

        progress?.Report(new DeviceOnboardingProgress
        {
            Stage = DeviceOnboardingStage.Provisioning,
            Message = "Aguardando handshake serial do firmware...",
            Percent = 10,
        });

        var hello = await WaitForHelloAsync(serial, cancellationToken).ConfigureAwait(false);
        if (!hello.Success)
        {
            return hello;
        }

        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["type"] = "provision",
            ["requestId"] = request.RequestId,
            ["ssid"] = request.Ssid,
            ["password"] = request.Password,
            ["serverBaseUrl"] = request.ServerBaseUrl,
            ["pairCode"] = request.PairCode,
        });

        try
        {
            serial.WriteLine(payload);
        }
        catch (Exception ex)
        {
            return new SerialProvisioningResult
            {
                Success = false,
                ErrorCode = "serial_write_failed",
                Message = $"Falha ao enviar provisionamento: {ex.Message}",
            };
        }

        progress?.Report(new DeviceOnboardingProgress
        {
            Stage = DeviceOnboardingStage.Provisioning,
            Message = "Provisionamento enviado para o dispositivo.",
            Percent = 20,
        });

        return await WaitForResultAsync(serial, request.RequestId, progress, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SerialProvisioningResult> WaitForHelloAsync(SerialPort serial, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + HelloTimeout;
        var buffer = string.Empty;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = serial.ReadExisting();
            if (!string.IsNullOrEmpty(chunk))
            {
                buffer += chunk;
                foreach (var line in DrainLines(ref buffer))
                {
                    if (!TryParseMessage(line, out var type, out var document))
                    {
                        continue;
                    }

                    using (document)
                    {
                        if (!string.Equals(type, "hello", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var protocol = document.RootElement.TryGetProperty("protocol", out var protocolElement)
                            ? protocolElement.GetString()
                            : null;

                        if (!string.Equals(protocol, "mica.serial.v1", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        return new SerialProvisioningResult
                        {
                            Success = true,
                            DeviceId = document.RootElement.TryGetProperty("deviceId", out var deviceElement)
                                ? deviceElement.GetString()
                                : null,
                            Message = "Handshake serial concluido.",
                        };
                    }
                }
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        return new SerialProvisioningResult
        {
            Success = false,
            ErrorCode = "serial_hello_timeout",
            Message = "Timeout aguardando handshake serial mica.serial.v1.",
        };
    }

    private async Task<SerialProvisioningResult> WaitForResultAsync(
        SerialPort serial,
        string requestId,
        IProgress<DeviceOnboardingProgress>? progress,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + ResultTimeout;
        var buffer = string.Empty;

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunk = serial.ReadExisting();
            if (!string.IsNullOrEmpty(chunk))
            {
                buffer += chunk;
                foreach (var line in DrainLines(ref buffer))
                {
                    if (!TryParseMessage(line, out var type, out var document))
                    {
                        logger.LogDebug("[serial] {Line}", line);
                        continue;
                    }

                    using (document)
                    {
                        if (string.Equals(type, "progress", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!IsMatchingRequest(document.RootElement, requestId))
                            {
                                continue;
                            }

                            var stageRaw = document.RootElement.TryGetProperty("stage", out var stageElement)
                                ? stageElement.GetString() ?? string.Empty
                                : string.Empty;
                            var progressMessage = document.RootElement.TryGetProperty("message", out var progressMsgElement)
                                ? progressMsgElement.GetString() ?? "Processando..."
                                : "Processando...";

                            var stage = ResolveStage(stageRaw);
                            var percent = ResolveProgressPercent(stageRaw);
                            progress?.Report(new DeviceOnboardingProgress
                            {
                                Stage = stage,
                                Message = progressMessage,
                                Percent = percent,
                            });
                            continue;
                        }

                        if (!string.Equals(type, "result", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!IsMatchingRequest(document.RootElement, requestId))
                        {
                            continue;
                        }

                        var ok = document.RootElement.TryGetProperty("ok", out var okElement) && okElement.GetBoolean();
                        var resultMessage = document.RootElement.TryGetProperty("message", out var resultMsgElement)
                            ? resultMsgElement.GetString() ?? string.Empty
                            : string.Empty;

                        return new SerialProvisioningResult
                        {
                            Success = ok,
                            DeviceId = document.RootElement.TryGetProperty("deviceId", out var deviceElement)
                                ? deviceElement.GetString()
                                : null,
                            ErrorCode = document.RootElement.TryGetProperty("errorCode", out var errorElement)
                                ? errorElement.GetString()
                                : null,
                            Message = ok ? "Provisionamento serial concluido." : (string.IsNullOrWhiteSpace(resultMessage) ? "Provisionamento serial falhou." : resultMessage),
                        };
                    }
                }
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }

        return new SerialProvisioningResult
        {
            Success = false,
            ErrorCode = "serial_result_timeout",
            Message = "Timeout aguardando resultado de provisionamento via serial.",
        };
    }

    private static bool IsMatchingRequest(JsonElement root, string requestId)
    {
        if (!root.TryGetProperty("requestId", out var requestIdElement))
        {
            return false;
        }

        return string.Equals(requestIdElement.GetString(), requestId, StringComparison.OrdinalIgnoreCase);
    }

    private static DeviceOnboardingStage ResolveStage(string stage)
    {
        if (stage.Contains("pair", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceOnboardingStage.Pairing;
        }

        if (stage.Contains("wifi", StringComparison.OrdinalIgnoreCase)
            || stage.Contains("provision", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceOnboardingStage.Provisioning;
        }

        if (stage.Contains("done", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceOnboardingStage.Done;
        }

        if (stage.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return DeviceOnboardingStage.Failed;
        }

        return DeviceOnboardingStage.Provisioning;
    }

    private static int ResolveProgressPercent(string stage)
    {
        if (stage.Contains("wifi_connecting", StringComparison.OrdinalIgnoreCase))
        {
            return 35;
        }

        if (stage.Contains("wifi_connected", StringComparison.OrdinalIgnoreCase))
        {
            return 55;
        }

        if (stage.Contains("pair", StringComparison.OrdinalIgnoreCase))
        {
            return 70;
        }

        if (stage.Contains("done", StringComparison.OrdinalIgnoreCase))
        {
            return 90;
        }

        if (stage.Contains("error", StringComparison.OrdinalIgnoreCase))
        {
            return 100;
        }

        return 30;
    }

    private static bool TryParseMessage(string line, out string type, out JsonDocument document)
    {
        type = string.Empty;
        document = null!;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(line);
            if (!document.RootElement.TryGetProperty("type", out var typeElement))
            {
                document.Dispose();
                return false;
            }

            type = typeElement.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(type);
        }
        catch
        {
            return false;
        }
    }

    private static IReadOnlyList<string> DrainLines(ref string buffer)
    {
        var lines = new List<string>();

        while (true)
        {
            var lineBreak = buffer.IndexOf('\n');
            if (lineBreak < 0)
            {
                break;
            }

            var line = buffer[..lineBreak].TrimEnd('\r');
            buffer = buffer[(lineBreak + 1)..];
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add(line);
            }
        }

        return lines;
    }
}
