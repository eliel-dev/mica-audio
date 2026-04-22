# Handoff - Device Server Panels Batch Storage

## Objetivo

Extrair o storage efemero de batches WebP de `DeviceServerHost` para uma fronteira in-memory first sem mudar o wire protocol.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `DeviceServerHost` delega save/lookup/clear de batches a `IPanelsBatchStore`, `App.WinUI` registra `InMemoryPanelsBatchStore`, downloads autenticados continuam com o mesmo endpoint e os testes focados permanecem verdes.

## Arquivos alterados

- `src/Device.Server.Abstractions/Hosting/IPanelsBatchStore.cs`
- `src/Device.Server.Abstractions/Hosting/PanelsBatchWrite.cs`
- `src/Device.Server.Abstractions/Hosting/PanelsBatchEntry.cs`
- `src/Device.Server/Hosting/InMemoryPanelsBatchStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs`
- `src/App.WinUI/App.xaml.cs`
- `tests/Output.Tests/InMemoryPanelsBatchStoreTests.cs`
- `tests/Output.Tests/ServerAbstractionBoundaryTests.cs`
- `tests/Integration.Smoke/WinUiBootstrapSmokeTests.cs`
- `BenchmarkSuite1/packages.lock.json`
- `tests/Integration.Smoke/packages.lock.json`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/modules/app-winui.md`

## Decisoes tomadas

1. `IPanelsBatchStore` ficou em `Device.Server.Abstractions`, porque e fronteira do host/server e nao contrato direto de client remoto.
2. `InMemoryPanelsBatchStore` preserva a semantica atual: payload em memoria, chave por `deviceId`, um `panelsSessionId` ativo por device e limite de `4` batches recentes.
3. `DeviceServerHost` aceita `IPanelsBatchStore` por construtor com fallback para `InMemoryPanelsBatchStore`, mantendo compatibilidade dos construtores legados e explicitando o registro no composition root WinUI.
4. O endpoint `GET /api/v1/device/panels/batches/{batchSequence}.webp?panelsSessionId=...` nao mudou; apenas a origem interna do payload foi isolada.
5. `dotnet restore` em locked mode expos lockfiles stale em `BenchmarkSuite1` e `Integration.Smoke`; os locks foram regenerados para o runtime x64 declarado nos respectivos `.csproj`, sem alterar pacotes.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj --no-build --filter "FullyQualifiedName~InMemoryPanelsBatchStoreTests|FullyQualifiedName~ServerAbstractionBoundaryTests|FullyQualifiedName~DeviceServerHostPanelsBatchTests" -> passou (13 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj --no-build --filter "FullyQualifiedName~WinUiBootstrapSmokeTests|FullyQualifiedName~Hub75PriorityArbitrationTests" -> passou (14 testes)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> passou
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> passou
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> passou
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> passou apos alinhar lockfiles stale de RID
dotnet build .\MicaAudio.sln -c Debug -> passou (0 warnings, 0 erros)
```

## Riscos e rollback

- Risco principal: divergencia entre semantica antiga do dicionario interno e a nova implementacao in-memory ao trocar sessao ou podar batches antigos.
- Como reverter: restaurar o armazenamento interno em `DeviceServerHost.PanelsBatches` e remover o registro `IPanelsBatchStore` do composition root, sem precisar tocar firmware ou DTOs wire.

## Proximos passos

1. Validar em hardware o download autenticado de batch WebP no fluxo `queue_panels_batch`.
2. Usar o mesmo padrao de storage boundary para outros estados efemeros apenas depois que batches estiverem validados.
