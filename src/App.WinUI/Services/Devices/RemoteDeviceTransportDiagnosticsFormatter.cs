using Device.Client.Remote;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-29-lan-panel-architecture-realignment.md
// DOCS: docs/handoffs/2026-04-29-server-mediated-visual-udp.md
internal static class RemoteDeviceTransportDiagnosticsFormatter
{
    public static string Format(RemoteDeviceFrameTransportDiagnostics? diagnostics)
    {
        if (diagnostics is null)
        {
            return "Diagnostico visual: Embedded ativo; transporte remoto via servidor indisponivel neste modo.";
        }

        var message =
            $"Diagnostico visual: servidor WS enfileirados: {diagnostics.ServerWebSocketFramesQueued}; " +
            $"enviados: {diagnostics.ServerWebSocketFramesSent}; " +
            $"reconnects: {diagnostics.ServerWebSocketReconnects}.";

        var webSocketError = NormalizeError(diagnostics.LastWebSocketError);
        if (webSocketError is null)
        {
            return message + " Ultimo erro: nenhum.";
        }

        return message + $" Ultimo erro: WS admin: {webSocketError}.";
    }

    private static string? NormalizeError(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
