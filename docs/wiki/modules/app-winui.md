# Modulo AppWinUI

## Responsabilidades

1. montar o analyzer ativo para a sessao de visualizacao
2. enviar output HUB75 nativo `128x64` para simulador e device
3. renderizar preview HUB75 local unico e nativo `128x64`
4. integrar setup e catalogo com firmware oficial DevKitC-1

## Fluxo de execucao

1. carregar `AppSettings` e presets
2. migrar estado legado
3. construir `AnalyzerConfig` com faixa fixa `-85/-25`
4. iniciar `AudioPipelineCoordinator`
5. renderizar `MainCanvas` e preview HUB75 com a mesma base `128x64`

## Referencias de codigo

- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1)
- [AudioPipelineCoordinator](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L1)
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)

## Atualizacao 2026-03 - DevicesPage Estavel

- A `DevicesPage` continua usando UI programatica.
- A lista de devices agora usa atualizacao incremental por diff, sem rebuild total a cada refresh.
- O objetivo e reduzir flicker visual e evitar que o preview do device selecionado reinicie sem mudanca real.
## Atualizacao 2026-03 - DevicesPage Offline e Remocao Local

- Devices offline continuam visiveis, mas nao exibem preview visual do app.
- O painel da direita mostra Ultimo app conhecido apenas como texto quando o device esta offline.
- A acao Remover exclui apenas o registro local do app; Revogar permanece como a acao remota para devices online.
