# Handoff - P0 Revisado (Testar LED + Wi-Fi/AP + Dashboard WinUI)

## Objetivo

Aplicar hotfix operacional para:
1. restaurar a UX de campo com botao `Testar LED` (pulso curto);
2. suportar LED onboard WS2812 no ESP32-S3 e manter LED auxiliar opcional por GPIO;
3. estabilizar provisioning/AP (sem loop de reboot e sem fallback indevido por oscilacao apenas de WS);
4. manter dashboard WinUI observavel com heartbeat e estados de conectividade.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Superficie alterada:
  - firmware (`firmware/esp32s3-devkitc1`)
  - protocolo/host (`src/Device.Protocol`, `src/Device.Server`)
  - UI/store/coordinator (`src/App.WinUI`)
  - testes (`tests/Output.Tests`, `tests/Integration.Smoke`)
  - wiki + handoff.

## Arquivos alterados

- Firmware:
  - `firmware/esp32s3-devkitc1/src/main.cpp`
  - `firmware/esp32s3-devkitc1/platformio.ini` (mantido default seguro `MICA_TEST_LED_GPIO=-1`)
- Protocolo/host/store:
  - `src/Device.Protocol/Models/DeviceTelemetryMessage.cs`
  - `src/Device.Protocol/Models/DeviceRecord.cs`
  - `src/Device.Protocol/Models/DeviceSnapshot.cs`
  - `src/Device.Server/Hosting/DeviceServerHost.cs`
  - `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
  - `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
  - `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- WinUI:
  - `src/App.WinUI/Views/DevicesPage.Ui.cs`
  - `src/App.WinUI/Views/DevicesPage.xaml.cs`
  - `src/App.WinUI/Services/Devices/DeviceMetricsFormatter.cs`
- Testes:
  - `tests/Output.Tests/DeviceTelemetryMessageTests.cs`
  - `tests/Output.Tests/DeviceServerHostSecurityTests.cs`
  - `tests/Output.Tests/DeviceOperationsCoordinatorBrightnessTests.cs`
  - `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- Docs:
  - `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
  - `docs/wiki/modules/device-server-protocol.md`
  - `docs/wiki/reference/device-telemetry-v2-fields.md`
  - `docs/wiki/modules/app-winui.md`
  - `docs/wiki/guides/operate-device-lifecycle.md`
  - `docs/wiki/reference/troubleshooting-matrix.md`
  - `docs/wiki/guides/criticality-context7-audit.md`

## Decisoes tomadas

1. A UI voltou para botao momentaneo `Testar LED` (sem toggle continuo na tela).
2. O firmware passou a considerar LED de teste disponivel quando houver:
   - LED onboard WS2812, ou
   - LED auxiliar por GPIO valido.
3. O comando `test_led`:
   - sem parametros: pulso curto (caminho principal);
   - com `enabled`: aceito como compat legado, sem depender de modo continuo na UI.
4. Foi adicionado `testLedAvailable` na telemetria/snapshot/record/store.
5. Provisioning:
   - manteve `setConfigPortalTimeout(0)` e sem reboot automatico no fail de `autoConnect`;
   - fallback para portal apenas por perda sustentada de Wi-Fi;
   - desconexao de WS agora tenta reconectar WS sem abrir portal automaticamente.
6. Telemetria e dashboard mantiveram foco em observabilidade operacional (`wifiState`, `provisioningPortalActive`, heartbeat e eventos curtos).

## Validacoes executadas

1. `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceTelemetryMessageTests|FullyQualifiedName~DeviceOperationsCoordinatorBrightnessTests|FullyQualifiedName~DeviceServerHostSecurityTests"`
   - Resultado: **OK** (28 aprovados, 0 falhas).
2. `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests"`
   - Resultado: **OK** (4 aprovados, 0 falhas).
3. `platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1`
   - Resultado: **OK** (build firmware concluido).
4. `powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1`
   - Resultado: **OK**.
   - Firmware version gerada no build: `v2026.03.04-untagged-54227c7`.
   - BIN oficial atualizado: `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin`.
   - Tamanho: `1118528` bytes.
   - SHA-256: `C97EFA1F00B4E378B9E92539B881D6299A4F1A0343B428D9C500005BDDAA3213`.
   - Timestamp UTC: `2026-03-04T18:29:35Z`.
5. `dotnet build MicaAudio.sln -c Debug`
   - Resultado: **OK**.
6. `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
   - Resultado: **OK**.
7. `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
   - Resultado: **OK**.

## Riscos e rollback

1. Risco: variação de comportamento de provisioning entre APs/roteadores.
   - Mitigacao: logs seriais de conectividade + estados canônicos no dashboard.
2. Risco: uso incorreto de GPIO auxiliar por build flag em ambiente de campo.
   - Mitigacao: default seguro `MICA_TEST_LED_GPIO=-1` e validacao runtime de pino.
3. Rollback:
   - reflash do BIN estavel anterior;
   - manter fallback seguro de pinagem auxiliar desabilitada;
   - no app, rollback da UX e simples (botao de teste sem dependencia de modo continuo).

## Proximos passos

1. Executar smoke manual de bancada em pelo menos 2 ciclos:
   - boot sem credenciais;
   - provisionar com credencial valida;
   - validar `Testar LED`;
   - forcar queda de Wi-Fi e confirmar retorno ao portal.
2. Validar em campo a visibilidade do AP `MicaAudio-Setup-*` em cenarios de erro real.
3. Se estavel, publicar release com o BIN gerado nesta entrega.
