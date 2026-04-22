# Device Server Session State Store Boundary

## Objetivo

Extrair o estado efemero de sessoes de device para `ISessionStateStore`, mantendo WebSocket e frame stream como detalhes internos do `Device.Server`.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `DeviceServerHost` usa a fronteira de session state sem alterar endpoints HTTP/WS, topicos MQTT, DTOs wire, auth, firmware ou portas `5272/5273`.

## Arquivos alterados

- `src/Device.Server.Abstractions/Hosting/ISessionStateStore.cs`
- `src/Device.Server.Abstractions/Hosting/DeviceSessionState.cs`
- `src/Device.Server.Abstractions/Hosting/DeviceRecordMutations.cs`
- `src/Device.Server.Abstractions/Properties/AssemblyInfo.cs`
- `src/Device.Server/Hosting/InMemorySessionStateStore.cs`
- `src/Device.Server/Hosting/DeviceFrameConnection.cs`
- `src/Device.Server/Hosting/DeviceFrameConnectionRegistry.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Firmware.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Mqtt.cs`
- `src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs`
- `src/App.WinUI/App.xaml.cs`
- `tests/Output.Tests/SessionStateStoreTests.cs`
- `tests/Output.Tests/DeviceSessionStateTests.cs`
- `tests/Output.Tests/DeviceFrameConnectionTests.cs`
- `tests/Output.Tests/DeviceServerHostTargetedFrameTests.cs`
- `tests/Output.Tests/ServerAbstractionBoundaryTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `tests/Integration.Smoke/packages.lock.json`
- `BenchmarkSuite1/packages.lock.json`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/architecture/08-render-cloud-migration-plan.md`
- `docs/wiki/guides/release-1.0-installer.md`
- `docs/wiki/reference/app-winui-audit-2026-03-23.md`

## Decisoes tomadas

1. `DeviceSessionState` foi movido para `Device.Server.Abstractions` para concentrar presenca, metadata, telemetry/stats e snapshots sem expor transporte.
2. `ISessionStateStore.Upsert(...)` substitui o nome planejado `Set(...)` para evitar CA1716 em API publica e manter semantica explicita de adicionar/substituir.
3. `DeviceFrameConnection` e `DeviceFrameConnectionRegistry` ficaram internos em `Device.Server`, preservando `WebSocket`, fila bounded `DropOldest` e `SendToken` fora dos contratos publicos.
4. `DeviceRecordMutations` foi movido para `Device.Server.Abstractions` como helper interno compartilhado por `DeviceSessionState`, com `InternalsVisibleTo("Device.Server")` para manter o host concreto sem duplicacao.
5. O composition root WinUI registra `ISessionStateStore -> InMemorySessionStateStore`; sessoes continuam efemeras e nao sobrevivem restart nesta entrega.
6. Links historicos para os arquivos WiX removidos em `installer/` foram convertidos para caminhos de texto para manter `docs-validate` valido sem restaurar arquivos deletados fora deste corte.
7. `packages.lock.json` de `Integration.Smoke` e `BenchmarkSuite1` foram realinhados ao `RuntimeIdentifier` atual `win-x64` para permitir `RestoreLockedMode`.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~SessionStateStoreTests|FullyQualifiedName~DeviceSessionStateTests|FullyQualifiedName~DeviceFrameConnectionTests|FullyQualifiedName~ServerAbstractionBoundaryTests|FullyQualifiedName~DeviceServerHostTargetedFrameTests" -> aprovado (20 testes)
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DeviceServerHostMqttTests|FullyQualifiedName~DeviceServerHostSecurityTests|FullyQualifiedName~DeviceServerHostTargetedFrameTests|FullyQualifiedName~DeviceServerHostTimeProviderTests|FullyQualifiedName~ServerAbstractionBoundaryTests|FullyQualifiedName~SessionStateStoreTests|FullyQualifiedName~DeviceSessionStateTests|FullyQualifiedName~DeviceFrameConnectionTests" -> aprovado (58 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-restore --filter "FullyQualifiedName~WinUiBootstrapSmokeTests" -> aprovado (9 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-restore --filter "FullyQualifiedName~WinUiBootstrapSmokeTests|FullyQualifiedName~Hub75PriorityArbitrationTests" -> aprovado (14 testes)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> aprovado
dotnet restore .\MicaAudio.sln --force-evaluate -> aprovado, realinhando lock files win-x64
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> aprovado
dotnet build .\MicaAudio.sln -c Debug -> aprovado (0 warnings, 0 errors)
```

## Riscos e rollback

- Risco principal: regressao de presenca/legacy/offline se um store futuro nao preservar ordenacao por `LastSeenUtc`, chaves case-insensitive e grace de desconexao MQTT.
- Como reverter: restaurar `DeviceSession`/`DeviceSessionRegistry` internos e recolocar o registry direto no `DeviceServerHost`, mantendo endpoints e payloads intactos.

## Proximos passos

1. Projetar persistencia/cloud real para `IDeviceRegistryStore`, `IBlobStore` e catalogo remoto depois que o server standalone for definido.
2. Definir contrato WSS publico e politica de reconnect/handoff antes de mover `DeviceFrameConnectionRegistry` para qualquer fronteira remota.
