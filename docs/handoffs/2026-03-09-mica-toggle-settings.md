# Handoff - Mica configuravel em Configuracoes > Geral

## Objetivo

Tornar o uso de `MicaBackdrop` uma preferencia global persistida em `settings.json`, com toggle em `SettingsPage` e aplicacao imediata na janela atual.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `UseMicaBackdrop` persistido, startup respeitando a preferencia antes da primeira aplicacao do backdrop, toggle funcionando em `Configuracoes > Geral`, build/testes sem regressao.

## Arquivos alterados

- `src/MicaAudio.Core/Presets/AppSettings.cs`
- `src/App.WinUI/Services/AppSettingsDomainService.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/SettingsPage.xaml.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `tests/Output.Tests/AppSettingsDomainServiceTests.cs`
- `tests/Output.Tests/SettingsRepositoryTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `tests/Integration.Smoke/SettingsPageSmokeTests.cs`
- `docs/wiki/modules/settings-presets-persistence.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

1. `UseMicaBackdrop` entrou em `AppSettings` com default `true`, para manter compatibilidade com `settings.json` legado e preservar a experiencia atual do app.
2. O startup foi reorganizado para carregar `SettingsRepository` e aplicar o backdrop so depois de `EnsureServicesInitialized()`, evitando tentar `MicaBackdrop` antes de conhecer a preferencia do usuario.
3. O toggle foi implementado no code-behind da `SettingsPage`, sem abrir uma rodada nova de MVVM, para manter escopo curto e ownership claro.
4. O caminho de backdrop ficou unico em `App.ApplyBackdropPreference(...)`, reutilizado no startup e no toggle da pagina.
5. Foram removidos dois campos mortos em `DevicesPage.Ui.cs` para manter a baseline de `0 warnings` do projeto apos o build de `App.WinUI`.

## Validacoes executadas

```text
dotnet build .\src\App.WinUI\App.WinUI.csproj -c Debug --no-restore -m:1 -> OK
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore -m:1 --filter "AppSettingsDomainServiceTests|SettingsRepositoryTests" -> OK (15 aprovados)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-restore -m:1 --filter "WinUiBootstrapSmokeTests|SettingsPageSmokeTests" -> OK (8 aprovados)
```

## Riscos e rollback

- Risco principal: a `SettingsPage` continua imperativa e a secao `Geral` agora tem persistencia imediata; regressao mais provavel fica concentrada no code-behind da pagina.
- Como reverter:
  - restaurar `App.xaml.cs` para o fluxo anterior de backdrop hardcoded;
  - remover `UseMicaBackdrop` de `AppSettings` e `AppSettingsDomainService`;
  - voltar `SettingsPage` para o placeholder de `Geral`.

## Proximos passos

1. Rodar a validacao completa da solucao (`docs-validate`, `ai-governance-check`, `mvvm-validate`, `build`, `test`).
2. Validar manualmente o fluxo real:
   - abrir `Configuracoes > Geral`;
   - desligar/ligar Mica;
   - fechar e reabrir a app para confirmar persistencia;
   - observar fallback solido em ambiente onde o Mica falhar.
