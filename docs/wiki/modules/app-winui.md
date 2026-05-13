# Modulo AppWinUI

## Responsabilidades

1. consumir `VisualizerRuntimeSettings` e `AnalyzerRuntimeProfile` como fonte unica de invariantes do visualizer
2. orquestrar captura, analyzer e output HUB75 sem concentrar toda a regra no code-behind
3. enviar output HUB75 nativo `128x64` para simulador e device
4. renderizar preview HUB75 local unico e nativo `128x64`
5. integrar setup e catalogo com firmware oficial DevKitC-1

## Referencias relacionadas

- [Auditoria desktop WinUI (2026-03-23)](../reference/app-winui-audit-2026-03-23.md)

## Fluxo de execucao

1. carregar `AppSettings` e presets
2. migrar estado legado
3. derivar `VisualizerRuntimeSettings` e `AnalyzerRuntimeProfile` a partir de `settings + preset + viewport`
4. iniciar `AudioPipelineCoordinator` com `AudioPipelineCaptureProfile`
5. processar `PcmFrame -> SpectrumFrame -> LedPayload` pelo runtime do pipeline
6. renderizar `MainCanvas` e preview HUB75 com a mesma base `128x64`

## Atualizacao 2026-04 - Remocao do flash USB interno

- `Dispositivos` nao executa mais flash USB, `esptool`, wizard de onboarding ou diagnostico serial de boot.
- O fluxo oficial do desktop passou a ser:
  - `Baixar firmware`;
  - gravar com ferramenta externa;
  - `Parear`;
  - `Copiar host`;
  - concluir no AP `MicaAudio-Setup-xxxx`.
- O endereco do servidor continua resolvido automaticamente via `IDeviceServerClient.GetServerBaseAddress()` e o usuario o reutiliza no campo `Servidor` do portal AP.
- O banner inline de `Parear` virou a superficie oficial para emitir e copiar o `pair code`.
- O dashboard continua mostrando `Firmware atual` e `Ultimo release` offline, mas `Atualizar firmware` ficou restrito a device online e elegivel para OTA.
- `PrecompiledFirmwareService` continua sendo a fonte unica do "ultimo firmware":
  - em workspace/dev valida frescor contra o workspace;
  - fora disso usa o pacote oficial embarcado no app.

## Atualizacao 2026-04 - Composition Root Do Server Embutido

- `App.BuildServiceProvider()` continua sendo o unico ponto que conhece a implementacao concreta do server local.
- `DeviceServerHost` permanece como host do servidor standalone e recebe `IPanelsBatchStore`, `IDevicePairingStore`, `ICommandStateStore` e `ISessionStateStore` pelo composition root do servidor.
- Os registros default sao `IPanelsBatchStore -> InMemoryPanelsBatchStore`, `IDevicePairingStore -> InMemoryDevicePairingStore`, `ICommandStateStore -> InMemoryCommandStateStore` e `ISessionStateStore -> InMemorySessionStateStore`, preservando batches `WebP`, pairing, comandos tracked e sessoes efemeras apenas em memoria sem alterar URLs, autenticacao ou payloads.
- `IDeviceServerClient`, `IDeviceServerClientRuntime` e `IDeviceFrameTransport` continuam resolvidos pelas fronteiras de client remoto ja existentes.

## Atualizacao 2026-04 - WinUI Remote Full Visual Client

- O modo oficial passou a ser remote-only, apontando para `MicaAudio.Server`.
- `AppSettings.RemoteServerBaseAddress` guarda a URL do `MicaAudio.Server`; o admin token fica fora do `settings.json`, protegido por DPAPI em `remote-server-secrets.json`.
- `App.BuildServiceProvider()` registra `RemoteDeviceServerClient + RemoteDeviceFrameTransport + RemoteDeviceServerRuntime`.
- `App.StartDeviceIntegrationAsync` agora usa `IDeviceServerClientRuntime`, preservando o lifecycle comum entre embedded e remote.
- Trocar modo/URL/token exige restart nesta entrega.

## Atualizacao 2026-03 - Refresh automatico do release oficial de firmware

- A `App.WinUI` continua trabalhando apenas com o conceito de release oficial local do firmware, validado por manifesto.
- Em workspace/dev, o app agora detecta se esse release oficial ficou stale em relacao aos fontes reais do firmware e tenta regenerar o pacote automaticamente.
- O ciclo ficou dividido em dois pontos:
  - warm-up assincromo no startup, em background;
  - preflight obrigatorio antes de OTA e antes do download manual do BIN.
- O backend nao usa `pio run` como sinal de release valido:
  - `pio run` recompila o firmware bruto;
  - o release oficial consumido pelo dashboard, OTA e download manual continua nascendo do script `scripts/build-precompiled-firmware.ps1`.
- Quando o pacote oficial local esta stale e a regeneracao ainda nao ocorreu com sucesso:
  - o dashboard deixa de anunciar `Ultimo release`;
  - OTA e download manual bloqueiam a operacao em vez de reaproveitar silenciosamente um pacote velho.

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

## Atualizacao 2026-03 - Prioridade HUB75 visualizador sobre paineis

- O toggle HUB75 da `MainPage` passou a arbitrar explicitamente com a sessao `Paineis`.
- Ao ativar HUB75 no `Visualizador`, o app:
  - suspende qualquer painel HUB75 ativo sem restaurar o app anterior naquele momento;
  - impede novas ativacoes de paineis enquanto o visualizador estiver dono do HUB75;
  - envia `visualizer-hub75` como app prioritario no device.
- Ao desligar HUB75 no `Visualizador`, o app:
  - desativa a sessao `visualizer-hub75`;
  - retoma apenas o painel que estava ativo antes da preempcao, no mesmo `deviceId`;
  - nao mantem fila para novos paineis pedidos durante a prioridade do visualizador.
- O runtime suspenso do painel continua intencionalmente invisivel na galeria:
  - sem badge novo;
  - sem estado visual intermediario;
  - apenas retomada automatica quando a prioridade do visualizador termina.

## Atualizacao 2026-03 - Toggle HUB75 como gate e runtime em background

- O toggle `Modo HUB75` da `MainPage` passou a ser a fonte unica de verdade para o device output do visualizador:
  - abrir a pagina do visualizador nao inicia mais stream para o ESP32 por si so;
  - o preview local e o `MainCanvas` continuam independentes do envio remoto.
- A `MainPage` agora separa tres conceitos que antes estavam misturados:
  - `hub75ModeEnabled` para sessao remota + stream ao HUB75;
  - `ShouldShowHubPreviewPanel()` para o preview local no app;
  - `isPageVisible` para invalidações visuais do XAML.
- Quando o toggle fica ativo, o runtime do visualizador continua vivo ao navegar para outra pagina:
  - a pagina permanece cacheada por `ShellPageContentFactory`;
  - captura/audio e frame pumping nao sao encerrados no `Unloaded`;
  - apenas a invalidacao visual local e pausada enquanto a pagina nao esta visivel.
- Ao desligar o toggle:
  - o app deixa de arbitrar o `visualizer-hub75`;
  - nao restaura app anterior generico no device;
  - se existir painel suspenso, apenas esse painel e retomado;
  - se nao existir painel suspenso, o app envia um frame preto para apagar o HUB75 e encerra o stream remoto.

## Atualizacao 2026-03 - Shipping mode HUB75 padrao em Bins128

- O desktop voltou a operar com `RendererHubTransportMode.Bins128` como modo shipping/default:
  - `AudioPipelineFrameProcessor` continua emitindo `Type 1` com FFT real no caminho normal;
  - `Frame128x64` permanece implementado, mas deixa de ser o caminho normal do app.
- A `MainPage` continua renderizando o preview WinUI completo localmente, mas o HUB75 fisico recebe a interpretacao `Bins128` do firmware para o fluxo de audio.
- O toggle `Modo HUB75` continua sendo o gate unico do device output, porem so ativa sessao remota quando o conteudo atual e `Audio`:
  - `GIF` no visualizador principal deixa de manter sessao/takeover no device;
  - ao sair de `Audio` para `GIF`, a sessao `visualizer-hub75` e encerrada e qualquer painel suspenso pode voltar.
- `Apps` do fluxo legado permanecem fora do shipping path do HUB75 neste modo, mas `Paineis` continuam com transporte fisico proprio:
  - o visualizador principal fica em `Bins128`;
  - a sessao `Paineis` preserva takeover por `deviceId` e stream `Frame128x64`;
  - a arbitragem continua sendo `Visualizador HUB75` com prioridade sobre `Paineis`, seguida de retomada do painel suspenso.

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

## Atualizacao 2026-03 Regra global de scroll canvas-first

- O `App.WinUI` passou a adotar uma regra global para paginas editoriais com superficie primaria de trabalho, alinhada ao guidance oficial de WinUI/Fluent:
  - quando o conteudo nao cabe no viewport, a regiao de conteudo da pagina deve rolar verticalmente;
  - a compressao do layout nao pode esconder a superficie principal;
  - scroll horizontal da pagina nao e permitido no fluxo normal.
- O dono padrao do scroll deixou de ser o card interno e passou a ser o body da pagina, abaixo de header e comandos.
- Regioes internas so devem ter scroll proprio quando forem secundarias e semanticamente delimitadas, como listas, logs e formularios longos.
- A primeira consumidora desta politica e a `PanelsPage`, que agora preserva o canvas HUB75 com minimo visivel canonico antes de sacrificar a navegacao do usuario.

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

## Atualizacao 2026-04 - Remocao do monitor serial de Configuracoes

- `Configuracoes` ficou restrita a `Geral`, com toggle de Mica e card `Logs de erro`.
- O desktop nao abre mais porta `COM` nem mantem monitor serial interno; diagnostico serial de campo deve ser feito por ferramenta externa.
- A `DevicesPage` continua focada no dashboard seguro do device selecionado, download manual, pareamento e OTA para devices online.
- O contrato de logging do app permanece simplificado:
  - `crash.log` em `%LocalAppData%\MicaAudio` e o arquivo unico de erro;
  - `AppLogStore` continua mantendo eventos em memoria para uso interno;
  - apenas entradas `Error` passam a ser persistidas em disco;
- `Info` e `Warning` deixam de ser gravados em `app-logs.json`.
- `DeviceLogBook` e logs estruturados continuam no backend para diagnostico interno, mas nao sao exibidos na `SettingsPage`.

## Atualizacao 2026-03 - Monitoramento local com HWiNFO64

- A shell ganhou a sessao `Monitoramento`, resolvida de forma lazy por `ShellPageContentFactory` no mesmo padrao das demais areas primarias.
- O v1 usa leitura local do HWiNFO64 via Shared Memory:
  - map file `Global\\HWiNFO_SENS_SM2`;
  - mutex `Global\\HWiNFO_SM2_MUTEX`;
  - parsing manual do header/sensors/readings para manter o codigo testavel em `.NET 10 / C# 14` sem `unsafe`;
  - decode narrow compativel com Windows local para preservar strings PT-BR do HWiNFO64.
- A UX segue uma direcao inspirada no InfoPanel, mas sem editor de paineis:
  - faixa superior de status;
  - grade adaptativa `3x2 / 2x3 / 1x6` de cards compostos;
  - cada card mostra contexto de hardware detectado (`CPU`, `GPU`, `RAM`, `VRAM`) e ate duas metricas internas;
  - `Memoria RAM` e `VRAM GPU` usam uma barra unica de capacidade, com percentual + `GB` usados no centro e apoio de `disponivel` / `total` no rodape;
  - superficie focada apenas no dashboard, sem lista secundaria de sensores.
- Os 6 cards principais do topo sao fixos e opinados:
  - `Uso total` com CPU + GPU;
  - `Temperatura geral` com CPU + GPU;
  - `Memoria RAM` com usada + disponivel, normalizada para `GB` na UI;
  - `VRAM GPU` com usada + disponivel, normalizada para `GB` na UI;
  - `Consumo` com CPU + GPU;
  - `Frequencia` com CPU + GPU.
- A resolucao de nomes de hardware foi separada da secao do sensor:
  - o card tenta mostrar o modelo real do dispositivo (`AMD Ryzen ...`, `DELL GeForce RTX 5070`);
  - categorias como `C-State Ocupacao`, `DTS` e `Enhanced` deixam de substituir o nome da peca.
- `RAM` e `VRAM` continuam preferindo o HWiNFO64, mas agora:
  - aceitam labels em ingles e PT-BR;
  - fazem matching accent-insensitive;
  - podem cair em fallback local do Windows quando o HWiNFO nao resolver os slots de memoria.
- O app trata quatro estados de fonte para o monitoramento:
  - `Connected`;
  - `Stale`;
  - `Unavailable`;
  - `Error`.
- O refresh roda a cada `1s` com protecao contra concorrencia e sem acao manual exposta na UI.
- O escopo desta entrega e snapshot-only:
  - sem historico temporal;
  - sem layout customizavel;
  - sem monitoramento remoto;
  - sem preferencias persistidas em `settings.json`.
- Os componentes centrais desta trilha sao:
  - `HwinfoSharedMemorySource`;
  - `HwinfoSharedMemoryBinaryParser`;
  - `MonitoringSnapshotProjector`;
  - `MonitoringKpiSelector`;
  - `MonitoringPage`.

## Atualizacao 2026-03 - Consolidacao de Apps em Paineis

- A sessao `Apps` saiu da shell como fluxo principal.
- O catalogo HUB75 continua existindo, mas agora alimenta:
  - a biblioteca de widgets de `Paineis`;
  - previews/diagnosticos em `DevicesPage`.
- A configuracao operacional de apps passou a ser por instancia de widget dentro de `Paineis`.
- O fluxo de deploy individual por app deixou de fazer parte da UX principal; ativacao no ESP32 passa pelo painel carregado.
- Os drafts locais em `apps/modifiers.json` foram mantidos e reaproveitados como defaults de widget.

## Integracoes HTTP externas

- As chamadas HTTP de internet do app passaram a ter registro centralizado em `AddExternalHttpClients`, sem aplicar politicas globais ao HTTP local/in-process.
- O app hoje registra dois named clients externos:
  - `open-meteo-geocoding` para autocomplete de cidades;
  - `open-meteo-forecast` para preview do clima.
- Os perfis internos de timeout/resiliencia seguem o tipo de endpoint:
  - `Short = 8s total / 3s attempt`;
  - `Medium = 15s total / 5s attempt`;
  - `Long = 30s total / 10s attempt` para futuros catalogos/integracoes mais lentas.
- A pipeline usa `Microsoft.Extensions.Http.Resilience` com:
  - retry apenas para metodos seguros;
  - circuit breaker ajustado para desktop de baixo volume;
  - `HttpClient.Timeout = InfiniteTimeSpan`, deixando o budget de tempo sob a pipeline.
- `CityAutocompleteService` e `OpenMeteoForecastClient` consomem `IHttpClientFactory`; o renderer do clima nao cria fallback de rede fora do DI.

## Cache compartilhado

- O app passou a registrar `AddHybridCache()` no bootstrap como baseline oficial de cache compartilhado.
- Nesta etapa, o uso concreto ficou restrito ao catalogo de apps:
  - `AppCatalogService.LoadCatalogAsync()` usa cache;
  - `AppCatalogService.ReloadCatalogAsync()` invalida a chave e recarrega do disco/seed.
- O catalogo efetivo cacheado e o resultado final mergeado/normalizado, nao os arquivos brutos.
- A politica atual do catalogo usa TTL de `10 minutos`, reduzindo reparse/disco em navegacao repetida sem esconder reload manual.
- O fluxo de clima ficou explicitamente fora deste item:
  - `WeatherPreviewDataService` continua com o cache manual atual;
  - o futuro refactor do clima deve nascer com cidades fixas em codigo, começando por `Timbó-SC`;
  - `CityAutocompleteService` nao recebeu cache novo nesta etapa para evitar churn em um fluxo que deve ser substituido.

## Observabilidade tecnica

- O bootstrap da `App.WinUI` agora centraliza logs tecnicos em `Serilog` e usa `OpenTelemetry` apenas para `traces` e `metrics`.
- O arquivo local de engenharia fica em `%LocalAppData%\MicaAudio\logs\engineering-.clef`, com rolling diario e retencao de 7 arquivos.
- O export OTLP fica desligado por default e so sobe quando `OTEL_EXPORTER_OTLP_ENDPOINT` esta presente; os env vars padrao suportados nesta etapa sao `OTEL_EXPORTER_OTLP_HEADERS`, `OTEL_EXPORTER_OTLP_PROTOCOL` e `OTEL_SERVICE_NAME`.
- A infraestrutura comum do app fica em `AppObservability` e `ObservabilityOptions`, cobrindo `ActivitySource`, `Meter`, parsing de env vars, scopes estruturados e metricas customizadas.
- Fluxos instrumentados nesta baseline:
  - `CityAutocompleteService`
  - `OpenMeteoForecastClient`
  - `WeatherPreviewDataService`
  - `App.WriteCrashLog`
- Metricas customizadas do app:
  - `mica.ui.error.count`
- `AppLogStore` e `crash.log` continuam sendo a superficie local para o usuario; a nova trilha estruturada serve diagnostico tecnico e correlacao.

## Atualizacao 2026-03 - DevicesPage com dashboard WebView2 local

- O painel direito da `DevicesPage` deixou de renderizar cards WinUI e agora hospeda um `WebView2` full-size.
- O `WebView2` navega para `http://127.0.0.1:{porta}/dashboard?embedded=1`, usando a mesma instancia local do `DeviceServerHost` iniciada pelo app.
- A troca de device selecionado nao recarrega a pagina:
  - o host WinUI envia `select-device` e `clear-selection` via `CoreWebView2.PostWebMessageAsJson(...)`;
  - o JavaScript abre/fecha `WS /ws/device/{deviceId}` e atualiza o dashboard em tempo real.
- As acoes continuam nativas no host:
  - `set-brightness`
  - `test-led`
  - `remove-device`, preservando o `ContentDialog` nativo de confirmacao.
- `Configuracoes` permanece restrita a preferencias gerais e logs de erro; console serial bruto fica fora do desktop e deve usar ferramenta externa.
- A referencia de contrato desta entrega esta em [device-observability-dashboard](../reference/device-observability-dashboard.md#objetivo).

## Atualizacao 2026-03 - Link do dashboard do device para celular

- A `DevicesPage` agora oferece `Copiar link do dashboard` na barra superior.
- O link compartilhavel usa o host resolvido pelo `IDeviceServerClient` implementado por `RemoteDeviceServerClient`, preserva a porta anunciada do servidor e fixa o device selecionado em `/dashboard?deviceId=<id>`.
- O dashboard embutido continua em `127.0.0.1 + embedded=1`; apenas o link externo usa o host de rede local.
- No navegador externo, o dashboard entra em modo leitura para nao expor controles que dependem da bridge do `WebView2`.

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
- [MonitoringPage](../../../src/App.WinUI/Views/MonitoringPage.xaml.cs#L1)
- [MonitoringPage UI](../../../src/App.WinUI/Views/MonitoringPage.Ui.cs#L1)
- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DevicesPage UI](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DevicesPage WebView dashboard](../../../src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs#L1)
- [SettingsPage](../../../src/App.WinUI/Views/SettingsPage.xaml.cs#L1)
- [AppStartupDiagnostics](../../../src/App.WinUI/Infrastructure/AppStartupDiagnostics.cs#L1)
- [AppCacheKeys](../../../src/App.WinUI/Infrastructure/Cache/AppCacheKeys.cs#L1)
- [AppObservability](../../../src/App.WinUI/Infrastructure/Observability/AppObservability.cs#L1)
- [ObservabilityOptions](../../../src/App.WinUI/Infrastructure/Observability/ObservabilityOptions.cs#L1)
- [ExternalHttpClients](../../../src/App.WinUI/Infrastructure/Http/ExternalHttpClients.cs#L1)
- [App](../../../src/App.WinUI/App.xaml.cs#L1)
- [RemoteDeviceServerClient](../../../src/Device.Client.Remote/RemoteDeviceServerClient.cs#L1)
- [IPanelsBatchStore](../../../src/Device.Server.Abstractions/Hosting/IPanelsBatchStore.cs#L1)
- [InMemoryPanelsBatchStore](../../../src/Device.Server/Hosting/InMemoryPanelsBatchStore.cs#L1)
- [IDevicePairingStore](../../../src/Device.Server.Abstractions/Hosting/IDevicePairingStore.cs#L1)
- [InMemoryDevicePairingStore](../../../src/Device.Server/Hosting/InMemoryDevicePairingStore.cs#L1)
- [ICommandStateStore](../../../src/Device.Server.Abstractions/Hosting/ICommandStateStore.cs#L1)
- [InMemoryCommandStateStore](../../../src/Device.Server/Hosting/InMemoryCommandStateStore.cs#L1)
- [ISessionStateStore](../../../src/Device.Server.Abstractions/Hosting/ISessionStateStore.cs#L1)
- [InMemorySessionStateStore](../../../src/Device.Server/Hosting/InMemorySessionStateStore.cs#L1)
- [RemoteDeviceServerSecretStore](../../../src/App.WinUI/Services/Devices/RemoteDeviceServerSecretStore.cs#L1)
- [RemoteDeviceServerClientOptions](../../../src/Device.Client.Remote/RemoteDeviceServerClientOptions.cs#L1)
- [AppLogStore](../../../src/App.WinUI/Services/Logging/AppLogStore.cs#L1)
- [AppCatalogService](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L1)
- [AppModifierStateStore](../../../src/App.WinUI/Services/Apps/AppModifierStateStore.cs#L1)
- [CityAutocompleteService](../../../src/App.WinUI/Services/Apps/CityAutocompleteService.cs#L1)
- [OpenMeteoForecastClient](../../../src/App.WinUI/Services/Apps/OpenMeteoForecastClient.cs#L1)
- [WeatherPreviewDataService](../../../src/App.WinUI/Services/Apps/WeatherPreviewDataService.cs#L1)
- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [AppModifierEditorHost](../../../src/App.WinUI/Views/Controls/AppModifierEditorHost.cs#L1)
- [WeatherPreviewRenderer](../../../src/App.WinUI/Views/Controls/Renderers/WeatherPreviewRenderer.cs#L1)
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
- [HwinfoSharedMemorySource](../../../src/App.WinUI/Services/Monitoring/HwinfoSharedMemorySource.cs#L1)
- [HwinfoSharedMemoryBinaryParser](../../../src/App.WinUI/Services/Monitoring/HwinfoSharedMemoryBinaryParser.cs#L1)
- [MonitoringSnapshotProjector](../../../src/App.WinUI/Services/Monitoring/MonitoringSnapshotProjector.cs#L1)
- [MonitoringKpiSelector](../../../src/App.WinUI/Services/Monitoring/MonitoringKpiSelector.cs#L1)

## Atualizacao 2026-03 - DevicesPage Estavel

- A `DevicesPage` continua usando UI programatica.
- A lista de devices agora usa atualizacao incremental por diff, sem rebuild total a cada refresh.
- O objetivo e reduzir flicker visual e manter a lista/miniaturas inline estaveis sem rebuild desnecessario.

## Atualizacao 2026-03 - Fase 9 Wave 2 e consolidacao em Paineis

- A trilha de qualidade estrutural em `.NET 10 / C# 14` passou a tratar `DevicesPage` e `PanelsPage` como bordas de UI com responsabilidades mais claras.
- `DevicesPage` foi quebrada em blocos estaveis sem mudar UX:
  - `DevicesPage.Onboarding`
  - `DevicesPage.ListState`
  - `DevicesPage.PreviewPump`
  - `DevicesPage.Dashboard`
  - `DevicesPage.Selection`
- `PanelsPage` concentrou o fluxo antes espalhado entre catalogo e configuracao individual:
  - galeria de paineis
  - biblioteca de widgets com busca
  - editor dedicado do painel
  - integracao com `PanelsPlaybackService`
- O arquivo principal de cada pagina ficou restrito a estado, composicao, lifecycle e wiring central.
- A experiencia visivel mudou no caso de `Paineis`: o catalogo continua existindo, mas a configuracao operacional passou a ser apenas por widget/painel.

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
- Na galeria de `Paineis`, os cards usam poster frame estatico por default e apenas o painel ativo recebe frames animados do `PanelsPlaybackService`.
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

## Atualizacao 2026-04 - Pair code inline na DevicesPage

- O botao `Parear` emite um `pair code` visivel inline logo abaixo da barra global de comandos.
- O banner inline permite copiar o codigo sem abrir fluxo USB.
- O mesmo surface inline concentra avisos e sucessos operacionais da `DevicesPage`.
- Esse fluxo permite usar ferramenta externa de flash/log e ainda assim gerar o `pair code` oficial no desktop.

## Atualizacao 2026-03 - Paridade visual com HTML canonico

- A `DevicesPage` agora usa um dashboard HTML/JS servido localmente para seguir o contrato visual do arquivo aprovado em `C:\Users\eliels\Documents\nice\mica-dashboard.html`.
- Estrutura fixa do detalhe:
  - header do dispositivo com `RSSI` + acoes verticais (`Testar LED` e `Remover`);
  - bloco de brilho (`30..160`);
  - grade de metricas (CPU/RAM/PSRAM);
  - cards auxiliares `FPS atual do HUB75` e `Sinal`;
  - charts HTML/Canvas para historico de `Loop load` e `Heap`.
- A selecao do device passa por `postMessage` do host WinUI; o HTML abre `WS /ws/device/{deviceId}` para DTO dedicado do dashboard.
- O dashboard HTML nao incorpora fluxo de flash USB; firmware manual e pairing continuam na barra superior da `DevicesPage`.

## Atualizacao 2026-03 - Hotfix de estabilidade ao selecionar device offline

- Foi aplicado fallback seguro na `DevicesPage` para o estado offline.
- Quando o device selecionado esta offline (ou sem snapshot valido), o dashboard entra em modo simplificado:
  - mantem resumo do device e logs;
  - oculta renderizacao avancada (`ESP-DASH`, conectividade detalhada, charts dinamicos).
- O caminho de render de selecao/dashboard ganhou hardening e telemetria local de erro para evitar encerramento do app por excecao de XAML.
- O modo online agora usa o mesmo contrato seguro como padrao: sem `Canvas`, `Polyline`, `Polygon`, `Path` ou `WrapGrid` no painel principal.

## Atualizacao 2026-04 - Download manual como caminho oficial

- `Baixar firmware` continua usando o pacote oficial validado pelo `PrecompiledFirmwareService`.
- O app nao tenta mais gravar esse BIN por USB.
- O suporte de firmware no desktop ficou dividido em dois caminhos:
  - OTA quando o device esta online e elegivel;
  - download manual do BIN quando o flash externo for necessario.

## Referencias de codigo

- [Hub75VisualizerSessionService](../../../src/App.WinUI/Services/Devices/Hub75VisualizerSessionService.cs#L1)
- [DeviceListRowControl](../../../src/App.WinUI/Views/Controls/DeviceListRowControl.cs#L1)
- [AppPreviewThumbnailControl](../../../src/App.WinUI/Views/Controls/AppPreviewThumbnailControl.cs#L1)
- [AppCatalogCardControl](../../../src/App.WinUI/Views/Controls/AppCatalogCardControl.cs#L1)
