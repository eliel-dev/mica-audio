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
- O slider de brilho (`30..160`) envia `set_brightness` no commit e atualiza o painel.
- A acao `Remover` foi consolidada: online tenta `revogar/reiniciar` e depois remove do registro local; offline remove apenas localmente.

## Atualizacao 2026-03 - Dashboard ESP e logs por dispositivo

- O card de logs gerais foi substituido por dois cards na `DevicesPage`: `Dashboard ESP` e `Logs do dispositivo`.
- O dashboard usa `DeviceMetricsFormatter` para montar labels e barras a partir do snapshot selecionado, incluindo `Carga do loop`, heap, PSRAM e rede.
- O dashboard segue visual de painel NOC com grade de blocos operacionais e tendencia curta de carga do loop.
- A paleta do dashboard foi suavizada para seguir o estilo Fluent/Settings da Microsoft: superficies neutras e icones discretos em vez de blocos saturados.
- Quando o device esta offline, a pagina exibe o ultimo snapshot conhecido com aviso explicito de offline.
- Quando nao ha selecao, dashboard e logs exibem placeholders estaveis.
- A linha de status da lista removeu `IP` e `RSSI`; o `RSSI` agora aparece no topo do card de resumo ao lado das acoes.
- A atualizacao evita flicker usando assinatura/cache para dashboard e logs do device selecionado.

## Atualizacao 2026-03 - Preview animado e pump de frame real

- Na `DevicesPage`, miniaturas de app ficam sempre animadas (`preview.Start()` chamado automaticamente no `Bind`).
- Na `AppsPage`, miniaturas animam apenas no hover do card (`PointerEntered` → `Start`, `PointerExited` → `Stop`).
- Um timer de UI leve (`DispatcherQueueTimer`, 8 Hz / 125ms) alimenta frames reais do `SimulatorLedOutput` para linhas cujo app ativo e `visualizer-hub75`.
- O pump respeita a flag `isApplyingDeviceList` para nao competir com o diff incremental.
- A leitura do frame do simulador e lazy: so ocorre se houver ao menos uma linha com visualizer ativo.
- O `DeviceListRowControl` expoe `StartPreview()` e `StopPreview()` simetricos; o caminho de remocao no diff chama `StopPreview()` para evitar leak de timer.

## Atualizacao 2026-03 - Cleanup P0 para priorizar logs

- O card visual `Comandos:` foi removido da `DevicesPage` para liberar area util de diagnostico.
- Chips redundantes (online/Wi-Fi/snapshot) e bloco de conectividade/eventos foram removidos do dashboard.
- O `RSSI` foi movido para o topo do card de resumo, ao lado dos botoes `Testar LED` e `Remover`.
- O card `Logs do dispositivo` recebeu prioridade de espaco vertical para facilitar leitura operacional.
- O botao `Testar LED` continua respeitando `testLedAvailable` (fallback para firmware legado):
  - quando indisponivel, fica desabilitado e mostra rotulo `LED indisponivel`.

## Atualizacao 2026-03 - Rollback onboarding para COM+flash + AP

- O wizard `Novo dispositivo` voltou para etapa funcional unica:
  - selecao de porta COM + flash de firmware.
- SSID/senha deixaram de ser coletados pela UI nesse fluxo.
- Ao fim do flash, o app exibe `pair code` em modal com instrucoes de provisioning via AP.
- O onboarding oficial nao depende mais de handshake serial para concluir.

## Atualizacao 2026-03 - Paridade visual com HTML canonico

- A `DevicesPage` agora segue contrato visual 1:1 do arquivo canonicamente aprovado em `C:\Users\eliels\Pictures\nice\mica-dashboard.html`.
- Estrutura fixa do detalhe:
  - header do dispositivo com `RSSI` + acoes verticais (`Testar LED` e `Remover`);
  - bloco de brilho (`30..160`) com status/aplicado/heartbeat;
  - grade de metricas (CPU/RAM/PSRAM);
  - tendencia de CPU;
  - secao `Status em tempo real` (ESP-DASH style);
  - linha de conectividade;
  - historico de eventos (logs).
- O wizard foi migrado para overlay custom (sem `ContentDialog`) para controlar dimensoes/padding/radius iguais ao HTML.
- O fluxo tecnico de onboarding USB nao foi alterado: a mudanca foi de composicao visual.

## Atualizacao 2026-03 - Hotfix de estabilidade ao selecionar device offline

- Foi aplicado fallback seguro na `DevicesPage` para o estado offline.
- Quando o device selecionado esta offline (ou sem snapshot valido), o dashboard entra em modo simplificado:
  - mantem resumo do device e logs;
  - oculta renderizacao avancada (`ESP-DASH`, conectividade detalhada, charts dinamicos).
- O caminho de render de selecao/dashboard ganhou hardening e telemetria local de erro para evitar encerramento do app por excecao de XAML.
- O modo online continua exibindo dashboard completo.

## Atualizacao 2026-03 - Onboarding USB com perfil esptool fixo + progresso visual

- O onboarding USB passou a usar perfil canonico de flash:
  - `--chip esp32s3`
  - `--baud 115200`
  - `--before default_reset`
  - `--after hard_reset`
  - `write_flash --no-compress 0x0 <firmware.bin>`
- O wizard de `Novo dispositivo` mostra barra de progresso + percentual real na etapa `Flashing`.
- O percentual e derivado diretamente das linhas de saida do `esptool` (`NN%` e `NN %`).
- Em sucesso, o wizard encerra apos mostrar o `pair code` e orientar configuracao no AP do ESP32.

## Referencias de codigo

- [Hub75VisualizerSessionService](../../../src/App.WinUI/Services/Devices/Hub75VisualizerSessionService.cs#L1)
- [DeviceListRowControl](../../../src/App.WinUI/Views/Controls/DeviceListRowControl.cs#L1)
- [AppPreviewThumbnailControl](../../../src/App.WinUI/Views/Controls/AppPreviewThumbnailControl.cs#L1)
- [AppCatalogCardControl](../../../src/App.WinUI/Views/Controls/AppCatalogCardControl.cs#L1)
