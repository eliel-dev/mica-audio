# Handoff — MVVM Toolkit + APPX3217/DEP0840 local fix

## Objetivo

Padronizar a camada `App.WinUI` com `CommunityToolkit.Mvvm`, remover service-locator de construtores de pagina e estabilizar o fluxo local no VS Community sem relaxar os gates de CI.

## Escopo classificado

- Estrutural (`src/`, `scripts/`, `.github/workflows/`, `docs/`, `README.md`).

## Arquivos alterados

- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/ViewModels/MainPageViewModel.cs`
- `src/App.WinUI/ViewModels/AppsPageViewModel.cs`
- `src/App.WinUI/ViewModels/DevicesPageViewModel.cs`
- `src/App.WinUI/ViewModels/ShellPageViewModel.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `src/App.WinUI/Views/ServerPage.xaml.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `scripts/mvvm-validate.ps1`
- `scripts/local-prepush-gate.ps1`
- `.github/workflows/governance.yml`
- `MicaAudio.Dev.slnf`
- `README.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/ai/change-classification.md`

## Decisoes tomadas

1. `Debug` do `App.WinUI` roda unpackaged (`WindowsPackageType=None`, `EnableMsixTooling=false`) para evitar erro local `DEP0840`.
2. `Release` permanece com MSIX tooling ativo para manter pipeline de instalador/release.
3. `Microsoft.Windows.SDK.BuildTools` foi condicionado ao modo empacotado.
4. Adoção de `CommunityToolkit.Mvvm` foi concentrada em `App.WinUI/ViewModels` nesta etapa.
5. Construtores `Page(IServiceProvider services)` foram removidos em favor de DI explicito por tipo.
6. Guardrail novo `scripts/mvvm-validate.ps1` foi integrado ao gate local leve e ao workflow de governanca.
7. `MicaAudio.Dev.slnf` foi criado para desenvolvimento diario sem `Integration.Smoke`; CI continua com `MicaAudio.sln` completo.

## Validacoes executadas

- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`

## Riscos e rollback

- Risco: regressao em pages ainda com code-behind extenso (`AppsPage`, `DevicesPage`).
- Risco: diferenca entre fluxo local (`MicaAudio.Dev.slnf`) e CI (`MicaAudio.sln`) mascarar falha de `Integration.Smoke` local.
- Rollback rapido:
  1. Reverter somente `App.WinUI.csproj` para perfil MSIX unico caso necessario.
  2. Reverter integracao do `mvvm-validate.ps1` no workflow/hook sem tocar runtime.
  3. Reverter classes ViewModel novas sem alterar contratos de dominio/protocolo.

## Proximos passos

1. Migrar progressivamente comandos/eventos de `AppsPage` e `DevicesPage` para `RelayCommand/AsyncRelayCommand` sem wrappers de evento.
2. Reduzir codigo de orquestracao no code-behind para use cases/VMs até remover duplicacao de estado visual.
3. Adicionar testes unitarios dedicados para `AppsPageViewModel`, `DevicesPageViewModel` e `ShellPageViewModel`.
4. Revisar `MicaAudio.Dev.slnf` sempre que entrar novo projeto de apoio de desenvolvimento.
