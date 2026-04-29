using Device.Client.Remote;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/modules/app-winui.md#fluxo-de-execucao
// DOCS: docs/wiki/modules/device-server-protocol.md#admin-api-remota
// DOCS: docs/handoffs/2026-04-29-lan-panel-architecture-realignment.md
internal static class RemoteDeviceTransportDiagnosticsFormatter
{
    public static string Format(RemoteDeviceFrameTransportDiagnostics? diagnostics)
    {
        if (diagnostics is null)
        {
            return "Diagnostico visual: Embedded ativo; UDP direto remoto indisponivel neste modo.";
        }

        var message =
            $"Diagnostico visual: UDP direto enviados: {diagnostics.DirectUdpFramesSent}; " +
            $"fallback WS: {diagnostics.WebSocketFallbackFramesQueued}; " +
            $"endpoint ausente: {diagnostics.MissingEndpointFrames}.";

        var endpointError = NormalizeError(diagnostics.LastEndpointRefreshError);
        var udpError = NormalizeError(diagnostics.LastUdpError);
        if (endpointError is null && udpError is null)
        {
            return message + " Ultimo erro: nenhum.";
        }

        var details = new List<string>(capacity: 2);
        if (endpointError is not null)
        {
            details.Add($"endpoint: {endpointError}");
        }

        if (udpError is not null)
        {
            details.Add($"UDP: {udpError}");
        }

        return message + " Ultimo erro: " + string.Join("; ", details) + ".";
    }

    private static string? NormalizeError(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
