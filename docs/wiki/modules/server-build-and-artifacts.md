# Modulo Server Build and Artifacts

## Objetivo

Cobrir startup do servidor local, geracao/export de firmware e localizacao de artefatos consumidos por flash manual e OTA.

## Responsabilidades

- Inicializar servidor local com host publico e mDNS.
- Gerar build PlatformIO por perfil.
- Gerar arquivo merged e pacote de export.
- Expor status/log para ServerPage.

## Fluxo de execucao

1. `DeviceIntegrationService.StartAsync` sobe host HTTP/WS.
2. `DeviceOperationsCoordinator.BuildAndExportAsync` dispara build.
3. `FirmwareBuildService.BuildAsync` compila e coleta artifacts.
4. `FirmwareBuildService.ExportAsync` cria pasta final para uso operacional.

## Pontos de alteracao frequente

- Porta e host publicados.
- Perfis `stable` e `dma_exp`.
- Nome e conteudo do pacote exportado.
- Parser de progresso de build.

## Riscos e efeitos colaterais

- Toolchain inconsistente quebra build.
- Sem merged bin valido OTA/flash falham.
- Mudanca de path afeta botao "Abrir pasta".

## Checklist apos alteracao

- Build stable conclui e exporta pasta.
- Build logs exibidos em tempo real.
- Botao abrir pasta aponta para diretorio correto.

## Referencias de codigo

- [DeviceIntegrationService.StartAsync](../../../src/App.WinUI/Services/Devices/DeviceIntegrationService.cs#L45) - assinatura: `Task StartAsync(CancellationToken)`
- [FirmwareBuildService](../../../src/App.WinUI/Services/Devices/FirmwareBuildService.cs#L7) - assinatura: `internal sealed class FirmwareBuildService`
- [FirmwareBuildService.BuildAsync](../../../src/App.WinUI/Services/Devices/FirmwareBuildService.cs#L66) - assinatura: `Task<FirmwareArtifactSet> BuildAsync(...)`
- [FirmwareBuildService.ExportAsync](../../../src/App.WinUI/Services/Devices/FirmwareBuildService.cs#L154) - assinatura: `Task<string> ExportAsync(...)`
- [ServerPage.OnBuildStableClicked](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L100) - assinatura: `private async void OnBuildStableClicked(...)`

## Backlinks no codigo

- `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs`
- `src/App.WinUI/Services/Devices/FirmwareBuildService.cs`
- `src/App.WinUI/Views/ServerPage.xaml.cs`