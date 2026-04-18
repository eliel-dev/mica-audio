using App.WinUI.Infrastructure.Serial;
using App.WinUI.Services.Devices.Onboarding;
using App.WinUI.Services.Firmware;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace App.WinUI.Views;

// DOCS: docs/wiki/guides/setup-new-device.md#contrato-visual-do-wizard
// DOCS: docs/wiki/guides/setup-new-device.md#logs-seriais-no-wizard
// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-04---logs-seriais-sob-demanda-no-wizard-usb
// DOCS: docs/handoffs/2026-04-16-ap-first-wifi-mem-and-copy-logs.md
public sealed partial class DevicesPage
{
    private async void OnDownloadFirmwareClicked(object sender, RoutedEventArgs e)
    {
        await SaveFirmwareAsync().ConfigureAwait(false);
    }

    private async void OnNewDeviceClicked(object sender, RoutedEventArgs e)
    {
        await ShowNewDeviceWizardAsync().ConfigureAwait(false);
    }

    private async Task ShowNewDeviceWizardAsync()
    {
        await ShowWizardOverlayAsync(
            "Selecione a porta COM para apagar toda a flash e gravar o firmware. Depois do flash, o wizard assume a porta a 115200 para acompanhar o boot; se o AP MicaAudio-Setup-xxxx nao aparecer, use 'Ver mais' e 'Recapturar boot'.")
            .ConfigureAwait(true);
    }

    private async Task ShowUsbFirmwareRefreshWizardAsync(DeviceSnapshot snapshot, string latestVersion)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var currentVersion = string.IsNullOrWhiteSpace(snapshot.FirmwareVersion)
            ? "desconhecida"
            : snapshot.FirmwareVersion;
        var summary = $"Atualizacao por USB para {snapshot.DeviceId}. O processo apaga toda a flash, grava {latestVersion} e depois reabre a serial a 115200 para diagnostico de boot antes do reprovisionamento no AP MicaAudio-Setup-xxxx com um novo pair code. Firmware atual: {currentVersion}.";
        await ShowWizardOverlayAsync(summary).ConfigureAwait(true);
    }

    private async Task ShowWizardOverlayAsync(string summaryNote)
    {
        var serialCatalog = SerialPortCatalogService;
        var onboarding = OnboardingService;
        if (serialCatalog is null || onboarding is null)
        {
            AddLocalLog("Fluxo de onboarding indisponivel: servicos nao resolvidos.");
            return;
        }

        EnsureWizardBindings();

        wizardOperationInFlight = false;
        wizardCompletedSuccessfully = false;
        WizardStatusText.Text = string.Empty;
        if (WizardPairCodeText is not null)
        {
            WizardPairCodeText.Text = "-";
        }
        WizardSummaryNoteText.Text = summaryNote;
        WizardPortComboBox.ItemsSource = Array.Empty<SerialPortDescriptor>();
        WizardPortComboBox.SelectedIndex = -1;
        UpdateWizardServerBaseAddressUi();
        ResetWizardFlashProgressUi();
        await PrepareWizardSerialMonitorAsync().ConfigureAwait(true);
        ApplyWizardBusyState(false);
        ShowWizardOverlay();
        await RefreshWizardPortsAsync().ConfigureAwait(true);
    }

    private void EnsureWizardBindings()
    {
        if (wizardBindingsInitialized)
        {
            return;
        }

        wizardBindingsInitialized = true;
        WizardCloseButton.Click += (_, _) =>
        {
            if (!wizardOperationInFlight)
            {
                HideNewDeviceWizard();
            }
        };

        WizardOverlay.PointerPressed += (_, e) =>
        {
            if (!wizardOperationInFlight && ReferenceEquals(e.OriginalSource, WizardOverlay))
            {
                HideNewDeviceWizard();
            }
        };

        WizardRefreshPortsButton.Click += async (_, _) => await RefreshWizardPortsAsync().ConfigureAwait(true);
        WizardDetailsToggleButton.Click += (_, _) => SetWizardDiagnosticsExpanded(!wizardDiagnosticsExpanded);
        WizardRecaptureBootButton.Click += async (_, _) => await RecaptureWizardBootAsync().ConfigureAwait(true);
        WizardCopySerialLogsButton.Click += (_, _) => CopyWizardSerialLogsToClipboard();
        WizardClearSerialLogsButton.Click += (_, _) =>
        {
            var serialMonitor = SerialMonitorService;
            if (serialMonitor is null)
            {
                return;
            }

            serialMonitor.Clear();
            wizardSerialParsedLineCount = 0;
            wizardCurrentSerialLines = Array.Empty<SerialMonitorLine>();
            ApplyWizardSerialMonitorSnapshot(serialMonitor.GetStateSnapshot());
        };
        WizardFinishButton.Click += async (_, _) =>
        {
            if (wizardOperationInFlight)
            {
                return;
            }

            if (wizardCompletedSuccessfully)
            {
                HideNewDeviceWizard();
                return;
            }

            await RunWizardOnboardingAsync().ConfigureAwait(true);
        };
    }

    private void ShowWizardOverlay()
    {
        WizardOverlay.Visibility = Visibility.Visible;
        WizardOverlay.IsHitTestVisible = true;
    }

    private void HideNewDeviceWizard()
    {
        WizardOverlay.Visibility = Visibility.Collapsed;
        WizardOverlay.IsHitTestVisible = false;
        wizardOperationInFlight = false;
        wizardCompletedSuccessfully = false;
        ResetWizardFlashProgressUi();
        SetWizardDiagnosticsExpanded(false);
        ReleaseWizardSerialMonitorSubscription();
        _ = ShutdownWizardSerialMonitorAsync(clearLines: true);
    }

    private void ApplyWizardBusyState(bool busy)
    {
        wizardOperationInFlight = busy;
        WizardCloseButton.IsEnabled = !busy;
        WizardFinishButton.IsEnabled = !busy;
        WizardPortComboBox.IsEnabled = !busy;
        WizardRefreshPortsButton.IsEnabled = !busy;
        WizardDetailsToggleButton.IsEnabled = true;
        WizardRecaptureBootButton.IsEnabled = !busy && wizardPreferredPort is not null;
        WizardCopySerialLogsButton.IsEnabled = !busy && wizardCurrentSerialLines.Count > 0;
        WizardClearSerialLogsButton.IsEnabled = !busy && wizardCurrentSerialLines.Count > 0;
        WizardFinishButton.Content = busy
            ? BuildButtonWithGlyph("\uE895", "Processando...")
            : BuildButtonWithGlyph(wizardCompletedSuccessfully ? "\uE8BB" : "\uE73E", wizardCompletedSuccessfully ? "Fechar" : "Concluir");
    }

    private void UpdateWizardServerBaseAddressUi()
    {
        var serverBaseAddress = DeviceOps?.GetServerBaseAddress();
        WizardServerBaseAddressText.Text = string.IsNullOrWhiteSpace(serverBaseAddress)
            ? "Servidor local indisponivel."
            : serverBaseAddress;
    }

    private void ResetWizardFlashProgressUi()
    {
        WizardFlashProgressBar.Value = 0;
        WizardFlashPercentText.Text = "0%";
        WizardFlashProgressHost.Visibility = Visibility.Collapsed;
        WizardFlashProgressBar.IsIndeterminate = false;
    }

    private void SetWizardFlashProgressUi(int percent, bool visible)
    {
        var normalized = Math.Clamp(percent, 0, 100);
        WizardFlashProgressBar.IsIndeterminate = false;
        WizardFlashProgressBar.Value = normalized;
        WizardFlashPercentText.Text = $"{normalized}%";
        WizardFlashProgressHost.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task RefreshWizardPortsAsync()
    {
        var catalog = SerialPortCatalogService;
        if (catalog is null)
        {
            WizardStatusText.Text = "Catalogo serial indisponivel.";
            return;
        }

        var selectedPort = (WizardPortComboBox.SelectedItem as SerialPortDescriptor)?.PortName;
        var ports = await catalog.ListAsync(includeAllPorts: false).ConfigureAwait(true);
        WizardPortComboBox.ItemsSource = ports;
        if (!string.IsNullOrWhiteSpace(selectedPort))
        {
            WizardPortComboBox.SelectedItem = ports.FirstOrDefault(item =>
                string.Equals(item.PortName, selectedPort, StringComparison.OrdinalIgnoreCase));
        }

        if (WizardPortComboBox.SelectedIndex < 0 && ports.Count > 0)
        {
            WizardPortComboBox.SelectedIndex = 0;
        }

        WizardStatusText.Text = ports.Count == 0
            ? "Nenhuma porta COM detectada. Conecte o ESP32 e atualize a lista."
            : $"Portas detectadas: {ports.Count}.";
    }

    private async Task RunWizardOnboardingAsync()
    {
        if (wizardOperationInFlight)
        {
            return;
        }

        var selectedPort = WizardPortComboBox.SelectedItem as SerialPortDescriptor;
        if (selectedPort is null)
        {
            WizardStatusText.Text = "Selecione uma porta COM valida para continuar.";
            return;
        }

        var onboarding = OnboardingService;
        if (onboarding is null)
        {
            WizardStatusText.Text = "Servico de onboarding indisponivel.";
            return;
        }

        ApplyWizardBusyState(true);

        var progress = new Progress<DeviceOnboardingProgress>(update =>
        {
            WizardStatusText.Text = $"[{DescribeOnboardingStage(update.Stage)}] {update.Message}";
            if (update.Stage == DeviceOnboardingStage.Flashing)
            {
                SetWizardFlashProgressUi(update.Percent, visible: true);
            }
            else if (WizardFlashProgressHost.Visibility == Visibility.Visible && update.Percent >= 100)
            {
                SetWizardFlashProgressUi(100, visible: true);
            }
        });

        var result = await onboarding.RunAsync(
            new DeviceOnboardingRequest
            {
                PortName = selectedPort.PortName,
            },
            progress).ConfigureAwait(true);

        if (result.Success)
        {
            var pairCodeText = string.IsNullOrWhiteSpace(result.PairCode) ? "-" : result.PairCode;
            var serverBaseAddress = WizardServerBaseAddressText.Text;
            wizardCompletedSuccessfully = true;
            wizardPreferredPort = selectedPort;
            ShowInlineStatusMessage(InfoBarSeverity.Success, $"Flash concluido. Pair code: {pairCodeText}. Conecte ao Wi-Fi MicaAudio-Setup-xxxx e informe o servidor {serverBaseAddress} no portal do ESP32.");
            if (WizardPairCodeText is not null)
            {
                WizardPairCodeText.Text = pairCodeText;
            }
            WizardSummaryNoteText.Text = $"Flash concluido. A COM foi liberada para logs a 115200. Agora conecte o celular ao Wi-Fi MicaAudio-Setup-xxxx, abra o portal do ESP32 e use o pair code abaixo com o servidor exibido.";
            WizardStatusText.Text = $"Pair code: {pairCodeText}{Environment.NewLine}Servidor: {serverBaseAddress}{Environment.NewLine}{result.Message}";
            SetWizardFlashProgressUi(100, visible: true);
            AddLocalLog($"Flash concluido na porta {selectedPort.PortName}. Use o AP MicaAudio-Setup-xxxx para finalizar o onboarding com o pair code exibido.");
            ApplyWizardBusyState(false);
            await BeginWizardSerialMonitoringAsync(selectedPort).ConfigureAwait(true);
            return;
        }

        ShowInlineStatusMessage(InfoBarSeverity.Error, $"Onboarding falhou: {result.Message}");
        var pairCodeHint = string.IsNullOrWhiteSpace(result.PairCode)
            ? string.Empty
            : $" Codigo de pareamento para o AP: {result.PairCode}";
        WizardStatusText.Text = $"Falha ({result.ErrorCode ?? "erro"}): {result.Message}{pairCodeHint}";
        AddLocalLog($"Onboarding USB falhou na porta {selectedPort.PortName}: {result.ErrorCode ?? "erro"} - {result.Message}{pairCodeHint}");
        ApplyWizardBusyState(false);
        wizardPreferredPort = selectedPort;
        await BeginWizardSerialMonitoringAsync(selectedPort).ConfigureAwait(true);
        SetWizardDiagnosticsExpanded(true);
    }

    private static string DescribeOnboardingStage(DeviceOnboardingStage stage)
    {
        return stage switch
        {
            DeviceOnboardingStage.Flashing => "Flashing",
            DeviceOnboardingStage.Provisioning => "Provisionando",
            DeviceOnboardingStage.Pairing => "Pair code",
            DeviceOnboardingStage.Verifying => "Verificando",
            DeviceOnboardingStage.Done => "Concluido",
            DeviceOnboardingStage.Failed => "Falha",
            DeviceOnboardingStage.SelectPort => "Selecionando porta",
            DeviceOnboardingStage.InputWifi => "Wi-Fi",
            _ => "Onboarding",
        };
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

        if (!service.TryResolveSource(option.Id, out _, out var resolveError))
        {
            ShowInlineStatusMessage(InfoBarSeverity.Error, "Firmware: arquivo ausente.");
            AddLocalLog(resolveError);
            return;
        }

        StorageFile? targetFile;
        try
        {
            targetFile = await PickFirmwareDestinationFileAsync(option).ConfigureAwait(true);
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
            await service.CopyToAsync(option.Id, targetFile.Path).ConfigureAwait(true);
            ShowInlineStatusMessage(InfoBarSeverity.Success, $"Firmware: salvo em {targetFile.Name}.");
            AddLocalLog($"Firmware salvo em: {targetFile.Path}");
        }
        catch (Exception ex)
        {
            ShowInlineStatusMessage(InfoBarSeverity.Error, "Firmware: erro ao salvar arquivo.");
            AddLocalLog($"Falha ao salvar firmware: {ex.Message}");
        }
    }

    private static async Task<StorageFile?> PickFirmwareDestinationFileAsync(PrecompiledFirmwareOption option)
    {
        var picker = new FileSavePicker
        {
            SuggestedFileName = option.FileName,
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
