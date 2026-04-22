# Handoff - Device Server Pairing Store

## Objetivo

Extrair o estado efemero de pairing de `DeviceServerHost` para uma fronteira in-memory first sem mudar `/api/v1/pair`.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `DeviceServerHost` delega pair codes e tentativas por IP a `IDevicePairingStore`, `App.WinUI` registra `InMemoryDevicePairingStore`, e o pareamento continua com os mesmos erros, TTL, rate limits e respostas HTTP.

## Arquivos alterados

- `src/Device.Server.Abstractions/Hosting/IDevicePairingStore.cs`
- `src/Device.Server/Hosting/InMemoryDevicePairingStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DevicePairingState.cs`
- `src/App.WinUI/App.xaml.cs`
- `tests/Output.Tests/InMemoryDevicePairingStoreTests.cs`
- `tests/Output.Tests/DevicePairingStateTests.cs`
- `tests/Output.Tests/ServerAbstractionBoundaryTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/architecture/08-render-cloud-migration-plan.md`

## Decisoes tomadas

1. `IDevicePairingStore` ficou em `Device.Server.Abstractions`, porque e fronteira do host/server e prepara troca futura por Key Value sem expor implementacao concreta ao composition root.
2. `InMemoryDevicePairingStore` preserva a semantica atual: codigos case-insensitive, uso unico, TTL por `ExpiresAtUtc`, tentativas por `remoteIpKey` e `retryAfterSeconds` com minimo `1`.
3. `DeviceServerHost` continua gerando o codigo aleatorio e usando `TimeProvider`; o store recebe `DateTimeOffset now` para permanecer deterministico e testavel.
4. `DevicePairingState` foi removido para evitar dois estados concorrentes de pairing no assembly concreto.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj --no-build --filter "FullyQualifiedName~InMemoryDevicePairingStoreTests|FullyQualifiedName~ServerAbstractionBoundaryTests|FullyQualifiedName~DeviceServerHostSecurityTests|FullyQualifiedName~DeviceServerHostTimeProviderTests" -> passou (38 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj --no-build --filter "FullyQualifiedName~WinUiBootstrapSmokeTests" -> passou (9 testes)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> passou
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> passou
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> passou
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> passou
dotnet build .\MicaAudio.sln -c Debug -> passou (0 warnings, 0 erros)
```

## Riscos e rollback

- Risco principal: divergencia no consumo de codigo de uso unico ou na janela de tentativas por IP.
- Como reverter: restaurar `DevicePairingState`, voltar `DeviceServerHost` a instanciar esse estado interno e remover `IDevicePairingStore` do composition root, sem tocar firmware ou DTOs wire.

## Proximos passos

1. Validar pareamento manual com codigo valido, codigo expirado e burst de tentativas.
2. Aplicar o mesmo padrao a comandos tracked ou sessoes somente depois que o pairing store estiver estavel.
