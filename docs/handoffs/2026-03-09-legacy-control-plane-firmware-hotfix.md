# Handoff de mudanca estrutural

## Objetivo

Corrigir o onboarding de devices novos que apareciam offline por uso de firmware precompilado anterior ao cutover MQTT e melhorar o diagnostico de firmware legado no host/UI.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - onboarding passa a validar `BIN + manifesto` antes do flash;
  - pacote precompilado oficial embarcado no app passa a ser compativel com MQTT control plane;
  - snapshot diferencia `MqttOnline`, `LegacyOnly` e `Offline`;
  - UI deixa de mostrar offline generico quando houver trafego legado recente sem MQTT.

## Arquivos alterados

- `src/Device.Protocol/Models/DeviceControlPlaneState.cs`
- `src/Device.Protocol/Models/DeviceSnapshot.cs`
- `src/Device.Server/Hosting/DeviceRecordMutations.cs`
- `src/Device.Server/Hosting/DeviceSession.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/App.WinUI/Services/Devices/DeviceLifecyclePolicy.cs`
- `src/App.WinUI/Services/Devices/DeviceMetricsFormatter.cs`
- `src/App.WinUI/Services/Devices/DeviceLogBook.cs`
- `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
- `src/App.WinUI/Services/Firmware/FirmwareArtifactManifest.cs`
- `src/App.WinUI/Services/Firmware/ResolvedFirmwareArtifact.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`
- `firmware/esp32s3-devkitc1/src/firmware_version.h`
- `tests/Output.Tests/DeviceSessionTests.cs`
- `tests/Output.Tests/DeviceServerHostMqttTests.cs`
- `tests/Output.Tests/DeviceServerHostSecurityTests.cs`
- `tests/Output.Tests/PrecompiledFirmwareServiceTests.cs`
- `tests/Output.Tests/OnboardingObservabilityTests.cs`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

1. O diagnostico de compatibilidade foi separado do `DeviceStatus` por meio de `DeviceControlPlaneState`, preservando contratos existentes que dependem de `Online/Offline`.
2. O estado `LegacyOnly` e efemero e nasce apenas de trafego real via WS-texto ou `/api/v1/device/command-ack` sem MQTT ativo.
3. O onboarding passou a tratar o firmware precompilado como um pacote `BIN + manifesto`, exigindo `controlPlane = mqtt` antes do flash.
4. O hotfix atualizou o `merged.bin` oficial e o fallback de versao do firmware para reduzir ambiguidade em diagnostico de campo.

## Validacoes executadas

```text
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug -nologo -> sucesso
platformio run -e esp32s3_devkitc1_dma_exp -> sucesso
python -m esptool --chip esp32s3 merge_bin -o src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin ... -> sucesso
```

## Riscos e rollback

- Risco principal: devices antigos em campo continuam aparecendo como `Firmware legado` ate novo flash, mesmo com stream visual ativo.
- Como reverter:
  - restaurar o `.bin` e o manifesto anteriores em `src/App.WinUI/AppData/Firmware/`;
  - remover o gating de manifesto no onboarding;
  - remover `DeviceControlPlaneState` e voltar ao diagnostico binario online/offline.

## Proximos passos

1. Rodar smoke manual completo com hardware regravado e confirmar `Online` apos setup.
2. Se o processo de build oficial do firmware ja gerar `firmware_version.auto.h`, substituir o update manual do fallback por automacao do pacote precompilado.
3. Considerar exibir a versao do manifesto no wizard para facilitar diagnostico de suporte.
