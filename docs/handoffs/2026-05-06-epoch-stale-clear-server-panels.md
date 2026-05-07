# Epoch Stale-Clear: Server Panels Runtime

## Objetivo

Corrigir bug onde paineis nao ativam apos device restart ou Wi-Fi reconnect. O cache `ownerEpochByDeviceId` no servidor mantinha epoch stale apos o device reiniciar (epoch reseta para 0 no firmware), causando desalinhamento de sessao entre servidor e ESP32.

## Escopo classificado

Estrutural — altera contrato de sessao no `ServerPanelRuntimeService.CreateCommandContext()`.

## Arquivos alterados

| Arquivo | Mudanca |
|---------|---------|
| `src/MicaAudio.Server/ServerPanelRuntimeService.cs` | Adicionado Case 0 em `CreateCommandContext()`: detecta regressao de epoch no shadow do device e limpa cache stale |
| `firmware/esp32s3-devkitc1/src/mica_network.cpp` | WiFi reconnect: reset `gWsAutoReconnectInitialized=false` + `connectWebSocket()` imediato (ja existente) |
| `firmware/esp32s3-devkitc1/src/mica_network.cpp` | Stream diagnostics: `publishDeviceLog` em epoch stale/missing/bins_accepted/frame_accepted (ja existente) |

## Decisoes tomadas

1. **Case 0 - Epoch regression detection**: Antes dos cases existentes em `CreateCommandContext()`, verifica se o `SessionActiveOwnerEpoch` do device shadow regrediu (valor 0 ou menor que o cacheado). Se sim, remove a entrada do `ownerEpochByDeviceId`, forçando Case 3 (prediction) que calcula `Max(1, shadowEpoch + 1)`.
   - Device reiniciado com shadow epoch=0 → cache limpo → prediction = `Max(1, 0+1) = 1` → alinha com firmware que adota em epoch 1.
   - Device com shadow epoch menor que cacheado ( Ownership perdeu/expirou) → cache limpo → prediction volta ao correto.

2. **Sem necessidade de auto-sync adicional**: O Case 3 ( prediction) ja escreve o epoch previsto no cache (linha 556). Quando o firmware adota com `activeOwnerEpoch++` partindo de 0, o resultado e epoch 1, que coincide com `Max(1, 0+1) = 1`. O desalinhamento so ocorria quando o Case 2 (cache stale) era atingido, que agora e pre-emptido pelo Case 0.

3. **Firmware WiFi reconnect fix**: Ja implementado em `mica_network.cpp:1388-1392` — `gWsAutoReconnectInitialized = false` + `connectWebSocket()` na reconexao Wi-Fi. Elimina a dependencia do retry periodico de 60s.

## Validacoes executadas

| Comando | Resultado |
|---------|----------|
| `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` | OK - nenhuma falha |
| `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` | OK - governanca IA valida |
| `dotnet build MicaAudio.sln -c Debug` | OK - 0 erros, 0 avisos |

## Riscos e rollback

- Risco baixo: a mudanca so afeta o path de prediction de epoch quando shadow indica regressao ou reset.
- Rollback: remover o bloco `if (device?.SessionActiveOwnerEpoch is { } shadowEpoch)` adicionado antes dos cases existentes em `CreateCommandContext()`.
- Atencao: o firmware build do `esp32s3_devkitc1_dma_diag` precisa de `pio run -t clean` antes de rebuild por causa de cache corrompido do PlatformIO.

## Proximos passos

1. `pio run -t clean -e esp32s3_devkitc1_dma_diag && pio run --target upload -e esp32s3_devkitc1_dma_diag` — reflash firmware.
2. Testar cenario: boot sem Wi-Fi → reconexao Wi-Fi → verificar WebSocket conectado + paineis ativam.
3. Testar cenario: server restart → device com epoch previo → confirmar que prediction = 1 apos shadow com epoch=0.
4. Monitorar logs seriais para verificar `owner_adopted` e `bins_accepted`/`frame_accepted`.