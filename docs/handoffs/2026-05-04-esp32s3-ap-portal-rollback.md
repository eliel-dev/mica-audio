# Handoff - 2026-05-04 - ESP32-S3 AP Portal Rollback

## Objetivo

Voltar o onboarding do ESP32-S3 para o fluxo AP manual, removendo o caminho experimental de `config.json`/factory local com credenciais Wi-Fi embutidas.

## Escopo classificado

**Firmware/protocolo** - altera firmware ESP32-S3 e pipeline de build, sem mudar DTO/API/wire protocol.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp` - remove leitura de `config.json` no boot e volta a abrir AP sempre que o provisioning estiver incompleto.
- `firmware/esp32s3-devkitc1/src/mica_fs_config.h/.cpp` - removidos.
- `firmware/esp32s3-devkitc1/data/config.json` - removido.
- `firmware/esp32s3-devkitc1/platformio.ini` - remove `data_dir` e `board_build.filesystem = fatfs`.
- `scripts/build-precompiled-firmware.ps1` - remove `-IncludeLocalFsConfig` e factory local.
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md` - documenta rollback para AP manual.
- `docs/wiki/guides/build-export-firmware.md` e `docs/wiki/guides/setup-new-device.md` - removem instrucoes do factory local.
- `docs/wiki/reference/code-index.md` - remove referencias ao FS config.

## Decisoes tomadas

1. O caminho oficial volta a ser AP `MicaAudio-Setup-xxxx` para preencher Wi-Fi, nome e `Servidor`.
2. `config.json` deixa de participar do boot, evitando que credenciais locais pulem o portal AP.
3. O pacote precompilado volta a ser somente firmware generico.
4. Discovery LAN e auto-registro permanecem apos o portal conectar o Wi-Fi.

## Validacoes executadas

Executadas nesta edicao:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug
powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -SkipToolInstall -OutputRoot .\.artifacts\firmware-ap-rollback
```

- `docs-validate.ps1`: OK.
- `ai-governance-check.ps1`: OK.
- `dotnet build MicaAudio.sln -c Debug`: OK; a execucao dentro do sandbox falhou antes da compilacao por acesso negado a `C:\Users\CodexSandboxOffline\.dotnet`, e a execucao fora do sandbox compilou com sucesso.
- `build-precompiled-firmware.ps1`: OK fora do sandbox, necessario porque PlatformIO usa cache/lock em `C:\Users\eliels\.platformio`.
- Guard estatico: OK; `src/main.cpp` nao contem mais `tryLoadFsConfig`, `mica_fs_config` ou `config.json`.

## Riscos e rollback

- **Risco operacional**: se o NVS antigo ainda tiver credenciais parciais, fazer erase total antes de gravar para forcar o AP limpo.
- **Rollback tecnico**: restaurar `mica_fs_config.*`, `data/config.json`, `board_build.filesystem = fatfs` e a chamada `tryLoadFsConfig()` em `main.cpp`.

## Proximos passos

- Gerar o BIN oficial e gravar no ESP32-S3.
- Se o serial mostrar apenas ROM/bootloader e nenhum `[boot]`, confirmar que o BIN correto foi gravado em `0x0` apos erase total.
