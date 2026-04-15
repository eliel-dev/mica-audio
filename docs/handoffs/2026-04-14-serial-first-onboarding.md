# Handoff - 2026-04-14 - serial-first-onboarding

## Objetivo

Migrar o onboarding oficial do desktop para `USB serial-first`, escondendo o `pair code` do usuario, usando `serverBaseUrl` automatico e rebaixando o AP do ESP32 a fallback manual/operacional.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Inclui: `App.WinUI` (wizard + onboarding service + serial provisioning), firmware ESP32-S3 (`boot`, `network poll`, `provisioning serial`) e documentacao operacional.
- Nao inclui: remocao do `pair code` no backend, novo endpoint de emissao de credenciais ou fluxo mobile/celular.

## Arquivos alterados

- `src/App.WinUI/Services/Devices/Onboarding/DeviceOnboardingModels.cs`
- `src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Infrastructure/Serial/SerialProvisioningClient.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.Onboarding.cs`
- `firmware/esp32s3-devkitc1/src/mica_types.h`
- `firmware/esp32s3-devkitc1/src/mica_globals.h`
- `firmware/esp32s3-devkitc1/src/mica_globals.cpp`
- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/src/mica_network.cpp`
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp`
- `tests/Integration.Smoke/DeviceUsbOnboardingServiceTests.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`

## Decisoes tomadas

1. O wizard desktop agora coleta `porta COM`, `SSID`, `senha Wi-Fi` e `nome do device` opcional antes do flash.
2. `DeviceUsbOnboardingService` so conclui com sucesso depois de `flash + provisioning serial`.
3. O `pair code` continua existindo para o backend, mas passou a ser gerado internamente sem exposicao em UI nem log operacional.
4. O `serverBaseUrl` do desktop continua automatico e nao ganhou override na UI; se o app resolver loopback, o onboarding falha cedo.
5. O firmware agora espera uma janela serial-first de `60 s` no boot limpo antes de abrir o AP.
6. O payload serial passou a aceitar `deviceName` opcional e persisti-lo no NVS antes do pareamento HTTP.

## Validacoes executadas

- `get_errors` nos arquivos alterados do app -> sem erros.
- `get_errors` nos arquivos alterados do firmware -> sem erros.
- `get_errors` nos testes alterados -> sem erros.
- Validacoes completas (`docs-validate`, `ai-governance-check`, `dotnet build`, `dotnet test`, `platformio run`) ficaram pendentes para execucao ao fim da implementacao.

## Riscos e rollback

- Risco: o desktop resolver `127.0.0.1` ou `localhost` como host publico do servidor.
  - Mitigacao: o onboarding falha antes do flash e orienta usar uma interface LAN/Wi-Fi valida.
- Risco: o provisioning serial falhar no boot limpo e o usuario achar que o device travou.
  - Mitigacao: o firmware abre o AP automaticamente apos expirar a janela serial-first.
- Risco: regressao em fluxos antigos que ainda esperavam modal com `pair code`.
  - Mitigacao: smoke tests do service e do wizard foram atualizados para o novo contrato.
- Rollback:
  1. restaurar o wizard AP-first no `App.WinUI`;
  2. restaurar a abertura imediata do AP em `main.cpp`;
  3. remover a janela serial-first do firmware e o `deviceName` opcional do payload serial.

## Proximos passos

1. Rodar `docs-validate`, `ai-governance-check`, `dotnet build` e `dotnet test` no workspace.
2. Rodar `platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1` para validar o firmware alterado.
3. Fazer smoke manual em bancada: flash, provisioning serial com Wi-Fi valido, device online no dashboard e abertura do AP apenas apos expirar a janela serial-first.