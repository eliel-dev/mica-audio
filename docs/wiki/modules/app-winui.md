# Modulo App.WinUI

## Objetivo

Camada de interface, navegacao, orquestracao da sessao de visualizacao e integração com servicos (pipeline, dispositivos, catalogo e build firmware).

## Responsabilidades

- Bootstrap da app e inicializacao global de servicos.
- Shell com navegacao entre `Visualizador`, `Dispositivos`, `Apps`, `Servidor`.
- Controle de ciclo de vida da sessao do visualizador.
- Persistencia de preferencias de sessao (`settings.json`) e presets.

## Fluxo de execucao

1. `App.OnLaunched` inicia shell.
2. `ShellPage.ShowPage` troca secoes sem recriar desnecessariamente.
3. `MainPage` controla timers, fullscreen, analyzer e render.
4. Servicos em `App.WinUI/Services` conectam UI com pipeline e device stack.

## Pontos de alteracao frequente

- Alterar comportamento do visualizador: `MainPage.xaml.cs`.
- Alterar troca de abas/sessoes: `ShellPage.xaml.cs`.
- Alterar defaults de presets: `DefaultPresets.cs`.
- Alterar persistencia de settings: `AppSettingsDomainService` e `SettingsRepository`.

## Riscos e efeitos colaterais

- Mudar `OnLoaded/OnUnloaded` da `MainPage` pode quebrar retomada do render.
- Mudar `App.OnLaunched` pode quebrar bootstrap do servidor de dispositivos.
- Mudar `ShellPage.ShowPage` pode quebrar cache de paginas e estado visual.

## Checklist apos alteracao

- Abrir app e alternar entre Visualizador/Dispositivos/Apps/Servidor.
- Voltar para Visualizador e confirmar render ativo.
- Testar fullscreen (F11/ESC) e chrome lateral.
- Fechar/reabrir app e verificar persistencia da ultima sessao.

## Referencias de codigo

- [App (classe)](../../../src/App.WinUI/App.xaml.cs#L14) - assinatura: `public partial class App : Application`
- [App.OnLaunched](../../../src/App.WinUI/App.xaml.cs#L44) - assinatura: `protected override void OnLaunched(...)`
- [ShellPage (classe)](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L8) - assinatura: `public sealed partial class ShellPage : Page`
- [ShellPage.ShowPage](../../../src/App.WinUI/Views/ShellPage.xaml.cs#L81) - assinatura: `private void ShowPage(string tag)`
- [MainPage (classe)](../../../src/App.WinUI/Views/MainPage.xaml.cs#L26) - assinatura: `public partial class MainPage : Page`
- [MainPage.CreateAnalyzer](../../../src/App.WinUI/Views/MainPage.xaml.cs#L912) - assinatura: `private IAnalyzer CreateAnalyzer(PresetDefinition preset)`
- [DevicesPage (classe)](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L9) - assinatura: `public sealed partial class DevicesPage : Page`
- [AppsPage (classe)](../../../src/App.WinUI/Views/AppsPage.xaml.cs#L10) - assinatura: `public sealed partial class AppsPage : Page`
- [ServerPage (classe)](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L8) - assinatura: `public sealed partial class ServerPage : Page`
- [MainPageViewModel](../../../src/App.WinUI/ViewModels/MainPageViewModel.cs#L8) - assinatura: `internal sealed class MainPageViewModel`
- [AudioPipelineCoordinator](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L10) - assinatura: `internal sealed class AudioPipelineCoordinator`
- [DeviceIntegrationService](../../../src/App.WinUI/Services/Devices/DeviceIntegrationService.cs#L10) - assinatura: `internal sealed class DeviceIntegrationService`
- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L5) - assinatura: `internal sealed class DeviceOperationsCoordinator`
- [AppCatalogService](../../../src/App.WinUI/Services/Apps/AppCatalogService.cs#L6) - assinatura: `internal sealed class AppCatalogService`
- [AppDeploymentService](../../../src/App.WinUI/Services/Apps/AppDeploymentService.cs#L7) - assinatura: `internal sealed class AppDeploymentService`

## Backlinks no codigo

- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Services/AudioPipelineCoordinator.cs`
