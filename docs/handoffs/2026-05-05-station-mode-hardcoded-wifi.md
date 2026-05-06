## Objetivo

Remover o modo AP (Access Point) e o portal de provisioning via WiFiManager do firmware ESP32-S3, substituindo por conexao direta em modo station (STA) com credenciais Wi-Fi e endereco do servidor hardcoded no codigo-fonte. O auto-registro por discovery LAN continua responsavel por obter `deviceId` e `token`.

## Escopo classificado

**Estrutural** - altera arquitetura de boot do firmware, contrato de configuracao e dependencias de build.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/mica_config.h` (novo) - define SSID, senha Wi-Fi, IP e porta do servidor
- `firmware/esp32s3-devkitc1/src/main.cpp` - boot simplificado: conecta direto em STA com credenciais hardcoded; remove fallback para portal AP
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp` - remove `WiFiManager` e logica de portal AP; `startProvisioningPortal` agora apenas reconecta Wi-Fi e aplica servidor
- `firmware/esp32s3-devkitc1/platformio.ini` - remove `tzapu/WiFiManager` de `lib_deps`

## Decisoes tomadas

1. **Manter framework Arduino**: em vez de migrar para APIs ESP-IDF puras (`esp_wifi.h`), mantivemos `WiFi.h` do Arduino-ESP32 para minimizar risco de regressao no loop principal e nas tasks existentes.
2. **Hardcoded como fallback**: `reloadProvisioningStateFromPrefs` usa `MICA_SERVER_HOST`/`MICA_SERVER_PORT` como default quando nao ha valores salvos em NVS, permitindo que OTA ou comandos futuros sobrescrevam sem reflash.
3. **Discovery LAN preservado**: `deviceId`/`token` continuam obtidos via broadcast UDP; o dispositivo inicia discovery automaticamente apos conectar no Wi-Fi.
4. **Portal removido, nao apenas desabilitado**: toda a logica de `WiFiManager` foi removida para reduzir tamanho do binario e memoria heap.

## Validacoes executadas

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
# OK: nenhuma falha encontrada.

powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
# Falha inicial devido a ausencia de handoff (resolvido com este documento).

dotnet build MicaAudio.sln -c Debug
# Compilacao com exito. 0 avisos, 0 erros.
```

## Riscos e rollback

- **Risco**: credenciais hardcoded ficam visiveis no repositorio; em caso de leak, o usuario deve alterar `mica_config.h` e reflashar todos os dispositivos.
- **Risco**: sem portal AP, perde-se a capacidade de reconfigurar Wi-Fi em campo sem recompilar/reflashar.
- **Rollback**: reverter o commit restaura `WiFiManager`, portal AP e logica de boot anterior. Nao ha mudanca em dados persistidos (NVS).

## Proximos passos

- Considerar mover `mica_config.h` para um arquivo nao versionado (ex: `mica_config.local.h`) e adicionar ao `.gitignore` para evitar leak de credenciais.
- Avaliar se o comando `enter_provisioning` ainda faz sentido no protocolo; pode ser renomeado para `reconnect_wifi` ou removido.
