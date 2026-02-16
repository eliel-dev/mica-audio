# 02 - Runtime Lifecycle

## Objetivo

Documentar o ciclo de vida da app, trocas de secao e sessao do visualizador.

## Startup

1. `App.OnLaunched` cria `Window` + `Frame` raiz.
2. `EnsureDeviceIntegrationInitialized` prepara servicos globais (server, ops, catalogo).
3. `ShellPage` entra como conteudo raiz.

## Navegacao

- `ShellPage` mantem instancias cacheadas de `MainPage`, `DevicesPage`, `AppsPage`, `ServerPage`.
- `ShowPage(tag)` troca apenas o conteudo do frame interno.

## Sessao do visualizador

- Primeira carga: `MainPage.OnLoaded` chama `InitializeAsync`.
- Troca de secao e retorno: `OnLoaded` reativa sessao rapidamente.
- `OnUnloaded` pausa timers de render e salva settings, sem destruir pipeline global por troca de aba.

## Fullscreen

- `MainPage` sinaliza `App.SetShellChromeHidden(true/false)`.
- `ShellPage` oculta/mostra chrome lateral conforme evento.

## Referencias de codigo

- [App.OnLaunched](../../../src/App.WinUI/App.xaml.cs#L44) - assinatura esperada: `protected override void OnLaunched(...)`
- [App.EnsureDeviceIntegrationInitialized](../../../src/App.WinUI/App.xaml.cs#L61) - assinatura esperada: `EnsureDeviceIntegrationInitialized()`
- [ShellPage (classe)](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L8) - assinatura esperada: `public sealed partial class ShellPage : Page`
- [ShellPage.ShowPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L81) - assinatura esperada: `private void ShowPage(string tag)`
- [MainPage.OnLoaded](../../../src/App.WinUI/Views/MainPage.xaml.cs#L140) - assinatura esperada: `private async void OnLoaded(...)`
- [MainPage.ActivateVisualizerSessionAsync](../../../src/App.WinUI/Views/MainPage.xaml.cs#L149) - assinatura esperada: `private async Task ActivateVisualizerSessionAsync()`

## Backlinks no codigo

- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
