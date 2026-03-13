# Análise Técnica: FreeRTOS no mica-audio

> **Classificação:** Documental  
> **Arquivo referenciado:** [`firmware/esp32s3-devkitc1/src/main.cpp`](../../../firmware/esp32s3-devkitc1/src/main.cpp) (2696 linhas, C++ Arduino/PlatformIO)  
> **Hardware alvo:** ESP32-S3 DevKitC-1  
> **Data:** 2026-03-13

---

## Sumário

1. [Introdução](#1-introdução)
2. [Vantagens do FreeRTOS no contexto do mica-audio](#2-vantagens-do-freertos-no-contexto-do-mica-audio)
3. [Desvantagens e riscos](#3-desvantagens-e-riscos)
4. [Onde aplicar FreeRTOS: proposta de arquitetura](#4-onde-aplicar-freertos-proposta-de-arquitetura)
5. [O que não precisa de FreeRTOS hoje](#5-o-que-não-precisa-de-freertos-hoje)
6. [Conclusão](#6-conclusão)

---

## 1. Introdução

### O que é FreeRTOS

FreeRTOS é um sistema operacional de tempo real (RTOS) open source amplamente usado em microcontroladores. Ele fornece um **escalonador preemptivo** que divide o tempo de CPU entre múltiplas tarefas (*tasks*), cada uma com sua própria pilha de execução e prioridade.

No ESP32-S3, **FreeRTOS já está presente sem nenhuma dependência extra**: o Arduino ESP32 core e o ESP-IDF são construídos sobre FreeRTOS. Qualquer chamada ao `xTaskCreatePinnedToCore()`, `xQueueCreate()` ou `vTaskDelay()` funciona imediatamente no projeto atual sem alterar o `platformio.ini`.

### Loop cooperativo (atual) vs multitarefa preemptiva

| Característica | Loop cooperativo (atual) | FreeRTOS preemptivo |
|---|---|---|
| Modelo de execução | Uma thread, rodada completa por vez | Múltiplas tasks, o escalonador interrompe e retoma |
| Bloqueio por `delay()` | Trava **todo** o processamento | `vTaskDelay()` suspende apenas a task atual |
| Isolamento de falhas | Falha em qualquer passo afeta todo o loop | Falha isolada por task |
| Métricas de tempo | Estimativa manual (`gLoopHealthyPercent`) | `vTaskGetRunTimeStats()` por task |
| Uso dos 2 cores | Apenas Core 0 (padrão Arduino) | Cada task pode ser fixada a um core específico |

### O ESP32-S3 tem 2 cores — o FreeRTOS aproveita isso

O ESP32-S3 possui dois núcleos Xtensa LX7 idênticos rodando a até 240 MHz. O Arduino framework usa apenas o **Core 0** por padrão (Protocol CPU), deixando o **Core 1** (Application CPU) praticamente ocioso.

Com FreeRTOS e `xTaskCreatePinnedToCore()`, é possível:
- Fixar a task de rede (WiFi + WebSocket + MQTT) no **Core 0**, onde a pilha de rede do ESP-IDF já opera nativamente;
- Fixar a task de display (renderização HUB75 DMA) no **Core 1**, garantindo que o display continue renderizando mesmo quando a rede está ocupada.

---

## 2. Vantagens do FreeRTOS no contexto do mica-audio

### 2.1 Isolamento de Core: rede e display em cores separados

**Problema atual:** `drawBars()` e `drawFrame128x64()` são chamados no mesmo core que `gWs.loop()`. Se o WebSocket demorar processando um frame RGB565, o display para de atualizar.

```cpp
// firmware/esp32s3-devkitc1/src/main.cpp#L2648
gWs.loop();   // pode demorar ao processar frame de 16KB

// firmware/esp32s3-devkitc1/src/main.cpp#L2689-L2693
if (gFrameModeActive) {
    drawFrame128x64();   // só chega aqui quando gWs.loop() terminar
} else {
    drawBars();
}
```

**Com FreeRTOS:**
```cpp
// TaskDisplay fixada no Core 1 — nunca é interrompida pela rede
xTaskCreatePinnedToCore(TaskDisplay, "Display", 4096, nullptr, 10, nullptr, 1);
// TaskNetwork fixada no Core 0 — onde a pilha WiFi já opera
xTaskCreatePinnedToCore(TaskNetwork, "Network", 12288, nullptr, 5, nullptr, 0);
```

### 2.2 Fim do `delay()` bloqueante

**Problema atual:** Quando o WiFi está desconectado, a linha `delay(120)` bloqueia **todo** o processamento por 120 ms — isso inclui serial provisioning, display e LED auxiliar:

```cpp
// firmware/esp32s3-devkitc1/src/main.cpp#L2629
delay(120);  // bloqueia absolutamente tudo
```

**Com FreeRTOS:**
```cpp
vTaskDelay(pdMS_TO_TICKS(120));  // suspende apenas TaskNetwork; TaskDisplay continua rodando
```

### 2.3 Comunicação segura entre tasks via Queue

**Problema atual:** `gBins` e `gFrameRgb565` são escritos em `onWsEvent()` (contexto do WebSocket) e lidos em `drawBars()` / `drawFrame128x64()` — sem nenhuma proteção contra race condition.

```cpp
// firmware/esp32s3-devkitc1/src/main.cpp#L2341 — escrita (onWsEvent)
memcpy(gBins, payload + 15, kBinsCount);
gFrameModeActive = false;
gLastFrameMs = millis();

// firmware/esp32s3-devkitc1/src/main.cpp#L2462 — leitura (drawBars)
void drawBars() { /* usa gBins diretamente */ }
```

**Com FreeRTOS:**
```cpp
// Fila de tamanho 1 — sempre contém o frame mais recente
static QueueHandle_t gBinsQueue;
static QueueHandle_t gFrameQueue;

// Na task de rede, após receber bins:
xQueueOverwrite(gBinsQueue, &gBins);

// Na task de display:
uint8_t localBins[kBinsCount];
if (xQueueReceive(gBinsQueue, &localBins, pdMS_TO_TICKS(16))) {
    drawBarsWithBins(localBins);
}
```

`xQueueOverwrite()` é thread-safe por design: garante que a task de display sempre leia o frame mais recente, sem mutex explícito para essa operação.

### 2.4 Métricas reais por task

**Problema atual:** `gLoopHealthyPercent` é uma estimativa manual baseada em microsegundos do loop ([L1417-L1446](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1417)). Não distingue onde o tempo está sendo consumido.

**Com FreeRTOS:**
```cpp
char statsBuffer[512];
vTaskGetRunTimeStats(statsBuffer);
// Saída:
// TaskNetwork    45%   Core 0
// TaskDisplay    18%   Core 1
// IDLE0          55%   Core 0
// IDLE1          82%   Core 1
```

Isso permite identificar com precisão se o gargalo é rede, display ou outro subsistema.

### 2.5 Watchdog por task

Com FreeRTOS, cada task tem seu próprio watchdog timer gerenciado pelo ESP-IDF. Se `TaskNetwork` travar por mais de `CONFIG_ESP_TASK_WDT_TIMEOUT_S` segundos, o watchdog reseta apenas aquela task (ou o sistema, dependendo da configuração) — sem afetar `TaskDisplay`.

No loop único atual, um travamento em qualquer ponto causa reset do dispositivo inteiro, incluindo o display.

### 2.6 Prioridades: display estável mesmo com rede ocupada

Com FreeRTOS, `TaskDisplay` pode ter prioridade maior que `TaskNetwork`:

```cpp
xTaskCreatePinnedToCore(TaskDisplay, "Display", 4096, nullptr, 10, nullptr, 1); // prioridade 10
xTaskCreatePinnedToCore(TaskNetwork, "Network", 12288, nullptr, 5, nullptr, 0); // prioridade 5
```

Isso garante que, mesmo que `TaskNetwork` esteja ocupada com reconexão WiFi ou parsing de frame grande, `TaskDisplay` não perde seu timeslice.

### 2.7 Eliminação do `processSerialProvisioning()` duplicado

**Problema atual:** `processSerialProvisioning()` é chamado **3 vezes** por iteração do `loop()` (linhas [L2610](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2610), [L2632](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2632) e [L2688](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2688)), o que é redundante e dificulta a leitura do fluxo.

**Com FreeRTOS:**
```cpp
void TaskSerial(void* pv) {
    for (;;) {
        processSerialProvisioning();
        vTaskDelay(pdMS_TO_TICKS(50));  // verifica serial a cada 50ms
    }
}
```

Uma task dedicada de baixa prioridade resolve completamente o problema.

---

## 3. Desvantagens e riscos

### 3.1 `WebSocketsClient` não é thread-safe

`gWs` é uma instância global compartilhada. Se `gWs.loop()` e `gWs.sendTXT()` forem chamados de tasks diferentes ao mesmo tempo, o comportamento é indefinido e quase sempre resulta em crash ou corrupção de heap.

```cpp
// firmware/esp32s3-devkitc1/src/main.cpp#L111
WebSocketsClient gWs;

// ERRO: gWs acessado de duas tasks sem proteção
// TaskNetwork: gWs.loop();
// TaskSerial: gWs.sendTXT(...)  ← crash garantido
```

**Mitigação obrigatória:**
```cpp
static SemaphoreHandle_t gWsMutex;

// Toda chamada a gWs.* deve ser protegida:
if (xSemaphoreTake(gWsMutex, pdMS_TO_TICKS(100)) == pdTRUE) {
    gWs.loop();
    xSemaphoreGive(gWsMutex);
}
```

O mesmo vale para `gMqtt` (instância de `PubSubClient`, linha [L113](../../../firmware/esp32s3-devkitc1/src/main.cpp#L113)).

### 3.2 `WiFiManager` é bloqueante por design

`wm.autoConnect()` ([L2011](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2011)) inicia um servidor HTTP e bloqueia a task que o chamou até que o usuário configure o WiFi pelo portal AP. Isso é intencional no WiFiManager, mas num cenário FreeRTOS precisa de tratamento especial.

```cpp
// firmware/esp32s3-devkitc1/src/main.cpp#L2011
if (!wm.autoConnect(apName.c_str())) {  // bloqueia TaskNetwork indefinidamente
    // ...
}
```

**Opções de mitigação:**
- Rodar `startProvisioningPortal()` em uma task dedicada de provisioning com stack adequada;
- Usar `wm.setConfigPortalTimeout()` para limitar o tempo bloqueante;
- Usar `wm.startConfigPortalAsync()` se disponível na versão da biblioteca.

### 3.3 Variáveis globais compartilhadas viram candidatas a race condition

Qualquer variável global lida em `TaskDisplay` e escrita em `TaskNetwork` (ou vice-versa) é um race condition em potencial:

| Variável | Linha | Risco |
|---|---|---|
| `gBins[kBinsCount]` | [L124](../../../firmware/esp32s3-devkitc1/src/main.cpp#L124) | Escrita em `onWsEvent`, leitura em `drawBars` |
| `gFrameRgb565[kMatrixPixelCount]` | [L128](../../../firmware/esp32s3-devkitc1/src/main.cpp#L128) | Buffer ~16KB, escrita em `onWsEvent`, leitura em `drawFrame128x64` |
| `gStreamBrightness` | [L126](../../../firmware/esp32s3-devkitc1/src/main.cpp#L126) | Escrita em `onWsEvent`, leitura em `resolveAppliedBrightness` |
| `gFrameModeActive` | [L145](../../../firmware/esp32s3-devkitc1/src/main.cpp#L145) | Flag de controle lida/escrita de múltiplos pontos |
| `gLastFrameMs` | [L129](../../../firmware/esp32s3-devkitc1/src/main.cpp#L129) | Timestamp, leitura/escrita concorrente |

Cada uma dessas variáveis precisaria ser protegida com `SemaphoreHandle_t` ou substituída por acesso via Queue.

### 3.4 Stack size por task pode ser significativo

O `WebSocketsClient`, `ArduinoJson` com documentos grandes, `HTTPClient` e `PubSubClient` fazem uso intenso da pilha durante operações de parsing e serialização. Stack insuficiente causa stack overflow silencioso ou crash por `__SANITY_CHECK__`.

Estimativas conservadoras para o `mica-audio`:

| Task | Stack recomendada | Justificativa |
|---|---|---|
| `TaskNetwork` | 12–16 KB | `WebSocketsClient` + `ArduinoJson` + `HTTPClient` + `PubSubClient` |
| `TaskDisplay` | 4 KB | `drawBars` e `drawFrame128x64` são simples |
| `TaskSerial` | 4 KB | `processSerialProvisioning` com parsing JSON local |

Para monitorar overflow em desenvolvimento:
```cpp
// Verificar high watermark da stack de cada task
UBaseType_t highWaterMark = uxTaskGetStackHighWaterMark(taskHandle);
// Valor < 256 words indica stack próxima do limite
```

### 3.5 Overhead de desenvolvimento e refatoração

O `main.cpp` atual tem 2696 linhas num único arquivo, com acoplamento entre WiFi, WebSocket, MQTT, serial e display. A migração para FreeRTOS exige:

- Definir fronteiras claras entre tasks (o que cada task acessa);
- Substituir variáveis globais compartilhadas por Queues ou Mutexes;
- Testar cada task de forma isolada antes de integrar;
- Ajustar todas as funções que atualmente assumem execução single-threaded.

É uma refatoração de médio porte, não trivial, mas bem delimitada.

### 3.6 `onWsEvent()` é chamado dentro de `gWs.loop()`

O callback `onWsEvent()` ([L2285](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2285)) é chamado **durante** `gWs.loop()`. Qualquer operação longa dentro de `onWsEvent` (como copiar o buffer de frame RGB565 de 16KB) bloqueia a task que chamou `gWs.loop()`.

```cpp
// firmware/esp32s3-devkitc1/src/main.cpp#L2367-L2373
for (size_t i = 0; i < kMatrixPixelCount; i++) {  // 8192 iterações
    const uint16_t rawPixel = (payload[offset] << 8) | payload[offset + 1];
    gFrameRgb565[i] = rawPixel;
    offset += 2;
}
```

Com FreeRTOS, `onWsEvent` deve apenas copiar os dados para uma Queue e retornar imediatamente, deixando o processamento pesado para `TaskDisplay`.

### 3.7 `ESP32-HUB75-MatrixPanel-I2S-DMA` já usa DMA interno

A biblioteca HUB75 já opera de forma assíncrona via DMA + interrupções — o framebuffer é enviado ao painel continuamente pelo hardware, sem ocupar a CPU. Isso significa que o ganho de isolar `TaskDisplay` no Core 1 é **mais em estabilidade** (evitar que a rede atrase o cálculo do próximo frame) do que em throughput bruto do display.

---

## 4. Onde aplicar FreeRTOS: proposta de arquitetura

### Visão geral das tasks

```
Core 0 (Protocol CPU — onde WiFi/BT já operam):
  ┌─────────────────────────────────────────────┐
  │  TaskNetwork   prioridade 5   stack 12 KB   │
  │    ├── gWs.loop()                           │
  │    ├── gMqtt.loop()                         │
  │    ├── sendTelemetry()                      │
  │    ├── connectWebSocket()                   │
  │    └── reconexão automática WiFi/WS/MQTT    │
  │                                             │
  │  TaskSerial    prioridade 2   stack 4 KB    │
  │    └── processSerialProvisioning()          │
  └─────────────────────────────────────────────┘

Core 1 (Application CPU — atualmente ocioso):
  ┌─────────────────────────────────────────────┐
  │  TaskDisplay   prioridade 10  stack 4 KB    │
  │    ├── drawBars()     (modo bins)           │
  │    ├── drawFrame128x64() (modo frame)       │
  │    └── updateTestLed()                      │
  └─────────────────────────────────────────────┘
```

### Comunicação entre tasks

```
TaskNetwork ──[xQueueOverwrite gBinsQueue]──► TaskDisplay
TaskNetwork ──[xQueueOverwrite gFrameQueue]─► TaskDisplay
TaskNetwork ──[SemaphoreHandle_t gWsMutex]──  protege gWs.* e gMqtt.*
```

### Esboço de implementação

```cpp
// Handles globais de comunicação entre tasks
static QueueHandle_t    gBinsQueue;    // 1 slot, sizeof(gBins)
static QueueHandle_t    gFrameQueue;   // 1 slot, sizeof(gFrameRgb565)
static SemaphoreHandle_t gWsMutex;

// ── TaskNetwork (Core 0, prioridade 5) ───────────────────────────────────
void TaskNetwork(void* pv) {
    for (;;) {
        xSemaphoreTake(gWsMutex, portMAX_DELAY);
        gWs.loop();
        gMqtt.loop();
        xSemaphoreGive(gWsMutex);

        if (gMqtt.connected()) {
            sendTelemetry(false);
        } else {
            connectMqtt();
        }

        if (!gWs.isConnected()) {
            connectWebSocket();
        }

        vTaskDelay(pdMS_TO_TICKS(1));
    }
}

// ── TaskDisplay (Core 1, prioridade 10) ──────────────────────────────────
void TaskDisplay(void* pv) {
    uint8_t localBins[kBinsCount];
    uint16_t localFrame[kMatrixPixelCount];

    for (;;) {
        if (gFrameModeActive) {
            if (xQueueReceive(gFrameQueue, localFrame, pdMS_TO_TICKS(16))) {
                drawFrame128x64WithBuffer(localFrame);
            }
        } else {
            if (xQueueReceive(gBinsQueue, localBins, pdMS_TO_TICKS(16))) {
                drawBarsWithBins(localBins);
            }
        }
        updateTestLed();
    }
}

// ── TaskSerial (Core 0, prioridade 2) ────────────────────────────────────
void TaskSerial(void* pv) {
    for (;;) {
        processSerialProvisioning();
        vTaskDelay(pdMS_TO_TICKS(50));
    }
}

// ── setup() ──────────────────────────────────────────────────────────────
void setup() {
    // ... init serial, prefs, matrix, WiFi, WebSocket (igual ao atual) ...

    gBinsQueue  = xQueueCreate(1, sizeof(gBins));
    gFrameQueue = xQueueCreate(1, sizeof(gFrameRgb565));
    gWsMutex    = xSemaphoreCreateMutex();

    xTaskCreatePinnedToCore(TaskNetwork, "Network", 12288, nullptr, 5,  nullptr, 0);
    xTaskCreatePinnedToCore(TaskDisplay, "Display", 4096,  nullptr, 10, nullptr, 1);
    xTaskCreatePinnedToCore(TaskSerial,  "Serial",  4096,  nullptr, 2,  nullptr, 0);
}

void loop() {
    vTaskDelete(nullptr);  // loop Arduino não é mais utilizado
}
```

### Adaptação do `onWsEvent()` para FreeRTOS

```cpp
// firmware/esp32s3-devkitc1/src/main.cpp#L2285 (adaptado)
void onWsEvent(WStype_t type, uint8_t* payload, size_t len) {
    if (type == WStype_BIN) {
        if (/* formato bins */) {
            uint8_t tmpBins[kBinsCount];
            memcpy(tmpBins, payload + 15, kBinsCount);
            xQueueOverwrite(gBinsQueue, tmpBins);  // thread-safe, não bloqueia
        } else if (/* formato frame RGB565 */) {
            // Decodifica diretamente para buffer temporário local
            uint16_t tmpFrame[kMatrixPixelCount];
            for (size_t i = 0; i < kMatrixPixelCount; i++) {
                tmpFrame[i] = (payload[15 + i*2] << 8) | payload[15 + i*2 + 1];
            }
            xQueueOverwrite(gFrameQueue, tmpFrame);  // thread-safe
        }
    }
}
```

---

## 5. O que não precisa de FreeRTOS hoje

O projeto atual **funciona corretamente** para o caso de uso principal: receber bins/frames por WebSocket e exibi-los no painel HUB75. O loop cooperativo é simples, previsível e sem overhead de sincronização.

FreeRTOS é recomendado **se e somente se** pelo menos uma das condições abaixo for verdadeira:

1. **O display travar durante reconexão WiFi/WS é um problema observado em produção.** Se isso ocorre com frequência (especialmente no `delay(120)` da linha [L2629](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2629)), FreeRTOS resolve diretamente.

2. **Há planos de adicionar periféricos adicionais** — sensor I2S, botões físicos, segundo painel, BLE — que exigiriam processamento paralelo real.

3. **A latência entre receber um frame e exibi-lo for crítica** (sub-16ms). Atualmente, qualquer operação de rede pode atrasar o render em dezenas de ms.

4. **O `gLoopHealthyPercent`** ([L149](../../../firmware/esp32s3-devkitc1/src/main.cpp#L149)) indicar consistentemente valores abaixo de 70%, sugerindo que o loop está sobrecarregado.

Se nenhuma dessas condições for verdadeira no momento, o custo de refatoração não se justifica.

---

## 6. Conclusão

### Tabela resumo

| Problema atual | Solução FreeRTOS | Complexidade |
|---|---|---|
| `delay(120)` trava display durante queda de WiFi ([L2629](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2629)) | `vTaskDelay()` em `TaskNetwork`; `TaskDisplay` no Core 1 continua | Baixa |
| Render e rede competem pelo mesmo core | `TaskDisplay` fixada no Core 1 via `xTaskCreatePinnedToCore` | Baixa |
| `processSerialProvisioning()` chamado 3× por loop ([L2610](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2610), [L2632](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2632), [L2688](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2688)) | `TaskSerial` dedicada com `vTaskDelay(50ms)` | Baixa |
| `gBins` e `gFrameRgb565` sem proteção de concorrência | `xQueueOverwrite` para bins e frame | Média |
| `gLoopHealthyPercent` é estimativa manual ([L149](../../../firmware/esp32s3-devkitc1/src/main.cpp#L149)) | `vTaskGetRunTimeStats()` por task | Baixa |
| `gWs` e `gMqtt` precisariam de mutex | `SemaphoreHandle_t gWsMutex` em toda chamada `gWs.*` / `gMqtt.*` | Alta |
| `wm.autoConnect()` bloqueia a task ([L2011](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2011)) | Task de provisioning dedicada ou timeout configurado | Alta |
| Todas as globais viram race condition | Revisão completa de acessos compartilhados | Alta |

### Recomendação

**Curto prazo (sem FreeRTOS):** O maior ganho imediato e de baixo risco seria eliminar o `delay(120)` bloqueante ([L2629](../../../firmware/esp32s3-devkitc1/src/main.cpp#L2629)) e as chamadas duplicadas a `processSerialProvisioning()`. Isso já melhora a responsividade do display sem nenhuma mudança arquitetural.

**Médio prazo (com FreeRTOS):** Migrar para a arquitetura de 3 tasks descrita na [seção 4](#4-onde-aplicar-freertos-proposta-de-arquitetura) se o display travar durante reconexões for um problema real ou se novos periféricos forem adicionados. A maior parte do código existente em `main.cpp` pode ser preservada — a mudança é na organização do fluxo, não na lógica de cada função.

**Pré-requisito crítico:** Antes de qualquer migração FreeRTOS, proteger `gWs` e `gMqtt` com mutex é obrigatório. Ignorar isso é a causa mais comum de crashes em projetos Arduino+FreeRTOS no ESP32.

---

> **Referências:**
> - [ESP-IDF FreeRTOS — ESP32-S3](https://docs.espressif.com/projects/esp-idf/en/v5.5.3/esp32s3/api-reference/system/freertos.html)
> - [ESP-IDF Task and Stack Sizes](https://docs.espressif.com/projects/esp-idf/en/v5.5.3/esp32s3/api-reference/system/freertos.html#task-creation)
> - [`firmware/esp32s3-devkitc1/src/main.cpp`](../../../firmware/esp32s3-devkitc1/src/main.cpp) — loop principal: L2608–L2696
> - [`docs/wiki/architecture/`](../architecture/) — arquitetura geral do sistema mica-audio
