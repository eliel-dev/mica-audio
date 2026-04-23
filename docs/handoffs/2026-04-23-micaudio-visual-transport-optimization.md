# MicaAudio Visual Transport Optimization

## Objetivo

Reduzir gargalos do caminho visual remoto/local e adicionar um transporte UDP LAN opt-in para `Bins128`, mantendo WS como fallback seguro e sem mudar pairing, Admin API, MQTT, WebP batch, OTA ou firmware cloud.

## Escopo classificado

- Tipo: firmware/protocolo + estrutural
- Criterio de aceite: o hot path `WinUI remoto -> Admin WS -> DeviceServerHost -> ESP` reduz alocacoes, a fila visual do device absorve jitter curto, o playback WebP deixa de acelerar apos decode lento e o server pode enviar `Bins128` por UDP LAN autenticado quando o device anunciar suporte.
- Fora de escopo: UDP para `Frame128x64 RGB565`, Render/cloud via UDP, substituicao de MQTT, pairing novo, client WSS v2 e persistencia real.

## Arquivos alterados

- `src/Device.Protocol/Stream/StreamFrameV2.cs`
- `src/Device.Protocol/Stream/VisualUdpFrameV1.cs`
- `src/Device.Protocol/Contracts/ServerConfig.cs`
- `src/Device.Protocol/Models/DeviceTelemetryMessage.cs`
- `src/Device.Protocol/Models/DeviceRecord.cs`
- `src/Device.Protocol/Models/DeviceSnapshot.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Admin.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceFrameConnection.cs`
- `src/Device.Server/Hosting/DeviceFrameConnectionRegistry.cs`
- `src/Device.Server/Hosting/DeviceServerRuntimeConfig.cs`
- `src/Device.Server/Hosting/VisualUdpSender.cs`
- `src/Device.Server.Abstractions/Hosting/DeviceRecordMutations.cs`
- `src/Device.Server.Abstractions/Hosting/DeviceSessionState.cs`
- `src/Device.Client.Remote/RemoteDeviceFrameTransport.cs`
- `src/MicaAudio.Server/MicaAudioServerBootstrap.cs`
- `src/MicaAudio.Server/MicaAudioServerOptions.cs`
- `src/MicaAudio.Server/Dockerfile`
- `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
- `src/App.WinUI/Services/Devices/DeviceRefreshCoordinator.cs`
- `firmware/esp32s3-devkitc1/src/mica_network.cpp`
- `firmware/esp32s3-devkitc1/src/mica_network.h`
- `firmware/esp32s3-devkitc1/src/mica_panels.cpp`
- `firmware/esp32s3-devkitc1/src/mica_types.h`
- `firmware/esp32s3-devkitc1/src/mica_globals.cpp`
- `firmware/esp32s3-devkitc1/src/mica_globals.h`
- `firmware/esp32s3-devkitc1/src/mica_visual_udp.cpp`
- `firmware/esp32s3-devkitc1/src/mica_visual_udp.h`
- testes em `tests/Output.Tests/*`
- docs em `docs/wiki/reference/*`, `docs/wiki/modules/*` e `docs/wiki/architecture/08-render-cloud-migration-plan.md`

## Decisoes tomadas

1. UDP e opt-in, LAN-only e limitado a `StreamFrameV2.Bins128`.
2. `Frame128x64 RGB565` continua em WS/WebP batch porque um datagrama bruto de 16KB induz fragmentacao IP.
3. O HMAC do UDP usa o token do device e tag truncado de 16 bytes para autenticar frames descartaveis sem criar uma nova credencial.
4. O host so tenta UDP quando `PreferLanUdpVisualTransport=true`, o device esta online no MQTT/control plane, possui `LastKnownIp` privado e declarou `visualUdpMode = bins128`.
5. A fila WS do device passou de `1` para `3`, ainda com `DropOldest`, para absorver jitter curto sem acumular backlog antigo.
6. O caminho admin WS e o remote transport foram otimizados com `ArrayPool<byte>` e parsing por `ReadOnlySpan<byte>`.
7. O playback WebP no firmware passou a esperar deltas entre timestamps apos apresentar o frame, evitando catch-up quando o primeiro decode atrasa.

## Validacoes executadas

```text
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~StreamFrameV2Tests|FullyQualifiedName~DeviceFrameConnectionTests|FullyQualifiedName~DeviceSessionStateTests" -> aprovado (13 testes)
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~VisualUdpFrameV1Tests|FullyQualifiedName~DeviceServerRuntimeConfigTests|FullyQualifiedName~MicaAudioServerStandaloneTests" -> aprovado (20 testes)
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DeviceServerHostTargetedFrameTests|FullyQualifiedName~RemoteDeviceServerClientTests|FullyQualifiedName~DeviceSessionStateTests|FullyQualifiedName~VisualUdpFrameV1Tests|FullyQualifiedName~MicaAudioServerStandaloneTests" -> aprovado (26 testes)
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~DeviceServerHostTargetedFrameTests|FullyQualifiedName~RemoteDeviceServerClientTests|FullyQualifiedName~StreamFrameV2Tests|FullyQualifiedName~DeviceFrameConnectionTests|FullyQualifiedName~DeviceSessionStateTests|FullyQualifiedName~VisualUdpFrameV1Tests|FullyQualifiedName~DeviceServerRuntimeConfigTests|FullyQualifiedName~MicaAudioServerStandaloneTests" -> aprovado (40 testes)
platformio run -e esp32s3_devkitc1_dma_exp -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> aprovado
dotnet restore .\MicaAudio.sln -p:RestoreLockedMode=true -> aprovado
dotnet build .\MicaAudio.sln -c Debug -> aprovado (0 warnings, 0 errors)
docker build -f src\MicaAudio.Server\Dockerfile -t mica-audio-server:visual-udp-dev . -> aprovado
docker run -d --name mica-audio-server-visual-smoke -e PORT=8080 -e MICA_SERVER__PREFERLANUDPVISUALTRANSPORT=true -p 5282:8080 -p 5275:5273 -p 5274:5274/udp mica-audio-server:visual-udp-dev -> container iniciado
GET http://127.0.0.1:5282/api/v1/health -> ok
GET http://127.0.0.1:5282/api/v1/server/info -> httpBase=http://127.0.0.1:5282, mqttPort=5273
docker logs --tail 40 mica-audio-server-visual-smoke -> mostra UDP visual LAN habilitado: udp://127.0.0.1:5274 (bins128)
```

## Riscos e rollback

- Risco: UDP pode ser bloqueado por firewall local, NAT ou rede guest Wi-Fi. Rollback operacional: deixar `MICA_SERVER__PREFERLANUDPVISUALTRANSPORT=false` ou omitir `-p 5274:5274/udp`; WS continua funcionando.
- Risco: `LastKnownIp` pode ficar obsoleto em redes com troca frequente de IP. O host so usa UDP para devices online no control plane e volta ao WS se o envio UDP falhar.
- Risco: capability `visualUdpSupported` em firmware antigo vem ausente. O host interpreta `null` como sem suporte e nao tenta UDP.

## Proximos passos

1. Rebuildar a imagem Docker e rodar com `-p 5274:5274/udp` apenas quando for testar UDP LAN.
2. Medir lado a lado WS vs UDP em outro PC na LAN usando `hub75_fps`, `streamSequenceGapCount`, invalid frames e percepcao visual.
3. Se raw frames ainda forem necessarios, planejar tile/delta/compressao em pacotes pequenos, nao UDP bruto de 16KB.
