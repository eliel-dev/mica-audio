# Guia - Setup New Device

## Objetivo

Documentar o fluxo oficial de setup para o painel HUB75 `128x64`.

## Passos

1. Selecionar a placa `ESP32-S3 DevKitC-1 / WROOM-1`.
2. Selecionar o painel `P2.5 128x64 (320x160mm, SMD2121, HUB75, 1/32 scan)`.
3. Usar o unico perfil oficial `dma_exp`.
4. Gravar o BIN `esp32s3-devkitc1-128x64-dma_exp_merged.bin`.
5. Na tela `Dispositivos`, usar o botao `Baixar firmware` para salvar esse BIN localmente.
6. Validar na telemetria:
   - `boardModel = esp32s3_devkitc1`
   - `panelType = hub75_p2_5_128x64_smd2121_scan32`
   - campos v2 basicos chegando no server (`uptimeSeconds`, `loopLoadPercent`, `freeHeapBytes`, `wifiConnected`)

## Referencias de codigo

- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)
- [DeviceConfigResponse](../../../src/Device.Protocol/Models/DeviceConfigResponse.cs#L1)

## Checklist rapido

- Catalogo mostra apenas DevKitC-1.
- Painel `64x32` nao aparece no fluxo ativo.
- O perfil `stable` nao aparece mais no fluxo ativo.
- Telemetria retorna o `panelType` canonico.


## Tela Dispositivos

- Cada dispositivo online mostra uma miniatura pequena do app ativo na lista; dispositivos offline nao exibem preview visual.
- O painel da direita mostra um preview maior apenas para dispositivos online; offline mostra placeholder e o ultimo app conhecido apenas como texto.
- Sem selecao, o preview maior fica em placeholder ate escolher um device.
- O card `Dashboard ESP` mostra metrica do device selecionado: carga do loop, uptime, heap, PSRAM e rede.
- Em offline, o dashboard exibe o ultimo snapshot conhecido com aviso explicito de offline.
- O card `Logs do dispositivo` mostra somente eventos do `deviceId` selecionado.
- Sem selecao, dashboard e logs mostram placeholders dedicados e estaveis.


- A acao Remover apaga apenas o registro local do app; para o ESP online, continue usando Revogar quando quiser alterar o dispositivo fisico.

## Referencias adicionais

- [Campos de telemetria v2](../reference/device-telemetry-v2-fields.md#objetivo)
