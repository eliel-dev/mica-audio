# Handoff - RSK-002 WS header cutover com legado OFF por default

## Objetivo

Fechar o risco `RSK-002` migrando autenticacao WebSocket para header no firmware oficial, desligando fallback legado de query-token por default no servidor e mantendo rollback emergencial por `settings.json`.

## Escopo classificado

- Tipo: firmware_protocolo
- Criterio de aceite: WS por header funcional, query token legado rejeitado por default, rollback via setting validado, `.bin` oficial regenerado e rastreavel.

## Arquivos alterados

- `src/Device.Protocol/Contracts/ServerConfig.cs`
- `src/MicaAudio.Core/Presets/AppSettings.cs`
- `src/App.WinUI/Services/AppSettingsDomainService.cs`
- `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `tests/Output.Tests/DeviceServerHostSecurityTests.cs`
- `tests/Output.Tests/AppSettingsDomainServiceTests.cs`
- `tests/Output.Tests/DeviceIntegrationServiceLegacyWsSettingTests.cs`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/settings-presets-persistence.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/wiki/guides/criticality-context7-audit.md`
- `docs/handoffs/2026-03-03-rsk-002-ws-header-cutover.md`

## Decisoes tomadas

1. `AllowLegacyWebSocketQueryToken` ficou com default seguro OFF no `ServerConfig`.
2. Fallback legado por query foi mantido no servidor apenas para rollback operacional quando flag estiver `true`.
3. O rollback foi ligado ao `settings.json` por novo campo em `AppSettings`: `AllowLegacyWebSocketQueryToken`.
4. O `DeviceIntegrationService` agora repassa a flag para `ServerConfig` e emite warning quando legado estiver reativado.
5. O firmware oficial `esp32s3_devkitc1_dma_exp` passou a abrir WS em `/ws/v1/stream` com headers `X-Device-Id` e `X-Device-Token` (sem token na query).
6. A versao do firmware foi carimbada como `v2026.03.03-rsk002-ws-header`.
7. A cobertura de testes foi expandida para:
   - rejeicao de query token por default;
   - aceitacao por header com legado desligado;
   - preservacao e toggling da nova setting;
   - wiring da setting ate o `StartAsync` do host.

## Validacoes executadas

```text
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceServerHostSecurityTests" -> OK (20/20)
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~AppSettingsDomainServiceTests|FullyQualifiedName~DeviceIntegrationService" -> OK (6/6)
powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK (133 warnings, 0 errors)
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
```

Metadados do artefato oficial:

```text
File: src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin
SHA256: EF3155681F12B253E9E81D897DCD1B94503F4FDCF279A61752E58453503AEE38
Size: 1111120 bytes
LastWriteUtc: 2026-03-03T22:30:13.2203279Z
FirmwareVersion: v2026.03.03-rsk002-ws-header
```

## Riscos e rollback

- Risco residual: devices antigos em campo que ainda conectam WS por query podem falhar com `401` apos upgrade.
- Mitigacao: atualizar firmware para handshake por header; manter troubleshooting documentado.
- Rollback emergencial sem recompilar:
  - editar `%AppData%\MicaAudio\settings.json`
  - setar `"AllowLegacyWebSocketQueryToken": true`
  - reiniciar app/servidor local
- Encerrado o incidente: voltar flag para `false` e validar reconnect por header.

## Proximos passos

1. Monitorar no proximo ciclo de release ocorrencias de `401` em handshake WS para confirmar estabilizacao da migracao.
2. Planejar fase N+2 para remover definitivamente fallback de query token no servidor, apos janela de compatibilidade de campo.
