# Handoff - 2026-04-17 - control-worker-watchdog-and-wifi-heap-regression-fix

## Objetivo

Corrigir a regressao introduzida pelo hardening do runtime em que o `control_worker` ficava inscrito permanentemente no `esp_task_wdt` mesmo quando bloqueado em `xQueueReceive(..., portMAX_DELAY)` e tambem consumia heap interno fixo no boot, pressionando a inicializacao do driver Wi-Fi.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Inclui:
  - inscricao do `control_worker` no watchdog apenas durante jobs lentos ativos;
  - criacao sob demanda do `control_worker` em vez de cria-lo no boot;
  - pequeno ajuste de stack do `control_worker` para reduzir pressao de heap interno.
- Nao inclui:
  - mudanca no wire de `commands`, `command-events` ou telemetria;
  - alteracao da estrategia do `panelsBatchPlaybackTask` ou do worker OTA;
  - nova fila dedicada de jobs de `Paineis`.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/mica_commands.cpp`
- `firmware/esp32s3-devkitc1/src/mica_types.h`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/handoffs/2026-04-17-control-worker-watchdog-and-wifi-heap-regression-fix.md`

## Decisoes tomadas

1. O `control_worker` deixou de ser inscrito no `esp_task_wdt` durante toda a sua vida util; agora ele entra no watchdog apenas depois de receber um `SlowCommandRequest` e sai antes de voltar a bloquear na fila.
2. A criacao do `control_worker` saiu de `initializeControlCommandRuntime()` e passou para o agendamento efetivo de `update_firmware` e `queue_panels_batch`, removendo custo fixo no caminho de `WiFi.begin()` do boot.
3. O stack do `control_worker` foi reduzido de `16 KB` para `12 KB` para aliviar heap interno sem desfazer o desenho de worker dedicado no `Core 0`.

## Validacoes executadas

```text
platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1 -> OK
```

## Riscos e rollback

- Risco principal: o stack menor do `control_worker` pode ainda precisar de confirmacao em hardware real sob carga de `queue_panels_batch` e descoberta OTA.
- Mitigacoes:
  - o worker continua isolando jobs lentos no `Core 0`;
  - o watchdog continua cobrindo jobs lentos ativos, sem gerar falso positivo quando a task esta ociosa;
  - o worker so existe quando necessario, reduzindo o custo fixo de RAM do boot.
- Rollback:
  1. recriar o `control_worker` no boot dentro de `initializeControlCommandRuntime()`;
  2. recolocar a inscricao permanente do worker no `esp_task_wdt`;
  3. restaurar `kControlWorkerTaskStackSize` para `16384`.

## Proximos passos

1. Validar em hardware real se o device volta a sair de `SEM WIFI` no boot apos flash da nova imagem.
2. Repetir o fluxo que antes travava: aplicar painel, alternar visualizador/app e observar se o `control_worker` nao reaparece em timeout do TWDT.
3. Se ainda houver `ESP_ERR_NO_MEM` em reinit de Wi-Fi depois de usar jobs lentos, instrumentar heap interno livre antes/depois de criar worker, OTA e playback para decidir se o worker deve ser destruido apos idle prolongado.
