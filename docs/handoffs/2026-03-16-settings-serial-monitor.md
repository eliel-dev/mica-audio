# Monitor serial em Configuracoes

## Objetivo

Substituir a antiga superficie de logs estruturados em `Configuracoes` por um monitor serial local, manual por `COM`, com UX inspirada no Serial Monitor da Arduino IDE.

## Escopo classificado

- Classificacao: `estrutural`
- Escopo efetivo:
  - `src/App.WinUI/Infrastructure/Serial/`
  - `src/App.WinUI/Views/SettingsPage*.cs`
  - `tests/Output.Tests`
  - `tests/Integration.Smoke`
  - `docs/wiki/modules/app-winui.md`
  - `docs/wiki/reference/code-index.md`
  - `docs/wiki/reference/device-observability-dashboard.md`

## Arquivos alterados

- `src/App.WinUI/Infrastructure/Serial/SerialPortCatalogContracts.cs`
- `src/App.WinUI/Infrastructure/Serial/SerialPortCatalogService.cs`
- `src/App.WinUI/Infrastructure/Serial/SerialMonitorService.cs`
- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/SettingsPage.xaml.cs`
- `src/App.WinUI/Views/SettingsPage.Observability.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/SerialMonitorServiceTests.cs`
- `tests/Integration.Smoke/SettingsPageSmokeTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/device-observability-dashboard.md`

## Decisoes tomadas

- A `SettingsPage` deixou de depender de `DeviceOperationsCoordinator` para a trilha de observabilidade local.
- O card antigo de `Observabilidade do device` foi substituido por `Monitor serial`, com foco em `COM` manual.
- O v1 ficou restrito a leitura:
  - `115200` baud fixo;
  - `Conectar/Desconectar`;
  - `Limpar`;
  - `Auto-scroll`.
- O monitor serial exibe linhas cruas, sem timestamps locais e sem reformatacao estruturada.
- O buffer da UI e circular, limitado a `2000` linhas.
- `DeviceLogBook` e logs estruturados continuam no backend, mas sairam da superficie principal da `SettingsPage`.
- O servico serial foi desenhado com porta fakeavel para cobrir estado, erro de abertura, remontagem de linhas e trim em teste de unidade.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build MicaAudio.sln -c Debug -m:1`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter SerialMonitorServiceTests -m:1`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "SettingsPageSmokeTests|WinUiBootstrapSmokeTests" -m:1`
- Launch verificado em `src/App.WinUI/bin/x64/Debug/net10.0-windows10.0.22621.0/win-x64/App.WinUI.exe` com `MainWindowTitle = WinUI Desktop` e `Responding = True`

## Riscos e rollback

- O monitor serial usa polling simples (`50 ms`) sobre `ReadExisting()`, o que e suficiente para o v1, mas pode exigir tuning se o firmware passar a emitir bursts muito longos.
- O mesmo `COM` nao pode ser compartilhado com provisioning/flash; nesses casos o monitor entra em erro e exige nova conexao manual.
- Rollback: restaurar o partial antigo de `SettingsPage.Observability`, remover `ISerialMonitorService` do DI e recolocar a UI de logs estruturados.

## Proximos passos

- Validar em hardware real reconexao manual apos reboot do ESP32-S3.
- Se o uso crescer, considerar seletor opcional de baud e envio de texto em iteracao futura.
- Se os logs estruturados precisarem voltar para UX, recoloca-los em superficie separada sem misturar com o monitor serial cru.
