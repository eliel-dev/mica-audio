# Phase 1A - Firmware Module Split

## Objetivo

Dividir o `main.cpp` monolitico (~5108 linhas) do firmware ESP32-S3 em ~12 modulos com responsabilidades isoladas, preservando funcionalidade identica e metricas de build estaveis. Preparacao para Phase 1B (FreeRTOS dual-core + APIs nativas ESP-IDF).

## Escopo classificado

**Estrutural** - altera arquitetura do firmware sem mudar contratos publicos, comportamento ou wire protocol.

## Arquivos alterados

### Criados (17 arquivos novos)
- `firmware/esp32s3-devkitc1/src/mica_types.h` - header-only: enums, structs, constexpr (~260 linhas)
- `firmware/esp32s3-devkitc1/src/mica_globals.h` - extern declarations (~206 linhas)
- `firmware/esp32s3-devkitc1/src/mica_globals.cpp` - definicoes de globals (~195 linhas)
- `firmware/esp32s3-devkitc1/src/mica_display.h` - 52 funcoes display + LED (~108 linhas)
- `firmware/esp32s3-devkitc1/src/mica_display.cpp` - implementacoes display (~907 linhas)
- `firmware/esp32s3-devkitc1/src/mica_visuals.h` - estilos visuais (~42 linhas)
- `firmware/esp32s3-devkitc1/src/mica_visuals.cpp` - implementacoes visuais (~590 linhas)
- `firmware/esp32s3-devkitc1/src/mica_network.h` - 34 funcoes rede (~95 linhas)
- `firmware/esp32s3-devkitc1/src/mica_network.cpp` - implementacoes rede (~970 linhas)
- `firmware/esp32s3-devkitc1/src/mica_ota.h` - 14 funcoes OTA (~39 linhas)
- `firmware/esp32s3-devkitc1/src/mica_ota.cpp` - implementacoes OTA (~580 linhas)
- `firmware/esp32s3-devkitc1/src/mica_panels.h` - 10 funcoes panels (~46 linhas)
- `firmware/esp32s3-devkitc1/src/mica_panels.cpp` - implementacoes panels (~480 linhas)
- `firmware/esp32s3-devkitc1/src/mica_commands.h` - 1 funcao publica (~6 linhas)
- `firmware/esp32s3-devkitc1/src/mica_commands.cpp` - parser de comandos (~373 linhas)
- `firmware/esp32s3-devkitc1/src/mica_provisioning.h` - 4 funcoes publicas (~10 linhas)
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp` - provisioning (~330 linhas)

### Alterados
- `firmware/esp32s3-devkitc1/src/main.cpp` - de ~5108 para ~223 linhas (orquestrador)
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md` - nova secao Module split + referencias atualizadas
- `docs/wiki/guides/add-device-command.md` - link corrigido para mica_commands.cpp

## Decisoes tomadas

1. **Sem renomear funcoes**: nomes exatos preservados para diff limpo e rastreabilidade.
2. **Remocao do namespace anonimo global**: necessario para visibilidade cross-TU; funcoes internas usam `static` local.
3. **Headers com `#pragma once`**: cada header inclui apenas o necessario.
4. **`constexpr` em header**: seguro em C++ (internal linkage implicito).
5. **`kHub75BaselineDriver` em `mica_display.h`**: depende de HUB75 library, nao pertence a `mica_types.h`.
6. **`gMqtt(gMqttNetClient)` em `mica_globals.cpp`**: ambos na mesma TU, ordem de inicializacao segura.
7. **Default arguments apenas no `.h`**: removidos das definicoes no `.cpp` para evitar erro de compilacao.
8. **Ordenacao no `.cpp`**: funcoes definidas antes de serem chamadas, eliminando forward declarations internas.
9. **Funcoes internas marcadas `static`**: nao expostas no `.h` quando nao chamadas de fora do modulo.
10. **Test LED functions em `mica_display`**: fit natural - usam apenas funcoes e globals de display.

## Validacoes executadas

| Comando | Resultado |
|---|---|
| `build-precompiled-firmware.ps1` | SUCCESS (26.3s) RAM 39.0% Flash 48.5% |
| `dotnet build MicaAudio.sln -c Debug` | SUCCESS (0 errors, 35 warnings NuGet pre-existentes) |
| `docs-validate.ps1` | PASS (498 links validados, link quebrado corrigido) |
| `ai-governance-check.ps1` | PASS (handoff e docs evidence criados) |

Build executado apos cada passo (1-10) para garantir compilacao incremental.

## Riscos e rollback

- **Risco baixo**: nenhuma funcao renomeada, nenhum comportamento alterado, metricas de build identicas.
- **Rollback**: reverter os 17 arquivos novos e restaurar `main.cpp` do commit anterior.
- **ODR**: `constexpr` namespace-scope tem internal linkage em C++; nenhuma variavel extern duplicada.
- **Initialization order**: `gMqtt` e `gMqttNetClient` na mesma TU, ordem garantida.

## Proximos passos

1. **Phase 1B**: FreeRTOS dual-core + `esp_event` + `esp_ringbuf` + `ESP_LOG` + `esp_task_wdt`.
2. **Phase 2**: `esp_mqtt_client` + `esp_http_client` (substituir PubSubClient e Arduino HTTPClient).
3. **Phase 3**: `esp_https_ota` (substituir OTA manual).
4. **Phase 4**: PSRAM migration com `heap_caps_malloc`.
