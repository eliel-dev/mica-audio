# Modulo Server Build and Artifacts

## Objetivo

Cobrir startup do servidor local e o fluxo simplificado de firmware na UI: download de BINs pre-compilados para flash manual.

## Responsabilidades

- Inicializar servidor local com host publico e mDNS.
- Expor estado/log na ServerPage.
- Resolver e copiar BIN pre-compilado (`stable` e `dma_exp`) para destino escolhido.

## Fluxo de execucao

1. `DeviceIntegrationService.StartAsync` sobe host HTTP/WS.
2. `ServerPage` mostra host/log e acoes de download.
3. `PrecompiledFirmwareService.TryResolveSource` encontra o BIN no pacote.
4. `PrecompiledFirmwareService.CopyToAsync` salva no caminho escolhido.

## Pontos de alteracao frequente

- Nome/descricao das opcoes de firmware na UI.
- Localizacao dos assets de firmware no pacote.
- Mensagens de status/log da ServerPage.

## Riscos e efeitos colaterais

- BIN ausente no pacote impede download.
- Caminho sem permissao falha no salvamento.
- Alteracao de nome de arquivo quebra resolucao de asset.

## Checklist apos alteracao

- Download `stable` conclui com sucesso.
- Download `dma_exp` conclui com sucesso.
- Cancelamento do dialogo nao gera excecao.
- Logs e status refletem sucesso/falha corretamente.

## Referencias de codigo

- [DeviceIntegrationService.StartAsync](../../../src/App.WinUI/Services/Devices/DeviceIntegrationService.cs#L1) - assinatura: `Task StartAsync(CancellationToken)`
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1) - assinatura: `internal sealed class PrecompiledFirmwareService`
- [PrecompiledFirmwareService.CopyToAsync](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1) - assinatura: `Task CopyToAsync(...)`
- [ServerPage](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L1) - assinatura: `public sealed partial class ServerPage : Page`

## Backlinks no codigo

- `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs`
- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
- `src/App.WinUI/Views/ServerPage.xaml.cs`
