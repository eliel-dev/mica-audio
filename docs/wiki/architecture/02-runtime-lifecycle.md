# 02 - Runtime Lifecycle

## Objetivo

Documentar o ciclo de vida da app, as trocas de secao da shell e o runtime ativo de `Paineis`.

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
- `PanelsPlaybackService` mantem no maximo um painel ativo em background.
- O runtime de painel continua no desktop; o ESP32 recebe apenas o frame final composto.

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

## Backlinks no codigo

- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
