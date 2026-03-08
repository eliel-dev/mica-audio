# Handoff Estrutural — 2026-02-28 — visualizer-fixed-db-range-remove-sensitivity-ui

## Objetivo

Remover a configuracao manual de sensibilidade do Visualizador e fixar a faixa de dB em `-85/-25` para todas as visualizacoes, mantendo compatibilidade com `settings.json` existente.

## Escopo classificado

- Classificacao: estrutural.
- Escopo: `MainPage`, `MainPageViewModel`, `AppSettingsDomainService`, testes de `Output.Tests`/`Integration.Smoke` e documentacao do visualizador.
- Fora de escopo: alterar renderers, protocolo, firmware ou outros controles do analisador.

## Arquivos alterados

- `src/App.WinUI/Views/MainPage.xaml`
- `src/App.WinUI/ViewModels/MainPageViewModel.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Services/AppSettingsDomainService.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/AppSettingsDomainServiceTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/guides/change-visualizer-settings.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. A faixa de dB do analisador deixou de ser configuravel pela UI.
2. O runtime agora usa `MinDecibels = -85` e `MaxDecibels = -25` como referencia fixa para todas as visualizacoes.
3. Os campos `Sensitivity`, `SensitivityMinDb` e `SensitivityMaxDb` permanecem em `AppSettings` apenas por compatibilidade de serializacao.
4. `AppSettingsDomainService` ignora qualquer valor legado desses campos e sempre normaliza para `-85/-25`.
5. O schema de `settings.json` foi preservado; a limpeza completa desses campos ficou adiada.
6. O smoke de bootstrap WinUI foi ajustado para validar registro de dependencias de paginas, sem instanciar `Page` fora de contexto XAML/COM.

## Validacoes executadas

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~WinUiBootstrap"
dotnet build MicaAudio.sln -c Debug
```

## Riscos e rollback

- Risco: usuarios que dependiam visualmente de uma janela de dB customizada vao perceber mudanca imediata no visualizador.
- Risco: `settings.json` antigo continuara exibindo os campos de sensibilidade, mas agora sem efeito funcional.
- Rollback:
  1. restaurar a secao de sensibilidade em `MainPage.xaml`
  2. reintroduzir handlers e estado em `MainPage.xaml.cs`
  3. restaurar a migracao variavel de sensibilidade em `AppSettingsDomainService`

## Proximos passos

1. Validar manualmente que a secao de sensibilidade sumiu do painel `Configuracoes`.
2. Confirmar que `Linear Boost`, `FFT`, `Weighting` e faixa de frequencia continuam funcionando.
3. Se a politica fixa se mantiver estavel, considerar em entrega futura a remocao definitiva dos campos de sensibilidade do schema.
