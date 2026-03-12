using Device.Protocol.Models;

namespace App.WinUI.Services.Devices;

internal static class DeviceOperationsText
{
    public static string BuildLiveCommandStatus(DeviceCommandProgressMessage progress)
    {
        var stage = DescribeStage(progress.Stage);
        return $"Comandos: {Math.Clamp(progress.ProgressPercent, 0, 100)}% ({stage})";
    }

    public static string BuildFinalCommandStatus(CommandDispatchResult result)
    {
        if (result.Success)
        {
            return string.Equals(result.Stage, "validated", StringComparison.OrdinalIgnoreCase)
                ? "Comandos: firmware validado"
                : "Comandos: concluido";
        }

        if (string.Equals(result.Stage, "rolled-back", StringComparison.OrdinalIgnoreCase))
        {
            return "Comandos: rollback do firmware";
        }

        return result.ErrorCode switch
        {
            "timeout" => "Comandos: sem resposta do dispositivo",
            "device_offline" => "Comandos: dispositivo offline",
            "send_error" => "Comandos: erro de rede",
            _ => "Comandos: erro",
        };
    }

    public static string BuildResultLogMessage(CommandDispatchResult result)
    {
        if (result.Success)
        {
            return $"Comando concluido com sucesso ({result.Stage ?? "ok"}) para {result.DeviceId}.";
        }

        if (!string.IsNullOrWhiteSpace(result.Message))
        {
            return $"Comando falhou para {result.DeviceId}: {result.Message}";
        }

        return $"Comando falhou para {result.DeviceId}.";
    }

    public static string DescribeCommand(DeviceCommandType commandType)
        => commandType switch
        {
            DeviceCommandType.EnterProvisioning => "entrar em provisioning",
            DeviceCommandType.RevokeAndRestart => "revogar/reiniciar",
            DeviceCommandType.TestLed => "controlar LED auxiliar",
            DeviceCommandType.InstallApp => "instalar app",
            DeviceCommandType.ActivateApp => "ativar app",
            DeviceCommandType.SetAppConfig => "configurar app",
            DeviceCommandType.SetBrightness => "ajustar brilho do painel",
            DeviceCommandType.UpdateFirmware => "atualizar firmware",
            _ => "comando",
        };

    public static string DescribeStage(string? stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return "processando";
        }

        return stage.Trim().ToLowerInvariant() switch
        {
            "received" => "recebido",
            "metadata" => "validando pacote",
            "downloading" => "baixando firmware",
            "flashing" => "gravando firmware",
            "rebooting" => "reiniciando",
            "pending-verify" => "validando primeiro boot",
            "validated" => "firmware validado",
            "rolled-back" => "rollback automatico",
            _ => stage,
        };
    }
}
