# Guia - Setup de novo dispositivo

## Objetivo

Executar o onboarding de um novo ESP32/HUB75 pela aba `Dispositivos`, com selecao de placa, painel e firmware pre-compilado.

## Passos

1. Abrir `Dispositivos` e clicar em `Novo dispositivo`.
2. Selecionar a placa:
   - `Matrix Portal S3`
   - `ESP32-S3 N8R2/N16R8 (DevKitC-1 v1.0)`
3. Selecionar o painel `HUB75 64x32 (scan 1/32)`.
4. Selecionar o perfil de firmware (`stable` ou `dma_exp`).
5. Usar as acoes do wizard conforme necessidade:
   - `Baixar firmware`
   - `Gerar pareamento`
6. Fazer flash manual externo e concluir provisioning na placa.
7. A pinagem e fixa por variante de firmware da placa e nao e editada na UI nesta fase.

## Referencias de codigo

- [DevicesPage.ShowNewDeviceSetupDialogAsync](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L163) - assinatura: `private async Task ShowNewDeviceSetupDialogAsync()`
- [PrecompiledFirmwareOption](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareOption.cs#L1) - assinatura: `internal sealed class PrecompiledFirmwareOption`
- [PrecompiledFirmwareService.TryResolveSource](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L105) - assinatura: `bool TryResolveSource(...)`
- [DeviceIntegrationService.CreatePairingCode](../../../src/App.WinUI/Services/Devices/DeviceIntegrationService.cs#L158) - assinatura: `PairingCodeInfo CreatePairingCode(TimeSpan)`

## Checklist rapido

- [ ] O wizard persiste a ultima selecao local de placa/painel/perfil.
- [ ] A acao `Gerar pareamento` registra codigo e expiracao no log.
- [ ] A acao `Baixar firmware` falha com mensagem clara quando o BIN nao existe.
- [ ] O fluxo funciona com mais de um dispositivo online simultaneamente.
