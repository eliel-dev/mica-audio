# Device Server Command State Store Boundary

## Objetivo

Extrair o estado efemero de comandos tracked para `ICommandStateStore`, mantendo o server embutido e a implementacao default em memoria.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `Device.ServerHost` usa a fronteira de command state sem alterar endpoints HTTP/WS, topicos MQTT, DTOs wire, auth, firmware ou portas `5272/5273`.

## Arquivos alterados

- `src/Device.Server.Abstractions/Hosting/ICommandStateStore.cs`
- `src/Device.Server.Abstractions/Hosting/TrackedCommandState.cs`
- `src/Device.Server/Hosting/InMemoryCommandStateStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/App.WinUI/App.xaml.cs`
- `tests/Output.Tests/CommandStateStoreTests.cs`
- `tests/Output.Tests/DeviceServerHostSecurityTests.cs`
- `tests/Output.Tests/ServerAbstractionBoundaryTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `tests/Integration.Smoke/packages.lock.json`
- `BenchmarkSuite1/packages.lock.json`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/architecture/08-render-cloud-migration-plan.md`

## Decisoes tomadas

1. `TrackedCommandState` foi movido para `Device.Server.Abstractions` para permitir que `ICommandStateStore` seja uma fronteira publica sem depender do assembly concreto.
2. A observabilidade especifica (`DeviceServerObservability`) permaneceu em `Device.Server`; `TrackedCommandState` guarda apenas o `Activity?` opaco e o host registra tags/eventos.
3. `InMemoryCommandStateStore` preserva a semantica atual de dicionario case-insensitive por `commandId`, substituicao por id, `Remove` com retorno e `Drain` para shutdown.
4. Os lock files de `Integration.Smoke` e `BenchmarkSuite1` foram realinhados para `win-x64`, que e o `RuntimeIdentifier` declarado nesses projetos, para permitir `RestoreLockedMode`.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~CommandStateStoreTests|FullyQualifiedName~ServerAbstractionBoundaryTests" -> aprovado (13 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-restore --filter "FullyQualifiedName~WinUiBootstrapSmokeTests" -> aprovado (9 testes)
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~CommandStateStoreTests|FullyQualifiedName~ServerAbstractionBoundaryTests|FullyQualifiedName~DeviceServerHostSecurityTests|FullyQualifiedName~DeviceOperationsCoordinatorBrightnessTests|FullyQualifiedName~DeviceOperationsCoordinatorDeviceLogsTests" -> aprovado (46 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-restore --filter "FullyQualifiedName~WinUiBootstrapSmokeTests|FullyQualifiedName~Hub75PriorityArbitrationTests" -> aprovado (14 testes)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> aprovado
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> aprovado apos realinhar lock files win-x64
dotnet build .\MicaAudio.sln -c Debug -> aprovado (0 warnings, 0 errors)
```

## Riscos e rollback

- Risco principal: regressao no lifecycle de comandos pendentes se um store futuro nao preservar `Drain` no shutdown e lookup case-insensitive.
- Como reverter: restaurar `PendingTrackedCommand`/`PendingTrackedCommandStore` internos e recolocar o campo local no `DeviceServerHost`, mantendo os endpoints e payloads intactos.

## Proximos passos

1. Extrair `DeviceSessionRegistry` para uma fronteira de sessao efemera quando o padrao dos stores atuais estiver estabilizado.
2. Planejar persistencia real/cloud para pairing, command state e batches somente depois de definir o server standalone.
