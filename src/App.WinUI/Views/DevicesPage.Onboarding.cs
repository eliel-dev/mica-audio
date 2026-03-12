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
            "Selecione a porta COM para apagar toda a flash e gravar o firmware. O processo remove configuracoes anteriores do ESP32 e pode demorar mais. O Wi-Fi sera configurado no AP do ESP32 apos o flash.")
            .ConfigureAwait(true);
    }

    private async Task ShowUsbFirmwareRefreshWizardAsync(DeviceSnapshot snapshot, string latestVersion)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var currentVersion = string.IsNullOrWhiteSpace(snapshot.FirmwareVersion)
            ? "desconhecida"
            : snapshot.FirmwareVersion;
        var summary = $"Atualizacao por USB para {snapshot.DeviceId}. O processo apaga toda a flash, grava {latestVersion} e exige novo provisionamento Wi-Fi/pareamento. Firmware atual: {currentVersion}.";
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
        WizardStatusText.Text = string.Empty;
        WizardSummaryNoteText.Text = summaryNote;
        WizardPortComboBox.ItemsSource = Array.Empty<SerialPortDescriptor>();
        WizardPortComboBox.SelectedIndex = -1;
        ResetWizardFlashProgressUi();
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
        WizardFinishButton.Click += async (_, _) => await RunWizardOnboardingAsync().ConfigureAwait(true);
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
        ResetWizardFlashProgressUi();
    }

    private void ApplyWizardBusyState(bool busy)
    {
        wizardOperationInFlight = busy;
        WizardCloseButton.IsEnabled = !busy;
        WizardFinishButton.IsEnabled = !busy;
        WizardPortComboBox.IsEnabled = !busy;
        WizardRefreshPortsButton.IsEnabled = !busy;
        WizardFinishButton.Content = busy
            ? BuildButtonWithGlyph("\uE895", "Processando...")
            : BuildButtonWithGlyph("\uE73E", "Concluir");
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
            var pairCode = string.IsNullOrWhiteSpace(result.PairCode) ? "-" : result.PairCode;
            PairingCodeText.Severity = InfoBarSeverity.Informational;
            PairingCodeText.Message = $"Pareamento pendente: use o codigo {pairCode} no AP do dispositivo.";
            WizardStatusText.Text = result.Message;
            AddLocalLog($"Flash concluido na porta {selectedPort.PortName}; aguardando provisionamento via AP.");
            ApplyWizardBusyState(false);
            await ShowPairCodeDialogAsync(pairCode).ConfigureAwait(true);
            HideNewDeviceWizard();
            return;
        }

        PairingCodeText.Severity = InfoBarSeverity.Error;
        PairingCodeText.Message = $"Onboarding falhou: {result.Message}";
        WizardStatusText.Text = $"Falha ({result.ErrorCode ?? "erro"}): {result.Message}";
        ApplyWizardBusyState(false);
    }

    private async Task ShowPairCodeDialogAsync(string pairCode)
    {
        var dialog = new ContentDialog
        {
            Title = "Flash concluido",
            PrimaryButtonText = "Entendi",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot,
            Content = "Codigo de pareamento: " + pairCode + Environment.NewLine
                + "1) Conecte no AP MicaAudio-Setup-xxxx." + Environment.NewLine
                + "2) Configure Wi-Fi/servidor no portal." + Environment.NewLine
                + "3) Informe este codigo no campo de pareamento.",
        };

        await dialog.ShowAsync();
    }

    private static string DescribeOnboardingStage(DeviceOnboardingStage stage)
    {
        return stage switch
        {
            DeviceOnboardingStage.Flashing => "Flashing",
            DeviceOnboardingStage.Provisioning => "Provisionando",
            DeviceOnboardingStage.Pairing => "Pareando",
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
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: servico indisponivel.";
            AddLocalLog("Servico de firmware indisponivel.");
            return;
        }

        var options = service.GetOptions();
        var option = options.Count > 0 ? options[0] : null;
        if (option is null)
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: opcao indisponivel.";
            AddLocalLog("Nenhuma opcao de firmware disponivel.");
            return;
        }

        if (!service.TryResolveSource(option.Id, out _, out var resolveError))
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: arquivo ausente.";
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
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: erro ao abrir seletor.";
            AddLocalLog($"Falha ao abrir seletor de arquivo: {ex.Message}");
            return;
        }

        if (targetFile is null)
        {
            PairingCodeText.Severity = InfoBarSeverity.Warning;
            PairingCodeText.Message = "Firmware: download cancelado.";
            AddLocalLog("Salvamento de firmware cancelado pelo usuario.");
            return;
        }

        try
        {
            await service.CopyToAsync(option.Id, targetFile.Path).ConfigureAwait(true);
            PairingCodeText.Severity = InfoBarSeverity.Success;
            PairingCodeText.Message = $"Firmware: salvo em {targetFile.Name}.";
            AddLocalLog($"Firmware salvo em: {targetFile.Path}");
        }
        catch (Exception ex)
        {
            PairingCodeText.Severity = InfoBarSeverity.Error;
            PairingCodeText.Message = "Firmware: erro ao salvar arquivo.";
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
