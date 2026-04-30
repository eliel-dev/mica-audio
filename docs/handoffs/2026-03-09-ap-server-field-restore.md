# Handoff de mudanca estrutural

## Objetivo

Restaurar a edicao manual do campo `Servidor` no portal AP do firmware ESP32-S3, preservando o cutover MQTT do control plane e o WS binario exclusivo para stream visual.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o portal AP volta a exibir um campo `Servidor`;
  - o firmware aceita `http://host:porta`, `host:porta` e `host`, com default `5272`;
  - host salvo valido nao e apagado quando o campo vier vazio ou invalido;
  - o pacote precompilado embarcado no app passa a incluir esse firmware atualizado.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/src/firmware_version.h`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

1. O portal AP voltou com um unico campo `Servidor`, em vez de separar host e porta, para reaproveitar o parser existente de `serverBaseUrl`.
2. Quando o valor digitado no portal vem vazio ou invalido, o firmware preserva um host salvo valido e registra um `lastWifiEvent` dedicado (`portal_server_*`) para diagnostico em campo.
3. Ao aceitar um novo `Servidor`, o firmware tambem reseta o fallback MQTT local para `host + 5273 + mica/v1/devices` antes do pareamento, garantindo coerencia quando o device reprovisiona sem novo pair code.
4. O pacote `merged.bin + manifesto` embarcado no app foi regenerado para que o wizard USB realmente grave a versao com o campo restaurado.

## Validacoes executadas

```text
platformio run -e esp32s3_devkitc1_dma_exp -> sucesso
python -m esptool --chip esp32s3 merge_bin -o src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin ... -> sucesso
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> sucesso
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> sucesso
dotnet build MicaAudio.sln -c Debug -nologo -> sucesso
```

## Riscos e rollback

- Risco principal: o portal AP fecha apos associar ao Wi-Fi; em caso de `Servidor` vazio/invalido sem host salvo anterior, o diagnostico fica concentrado em serial/`lastWifiEvent` ate novo reprovisionamento.
- Como reverter:
  - remover o campo `Servidor` do `WiFiManager` em `main.cpp`;
  - restaurar o `merged.bin` e o manifesto anteriores em `src/App.WinUI/AppData/Firmware/`;
  - voltar a documentacao do setup/AP para o fluxo sem edicao manual do host.

## Proximos passos

1. Rodar smoke manual no hardware informando `http://192.168.1.16:5272` no portal AP e confirmar que o device aparece na UI.
2. Se o reprovisionamento sem pair code for um fluxo frequente, considerar telemetry/log mais visivel no app para `portal_server_invalid` e `portal_server_missing`.
3. Quando houver novo ciclo oficial de pacote, manter o `merged.bin` do app sincronizado com qualquer ajuste futuro do portal AP.
