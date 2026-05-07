# Handoff - Panels batch firmware crash and remote contract

## Objetivo

Corrigir o incidente em que ativar um painel remoto deixava o HUB75 preto e reiniciava o ESP32-S3. O backtrace simbolizado apontou para `mica_commands.cpp` no `Serial.printf` de `queue_panels_batch`, antes do download do lote, e o servidor aguardava timeout porque o firmware reiniciava antes de publicar `commandProgress` final.

## Escopo classificado

Firmware/protocolo - altera firmware ESP32-S3, contrato operacional de comandos tracked no runtime remoto de paineis e documentacao de protocolo.

## Arquivos alterados

| Arquivo | Mudanca |
|---------|---------|
| `firmware/esp32s3-devkitc1/src/mica_commands.cpp` | `queue_panels_batch` valida `parameters` e campos obrigatorios antes do log detalhado, e troca o log de payload por `Serial.print` tipado sem varargs. |
| `src/MicaAudio.Server/ServerPanelRuntimeService.cs` | Transporte WebP batch voltou ao contrato funcional: ativa app de forma sincrona, prepara dois batches, nao envia frame direto e nao usa session context/heartbeat no hot path batch. |
| `tests/Output.Tests/FirmwareBootSourceLayoutTests.cs` | Guards para validacao antes do log detalhado e proibicao de `Serial.printf` no log detalhado de payload. |
| `tests/Output.Tests/ServerPanelRuntimeServiceTests.cs` | Guards para dois batches iniciais e comandos batch sem `DeviceCommandSessionContext`. |
| `docs/wiki/modules/device-server-protocol.md` | Documenta contrato legado do WebP batch e proibicao de log pre-validacao no firmware. |
| `docs/wiki/modules/paineis.md` | Documenta invariantes do runtime remoto de paineis. |
| `docs/wiki/modules/firmware-esp32s3-devkitc1.md` | Documenta validacao segura de `queue_panels_batch`. |

## Decisoes tomadas

1. **Corrigir o panic na origem** - O firmware nao pode usar `Serial.printf`/varargs no log detalhado de `queue_panels_batch`; o backtrace simbolizado cai em `_svfprintf_r` a partir de `mica_commands.cpp:595`, e os `EXCVADDR` pequenos (`0x1`, `0x3`, `0x5`, `0x7`) indicam leitura de ponteiro invalido antes do download do lote.
2. **Restaurar contrato batch que funcionou em `Funcionando100`** - `activate_app` e `queue_panels_batch` no caminho WebP batch sao comandos tracked legados, sem `clientId/ownerEpoch`.
3. **Heartbeat fora do batch hot path** - Batch WebP usa o ACK de `queue_panels_batch` como back-pressure; heartbeat/session context permanece relevante para frame transport owner-bound.
4. **Sem frame imediato em batch** - Stream binario bruto cancela playback WebP no firmware, entao o primeiro frame visivel do painel deve vir do lote baixado.

## Validacoes executadas

| Comando | Resultado |
|---------|-----------|
| `xtensa-esp32s3-elf-addr2line -pfiaC -e firmware.elf ...` | Confirmou `Serial.printf` em `mica_commands.cpp:595` como origem do panic. |
| Source guard PowerShell para `queue_panels_batch` | PASS: o trecho nao contem `Serial.printf("[cmd] queue_panels_batch` e chama `writePanelsBatchCommandLog(request);`. |
| `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` | PASS: 66 arquivos wiki, 425 links wiki->codigo e 93 backlinks validados. |
| `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` | PASS: governanca IA valida. |
| `dotnet build MicaAudio.sln -c Debug --no-restore` | Bloqueado no sandbox: falhou apos `MicaAudio.Core` sem warnings nem erros MSBuild reportados. |
| `dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter FullyQualifiedName~FirmwareBootSourceLayoutTests --no-restore` | Bloqueado no sandbox: `dotnet` tentou criar estado em `C:\Users\CodexSandboxOffline\.dotnet`; com `DOTNET_CLI_HOME` no workspace, o restore/build falhou sem erros MSBuild reportados. |
| `pio run -e esp32s3_devkitc1_dma_exp` | Bloqueado no sandbox: PlatformIO tentou criar lock em `C:\Users\eliels\.platformio\platforms.lock`; aprovacao externa foi recusada pelo ambiente. |

## Riscos e rollback

- **Firmware antigo**: servidor novo evita o contrato session-aware no batch, mas o ESP ainda precisa receber firmware com log de payload sem `Serial.printf` para eliminar o reboot.
- **Rollback servidor**: reintroduzir session context/heartbeat no batch pode voltar a travar o fluxo remoto que foi validado na branch funcional.
- **Rollback firmware**: mover o log detalhado para antes da validacao ou voltar a usar `Serial.printf` no log de payload reabre risco de panic antes do ACK tracked.

## Proximos passos

1. Flashar o firmware corrigido no ESP32-S3.
2. Subir o servidor Docker com este branch e ativar um painel server-capable.
3. Confirmar no serial que `queue_panels_batch` chega a `[batch] START`, `[batch] DOWNLOAD OK`, `[batch] VALIDATE OK` e `[batch] QUEUE OK`, sem reboot.
