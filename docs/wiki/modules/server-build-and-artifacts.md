# Modulo Server build and artifacts

## Objetivo

Cobrir o startup do servidor local e o fluxo de setup/download de firmware centralizado na aba `Dispositivos`.

## Responsabilidades

- Inicializar servidor local com host publico e mDNS.
- Expor host, pareamento e logs operacionais na tela `Dispositivos`.
- Resolver BIN pre-compilado por `placa + painel + perfil`.
- Copiar BIN para destino escolhido pelo usuario.

## Fluxo de execucao

1. `DeviceIntegrationService.StartAsync` sobe host HTTP/WS.
2. `DevicesPage` oferece wizard `Novo dispositivo`.
3. `PrecompiledFirmwareService.GetOptions` filtra firmware por selecao.
4. `PrecompiledFirmwareService.CopyToAsync` salva o binario escolhido.

## Pontos de alteracao frequente

- Inclusao de novas placas/perfis no catalogo de firmware.
- Nome/descricao das opcoes exibidas no wizard.
- Mensagens de status/log em `DevicesPage`.

## Riscos e efeitos colaterais

- BIN ausente no pacote impede download para a combinacao selecionada.
- Renomear arquivo sem atualizar o catalogo quebra resolucao do artefato.
- Permissao negada no caminho de destino falha a copia.

## Checklist apos alteracao

- `Novo dispositivo` abre e lista as opcoes esperadas.
- Download de `matrixportal_s3` funciona para `stable` e `dma_exp`.
- Download de `esp32s3_devkitc1` funciona para `stable` e `dma_exp`.
- `Gerar pareamento` funciona no wizard.
- Nao existe acao `Copiar host` na UX de `Dispositivos`.

## Referencias de codigo

- [DeviceIntegrationService.StartAsync](../../../src/App.WinUI/Services/Devices/DeviceIntegrationService.cs#L110) - assinatura: `Task StartAsync(CancellationToken)`
- [DevicesPage.ShowNewDeviceSetupDialogAsync](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L163) - assinatura: `private async Task ShowNewDeviceSetupDialogAsync()`
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L8) - assinatura: `internal sealed class PrecompiledFirmwareService`
- [PrecompiledFirmwareOption](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareOption.cs#L1) - assinatura: `internal sealed class PrecompiledFirmwareOption`

## Backlinks no codigo

- `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
