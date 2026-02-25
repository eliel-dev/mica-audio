# Modulo App.WinUI

## Objetivo

Camada de interface e composicao da aplicacao desktop (WinUI 3), incluindo bootstrap, navegacao e orquestracao dos servicos de visualizacao/dispositivos/apps.

## Responsabilidades

- Construir o `ServiceProvider` no startup (`composition root`).
- Configurar `MicaAudioOptions` e distribuir paths via `IOptions<MicaAudioOptions>`.
- Resolver `ShellPage` e paginas principais por DI.
- Gerenciar estado global de janela/chrome (`MainWindow`, fullscreen chrome hide/show).
- Renderizar preview HUB75 local em dois formatos (64x32 nativo e 128x64 simulado 2x no Visualizador).
- Encaminhar eventos de falha de startup para logging estruturado com fallback seguro.

## Fluxo de execucao

1. `App.OnLaunched` inicializa janela/frame e aplica backdrop.
2. `App.EnsureServicesInitialized` cria o container com todos os servicos/paginas.
3. `App.StartDeviceIntegrationAsync` sobe servidor e carrega estado inicial (catalogo/modificadores).
4. `ShellPage.ShowPage` troca abas sem recriar pagina, preservando estado.
5. `MainPage` orquestra pipeline de visualizacao; `DevicesPage`, `AppsPage`, `ServerPage` usam services/use cases injetados.

## Padrao DI e options (canonicos)

- Paginas de startup possuem construtor publico DI-friendly.
- Servicos de persistencia recebem `IOptions<MicaAudioOptions>`.
- Paths de `settings`, `presets`, `devices`, `apps` e `crash log` ficam centralizados em `MicaAudioOptions`.
- Uso de `App.*` limitado a estado de janela/chrome nesta fase de transicao.

## Logging e resiliencia

- Bootstrap usa `ILogger` quando disponivel.
- `WriteCrashLog` permanece apenas como fallback para diagnostico de inicializacao.
- Falhas de startup mostram fallback UI com caminho do `crash.log`.

## Pontos de alteracao frequente

- Registro/injecao de servicos: `App.xaml.cs`.
- Paths de persistencia: `MicaAudioOptions` + composition root.
- Navegacao e cache de abas: `ShellPage.xaml.cs`.
- Fluxo da aba Apps/dispositivos/servidor: paginas em `Views/*.xaml.cs`.

## Riscos e efeitos colaterais

- Remover registro de servico essencial quebra ativacao de pagina no startup.
- Mudar construtor publico de pagina sem ajustar DI gera `Unable to resolve service`.
- Alterar `MicaAudioOptions` sem defaults no composition root quebra repositorios.

## Checklist apos alteracao

- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- Navegar manualmente entre Visualizador/Dispositivos/Apps/Servidor sem crash.

## Referencias de codigo

- [App (classe)](../../../src/App.WinUI/App.xaml.cs#L1) - assinatura: `public partial class App : Application`
- [App.BuildServiceProvider](../../../src/App.WinUI/App.xaml.cs#L85) - assinatura: `internal static IServiceProvider BuildServiceProvider()`
- [App.StartDeviceIntegrationAsync](../../../src/App.WinUI/App.xaml.cs#L154) - assinatura: `private static async Task StartDeviceIntegrationAsync(...)`
- [MicaAudioOptions](../../../src/MicaAudio.Core/Config/MicaAudioOptions.cs#L1) - assinatura: `public class MicaAudioOptions`
- [ShellPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L1) - assinatura: `public sealed partial class ShellPage : Page`
- [MainPage](../../../src/App.WinUI/Views/MainPage.xaml.cs#L1) - assinatura: `public partial class MainPage : Page`
- [DevicesPage](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1) - assinatura: `public sealed partial class DevicesPage : Page`
- [AppsPage](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L1) - assinatura: `public sealed partial class AppsPage : Page`
- [ServerPage](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L1) - assinatura: `public sealed partial class ServerPage : Page`
- [WinUiBootstrapSmokeTests](../../../tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs#L1) - assinatura: `public sealed class WinUiBootstrapSmokeTests`

## Backlinks no codigo

- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/ServerPage.xaml.cs`
- `src/MicaAudio.Core/Config/MicaAudioOptions.cs`
