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
- [DevicesPage UI](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DevicesPage code-behind](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DeviceMetricsFormatter](../../../src/App.WinUI/Services/Devices/DeviceMetricsFormatter.cs#L1)
- [DeviceMetricsPresentation](../../../src/App.WinUI/Services/Devices/DeviceMetricsPresentation.cs#L1)
- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1)

## Atualizacao 2026-03 - DevicesPage Estavel

- A `DevicesPage` continua usando UI programatica.
- A lista de devices agora usa atualizacao incremental por diff, sem rebuild total a cada refresh.
- O objetivo e reduzir flicker visual e manter a lista/miniaturas inline estaveis sem rebuild desnecessario.

## Atualizacao 2026-03 - DevicesPage Offline e Remocao Local

- Devices offline continuam visiveis, mas nao exibem preview visual do app.
- O painel da direita mostra apenas informacoes textuais do app ativo/ultimo app conhecido.
- As acoes de device ficam no card de resumo: `Testar LED` e `Remover`.
- A acao `Remover` foi consolidada: online tenta `revogar/reiniciar` e depois remove do registro local; offline remove apenas localmente.

## Atualizacao 2026-03 - Dashboard ESP e logs por dispositivo

- O card de logs gerais foi substituido por dois cards na `DevicesPage`: `Dashboard ESP` e `Logs do dispositivo`.
- O dashboard usa `DeviceMetricsFormatter` para montar labels e barras a partir do snapshot selecionado, incluindo `Carga do loop`, heap, PSRAM e rede.
- O dashboard segue visual de painel NOC: chips de status, grade de blocos operacionais e tendencia curta de carga do loop.
- A paleta do dashboard foi suavizada para seguir o estilo Fluent/Settings da Microsoft: superficies neutras e icones discretos em vez de blocos saturados.
- Quando o device esta offline, a pagina exibe o ultimo snapshot conhecido com aviso explicito de offline.
- Quando nao ha selecao, dashboard e logs exibem placeholders estaveis.
- A linha de status da lista removeu `IP` e `RSSI`; o `RSSI` aparece apenas no dashboard quando o device esta online.
- A atualizacao evita flicker usando assinatura/cache para dashboard e logs do device selecionado.

## Atualizacao 2026-03 - Preview animado e pump de frame real

- Na `DevicesPage`, miniaturas de app ficam sempre animadas (`preview.Start()` chamado automaticamente no `Bind`).
- Na `AppsPage`, miniaturas animam apenas no hover do card (`PointerEntered` → `Start`, `PointerExited` → `Stop`).
- Um timer de UI leve (`DispatcherQueueTimer`, 8 Hz / 125ms) alimenta frames reais do `SimulatorLedOutput` para linhas cujo app ativo e `visualizer-hub75`.
- O pump respeita a flag `isApplyingDeviceList` para nao competir com o diff incremental.
- A leitura do frame do simulador e lazy: so ocorre se houver ao menos uma linha com visualizer ativo.
- O `DeviceListRowControl` expoe `StartPreview()` e `StopPreview()` simetricos; o caminho de remocao no diff chama `StopPreview()` para evitar leak de timer.

## Referencias de codigo

- [Hub75VisualizerSessionService](../../../src/App.WinUI/Services/Devices/Hub75VisualizerSessionService.cs#L1)
- [DeviceListRowControl](../../../src/App.WinUI/Views/Controls/DeviceListRowControl.cs#L1)
- [AppPreviewThumbnailControl](../../../src/App.WinUI/Views/Controls/AppPreviewThumbnailControl.cs#L1)
- [AppCatalogCardControl](../../../src/App.WinUI/Views/Controls/AppCatalogCardControl.cs#L1)
