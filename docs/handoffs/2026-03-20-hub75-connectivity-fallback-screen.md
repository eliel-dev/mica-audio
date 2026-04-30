# Handoff - HUB75 connectivity fallback screen

## Objetivo

Adicionar uma tela local de aviso/descanso no HUB75, renderizada pelo proprio ESP32-S3, para cobrir perda de conectividade operacional sem depender do desktop.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o painel mostra `SEM WIFI` quando nao houver Wi-Fi estavel;
  - o painel mostra `SEM SERV` quando houver Wi-Fi, mas nao houver sessao WebSocket estavel;
  - o painel mostra `SETUP WIFI` quando o portal de provisioning estiver ativo;
  - o fallback respeita debounce de `1000 ms` para `NoWifi` e `NoServer`;
  - ao voltar o stream normal, o fallback desaparece sem exigir reboot.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/handoffs/2026-03-20-hub75-connectivity-fallback-screen.md`

## Decisoes tomadas

1. O fallback ficou inteiramente no firmware:
   - sem mudanca em `Device.Server`, `Device.Protocol` ou no host WinUI;
   - sem mudanca em `Type 1` / `Type 2`.
2. O criterio visual considera apenas a conectividade que afeta o stream do HUB75:
   - `Portal`
   - `Wi-Fi`
   - `WebSocket`
   - MQTT nao entra no gatilho da tela.
3. `Portal` tem prioridade maxima e aparece imediatamente.
4. `NoWifi` e `NoServer` usam debounce fixo de `1000 ms` para evitar flicker em reconexoes curtas.
5. O fallback foi mantido minimalista e estatico:
   - icone simples;
   - titulo central;
   - subtitulo curto;
   - sem animacao.
6. O timeout atual de stream (`15 s`) continua fora do fallback:
   - se houver servidor conectado, mas faltar frame/sinal, o comportamento atual permanece.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp
```

## Riscos e rollback

- Risco principal: o painel `128x64` tem baixa resolucao, entao copy/espacamento/icone ainda podem precisar ajuste fino em hardware real.
- Risco secundario: a tela `SEM SERV` depende estritamente do `WebSocket`; se o transporte visual mudar no futuro, o criterio tambem precisara mudar.
- Rollback:
  - remover o `Hub75FallbackState` e os helpers associados do `main.cpp`;
  - remover a secao documental desta fase.

## Proximos passos

1. Validar em hardware os tres estados:
   - sem Wi-Fi
   - Wi-Fi sem servidor WS
   - portal ativo
2. Confirmar se o debounce de `1000 ms` evita flicker sem atrasar demais a percepcao do erro.
3. Se a leitura no painel ficar fraca, ajustar apenas copy/espacamento/icone, sem mudar a precedencia nem o criterio de estado.
