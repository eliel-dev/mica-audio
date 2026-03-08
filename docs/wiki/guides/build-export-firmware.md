# Guia - Download de firmware pre-compilado

## Objetivo

Baixar um BIN pre-compilado na aba `Dispositivos` e salvar no local escolhido para flash manual externo.

## Passos

1. Abrir a aba `Dispositivos`.
2. Clicar em `Novo dispositivo`.
3. Escolher `Placa`, `Painel` e `Firmware` no wizard.
4. Clicar em `Baixar firmware`.
5. Escolher o destino no `FileSavePicker` e confirmar.
6. Validar no log da tela que o download foi concluido.

## Referencias de codigo

- [DevicesPage.ShowNewDeviceSetupDialogAsync](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L163) - assinatura: `private async Task ShowNewDeviceSetupDialogAsync()`
- [DevicesPage.DownloadFirmwareFromSelectionAsync](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L349) - assinatura: `private async Task DownloadFirmwareFromSelectionAsync(...)`
- [PrecompiledFirmwareService.GetOptions](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L73) - assinatura: `IReadOnlyList<PrecompiledFirmwareOption> GetOptions(...)`
- [PrecompiledFirmwareService.CopyToAsync](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L144) - assinatura: `Task CopyToAsync(string optionId, string destinationPath, CancellationToken)`

## Checklist rapido

- [ ] O wizard abre pela aba `Dispositivos`.
- [ ] A selecao placa/painel/perfil retorna o BIN esperado.
- [ ] Cancelar o FileSavePicker nao gera erro.
- [ ] O arquivo salvo possui tamanho maior que zero.
- [ ] O log informa caminho final salvo.
