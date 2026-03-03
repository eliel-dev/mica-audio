## Objetivo

Facilitar builds futuros de firmware `.bin` no ambiente local, reduzindo dependencias do Python global para uso de PlatformIO no repositorio.

## Escopo classificado

`estrutural`

- Alteracao em `scripts/` para robustez do fluxo de build/merge de firmware.
- Regeneracao do artefato precompilado oficial do firmware.

## Arquivos alterados

- `scripts/build-precompiled-firmware.ps1`
- `src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-128x64-dma_exp_merged.bin`
- `firmware/esp32s3-devkitc1/.pio/build/project.checksum`

## Decisoes tomadas

1. O script de build passou a resolver automaticamente o PlatformIO na ordem:
   - `platformio` no PATH
   - `pio` no PATH
   - `~/.platformio/penv/Scripts/platformio.exe`
   - fallback `python -m platformio`
2. O merge do firmware passou a usar `platformio pkg exec -p tool-esptoolpy -- esptool.py`, evitando dependencia direta de `python -m esptool` no Python global.
3. O fluxo foi validado com `-SkipToolInstall`, mantendo compatibilidade com ambiente que ja possui PlatformIO instalado.

## Validacoes executadas

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -SkipToolInstall
```

Status:
- `docs-validate`: OK.
- `build-precompiled-firmware`: OK (build + merge do binario unico).

## Riscos e rollback

- Risco: diferencas de CLI do `esptool.py` entre versoes podem exigir ajuste de flags no merge.
- Risco: ambiente sem PlatformIO local/global ainda depende de instalacao via `pip` quando `-SkipToolInstall` nao e usado.
- Rollback: restaurar `scripts/build-precompiled-firmware.ps1` para versao anterior e regenerar artefato `merged.bin` com fluxo legado.

## Proximos passos

1. Validar o `merged.bin` gerado em gravacao real de um ESP32-S3 DevKitC-1.
2. Incluir no README um atalho operacional unico para build de firmware precompilado.
3. Opcional: adicionar job CI dedicado para verificar apenas o script de build precompilado.
