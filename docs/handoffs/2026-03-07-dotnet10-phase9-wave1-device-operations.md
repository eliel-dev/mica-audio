## Objetivo

Decompor `DeviceOperationsCoordinator` em colaboradores internos menores, mantendo a API publica interna do app, o wire com `Device.Server` e o comportamento operacional atual.

## Escopo classificado

- Classificacao: estrutural
- Stack alvo: `.NET 10` / `C# 14`
- Limites mantidos:
  - sem mudanca de `Device.Protocol`
  - sem mudanca de firmware
  - sem mudanca de UX visivel

## Arquivos alterados

- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsText.cs`
- `src/App.WinUI/Services/Devices/DeviceCommandExecutionContext.cs`
- `src/App.WinUI/Services/Devices/DeviceCommandTracker.cs`
- `src/App.WinUI/Services/Devices/DeviceCommandDispatcher.cs`
- `src/App.WinUI/Services/Devices/DeviceLifecycleThresholdProvider.cs`
- `src/App.WinUI/Services/Devices/DeviceRefreshCoordinator.cs`
- `src/App.WinUI/Services/Devices/DeviceLogBook.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/DeviceCommandTrackerTests.cs`
- `tests/Output.Tests/DeviceRefreshCoordinatorTests.cs`
- `tests/Output.Tests/DeviceLifecycleThresholdProviderTests.cs`
- `tests/Output.Tests/DeviceLogBookTests.cs`
- `docs/wiki/modules/device-operations-coordinator.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- `DeviceOperationsCoordinator` passou a atuar como fachada thread-safe e manter apenas orquestracao.
- Responsabilidades foram separadas em cinco colaboradores fixos:
  - refresh/polling
  - dispatch de comandos
  - tracking e timeout de comandos
  - cap/log por device
  - thresholds de lifecycle com carga lazy
- Textos operacionais foram consolidados em `DeviceOperationsText` para reduzir duplicacao.
- O shape de `DeviceOperationsState` e os eventos `StateChanged` / `DeviceListChanged` foram preservados.

## Validacoes executadas

- `dotnet build MicaAudio.sln -c Debug --no-restore -m:1`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug -m:1`
- Validacao cumulativa final da fase 9 registrada nas ondas 2 e 3

## Riscos e rollback

- Risco principal: regressao no refresh ou no tracking de comando por device.
- Sinal de problema:
  - timeout precoce
  - perda de logs por device
  - `CommandByDevice` inconsistente apos refresh
- Rollback seguro:
  - reverter apenas os arquivos do coordenador e dos testes desta onda
  - manter `DevicesPage` e `AppsPage` intactas

## Proximos passos

- Decompor `DevicesPage` em partials focados por responsabilidade sem mudar UX.
- Decompor `AppsPage` com a mesma estrategia para catalogo, runtime GIF, modifiers e deploy.
