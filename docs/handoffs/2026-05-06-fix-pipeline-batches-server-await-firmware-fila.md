# Fix Pipeline de Batches — Server Await + Firmware Fila Não-Lenta

## Objetivo

Corrigir o pipeline de batches de paineis introduzido no handoff `2026-05-06-firmware-performance-optimization-phases-1-4`, onde a combinacao de dispatch fire-and-forget no servidor e fila unica bloqueante no firmware causava underrun de playback e desaparecimento do painel apos ~15s (`processSignalTimeout`).

## Escopo classificado

Estrutural — altera contrato de back-pressure entre servidor e firmware (server aguarda ACK antes de avancar) e logica de fila de comandos do ESP32 (permite processar comandos nao-lentos enquanto slow domain ocupado).

## Arquivos alterados

| Arquivo | Mudanca |
|---------|---------|
| `src/MicaAudio.Server/ServerPanelRuntimeService.cs:232-234` | Reduziu de 3 batches iniciais para 1, eliminando flood no boot |
| `src/MicaAudio.Server/ServerPanelRuntimeService.cs:372` | Trocou `_ = DispatchBatchCommandAsync(...)` por `await DispatchBatchCommandAsync(...).ConfigureAwait(false)` — servidor aguarda ACK do ESP32 antes de produzir o proximo batch |
| `src/MicaAudio.Server/ServerPanelRuntimeService.cs:400` | Trocou `CancellationToken.None` por `cancellationToken` no `SendCommandTrackedAsync` de batch, permitindo cancelamento correto |
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp:922-961` | Removeu retorno cedo em `processQueuedControlCommands()` quando `gDeferredControlCommand != nullptr && isSlowCommandDomainBusy()`. Agora o loop tenta consumir comandos da fila que nao precisam de slow domain. Se um comando da fila tambem for deferred, descarta-o para nao sobrescrever o deferred existente |

## Decisoes tomadas

1. **Server: await no dispatch de batch** — Restaura back-pressure do servidor para o ESP32. Sem isso, 3 batches eram enfileirados em ~300ms enquanto o ESP32 demora 130-1110ms para baixar cada um. O `InMemoryPanelsBatchStore` limita a 4 batches por device, mas o ritmo de producao superava o de consumo, causando descarte de batches novos e underrun.
2. **Server: 1 batch inicial** — Reduz o burst de boot de 3 para 1. O loop de preload (`BatchPreloadLead`) ja reabastece automaticamente a cada ~500ms quando necessario.
3. **Server: cancellationToken no SendCommandTrackedAsync** — O dispatch de batch agora respeita o `CancellationToken` do loop principal, evitando comandos fantasma durante shutdown ou mudanca de estado.
4. **Firmware: nao bloquear fila inteira por deferred** — O retorno cedo em `processQueuedControlCommands()` paralisava heartbeats, comandos de controle leves e qualquer outro evento enquanto um batch baixava. Ao permitir que o loop consuma itens da fila nao-lentos, o firmware continua responsivo.
5. **Firmware: protecao contra double-defer** — Se um item da fila tambem retornar `Deferred` enquanto `gDeferredControlCommand` ja existe, o novo envelope e deletado para evitar perder o batch ja pendente. Isso e uma salvaguarda; com o server corrigido, nao deve ocorrer na pratica.

## Validacoes executadas

| Comando | Resultado |
|---------|-----------|
| `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` | OK (nenhuma falha encontrada) |
| `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` | OK (apos criacao deste handoff) |
| `dotnet build MicaAudio.sln -c Debug` | SUCCESS (0 warnings, 0 errors) |

## Riscos e rollback

- **Throughput de batches reduzido**: Com `await`, o servidor produz batches no ritmo do ESP32 (1 a cada 130-1110ms). Isso e intencional, mas em redes muito lentas pode causar gaps visuais menores entre batches. O `BatchPreloadLead` de 500ms ainda da margem. Se necessario, aumentar `BatchPreloadLead` para 750-1000ms.
- **Firmware loop mais longo**: Processar itens da fila enquanto deferred existe aumenta o tempo de `processQueuedControlCommands()`. O loop esta limitado a `kMaxControlCommandsPerLoop` (tipicamente 4-8), entao o impacto e pequeno. Monitorar `serial_max_us` no telemetry.
- **Rollback server**: Reverter linha 372 para `_ = DispatchBatchCommandAsync(...)` e linhas 232-234 para 3 batches iniciais. Reverter linha 400 para `CancellationToken.None`.
- **Rollback firmware**: Reverter `processQueuedControlCommands()` para o bloco original com `if (gDeferredControlCommand != nullptr && isSlowCommandDomainBusy()) { return; }`.

## Proximos passos

1. **Flash + testar no hardware** — Validar que painel nao desaparece apos 15s e que `hub75_fps` se mantem estavel.
2. **Monitorar telemetry** — Verificar `net_max_us` e `serial_max_us` para garantir que o loop de firmware nao estourou o budget.
3. Se underrun persistir, avaliar aumentar `BatchPreloadLead` ou o numero maximo de batches no `InMemoryPanelsBatchStore`.
