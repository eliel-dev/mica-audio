# 02 - Runtime Lifecycle

## Objetivo

Documentar o ciclo de vida da app, as trocas de secao da shell e o runtime ativo de `Paineis`.

## Direcao oficial

- A shell WinUI continua sendo o primeiro cliente oficial do Mica.
- O runtime visual local passa a ser a fonte de dados para `visualizador`; `Paineis` server-owned rodam no `MicaAudio.Server` em modo Remote.
- O server participa da narrativa oficial de hot path visual remoto: cliente envia frames ao server e o server repassa ao device.

## Baseline atual / transicao

- O startup ainda inicializa servicos embedded/local por compatibilidade.
- `PanelsPlaybackService` permanece para modo Embedded, preview e compatibilidade local; em modo Remote o server assume composicao/envio de paineis server-owned.
- O modo `Embedded` continua sendo fallback seguro; o modo `Remote` usa `WS /ws/v1/admin/frames` em vez de UDP direto cliente->ESP.

## Startup

1. `App.OnLaunched` cria `Window` + `Frame` raiz.
2. `EnsureDeviceIntegrationInitialized` prepara servicos globais.
3. `ShellPage` entra como conteudo raiz.

## Navegacao

- `ShellPage` mantem instancias cacheadas de `MainPage`, `DevicesPage`, `PanelsPage`, `MonitoringPage` e `SettingsPage`.
- `ShowPage(tag)` troca apenas o conteudo do frame interno.
- `Visualizador` continua sendo a home inicial.

## Sessao do visualizador

- Primeira carga: `MainPage.OnLoaded` chama `InitializeAsync`.
- Troca de secao e retorno: `OnLoaded` reativa a sessao rapidamente.
- `OnUnloaded` pausa timers de render e salva settings, sem destruir a infraestrutura global.

## Sessao de paineis

- `PanelsPage` carrega catalogo, store e thumbnails ao entrar.
- Em modo `Remote`, `PanelsPage` salva/ativa o painel no server e nao inicia o compositor continuo local.
- `ServerOwnedPanelsRuntimeService` le `ActivePanels`, compoe widgets `dataSource=server`, registra batches `WebP` e envia `queue_panels_batch` ao ESP.
- Em modo `Embedded`, `PanelsPlaybackService` preserva o comportamento local porque fechar o WinUI encerra tambem o server embutido.

## Fullscreen

- `MainPage` sinaliza `App.SetShellChromeHidden(true/false)`.
- `ShellPage` oculta/mostra o chrome lateral conforme evento.

## Referencias de codigo

- [App.OnLaunched](../../../src/App.WinUI/App.xaml.cs#L44)
- [App.EnsureDeviceIntegrationInitialized](../../../src/App.WinUI/App.xaml.cs#L61)
- [ShellPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L1)
- [ShellPage.ShowPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L1)
- [MainPage.OnLoaded](../../../src/App.WinUI/Views/MainPage.xaml.cs#L36)
- [PanelsPage](../../../src/App.WinUI/Views/PanelsPage.xaml.cs#L1)
- [PanelsPlaybackService](../../../src/App.WinUI/Services/Panels/PanelsPlaybackService.cs#L1)
- [ServerOwnedPanelsRuntimeService](../../../src/MicaAudio.Server/ServerOwnedPanelsRuntimeService.cs#L1)

## Backlinks no codigo

- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
