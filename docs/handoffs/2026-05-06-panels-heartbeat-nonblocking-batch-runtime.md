# Handoff - Panels heartbeat nonblocking batch runtime

## Objetivo

Corrigir a regressao em que o runtime autonomo de paineis do `MicaAudio.Server` ficava sem exibir painel, mesmo com logs de batches enfileirados. A causa raiz foi o `session_heartbeat` no caminho critico: o servidor usava `SendCommandTrackedAsync`, enquanto o firmware renovava a sessao mas nao emitia `commandProgress` final para heartbeat, fazendo cada heartbeat aguardar timeout e atrasar `queue_panels_batch`.

## Escopo classificado

Firmware/protocolo - altera comportamento coordenado entre `MicaAudio.Server` e firmware ESP32-S3 para comandos session-aware, sem mudar o wire shape.

## Arquivos alterados

| Arquivo | Mudanca |
|---------|---------|
| `src/MicaAudio.Server/ServerPanelRuntimeService.cs` | Heartbeat de paineis passou a ser despachado fora do caminho critico do loop de batches/frames, com excecoes observadas e log debug. |
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp` | `session_heartbeat` com `commandId` agora publica `commandProgress` final com sucesso. |
| `tests/Output.Tests/ServerPanelRuntimeServiceTests.cs` | Adiciona regressao em que heartbeat tracked nunca conclui e os batches ainda precisam progredir. |
| `tests/Output.Tests/FirmwareBootSourceLayoutTests.cs` | Adiciona guarda para o contrato de `commandProgress` do heartbeat no firmware. |
| `docs/wiki/modules/device-server-protocol.md` | Documenta que heartbeat nao pode bloquear o caminho critico visual e deve completar tracked command. |
| `docs/wiki/modules/paineis.md` | Documenta heartbeat fora do caminho critico visual. |
| `docs/wiki/modules/firmware-esp32s3-devkitc1.md` | Documenta `commandProgress` final para `session_heartbeat`. |

## Decisoes tomadas

1. **Servidor nao aguarda heartbeat no loop visual** - Heartbeat renova lease, mas nao deve controlar back-pressure de batches. O back-pressure relevante para playback continua sendo o ACK de `queue_panels_batch`.
2. **Firmware fecha o contrato tracked** - Como o servidor envia `session_heartbeat` com `commandId`, o firmware precisa publicar progresso final. Sem isso, `SendCommandTrackedAsync` so termina por timeout.
3. **Sem reintroduzir frame imediato em batch** - O `SendFrame` imediato continua fora do modo batch porque o firmware cancela playback WebP quando recebe stream binario direto.
4. **Teste cobre firmware antigo e futuro** - O teste de servidor simula firmware que nunca finaliza heartbeat; o transporte de batches ainda deve continuar. O teste de firmware impede remover o `commandProgress` do heartbeat sem falha visivel.

## Validacoes executadas

| Comando | Resultado |
|---------|-----------|
| `dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter FullyQualifiedName~ServerPanelRuntimeServiceTests.BatchTransport_ShouldNotWaitForHeartbeatCompletionBeforeQueueingBatches --no-restore` | RED antes da correcao: falhou com `Heartbeat dispatch must not block batch queueing.` |
| `dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter FullyQualifiedName~FirmwareBootSourceLayoutTests.SessionHeartbeat_ShouldCompleteTrackedCommand --no-restore` | RED antes da correcao: falhou por ausencia de `sendCommandProgress` no heartbeat. |
| `dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter "FullyQualifiedName~ServerPanelRuntimeServiceTests.BatchTransport_ShouldNotWaitForHeartbeatCompletionBeforeQueueingBatches\|FullyQualifiedName~FirmwareBootSourceLayoutTests.SessionHeartbeat_ShouldCompleteTrackedCommand" --no-restore` | PASS (2/2). |
| `dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter FullyQualifiedName~ServerPanelRuntimeServiceTests --no-restore` | PASS (3/3). |
| `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` | OK: nenhuma falha encontrada. |
| `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` | OK: governanca IA valida. |
| `dotnet build MicaAudio.sln -c Debug` | SUCCESS (0 warnings, 0 errors). |
| `pio run -e esp32s3_devkitc1_dma_exp` | Nao executado: `pio`/`platformio` nao disponivel no PATH deste ambiente. |

## Riscos e rollback

- **Firmware nao atualizado**: servidor novo ainda nao bloqueia os batches mesmo se o firmware antigo nao finalizar heartbeat, mas logs tracked podem continuar registrando timeout ate o device receber firmware com esta correcao.
- **Heartbeat fire-and-forget no servidor**: falha pontual de heartbeat nao derruba o runtime; o loop seguinte reenviara. O lease tambem e renovado por comandos session-aware de batch.
- **Rollback servidor**: voltar `DispatchHeartbeat(...)` para `await SendHeartbeatAsync(...)` reintroduz o risco de bloquear batches se o firmware nao responder.
- **Rollback firmware**: remover `sendCommandProgress` de `session_heartbeat` reintroduz timeouts em qualquer cliente que use `SendCommandTrackedAsync` para heartbeat.

## Proximos passos

1. Flashar firmware atualizado no ESP32-S3 para eliminar timeouts tracked de heartbeat.
2. Testar painel ativo no servidor Docker observando cadencia de batches sem gaps de ~2s causados por heartbeat.
3. Se ainda houver tela apagada, coletar logs do firmware para `panelsWorkerState`, `lastSlowCommand`, `commandProgress` e `sessionActiveOwnerEpoch`.
