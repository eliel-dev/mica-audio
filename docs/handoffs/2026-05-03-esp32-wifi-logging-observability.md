## Objetivo

Melhorar a observabilidade do boot e da conectividade Wi-Fi no firmware ESP32-S3, permitindo diagnóstico rápido via Serial Monitor quando o dispositivo não conecta à rede.

## Escopo classificado

**Firmware/protocolo** — altera `firmware/esp32s3-devkitc1/src/` sem mudar contratos wire.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/mica_fs_config.cpp` — logs detalhados em cada etapa de leitura do `config.json` (montagem FFat, arquivo ausente, JSON inválido, campos aplicados).
- `firmware/esp32s3-devkitc1/src/mica_network.cpp` — função helper `resolveWifiStatusText()` e logs de status Wi-Fi quando desconectado.
- `firmware/esp32s3-devkitc1/src/mica_network.h` — declaração de `resolveWifiStatusText()`.
- `firmware/esp32s3-devkitc1/src/main.cpp` — log do SSID usado no boot, log de falha com código/status após grace period, log de IP/MAC quando conecta.

## Decisoes tomadas

1. **Não duplicar a helper**: `resolveWifiStatusText()` foi colocada em `mica_network.cpp` e exposta no header para reuso em `main.cpp`, evitando duplicação de código.
2. **Logging sem bloqueio**: todos os novos `Serial.printf` usam prefixos estruturados (`[fs_config]`, `[wifi_boot]`, `[wifi]`) para facilitar parsing no Serial Monitor.
3. **Sem alteração de comportamento**: o fluxo de boot e reconexão permanece idêntico; apenas a telemetria via Serial foi enriquecida.

## Validacoes executadas

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
# OK: nenhuma falha encontrada.

powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
# OK: governanca IA valida.

dotnet build MicaAudio.sln -c Debug
# Compilação com êxito. 0 erros.
```

## Riscos e rollback

- **Risco mínimo**: os novos logs só aumentam o tráfego de Serial; não alteram timing crítico.
- **Rollback**: reverter os 4 arquivos alterados para a versão anterior (`git checkout --`) restaura o comportamento anterior sem efeitos colaterais.

## Proximos passos

- Verificar no Serial Monitor se o `config.json` está sendo lido corretamente (`[fs_config] config.json carregado com sucesso do FATFS.`).
- Se `[fs_config] /config.json nao encontrado no FATFS.` aparecer, executar `pio run -t uploadfs`.
- Se `[wifi_boot] falha ao conectar no grace period. status=1 (NO_SSID_AVAIL)` aparecer, verificar SSID/senha no JSON ou NVS.
