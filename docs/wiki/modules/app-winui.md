# Modulo AppWinUI

## Responsabilidades

1. consumir `VisualizerRuntimeSettings` e `AnalyzerRuntimeProfile` como fonte unica de invariantes do visualizer
2. orquestrar captura, analyzer e output HUB75 sem concentrar toda a regra no code-behind
3. enviar output HUB75 nativo `128x64` para simulador e device
4. renderizar preview HUB75 local unico e nativo `128x64`
5. integrar setup e catalogo com firmware oficial DevKitC-1

## Fluxo de execucao

1. carregar `AppSettings` e presets
2. migrar estado legado
3. derivar `VisualizerRuntimeSettings` e `AnalyzerRuntimeProfile` a partir de `settings + preset + viewport`
4. iniciar `AudioPipelineCoordinator` com `AudioPipelineCaptureProfile`
5. processar `PcmFrame -> SpectrumFrame -> LedPayload` pelo runtime do pipeline
6. renderizar `MainCanvas` e preview HUB75 com a mesma base `128x64`

## Atualizacao 2026-03 - Fase 6 core, pipeline e borda de integracao

- A fase 6 ampliou a arquitetura do app em tres ondas sem mudar wire/protocolo:
  - `MicaAudio.Core` passou a concentrar invariantes de analyzer e payload (`VisualizerRuntimeSettings`, `AnalyzerRuntimeProfile`, `LedPayloadFactory`);
  - `VisualizerAnalyzerConfigFactory` e `AnalyzerRuntimeProfile` agora propagam `AnalyzerOutputMode`, mantendo `DisplayAndOutput` no modo interativo e habilitando `OutputOnly` em runtime sem display;
  - `AudioPipelineCoordinator` virou orquestrador fino e delega lifecycle/output/frame processing;
  - `MainPage` deixou de ser origem das regras de runtime e passou a consumir helpers dedicados em `MainPage.Pipeline`.
- O pipeline de audio agora se organiza assim:
  1. `AudioPipelineCaptureProfile` define a politica fixa de captura (`48 kHz`, mono, buffer leve).
  2. `AudioPipelineCoordinator` sobe/para a sessao e publica status.
  3. `AudioPipelineFrameProcessor` resolve analyzer, preset atual, modo de transporte e converte `SpectrumFrame` em `LedPayload`.
  4. `AudioPipelineOutputRouter` roteia o payload entre ESP32, simulador e null output.
- `MainPage` continua dona da UX, mas a parte tecnica central foi movida para services testaveis:
  - rebuild do analyzer;
  - sincronizacao preview/output HUB75;
  - persistencia do runtime visualizer;
  - alternancia entre audio e GIF no mesmo contrato de payload.

## Atualizacao 2026-03 - Startup estavel e observabilidade real

- O startup da `App.WinUI` passou a gravar `crash.log` sempre em `%LocalAppData%\MicaAudio\crash.log`, mesmo quando `ILogger<App>` tambem esta disponivel.
- O log de crash agora inclui breadcrumbs de startup para os pontos criticos:
  - `BuildServiceProvider`
  - `Resolve ShellPage`
  - `Resolve MainPage`
  - `MainPage.InitializeAsync`
  - `MainPage.RebuildAnalyzer`
  - `MainPage.ActivateVisualizerSessionAsync`
- A `ShellPage` nao recebe mais paginas prontas no construtor:
  - as abas sao resolvidas sob demanda por `ShellPageContentFactory`;
  - se uma pagina falhar ao resolver, a shell continua viva e mostra fallback local no `ContentFrame`.
- A `MainPage` ganhou um guard explicito de bootstrap:
  - hidratacao programatica de combos/toggle nao dispara persistencia nem sincronizacao de output;
  - o bootstrap aplica apenas uma sincronizacao ordenada ao final;
  - presets legados/parciais caem em fallback seguro sem derrubar a app.
- O endurecimento de startup ficou intencionalmente limitado ao core do visualizador:
  - `App` e `ShellPage` mantem apenas observabilidade real + isolamento da aba;
  - a carga de presets permanece crua;
  - a sanitizacao segura acontece apenas no runtime do analyzer da `MainPage`.

## Atualizacao 2026-03 - Visualizador fluido com debounce

- O `Visualizador` passou a separar runtime pendente do runtime realmente aplicado no analyzer.
- Ajustes finos (`BarCount`, `FFT`, `Smoothing`, `Weighting` e `Frequency`) agora entram em um debounce unico de `150 ms` antes do rebuild.
- Troca de preset e renderer continua imediata, mas passa por um apply consolidado:
  - sem cascata de `RebuildAnalyzer()`;
  - sem persistencia redundante quando o runtime efetivo nao mudou;
  - com render/preview HUB75 consumindo o ultimo runtime realmente aplicado.

## Atualizacao 2026-03 - Menu de configuracao Fluent 2 no Visualizador

- A `MainPage` manteve a metafora de painel lateral de configuracao, mas a composicao interna foi redesenhada como uma settings pane Fluent 2.
- O `SplitView` continua como mecanismo tecnico da lateral, mas o corpo da pane deixou de ser um formulario cru:
  - agora ha cabecalho proprio;
  - grupos nomeados (`Renderizacao`, `Analise`, `Frequencia`, `Acoes`);
  - linhas de configuracao com label, hint e controle;
  - recursos visuais baseados em `Fluent2Tokens` e `Fluent2Controls`.
- `RendererCombo` e `ContentModeCombo` sairam do `CommandBar` e passaram a viver dentro da pane de configuracao.
- O `CommandBar` superior voltou a ficar focado em acoes e modos rapidos do visualizador:
  - entrada para `Configuracoes`;
  - toggle `HUB75`;
  - status tecnico curto.
- A logica da pane foi extraida para partials dedicados:
  - `MainPage.SettingsPane`
  - `MainPage.SettingsBindings`
- O runtime do analyzer nao mudou de ownership:
  - debounce de `150 ms`;
  - rebuild consolidado;
  - fallback seguro;
  - persistencia pelo mesmo caminho existente.

## Atualizacao 2026-03 - Mica configuravel em Configuracoes > Geral

- O backdrop da janela deixou de ser hardcoded no startup.
- `App` agora carrega `AppSettings` antes da primeira aplicacao do backdrop e respeita `UseMicaBackdrop`.
- O caminho de aplicacao ficou unico:
  - `UseMicaBackdrop = true` tenta `MicaBackdrop`;
  - `UseMicaBackdrop = false` limpa `SystemBackdrop` e aplica superficie solida;
  - falha ao ativar Mica nao derruba a app e cai em fallback visual com diagnostico.
- `SettingsPage` ganhou toggle em `Geral > Aparencia da janela`:
  - a preferencia aplica imediatamente;
  - a mudanca persiste em `settings.json`;
  - o status local informa se o ambiente ficou em fallback solido.

## Atualizacao 2026-03 - SettingsPage simplificada e arquivo unico de erros

- A `SettingsPage` deixou de ser um viewer de logs.
- `Configuracoes` agora ficou reduzido a uma unica superficie `Geral`, com:
  - toggle de Mica;
  - card `Logs de erro` com atalho para abrir a pasta do `crash.log`.
- O contrato de logging do app foi simplificado:
  - `crash.log` em `%LocalAppData%\MicaAudio` virou o arquivo unico de erro;
  - `AppLogStore` continua mantendo eventos em memoria para uso interno;
  - apenas entradas `Error` passam a ser persistidas em disco;
  - `Info` e `Warning` deixam de ser gravados em `app-logs.json`.

## Referencias de codigo

- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1)
- [MainPage XAML](../../../src/App.WinUI/Views/MainPage.xaml#L1)
- [MainPage startup helpers](../../../src/App.WinUI/Views/MainPage.Startup.cs#L1)
- [MainPage settings pane helpers](../../../src/App.WinUI/Views/MainPage.SettingsPane.cs#L1)
- [MainPage settings bindings helpers](../../../src/App.WinUI/Views/MainPage.SettingsBindings.cs#L1)
- [MainPage Pipeline helpers](../../../src/App.WinUI/Views/MainPage.Pipeline.cs#L1)
- [MainPage visualizer runtime helpers](../../../src/App.WinUI/Views/MainPage.VisualizerRuntime.cs#L1)
- [Fluent2 controls](../../../src/App.WinUI/Styles/Fluent2/Fluent2Controls.xaml#L1)
- [ShellPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L1)
- [ShellPageContentFactory](../../../src/App.WinUI/Views/ShellPageContentFactory.cs#L1)
- [SettingsPage](../../../src/App.WinUI/Views/SettingsPage.xaml.cs#L1)
- [AppStartupDiagnostics](../../../src/App.WinUI/Infrastructure/AppStartupDiagnostics.cs#L1)
- [App](../../../src/App.WinUI/App.xaml.cs#L1)
- [AppLogStore](../../../src/App.WinUI/Services/Logging/AppLogStore.cs#L1)
- [AudioPipelineCoordinator](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L1)
- [AudioPipelineFrameProcessor](../../../src/App.WinUI/Services/AudioPipelineFrameProcessor.cs#L1)
- [AudioPipelineOutputRouter](../../../src/App.WinUI/Services/AudioPipelineOutputRouter.cs#L1)
- [AudioPipelineCaptureProfile](../../../src/App.WinUI/Services/AudioPipelineCaptureProfile.cs#L1)
- [AppSettingsDomainService](../../../src/App.WinUI/Services/AppSettingsDomainService.cs#L1)
- [VisualizerAnalyzerConfigFactory](../../../src/App.WinUI/Services/Visualizer/VisualizerAnalyzerConfigFactory.cs#L1)
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

## Atualizacao 2026-03 - Fase 9 Wave 2 e Wave 3, monolitos do app decompostos

- A trilha de qualidade estrutural em `.NET 10 / C# 14` passou a tratar `DevicesPage` e `AppsPage` como bordas de UI com partials focados por responsabilidade.
- `DevicesPage` foi quebrada em blocos estaveis sem mudar UX:
  - `DevicesPage.Onboarding`
  - `DevicesPage.ListState`
  - `DevicesPage.PreviewPump`
  - `DevicesPage.Dashboard`
  - `DevicesPage.Selection`
- `AppsPage` recebeu a mesma estrategia:
  - `AppsPage.Catalog`
  - `AppsPage.RuntimePreview`
  - `AppsPage.Modifiers`
  - `AppsPage.Deployment`
- O arquivo principal de cada pagina ficou restrito a:
  - estado/campos;
  - composicao;
  - lifecycle `Loaded/Unloaded`;
  - wiring central.
- A experiencia visivel foi preservada; a mudanca e de ownership interno e testabilidade.

## Atualizacao 2026-03 - DevicesPage Offline e Remocao Local

- Devices offline continuam visiveis, mas nao exibem preview visual do app.
- O painel da direita mostra apenas informacoes textuais do app ativo/ultimo app conhecido.
- As acoes de device ficam no card de resumo: `Testar LED` e `Remover`.
- O slider de brilho (`30..160`) envia `set_brightness` no commit e atualiza o painel.
- A acao `Remover` foi consolidada: online tenta `revogar/reiniciar` e depois remove do registro local; offline remove apenas localmente.

## Atualizacao 2026-03 - Dashboard seguro e logs por dispositivo

- O card de logs gerais foi substituido por dois cards na `DevicesPage`: `Dashboard seguro` e `Logs do dispositivo`.
- O dashboard usa `DeviceMetricsFormatter` para montar labels e barras a partir do snapshot selecionado, incluindo `Carga do loop`, heap, PSRAM e rede.
- O caminho padrao online prioriza estabilidade: bloco de brilho, grade de metricas, status textual (`Wi-Fi`, `uptime`, portal, ultimo evento, LED auxiliar), stream e logs.
- A composicao inspirada em ESP-Dash deixou de ser o caminho padrao enquanto o fluxo online e estabilizado contra crash nativo de XAML.
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
  - secao de status textual com rede, portal, ultimo evento e stream;
  - historico de eventos (logs).
- O wizard foi migrado para overlay custom (sem `ContentDialog`) para controlar dimensoes/padding/radius iguais ao HTML.
- O fluxo tecnico de onboarding USB nao foi alterado: a mudanca foi de composicao visual.

## Atualizacao 2026-03 - Hotfix de estabilidade ao selecionar device offline

- Foi aplicado fallback seguro na `DevicesPage` para o estado offline.
- Quando o device selecionado esta offline (ou sem snapshot valido), o dashboard entra em modo simplificado:
  - mantem resumo do device e logs;
  - oculta renderizacao avancada (`ESP-DASH`, conectividade detalhada, charts dinamicos).
- O caminho de render de selecao/dashboard ganhou hardening e telemetria local de erro para evitar encerramento do app por excecao de XAML.
- O modo online agora usa o mesmo contrato seguro como padrao: sem `Canvas`, `Polyline`, `Polygon`, `Path` ou `WrapGrid` no painel principal.

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
