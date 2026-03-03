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
- As miniaturas ficam sempre animadas (nao dependem de hover ou selecao).
- Quando o app ativo do device e `visualizer-hub75`, a miniatura exibe frame real do `SimulatorLedOutput` via pump de 8 Hz.
- O painel da direita nao mostra mais preview maior; a leitura visual do app fica na miniatura da lista.
- O card `Dashboard ESP` mostra metrica do device selecionado: carga do loop, uptime, heap, PSRAM e rede.
- O dashboard tem leitura estilo NOC com linguagem visual Fluent: chips de estado com icones discretos e barras de tendencia da carga do loop.
- Em offline, o dashboard exibe o ultimo snapshot conhecido com aviso explicito de offline.
- `RSSI` e exibido apenas quando o device esta online.
- A linha da lista/selecionado nao exibe mais `IP` nem `RSSI`.
- O card `Logs do dispositivo` mostra somente eventos do `deviceId` selecionado.
- Sem selecao, dashboard e logs mostram placeholders dedicados e estaveis.


- Na tela, os botoes de acao do device ficam no card de resumo (`Testar LED` e `Remover`).
- A acao `Remover` e consolidada: online tenta revogar/reiniciar e remove local; offline remove apenas o registro local.

## Referencias adicionais

- [Campos de telemetria v2](../reference/device-telemetry-v2-fields.md#objetivo)
