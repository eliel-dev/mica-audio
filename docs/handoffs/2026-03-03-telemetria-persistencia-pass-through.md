# Handoff - Entrega 1 Telemetria e Persistencia

## Objetivo

Entregar a telemetria estendida do firmware ate a persistencia local (protocol/server/store), mantendo pass-through no servidor sem renormalizacao dos campos de memoria.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite: firmware publica novas metricas, servidor propaga/persiste os valores como recebidos, snapshot/record expostos com compatibilidade para payload legado.

## Arquivos alterados

- firmware/matrixportal-s3/src/main.cpp
- src/Device.Protocol/Models/DeviceTelemetryMessage.cs
- src/Device.Protocol/Models/DeviceSnapshot.cs
- src/Device.Protocol/Models/DeviceRecord.cs
- src/Device.Server/Hosting/DeviceServerHost.Advanced.cs
- src/Device.Server/Hosting/DeviceServerHost.cs
- src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs
- tests/Output.Tests/DeviceTelemetryMessageTests.cs
- tests/Output.Tests/DeviceServerHostSecurityTests.cs

## Decisoes tomadas

1. Sanitizacao de `largestHeapBlockBytes` e `largestPsramBlockBytes` foi implementada exclusivamente no firmware, com omissao/clamp no emissor.
2. O servidor apenas repassa e persiste os campos de telemetria estendida, sem clamp/renormalizacao dos valores recebidos.
3. Os novos campos de protocolo/modelo foram mantidos como `nullable` para preservar compatibilidade com firmware legado.
4. A persistencia `devices.json` passou a fazer round-trip dos novos campos para manter o ultimo snapshot conhecido fora da sessao.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File ./scripts/docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File ./scripts/mvvm-validate.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -clp:ErrorsOnly -> OK (0 erros)
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceTelemetryMessageTests|FullyQualifiedName~TelemetryMetrics_ShouldPassThroughWithoutServerNormalization_AndPersistOnRecord" -> OK (4 aprovados)
python -m platformio run -e esp32s3_devkitc1_dma_exp --project-dir firmware/matrixportal-s3 -> OK
```

## Riscos e rollback

- Risco principal: diferencas de disponibilidade de PSRAM entre variantes de hardware podem alterar presenca dos campos `freePsramBytes`/`largestPsramBlockBytes` no payload.
- Como reverter: rollback dos arquivos acima para o commit anterior da entrega, restaurando o payload antigo e removendo os campos novos de protocolo/persistencia.

## Proximos passos

1. Implementar Entrega 2 (dashboard/logs na DevicesPage) consumindo os campos ja persistidos.
2. Atualizar documentacao de contrato de telemetria v2 em `docs/wiki/reference/device-telemetry-v2-fields.md`.
