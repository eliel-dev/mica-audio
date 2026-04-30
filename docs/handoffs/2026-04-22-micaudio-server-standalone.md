# MicaAudio.Server Standalone + Docker/Render Smoke

## Objetivo

Criar o primeiro executavel standalone do device server, reaproveitando `Device.Server` e preparando smoke local/Docker/Render sem mudar wire protocol.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `MicaAudio.Server` roda fora do WinUI, responde health/server info, publica dashboard assets, aceita `PORT`/`MICA_SERVER__*` e preserva endpoints, DTOs, firmware, MQTT topics e auth existentes.

## Arquivos alterados

- `src/MicaAudio.Server/MicaAudio.Server.csproj`
- `src/MicaAudio.Server/Program.cs`
- `src/MicaAudio.Server/MicaAudioServerBootstrap.cs`
- `src/MicaAudio.Server/MicaAudioServerOptions.cs`
- `src/MicaAudio.Server/MicaAudioServerRuntime.cs`
- `src/MicaAudio.Server/StandaloneDeviceRegistryStore.cs`
- `src/MicaAudio.Server/Dockerfile`
- `.dockerignore`
- `render.yaml`
- `MicaAudio.sln`
- `src/MicaAudio.Server/packages.lock.json`
- `tests/Integration.Smoke/Integration.Smoke.csproj`
- `tests/Integration.Smoke/packages.lock.json`
- `BenchmarkSuite1/BenchmarkSuite1.csproj`
- `BenchmarkSuite1/packages.lock.json`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/MicaAudioServerStandaloneTests.cs`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/server-build-and-artifacts.md`
- `docs/wiki/architecture/08-render-cloud-migration-plan.md`

## Decisoes tomadas

1. `MicaAudio.Server` usa `Microsoft.NET.Sdk.Web` e `net10.0`, mas delega o pipeline real a `DeviceServerHost` para manter o wire congelado.
2. `PORT` tem precedencia sobre `Port` para compatibilidade com Render; `MICA_SERVER__*` configura o runtime standalone.
3. `StandaloneDeviceRegistryStore` persiste somente `DeviceRecord` em JSON simples no `StorageRoot`; stores de pairing, sessoes, comandos e batches continuam in-memory nesta entrega.
4. O startup pair code e transitorio, emitido em log/console quando `StartupPairCodeTtlSeconds > 0`; admin API/token fica para etapa cloud posterior.
5. `render.yaml` usa Web Service Docker com disk `/data`; o plano `starter` foi usado porque persistent disks exigem instancia paga no Render.
6. `Integration.Smoke` e `BenchmarkSuite1` agora declaram `RuntimeIdentifier` e `RuntimeIdentifiers` como `win-x64`, evitando que o restore da solution regenere locks multi-RID e quebre `RestoreLockedMode`.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~MicaAudioServerStandaloneTests" -> aprovado (9 testes)
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~MicaAudioServerStandaloneTests|FullyQualifiedName~ServerAbstractionBoundaryTests" -> aprovado (14 testes)
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --no-restore --filter "FullyQualifiedName~WinUiBootstrapSmokeTests" -> aprovado (9 testes)
dotnet publish .\src\MicaAudio.Server\MicaAudio.Server.csproj -c Release -o <temp> --no-restore -> aprovado; dashboard publicado em wwwroot/dashboard/index.html
Smoke local com dotnet run --project src/MicaAudio.Server --no-build em portas livres -> /api/v1/health ok, /api/v1/server/info ok, /dashboard redirect ok, /dashboard/index.html ok, WS /ws/device/mp-smoke conectado, pair code de startup logado
docker --version -> indisponivel neste ambiente; build/run Docker nao executado localmente
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> aprovado
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> aprovado apos realinhar projetos e lock files win-x64
dotnet build .\MicaAudio.sln -c Debug -> aprovado (0 warnings, 0 errors)
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> aprovado novamente apos build; locks permanecem x64-only
```

## Riscos e rollback

- Risco principal: aplicar o Blueprint Render antes de entender que esta fase e smoke de runtime, nao operacao cloud completa de firmware/WinUI remoto.
- Como reverter: remover `src/MicaAudio.Server`, `.dockerignore`, `render.yaml` e a entrada da solution; `App.WinUI` embedded e `Device.Server` permanecem independentes.

## Proximos passos

1. Validar Docker build/run em maquina com Docker Desktop instalado.
2. Criar o primeiro client remoto HTTP/WSS para o WinUI ou a admin API/token antes de operar pairing cloud real.
