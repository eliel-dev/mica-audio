using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace App.WinUI.Views;

// DOCS: docs/wiki/guides/build-export-firmware.md#guia---download-de-firmware-pre-compilado
// DOCS: docs/wiki/guides/setup-new-device.md#passos
// DOCS: docs/wiki/modules/app-winui.md
// DOCS: docs/handoffs/2026-04-20-remove-usb-flash-flow.md
public sealed partial class DevicesPage
{
    private async void OnDownloadFirmwareClicked(object sender, RoutedEventArgs e)
    {
        await SaveFirmwareAsync().ConfigureAwait(false);
    }

    private async Task SaveFirmwareAsync()
    {
        var service = FirmwareService;
        if (service is null)
        {
            ShowInlineStatusMessage(InfoBarSeverity.Error, "Firmware: servico indisponivel.");
            AddLocalLog("Servico de firmware indisponivel.");
            return;
        }

        var options = service.GetOptions();
        var option = options.Count > 0 ? options[0] : null;
        if (option is null)
        {
            ShowInlineStatusMessage(InfoBarSeverity.Error, "Firmware: opcao indisponivel.");
            AddLocalLog("Nenhuma opcao de firmware disponivel.");
            return;
        }

        var export = await service.PrepareOfficialFirmwareExportAsync(option.Id).ConfigureAwait(true);
        if (!export.Success || export.ResolvedArtifact is null)
        {
            ShowInlineStatusMessage(InfoBarSeverity.Error, "Firmware: release oficial indisponivel.");
            AddLocalLog(export.FailureReason);
            return;
        }

        StorageFile? targetFile;
        try
        {
            targetFile = await PickFirmwareDestinationFileAsync(export.SuggestedFileName).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            ShowInlineStatusMessage(InfoBarSeverity.Error, "Firmware: erro ao abrir seletor.");
            AddLocalLog($"Falha ao abrir seletor de arquivo: {ex.Message}");
            return;
        }

        if (targetFile is null)
        {
            ShowInlineStatusMessage(InfoBarSeverity.Warning, "Firmware: download cancelado.");
            AddLocalLog("Salvamento de firmware cancelado pelo usuario.");
            return;
        }

        try
        {
            await service.CopyArtifactToAsync(export.ResolvedArtifact, targetFile.Path).ConfigureAwait(true);
            ShowInlineStatusMessage(InfoBarSeverity.Success, $"Firmware: salvo em {targetFile.Name}.");
            AddLocalLog($"Firmware salvo em: {targetFile.Path}");
        }
        catch (Exception ex)
        {
            ShowInlineStatusMessage(InfoBarSeverity.Error, "Firmware: erro ao salvar arquivo.");
            AddLocalLog($"Falha ao salvar firmware: {ex.Message}");
        }
    }

    private static async Task<StorageFile?> PickFirmwareDestinationFileAsync(string suggestedFileName)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = suggestedFileName,
            SuggestedStartLocation = PickerLocationId.Downloads,
        };

        picker.FileTypeChoices.Add("Firmware BIN", [".bin"]);

        if (App.MainWindow is not null)
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return await picker.PickSaveFileAsync();
    }
}
