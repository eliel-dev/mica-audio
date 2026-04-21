# Handoff - Remocao do monitor serial de Configuracoes

## Objetivo

Remover totalmente o monitor serial interno do desktop e deixar `Configuracoes` restrita a preferencias gerais, Mica e logs de erro.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `SettingsPage` nao depende de servicos seriais, o DI nao registra infraestrutura serial, os arquivos/testes seriais internos foram removidos e a documentacao aponta diagnostico serial para ferramentas externas.

## Arquivos alterados

- `src/App.WinUI/Views/SettingsPage.xaml.cs`
- `src/App.WinUI/Views/SettingsPage.Observability.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/packages.lock.json`
- `src/App.WinUI/Infrastructure/Serial/*`
- `tests/Integration.Smoke/SettingsPageSmokeTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `tests/Integration.Smoke/packages.lock.json`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/packages.lock.json`
- `tests/Output.Tests/MicaSerialProtocolTests.cs`
- `tests/Output.Tests/SerialMonitorServiceTests.cs`
- `BenchmarkSuite1/packages.lock.json`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/device-observability-dashboard.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/wiki/future-implementations/espconnect-usb-tool-viability-checklist.md`

## Decisoes tomadas

1. Remover o partial `SettingsPage.Observability.cs` em vez de esconder o card para eliminar lifecycle, eventos e buffers seriais do desktop.
2. Apagar `Infrastructure/Serial` e os testes associados porque, apos a remocao do flash USB e do monitor em configuracoes, nao restaram consumidores internos.
3. Manter download manual, pareamento, dashboard, OTA e contratos de firmware sem alteracao; diagnostico serial futuro passa a ser responsabilidade de ferramenta externa.

## Validacoes executadas

```text
dotnet test tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~SettingsPageSmokeTests|FullyQualifiedName~WinUiBootstrapSmokeTests" -> OK, 15 passed
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
dotnet test tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~SettingsPageSmokeTests|FullyQualifiedName~WinUiBootstrapSmokeTests" -> OK, 15 passed
dotnet test tests\Output.Tests\Output.Tests.csproj -c Debug -> OK, 261 passed
dotnet build MicaAudio.sln -c Debug -> OK, 0 warnings, 0 errors
```

## Riscos e rollback

- Risco principal: documentacao antiga ou automacao externa ainda assumir que o app abre a porta `COM`.
- Como reverter: restaurar `SettingsPage.Observability.cs`, `Infrastructure/Serial/*`, registros de DI e testes seriais removidos, alem de recolocar `System.IO.Ports`.

## Proximos passos

1. Se uma ferramenta externa for escolhida oficialmente, documentar o passo a passo separado sem reintroduzir dependencia no desktop.
2. Remover referencias historicas ao monitor serial de handoffs antigos apenas se algum validador passar a exigir limpeza retroativa.
