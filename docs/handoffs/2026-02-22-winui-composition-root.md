## Objetivo
Implantar composition root no App.WinUI para inicialização de serviços/páginas via `IServiceProvider`, removendo acoplamento direto da `AppsPage` com estáticos de `App` e adicionando smoke tests de bootstrap.

## Escopo classificado
Estrutural (mudança de bootstrap, composição de serviços e fluxo de criação de páginas).

## Arquivos alterados
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `src/App.WinUI/Services/Apps/IAppCatalogService.cs`
- `src/App.WinUI/Services/Apps/IAppDeploymentService.cs`
- `src/App.WinUI/Services/Apps/IAppModifierStateStore.cs`
- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/Services/Apps/AppDeploymentService.cs`
- `src/App.WinUI/Services/Apps/AppModifierStateStore.cs`
- `tests/Integration.Smoke/Integration.Smoke.csproj`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`

## Decisoes tomadas
- Composition root centralizado em `App.BuildServiceProvider()` com registros de serviços de dispositivo/apps e páginas WinUI.
- `AppsPage` passou a receber dependências por construtor (`DeviceOperationsCoordinator`, serviços de apps e integração de dispositivo).
- `ShellPage` passou a receber páginas e coordinator por construtor para composição via container.
- Foram criadas interfaces para os serviços consumidos pela `AppsPage` (`IAppCatalogService`, `IAppDeploymentService`, `IAppModifierStateStore`) e implementadas pelas classes concretas existentes.
- Smoke tests adicionados para validar resolução/registro de serviços críticos de bootstrap.

## Validacoes executadas
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `dotnet build MicaAudio.sln -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug`

## Riscos e rollback
- Risco de regressão no startup se algum serviço obrigatório não estiver registrado no container.
- Risco de ciclo de dependência ao evoluir registros de páginas/serviços sem teste de bootstrap.
- Rollback: reverter commit desta alteração para restaurar inicialização estática anterior.

## Proximos passos
- Migrar `DevicesPage` e `ServerPage` para dependência explícita por construtor (removendo estáticos residuais em `App`).
- Expandir smoke tests para validar cenários de ciclo de vida (`StartAsync`/dispose) com doubles de infraestrutura.
