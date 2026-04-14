# Handoff - Plano de migração FreeRTOS explícito (firmware ESP32-S3)

## Objetivo

Introduzir uso explícito de FreeRTOS no firmware ESP32-S3, com foco em segurança, observabilidade e rollout incremental, sem migrar para ESP-IDF puro e sem degradar o comportamento do painel HUB75.

## Escopo classificado

- Tipo: firmware/protocolo (estrutural + incrementos funcionais em fases)
- Critério de aceite:
  - Todos os invariantes listados abaixo continuam funcionando após cada fase
  - Telemetria incluindo `renderTimeUs` visível no dashboard
  - Nenhuma regressão no fluxo de render do HUB75 (bins, frame RGB565, fallback)

## Baseline atual (branch `hub75`)

### O que já existe de FreeRTOS no firmware

| Item | Onde | Observação |
|---|---|---|
| `#include <freertos/FreeRTOS.h>` | `main.cpp:14-16` | Já importado |
| `portMUX_TYPE gStreamBufferMux` | `main.cpp:240` | Protege `gBinsBuffers`, `gFrameRgb565Buffers`, `gBinsActiveIndex`, `gFrameRgb565ActiveIndex` |
| `SemaphoreHandle_t gPanelsBatchMutex` | `main.cpp:331` | Mutex para a task de playback WebP |
| `TaskHandle_t gPanelsBatchTaskHandle` | `main.cpp:332` | Handle da task de playback |
| `xTaskCreatePinnedToCore(panelsBatchPlaybackTask, ...)` | `main.cpp:2028` | Única task explícita, Core 1, priority 2 |
| `vTaskDelay`, `taskYIELD`, `ulTaskNotifyTake` | `main.cpp:2382,2384,2412` | Usados dentro da task de playback |
| `portENTER_CRITICAL / portEXIT_CRITICAL` | Múltiplos pontos | Protegem escrita/leitura de buffers de stream |

### O que NÃO existe ainda

- Task dedicada de render
- Task dedicada de rede/conectividade
- Queue para comandos de render ou de rede
- EventGroup para sinalizar "novo frame disponível" sem polling
- Watchdog de render por task
- Separação de Core 0 (rede) e Core 1 (render) para as funções principais

### Pontos de risco identificados

| Risco | Localização | Severidade |
|---|---|---|
| `drawFrame128x64()` lê `gFrameRgb565ActiveIndex` SEM seção crítica | `main.cpp:4355` | Médio — funciona por atomicidade de byte no ESP32, mas é tecnicamente unsafe |
| `gMatrixFrameDirty` e `gFrameModeActive` são escritos de `onWsEvent` (sem mutex) e lidos em `loop()` (sem mutex) | `main.cpp:3841,4626` | Médio — safe por single-core (loop e WS estão no mesmo core/tarefa Arduino) |
| `startProvisioningPortal()` bloqueia o `loop()` inteiro (WiFiManager) | `main.cpp:4419` | Alto — bloqueia render durante provisioning |
| `loop()` mistura rede + render na mesma iteração com budget cooperativo de 8ms | `main.cpp:4463` | Alto — pressão sobre cadência de render a 60fps |
| `panelsBatchPlaybackTask` (Core 1) chama `tryPresentWebpRgbaFrame()` que escreve `gFrameRgb565ActiveIndex` com portMUX | `main.cpp:2353` | Médio — correto com portMUX, mas sincronização frágil |

---

## Invariantes que não podem quebrar

Estes comportamentos devem ser preservados em todas as fases da migração:

1. **Provisioning serial**: resposta a comandos via `Serial` (pairing, reset, hello)
2. **Provisioning por portal AP**: WiFiManager abre AP e salva credenciais
3. **Conexão Wi-Fi**: reconexão automática, fallback após 20s de queda
4. **Conexão WebSocket**: reconexão após 60s, auto-reconnect a cada 2s
5. **Recepção de stream bins** (`messageType=1`, 145 bytes): atualiza `gBinsBuffers` + renderiza barras/visuais
6. **Recepção de frame RGB565 128x64** (`messageType=2`, 16400 bytes): atualiza `gFrameRgb565Buffers` + renderiza frame direto
7. **Telemetria MQTT**: heartbeat a cada 2s com todos os campos v2
8. **Controle de brilho**: cap 30–160, aplicado ao painel
9. **Test LED**: toggle e duty cycle via comando MQTT
10. **Fallback após perda de Wi-Fi**: exibe tela "SEM WIFI" no painel
11. **Limpeza após timeout de frames (15s)**: zera buffers e exibe estado inicial
12. **Playback WebP animado (Paineis)**: task `panelsBatchPlaybackTask` continua funcional
13. **OTA safe update**: fluxo de download, flash e rollback automático

---

## Arquitetura alvo (FreeRTOS explícito mínimo)

```
Core 0 (rede/conectividade — loop Arduino ou network task)
├── Wi-Fi (reconexão, eventos)
├── WebSocket (gWs.loop, onWsEvent)
├── MQTT (gMqtt.loop, telemetria, comandos)
├── Serial provisioning
├── WiFiManager AP (provisioning bloqueante → futuro: assíncrono)
└── OTA (download, flash)

Core 1 (render)
├── renderTask (NOVA)
│   ├── drawBinsVisual / drawFrame128x64 / drawConnectivityFallback
│   ├── commitMatrixFrame / flipDMABuffer
│   ├── brilho (setMatrixBrightness)
│   └── aguarda sinal: EventGroup RENDER_READY bit ou Queue de comandos
└── panelsBatchPlaybackTask (JÁ EXISTE — mantida)
    └── tryPresentWebpRgbaFrame (escreve buffer, sinaliza render)
```

### Buffers compartilhados e sincronização

| Buffer | Produtor | Consumidor | Mecanismo atual | Mecanismo alvo |
|---|---|---|---|---|
| `gBinsBuffers[2]` + `gBinsActiveIndex` | `onWsEvent` (Core 0) | `drawBinsVisual` (Core 1 futuro) | `portMUX` | `portMUX` (mantido) |
| `gFrameRgb565Buffers[2]` + `gFrameRgb565ActiveIndex` | `onWsEvent`, `tryPresentWebpRgbaFrame` | `drawFrame128x64` (Core 1 futuro) | `portMUX` | `portMUX` (mantido) |
| `gMatrixFrameDirty` | `markMatrixFrameDirty()` | render path | volatile bool | EventGroup bit |
| `gHub75FallbackState` | `updateHub75FallbackState()` | render path | single-core safe | `portMUX` ou EventGroup |
| `gMatrixShadowFrames[2]` | render path | render path | single-core | manter no Core 1 |

### Tasks e prioridades propostas

| Task | Core | Prioridade | Stack sugerido |
|---|---|---|---|
| Arduino `loopTask` (rede) | 0 | 1 (padrão Arduino) | 8192 (padrão) |
| `renderTask` (NOVA) | 1 | 3 | 8192 |
| `panelsBatchPlaybackTask` (existente) | 1 | 2 | 16384 (atual) |

> A prioridade de `renderTask` (3) deve ser maior que a de `panelsBatchPlaybackTask` (2) para que o render a 60fps não seja bloqueado pelo decoder WebP.

---

## Plano incremental de execução (fases)

## Fase 0 - Baseline e instrumentacao

**Objetivo**: medir sem mudar comportamento.

Alterações:
1. Adicionar `gLastRenderUs` (uint32_t) — tempo do último ciclo de render em µs
2. Adicionar `gRenderOverrunCount` (uint32_t) — contador de renders que excederam `kRenderOverrunThresholdUs`
3. Medir tempo do bloco de render em `loop()` usando `micros()`
4. Incluir `renderTimeUs` e `renderOverrunCount` na telemetria MQTT
5. Documentar plano (este arquivo)

**Risco**: zero — apenas adição de métricas sem alterar fluxo.

**Validação**: build passa, telemetria inclui novos campos, dashboard mostra `renderTimeUs`.

---

### Fase 1 — Extração da lógica de render

**Objetivo**: isolar o render em função bem definida e preparar para task.

Alterações:
1. Extrair o bloco de render de `loop()` para `runRenderStep()`:
   ```cpp
   void runRenderStep(uint32_t nowUs, unsigned long nowMs);
   ```
2. `loop()` chama `runRenderStep(nowUs, nowMs)` sem mudança de comportamento.
3. Adicionar `assert(xPortGetCoreID() == 0)` temporário dentro de `runRenderStep()` (removido na Fase 2).

**Risco**: baixo — refatoração local sem alterar controle de fluxo.

**Validação**: build + flash + teste manual dos invariantes 5, 6, 10.

---

### Fase 2 — EventGroup para sinalização de render

**Objetivo**: substituir polling de `gMatrixFrameDirty` por sinalização FreeRTOS.

Alterações:
1. Adicionar:
   ```cpp
   EventGroupHandle_t gRenderEventGroup = nullptr;
   constexpr EventBits_t kRenderEventStreamReady = BIT0;
   constexpr EventBits_t kRenderEventFallbackDirty = BIT1;
   constexpr EventBits_t kRenderEventTimeout = BIT2;
   ```
2. Criar `gRenderEventGroup` em `setup()` com `xEventGroupCreate()`.
3. Em `markMatrixFrameDirty()`: adicionar `xEventGroupSetBits(gRenderEventGroup, kRenderEventStreamReady)`.
4. Em `updateHub75FallbackState()` quando muda estado: setar `kRenderEventFallbackDirty`.
5. **Ainda em `loop()`**: render aguarda `xEventGroupWaitBits(gRenderEventGroup, kRenderEventStreamReady | kRenderEventFallbackDirty, pdTRUE, pdFALSE, pdMS_TO_TICKS(2))` com timeout de 2ms (para manter cadência mínima de render para bins contínuos).

**Risco**: médio — muda o timing do render. Testar que:
- Bins continuam renderizando de forma fluida
- Frame RGB565 é aplicado sem atraso visível
- Fallback aparece corretamente
- Timeout de 15s ainda funciona

**Validação**: build + flash + teste todos os invariantes + monitorar `loopHealthyPercent` e `renderTimeUs` no dashboard.

---

### Fase 3 — Task de render no Core 1

**Objetivo**: mover render para Core 1, liberando Core 0 exclusivamente para rede.

Alterações:
1. Criar `renderTask(void* param)` usando `runRenderStep()` em loop com EventGroup.
2. `xTaskCreatePinnedToCore(renderTask, "render", 8192, nullptr, 3, &gRenderTaskHandle, 1)` em `setup()`.
3. Remover bloco de render de `loop()`.
4. Adicionar `SemaphoreHandle_t gMatrixMutex` para sincronizar acesso ao `gMatrix` entre `renderTask` e `panelsBatchPlaybackTask`.
5. Proteção de `gHub75FallbackState` com `portMUX` (atualmente safe por single-core).
6. Proteção de `gMatrixFrameDirty`, `gFrameModeActive`, `gMatrixSignalTimedOut` com portMUX ou migrar para flags no EventGroup.

**Risco**: alto — muda core de execução do render e da HUB75 DMA.
- A biblioteca `ESP32-HUB75-MatrixPanel-DMA` precisa ser verificada quanto a thread-safety de `flipDMABuffer()`, `setBrightness8()`, `writeFrameRGB565()`.
- `panelsBatchPlaybackTask` já está no Core 1 e chama `tryPresentWebpRgbaFrame()`. Verificar se pode coexistir com `renderTask` no mesmo core.

**Validação obrigatória antes de merge**:
- [ ] Testar fluência do painel a 60fps durante bins streaming
- [ ] Testar frame RGB565 sem tearing
- [ ] Testar playback WebP Paineis
- [ ] Testar fallback Wi-Fi
- [ ] Testar provisioning serial sem congelar painel
- [ ] Monitorar `freeHeapBytes` (renderTask + stack adicional)
- [ ] Testar OTA sem crash

---

### Fase 4 — Provisioning assíncrono (opcional/futuro)

**Objetivo**: tirar WiFiManager bloqueante do `loop()`.

Alterações:
1. Mover `startProvisioningPortal()` para uma task temporária.
2. Garantir que o painel exibe "SETUP WIFI" durante o portal.
3. Sinalizar retorno via EventGroup ou queue.

**Risco**: alto — WiFiManager tem estado interno e não é thread-safe por padrão.

**Pré-requisito**: Fase 3 concluída e validada.

---

## Riscos e rollback

Cada fase é reversível com `git revert <commit>`:

- Fase 0: reverter adiciona/remove métricas — sem impacto funcional.
- Fase 1: reverter volta render para `loop()` inline — zero impacto.
- Fase 2: reverter remove EventGroup, volta polling por `gMatrixFrameDirty` — zero impacto funcional.
- Fase 3: reverter remove `renderTask` e `gMatrixMutex`, volta render para `loop()` — restaura comportamento original.

**Critério de rollback imediato**: se após Fase 3, `loopHealthyPercent < 70` por mais de 30s ou se painel apresentar tearing/congelamento em uso normal.

---

## Checklist de validação por fase

```text
Invariante                          | Fase 0 | Fase 1 | Fase 2 | Fase 3
------------------------------------|--------|--------|--------|-------
Provisioning serial                 |   ✓    |   ✓    |   ✓    |   ✓
Provisioning por portal             |   ✓    |   ✓    |   ✓    |   ✓
Conexão Wi-Fi                       |   ✓    |   ✓    |   ✓    |   ✓
Conexão WebSocket                   |   ✓    |   ✓    |   ✓    |   ✓
Stream bins (messageType=1)         |   ✓    |   ✓    |   ✓    |   ✓
Frame RGB565 (messageType=2)        |   ✓    |   ✓    |   ✓    |   ✓
Telemetria MQTT                     |   ✓    |   ✓    |   ✓    |   ✓
Controle de brilho                  |   ✓    |   ✓    |   ✓    |   ✓
Test LED                            |   ✓    |   ✓    |   ✓    |   ✓
Fallback após perda de Wi-Fi        |   ✓    |   ✓    |   ✓    |   ✓
Limpeza após timeout 15s            |   ✓    |   ✓    |   ✓    |   ✓
Playback WebP Paineis               |   ✓    |   ✓    |   ✓    |   ✓
OTA safe update                     |   ✓    |   ✓    |   ✓    |   ✓
```

---

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp` — adiciona `gLastRenderUs`, `gRenderOverrunCount`, instrumentação em `loop()` e campos na telemetria
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md` — seção sobre plano FreeRTOS e baseline atual
- `docs/wiki/reference/device-telemetry-v2-fields.md` — campos `renderTimeUs`, `renderOverrunCount`
- `docs/handoffs/2026-04-14-freertos-render-task-plan.md` — este arquivo

## Decisoes tomadas

1. **Não migrar para ESP-IDF puro**: a biblioteca `ESP32-HUB75-MatrixPanel-DMA` depende do Arduino framework; trocar empilharia riscos desnecessários.
2. **Começar com instrumentação zero-risco**: medir antes de mover é a forma mais segura de verificar se a migração está causando regressão de timing.
3. **`portMUX` se mantém**: é a abstração correta para proteção de seção crítica entre callback ISR-like (`onWsEvent`) e `loop()`. Não substituir por mutex ou semáforo nesta fase.
4. **`panelsBatchPlaybackTask` permanece intacta**: já está no Core 1 e funciona bem. A renderTask da Fase 3 precisará coordenar com ela via EventGroup/mutex de acesso ao `gMatrix`.
5. **EventGroup escolhido sobre Queue para sinalização de render**: o render precisa de sinal "há dado novo" sem perder amostras; EventGroup com bit auto-clear (`pdTRUE`) é mais adequado que queue de frames para esse padrão.
6. **Threshold de render overrun = `kHub75TargetPresentIntervalUs` (16666µs)**: se um ciclo de render demorar mais que 1 frame a 60fps, é uma sobrecarga que merece ser contada.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> ver resultado ao final
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> ver resultado ao final
dotnet build MicaAudio.sln -c Debug -> não afetado (apenas firmware + docs)
```

## Riscos remanescentes

1. **Sincronização `gMatrix` entre renderTask e panelsBatchPlaybackTask** (Fase 3): a biblioteca HUB75 não documenta explicitamente thread-safety. Necessita teste em campo.
2. **WiFiManager bloqueante** (Fase 4): não resolvido nesta entrega; o loop continua bloqueado durante provisioning por portal.
3. **`drawFrame128x64()` lê `gFrameRgb565ActiveIndex` sem critical section** (bug latente): funciona por atomicidade de byte no ESP32 LE, mas deve ser corrigido na Fase 3 adicionando leitura dentro de `portENTER_CRITICAL`.
4. **Stack de renderTask**: 8192 bytes é estimado. Medir `uxTaskGetStackHighWaterMark(gRenderTaskHandle)` após Fase 3 para confirmar.

## Proximos passos

1. [Fase 0 — esta entrega] Validar `renderTimeUs` no dashboard após flash.
2. [Fase 1] Extrair `runRenderStep()` e validar compilação/comportamento.
3. [Fase 2] Adicionar `gRenderEventGroup` e substituir polling de `gMatrixFrameDirty`.
4. [Fase 3] Criar `renderTask` no Core 1 e migrar render para fora do `loop()`.
5. [Fase 3] Adicionar `gMatrixMutex` e proteger `gHub75FallbackState` com portMUX.
6. [Fase 4 — futuro] Avaliar provisioning assíncrono após Fase 3 estável.
