# Handoff - Client-owned LAN data plane and session ownership

## Objetivo

Unificar o Mica em torno de `server = control plane`, `cliente = data plane LAN` e ownership explicito por device no firmware ESP32-S3.

## Escopo classificado

- Tipo: firmware/protocolo + documental
- Criterio de aceite:
  - firmware compila com runtime de ownership/shadow/lock lease;
  - `StreamFrameV3` e `DeviceSessionShadowMessage` entram no contrato;
  - docs canonicas passam a tratar o server como control plane e o cliente local como data plane LAN.

## Arquivos alterados

- firmware/esp32s3-devkitc1/src/main.cpp
- firmware/esp32s3-devkitc1/src/mica_commands.cpp
- firmware/esp32s3-devkitc1/src/mica_display.cpp
- firmware/esp32s3-devkitc1/src/mica_globals.cpp
- firmware/esp32s3-devkitc1/src/mica_globals.h
- firmware/esp32s3-devkitc1/src/mica_network.cpp
- firmware/esp32s3-devkitc1/src/mica_session.cpp
- firmware/esp32s3-devkitc1/src/mica_session.h
- firmware/esp32s3-devkitc1/src/mica_types.h
- firmware/esp32s3-devkitc1/src/mica_visual_udp.cpp
- src/Device.Protocol/Models/DeviceSessionShadowMessage.cs
- src/Device.Protocol/Models/DeviceTelemetryMessage.cs
- src/Device.Protocol/Stream/StreamFrameV3.cs
- src/Device.Protocol/Stream/VisualUdpFrameV1.cs
- tests/Output.Tests/DeviceSessionShadowMessageTests.cs
- tests/Output.Tests/DeviceTelemetryMessageTests.cs
- tests/Output.Tests/StreamFrameV3Tests.cs
- tests/Output.Tests/VisualUdpFrameV1Tests.cs
- docs/adr/0010-client-owned-lan-data-plane.md
- docs/wiki/architecture/01-system-overview.md
- docs/wiki/architecture/02-runtime-lifecycle.md
- docs/wiki/architecture/07-cloud-first-multi-panel-future-architecture.md
- docs/wiki/architecture/08-render-cloud-migration-plan.md
- docs/wiki/modules/app-winui.md
- docs/wiki/modules/device-server-protocol.md
- docs/wiki/modules/firmware-esp32s3-devkitc1.md
- docs/wiki/modules/output-led.md
- docs/wiki/modules/paineis.md
- docs/wiki/modules/server-build-and-artifacts.md
- docs/wiki/reference/cloud-first-control-plane-gap-map.md
- docs/wiki/reference/code-index.md
- docs/wiki/reference/device-telemetry-v2-fields.md
- docs/wiki/reference/ws-protocol-v2.md
- README.md

## Decisoes tomadas

1. `StreamFrameV2` foi preservado como wire legado; `StreamFrameV3` entrou como wire owner-bound com `ownerEpoch`.
2. Ownership novo nao quebra o baseline atual: comandos sem `clientId` continuam no caminho legado e o firmware so endurece o stream owner-bound quando existe owner ativo.
3. `MQTT` virou o plano canonico de sessao no firmware, com `shadow` retained, `last-writer-wins` e lock com lease.
4. O fallback visual `ClientDisconnected` foi separado de `NoServer`, para distinguir perda de owner de perda do control plane.
5. A wiki passou a marcar explicitamente `direcao oficial` vs `baseline atual / transicao`, evitando documentar o caminho legado como topologia final.

## Validacoes executadas

```text
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~StreamFrameV3Tests|FullyQualifiedName~VisualUdpFrameV1Tests|FullyQualifiedName~DeviceTelemetryMessageTests|FullyQualifiedName~DeviceSessionShadowMessageTests" -> passed
platformio run -e esp32s3_devkitc1_dma_exp -> passed
```

## Riscos e rollback

- Risco principal: cliente session-aware precisa observar `shadow` antes de usar `StreamFrameV3`, senao vai enviar `ownerEpoch` stale.
- Como reverter:
  - parar de usar `clientId/ownerEpoch/lockToken` no cliente;
  - continuar apenas no caminho legado `StreamFrameV2` + comandos antigos;
  - remover o modulo `mica_session.*` em um rollback dedicado se necessario.

## Proximos passos

1. Fazer o cliente Windows/Android observar `shadow` e enviar `session_heartbeat`.
2. Introduzir direct path real `cliente -> ESP` para visualizador e paineis, usando `StreamFrameV3`.
3. Decidir se o modo legado via server tera prazo de deprecacao ou convivencia longa.
