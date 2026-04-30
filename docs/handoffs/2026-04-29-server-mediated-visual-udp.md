# Handoff - Visualizador server-mediated com UDP servidor->ESP

## Objetivo

Reverter o hot path remoto do visualizador para `WinUI -> MicaAudio.Server -> ESP32`, removendo UDP direto cliente->ESP e mantendo baixa latencia no trecho `servidor -> ESP` via UDP visual.

## Escopo classificado

- Tipo: firmware_protocolo + estrutural
- Criterio de aceite:
  - `RemoteDeviceFrameTransport` nao consulta `/api/v1/admin/visual-endpoints` nem abre UDP direto para o ESP.
  - `Bins128` remoto entra por `WS /ws/v1/admin/frames`.
  - Com `PreferLanUdpVisualTransport=true`, o servidor envia `VisualUdpFrameV1` para `LanIpAddress:visualUdpPort`.
  - Docker local habilita UDP visual servidor->ESP por default e permite troubleshooting com `-DisableVisualUdp`.

## Arquivos alterados

- `src/Device.Client.Remote/RemoteDeviceFrameTransport.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/App.WinUI/Services/Devices/RemoteDeviceServerConnectionTester.cs`
- `src/App.WinUI/Services/Devices/RemoteDeviceTransportDiagnosticsFormatter.cs`
- `scripts/docker-server-redeploy.ps1`
- `tests/Output.Tests/RemoteDeviceServerClientTests.cs`
- `tests/Output.Tests/RemoteDeviceServerConnectionTesterTests.cs`
- `tests/Output.Tests/RemoteDeviceTransportDiagnosticsFormatterTests.cs`
- `tests/Output.Tests/MicaAudioServerStandaloneTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/output-led.md`
- `docs/wiki/modules/server-build-and-artifacts.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/wiki/reference/ws-protocol-v2.md`
- `docs/wiki/architecture/01-system-overview.md`
- `docs/wiki/architecture/02-runtime-lifecycle.md`
- `docs/wiki/reference/cloud-first-control-plane-gap-map.md`

## Decisoes tomadas

1. O WinUI Remote voltou a ser cliente do servidor: todo frame visual remoto e enfileirado no admin WebSocket.
2. O UDP visual continua sem confirmacao, mas somente no trecho servidor LAN -> ESP, onde perder um frame e aceitavel.
3. `/api/v1/admin/visual-endpoints` permanece como diagnostico/admin para inspecionar devices UDP-capable, mas nao e dependencia do cliente.
4. O Docker local publica `5274/udp` e define `MICA_SERVER__PREFERLANUDPVISUALTRANSPORT=true` por default; `-DisableVisualUdp` volta o caminho tecnico por WS.
5. O servidor registra falhas UDP servidor->ESP com throttling de 5 segundos por device antes de cair para WS.

## Validacoes executadas

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore --filter "RemoteDeviceFrameTransport|RemoteDeviceTransportDiagnosticsFormatter|RemoteDeviceServerConnectionTester|DeviceServerHostTargetedFrameTests" -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\docker-server-redeploy.ps1 -DryRun -PublicHost 192.168.1.50 -AdminToken test-token -> aprovado; default publica 5274/udp e `MICA_SERVER__PREFERLANUDPVISUALTRANSPORT=true`
powershell -ExecutionPolicy Bypass -File .\scripts\docker-server-redeploy.ps1 -DryRun -PublicHost 192.168.1.50 -AdminToken test-token -DisableVisualUdp -> aprovado; nao publica 5274/udp e usa `MICA_SERVER__PREFERLANUDPVISUALTRANSPORT=false`
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-restore -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> aprovado
dotnet build .\MicaAudio.sln -c Debug -> primeira tentativa falhou no XamlCompiler.exe; apos `dotnet build-server shutdown`, aprovado com 0 avisos/0 erros
git diff --check -> aprovado
```

## Riscos e rollback

- Risco principal: firewall/Docker Desktop pode bloquear `5274/udp` do container para a LAN; nesse caso os logs do servidor indicam `UDP visual servidor->ESP indisponivel` e o host tenta fallback por WS quando houver socket.
- Como reverter operacionalmente: rodar `scripts/docker-server-redeploy.ps1 -DisableVisualUdp` para testar o caminho WS server->ESP sem recompilar.
- Como reverter codigo: restaurar a versao anterior do `RemoteDeviceFrameTransport` que consultava `/api/v1/admin/visual-endpoints`, sabendo que isso reintroduz UDP direto cliente->ESP.

## Proximos passos

1. Rodar redeploy Docker local e validar que a saida mostra `Visual transport: UDP servidor->ESP`.
2. Testar fisicamente WinUI Remote + Docker + ESP e conferir aumento de `streamFramesReceived/Applied`.
3. Se o HUB75 nao renderizar, coletar logs do container e validar `LanIpAddress`, `visualUdpSupported`, `visualUdpPort` e firewall UDP `5274`.
