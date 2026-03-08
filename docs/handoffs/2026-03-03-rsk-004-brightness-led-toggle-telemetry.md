# Handoff - RSK-004 + Brilho seguro + Toggle LED auxiliar + Telemetria observavel

## Objetivo

Entregar em um unico lote o controle de brilho seguro por device (`30..160`), substituir `Testar LED` por toggle persistente `Habilitar LED`, acoplar brilho do LED auxiliar ao slider quando habilitado, aumentar observabilidade de telemetria no dashboard e automatizar versionamento de firmware por `UTC date + tag + commit` com fallback estatico.

## Escopo classificado

- Tipo: `firmware_protocolo` (altera `firmware/`, `Device.Protocol`, `Device.Server`, `App.WinUI`, `scripts/` e `docs/`).
- Criterio de aceite:
- Toggle `Habilitar LED` funcional e persistente por device.
- Slider de brilho envia `set_brightness` com clamp seguro e atualiza painel + LED auxiliar (quando habilitado).
- Telemetria expoe heartbeat e campos de brilho/LED, refletindo no dashboard.
- Build de firmware gera versao automatica e `.bin` oficial atualizado.

## Arquivos alterados

- Firmware e build:
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/src/firmware_version.h`
- `scripts/build-precompiled-firmware.ps1`
- `.gitignore`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin`
- Protocolo/host/persistencia:
- `src/Device.Protocol/Models/DeviceCommandType.cs`
- `src/Device.Protocol/Models/DeviceTelemetryMessage.cs`
- `src/Device.Protocol/Models/DeviceRecord.cs`
- `src/Device.Protocol/Models/DeviceSnapshot.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- UI e smoke:
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- Testes unitarios:
- `tests/Output.Tests/DeviceOperationsCoordinatorBrightnessTests.cs` (novo)
- `tests/Output.Tests/DeviceTelemetryMessageTests.cs`
- `tests/Output.Tests/DeviceServerHostSecurityTests.cs`
- `tests/Output.Tests/DeviceOperationsCoordinatorDeviceLogsTests.cs`
- Documentacao:
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/device-telemetry-v2-fields.md`
- `docs/wiki/guides/operate-device-lifecycle.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/wiki/guides/criticality-context7-audit.md`
- ajustes de consistencia:
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/guides/setup-new-device.md`

## Decisoes tomadas

1. Limites de brilho seguros fixos no firmware/app em `30..160` para reduzir risco termico/consumo e manter previsibilidade operacional.
2. `test_led` preserva compatibilidade legado: sem parametros continua pulso curto; com `enabled` ativa/desativa modo continuo persistente.
3. O duty do LED auxiliar foi acoplado ao brilho efetivo aplicado (`brightnessApplied`) para refletir cap seguro e estado real do painel.
4. `telemetrySequence` foi adicionado como heartbeat monotono para facilitar diagnostico visual de "dashboard parado".
5. Versionamento do firmware ficou automatico no script de build (`vYYYY.MM.DD-tag-commit`) com fallback estatico em header versionado no repo.

## Validacoes executadas

```text
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceTelemetryMessageTests|FullyQualifiedName~DeviceServerHostSecurityTests|FullyQualifiedName~DeviceOperationsCoordinator|FullyQualifiedName~Hub75VisualizerSessionServiceTests" -> OK (34 passed)
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests" -> OK (4 passed)
powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
```

Evidencia do artefato oficial gerado:

```text
arquivo: src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin
firmwareVersion gerada no build: v2026.03.04-untagged-54227c7
timestamp UTC do bin: 2026-03-04T00:12:14Z
tamanho: 1120080 bytes
SHA-256: 751D8514919C1237399523FB5640DC73FFB33B92F3448E7B768B623A91935E2A
```

## Riscos e rollback

- Risco principal: firmware antigo em campo sem novos campos de telemetria ou sem suporte a `set_brightness` pode apresentar dashboard parcial e comandos sem efeito.
- Como reverter:
- Reflash para `.bin` anterior conhecido estavel.
- Em caso de urgencia, manter fallback estatico de versao (`firmware_version.h`) e remover header auto no build.
- Compatibilidade legado de `test_led` sem parametros permanece disponivel para clientes antigos.

## Proximos passos

1. Flash manual em device real e validar checklist: toggle LED persistente, slider de brilho, heartbeat crescente e campos de brilho no dashboard.
2. Opcional: adicionar telemetria de temperatura/consumo para correlacionar brilho seguro com carga real de campo.
3. Avaliar warning de redefinicao `WEBSOCKETS_MAX_DATA_SIZE` na lib WS para eliminar ruido de build.
