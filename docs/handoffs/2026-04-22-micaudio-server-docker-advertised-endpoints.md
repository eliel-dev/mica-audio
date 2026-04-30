# MicaAudio.Server Docker Advertised Endpoint Hotfix

## Objetivo

Corrigir o server standalone em Docker para anunciar endpoints alcancaveis pelo ESP quando o bind interno HTTP (`PORT=8080`) difere da porta publicada no PC (`5272`).

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `/api/v1/server/info`, `/api/v1/pair` e URLs de batches WebP deixam de anunciar a porta interna do container quando existe `PublicHttpBaseAddress` ou `Request.Host` externo.
- Fora de escopo: firmware, WSS v2, protocolo Tronbyt, Render operacional completo, mudanca de MQTT topics, DTOs wire ou auth de device.

## Arquivos alterados

- `src/Device.Protocol/Contracts/ServerConfig.cs`
- `src/Device.Server/Hosting/DeviceServerRuntimeConfig.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
- `src/Device.Server/Hosting/DeviceServerHost.PanelsBatches.cs`
- `src/MicaAudio.Server/MicaAudioServerBootstrap.cs`
- `src/MicaAudio.Server/MicaAudioServerOptions.cs`
- `src/MicaAudio.Server/Dockerfile`
- `tests/Output.Tests/DeviceServerRuntimeConfigTests.cs`
- `tests/Output.Tests/DeviceServerHostMqttTests.cs`
- `tests/Output.Tests/DeviceServerHostPanelsBatchTests.cs`
- `tests/Output.Tests/MicaAudioServerStandaloneTests.cs`
- `tests/Output.Tests/DeviceServerTestHarness.cs`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/server-build-and-artifacts.md`
- `docs/wiki/architecture/08-render-cloud-migration-plan.md`

## Decisoes tomadas

1. `PublicHttpBaseAddress` e a fonte canonica para endpoints HTTP anunciados quando configurada.
2. Na ausencia de base publica configurada, o host usa `Request.Host` completo para preservar a porta externa de proxies, Docker port mapping e reverse proxies.
3. `MqttHost` continua derivando de `PublicHost` primeiro; se ele estiver vazio, usa o host de `PublicHttpBaseAddress` ou da request.
4. `Dockerfile` expõe `5273` para deixar claro que MQTT local/legado precisa ser publicado separadamente.
5. Os logs do `DeviceServerHost` agora diferenciam HTTP bind interno, HTTP anunciado e MQTT anunciado.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~MicaAudioServerStandaloneTests|FullyQualifiedName~DeviceServerRuntimeConfigTests|FullyQualifiedName~DeviceServerHostMqttTests|FullyQualifiedName~DeviceServerHostPanelsBatchTests" -> aprovado (33 testes)
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~MicaAudioServerStandaloneTests|FullyQualifiedName~DeviceServerRuntimeConfigTests|FullyQualifiedName~DeviceServerHostMqttTests|FullyQualifiedName~DeviceServerHostPanelsBatchTests|FullyQualifiedName~DeviceServerHostSecurityTests|FullyQualifiedName~RemoteDeviceServerClientTests" -> aprovado (61 testes)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> aprovado
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> aprovado
dotnet build .\MicaAudio.sln -c Debug --no-restore -m:1 -> aprovado (0 warnings, 0 errors)
dotnet build .\MicaAudio.sln -c Debug -> aprovado (0 warnings, 0 errors)
docker build -f src\MicaAudio.Server\Dockerfile -t mica-audio-server:remote-dev . -> aprovado
docker run -d --rm --name mica-audio-server-dev -e PORT=8080 -e MICA_SERVER__ADMINTOKEN=dev-token -e MICA_SERVER__RESTRICTTOPRIVATENETWORKS=false -e MICA_SERVER__PUBLICHTTPBASEADDRESS=http://192.168.1.16:5272 -e MICA_SERVER__PUBLICHOST=192.168.1.16 -p 5272:8080 -p 5273:5273 mica-audio-server:remote-dev -> container iniciado
GET http://127.0.0.1:5272/api/v1/health -> ok
GET http://192.168.1.16:5272/api/v1/server/info -> httpBase=http://192.168.1.16:5272, mqttHost=192.168.1.16, mqttPort=5273
docker logs --tail 80 mica-audio-server-dev -> mostra HTTP bind interno http://0.0.0.0:8080, HTTP anunciado http://192.168.1.16:5272 e MQTT anunciado mqtt://192.168.1.16:5273
Test-NetConnection 192.168.1.16 -Port 5272 -> TcpTestSucceeded=true
Test-NetConnection 192.168.1.16 -Port 5273 -> TcpTestSucceeded=true
```

Observacao: uma primeira execucao de `dotnet build .\MicaAudio.sln -c Debug --no-restore` falhou por lock temporario de outputs em build paralelo (`Audio.Loopback.deps.json` e `Visual.Win2D.deps.json`, bloqueados por `MSBuild.exe`). A repeticao serial e a repeticao do comando obrigatorio completo passaram.

## Riscos e rollback

- Risco: o ESP continua sem MQTT se o container for executado sem `-p 5273:5273`.
- Risco: provisionar com `localhost` ou `127.0.0.1` ainda cria endpoint invalido para hardware fisico.
- Rollback: remover `PublicHttpBaseAddress` de `ServerConfig`/options/runtime e voltar `HttpBase` para `host + Port`; isso reintroduz o problema de Docker com `5272 -> 8080`.

## Proximos passos

1. Rebuildar a imagem Docker e rodar com `MICA_SERVER__PUBLICHTTPBASEADDRESS=http://<IP_DO_PC>:5272`, `MICA_SERVER__PUBLICHOST=<IP_DO_PC>`, `-p 5272:8080` e `-p 5273:5273`.
2. Validar no serial do ESP que aparecem `ws://<IP_DO_PC>:5272/ws/v1/stream` e `mqtt://<IP_DO_PC>:5273`, sem `:8080`.
3. Depois do Docker local estabilizar, planejar WSS/HTTPS cloud para reduzir a dependencia de MQTT publico no Render.
