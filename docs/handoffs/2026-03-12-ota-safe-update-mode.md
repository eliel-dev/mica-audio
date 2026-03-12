# Handoff - OTA Safe Update Mode (ESP32-S3)

## Objetivo

Alinhar o fluxo oficial de OTA do ESP32-S3 com `Safe update mode` da Espressif, fechando o ciclo `pending verify -> validated/rolled-back` e eliminando o falso sucesso no estagio `rebooting`.

## Escopo classificado

- Tipo: estrutural + firmware/protocolo
- Criterio de aceite:
  - firmware trata `ESP_OTA_IMG_PENDING_VERIFY`;
  - firmware confirma a nova imagem com `esp_ota_mark_app_valid_cancel_rollback()` apos self-test local de `10 s`;
  - firmware usa `esp_ota_mark_app_invalid_rollback_and_reboot()` no caminho de falha local do safe mode;
  - `update_firmware` so conclui com sucesso no host quando o device publica `validated`;
  - rollback automatico volta no firmware anterior e conclui o tracked command como falha.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `src/App.WinUI/Views/DevicesPage.FirmwareUpdate.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsText.cs`
- `tests/Output.Tests/DeviceServerHostMqttTests.cs`
- `tests/Output.Tests/DeviceServerTestHarness.cs`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/reference/device-observability-dashboard.md`

## Decisoes tomadas

1. A validacao foi feita contra a documentacao oficial de OTA da Espressif para `ESP32-S3`, com a regra de `Safe update mode` como fonte primaria.
2. O download/escrita da imagem continua em `Update`, mas o controle de estado seguro passa a usar explicitamente as APIs de OTA do ESP-IDF.
3. O self-test oficial ficou local e minimo:
   - `10 s` de boot continuo;
   - sem depender de Wi-Fi/MQTT/WS;
   - sem transformar problema de rede em rollback.
4. O app nao ganhou novo estado visual dedicado; a entrega reaproveita `command-events` e logs do dispositivo.
5. O mesmo `commandId` passou a atravessar o reboot para que o host feche a operacao com `validated` ou `rolled-back`.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --no-build --filter "FullyQualifiedName~DeviceServerHostMqttTests|FullyQualifiedName~DeviceOperationsCoordinatorBrightnessTests" -> OK (17 testes)
C:\Users\eliels\AppData\Local\Programs\Python\Python313\Scripts\pio.exe run -e esp32s3_devkitc1_dma_exp -> OK
```

## Riscos e rollback

- Risco principal: como `command-events` continuam no canal MQTT existente, a validacao local pode concluir com sucesso antes de o host receber o evento final, levando a timeout no app mesmo com firmware novo valido.
- Como reverter:
  - remover o ciclo `pending verify` do firmware;
  - voltar `rebooting` a ser terminal no tracked command;
  - remover o contexto OTA persistido em `Preferences`;
  - restaurar o criterio antigo de sucesso no app.

## Proximos passos

1. Testar em hardware real os cenarios de reboot normal, rollback automatico e rede indisponivel no primeiro boot.
2. Se `command-events` QoS atual se mostrar fragil para o evento final, considerar endurecer a entrega do resultado pos-reboot sem mudar a UX.
3. Se houver necessidade futura de hardening adicional, avaliar `secure boot`, OTA assinada e `anti-rollback` separadamente.
