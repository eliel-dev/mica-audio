using Windows.ApplicationModel.DataTransfer;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md
// DOCS: docs/wiki/guides/setup-new-device.md#passos
public sealed partial class DevicesPage
{
    private string? lastPairingCode;
    private DateTimeOffset? lastPairingCodeExpiresAtUtc;

    private void ShowInlineStatusMessage(InfoBarSeverity severity, string message)
    {
        lastPairingCode = null;
        lastPairingCodeExpiresAtUtc = null;

        if (PairingCodeText is null)
        {
            return;
        }

        PairingCodeText.Title = string.Empty;
        PairingCodeText.ActionButton = null;
        PairingCodeText.Severity = severity;
        PairingCodeText.Message = message;
        PairingCodeText.IsOpen = true;
        PairingCodeText.Visibility = Visibility.Visible;
    }

    private void ShowPairingCodeNotice(string code, DateTimeOffset expiresAtUtc)
    {
        lastPairingCode = string.IsNullOrWhiteSpace(code) ? null : code.Trim();
        lastPairingCodeExpiresAtUtc = expiresAtUtc;

        if (PairingCodeText is null)
        {
            return;
        }

        PairingCodeText.Title = "Codigo de pareamento";
        PairingCodeText.ActionButton = string.IsNullOrWhiteSpace(lastPairingCode) ? null : PairingCopyCodeButton;
        PairingCodeText.Severity = InfoBarSeverity.Informational;
        PairingCodeText.Message = BuildPairingCodeBannerMessage(lastPairingCode ?? "-", expiresAtUtc);
        PairingCodeText.IsOpen = true;
        PairingCodeText.Visibility = Visibility.Visible;
    }

    private void OnCopyPairingCodeClicked(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(lastPairingCode))
        {
            ShowInlineStatusMessage(InfoBarSeverity.Warning, "Nenhum codigo de pareamento disponivel para copiar.");
            return;
        }

        var data = new DataPackage();
        data.SetText(lastPairingCode);
        Clipboard.SetContent(data);

        PairingCodeText.Title = "Codigo copiado";
        PairingCodeText.ActionButton = PairingCopyCodeButton;
        PairingCodeText.Severity = InfoBarSeverity.Success;
        PairingCodeText.Message = $"{BuildPairingCodeBannerMessage(lastPairingCode, lastPairingCodeExpiresAtUtc ?? DateTimeOffset.UtcNow)} Copiado para a area de transferencia.";
        PairingCodeText.IsOpen = true;
        PairingCodeText.Visibility = Visibility.Visible;
        AddLocalLog($"Codigo de pareamento copiado: {lastPairingCode}.");
    }

    private static string BuildPairingCodeBannerMessage(string code, DateTimeOffset expiresAtUtc)
    {
        return $"Pareamento: {code} (expira {expiresAtUtc:HH:mm:ss} UTC). Use este codigo no pareamento do dispositivo.";
    }
}
