# Handoff - 2026-04-18 - provisioned-boot-wifi-before-hub75

## Objetivo

Eliminar `ESP_ERR_NO_MEM` e falhas de `esp_wifi_init()` no boot de devices ja provisionados, fazendo o bring-up inicial do STA acontecer antes do runtime pesado do HUB75.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Inclui: ordem de boot do firmware ESP32-S3, inicializacao do watchdog, alocacao da fila de jobs lentos, logs seriais de memoria no boot e documentacao operacional.
- Nao inclui: retuning de buffers do Wi-Fi, troca do perfil DMA/HUB75, migracao para ESP-IDF nativo ou mudanca no wire protocol.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/src/mica_commands.cpp`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `tests/Output.Tests/FirmwareBootSourceLayoutTests.cs`
- `docs/handoffs/2026-04-18-provisioned-boot-wifi-before-hub75.md`

## Decisoes tomadas

1. O boot provisionado foi quebrado em `boot leve` e `boot HUB75`; o primeiro `WiFi.begin()` agora acontece antes de `initMatrixDisplay()` e `initializePanelsBatchRuntime()`.
2. O hotfix foi limitado ao ponto de maior pressao de heap interna: nao houve retuning de buffers do Wi-Fi nem mudanca do perfil oficial `dma_exp`.
3. `gSlowCommandQueue` deixou de ser criada no boot; ela volta a nascer apenas sob demanda junto com o `control worker`.
4. A configuracao do TWDT passou a tentar `esp_task_wdt_reconfigure()` antes de `esp_task_wdt_init()` para evitar o log ruidoso `TWDT already initialized`.
5. O serial oficial ganhou snapshots `boot_mem` em quatro pontos para comparar heap geral e bloco DMA interno antes do Wi-Fi e antes do HUB75.

## Validacoes executadas

- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter FullyQualifiedName~FirmwareBootSourceLayoutTests`
  - primeiro em `red`: falhou validando que o boot ainda inicializava HUB75 cedo demais, criava `gSlowCommandQueue` no boot e chamava `esp_task_wdt_init()` antes do reconfigure.
  - depois em `green`: OK.
- `platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1` -> OK.
- Validacoes obrigatorias completas pendentes de execucao ao fim da tarefa:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
  - `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
  - `dotnet build MicaAudio.sln -c Debug`

## Riscos e rollback

- Risco principal: o painel pode demorar alguns segundos a mais para aparecer no boot provisionado, porque o STA ganhou prioridade na grace inicial.
- Mitigacao: o hotfix nao altera o comportamento de provisioning manual nem o AP-first do flash limpo; apenas posterga o runtime pesado do HUB75 no boot provisionado.
- Rollback:
  1. recolocar `initMatrixDisplay()` e `initializePanelsBatchRuntime()` dentro do carregamento unico de runtime antes do `WiFi.begin()`;
  2. recriar `gSlowCommandQueue` em `initializeControlCommandRuntime()`;
  3. voltar a ordem anterior `esp_task_wdt_init() -> esp_task_wdt_reconfigure()`.

## Proximos passos

1. Validar em hardware real `power cycle` e botao `reset` com serial aberto, confirmando ausencia de `ESP_ERR_NO_MEM`.
2. Conferir os snapshots `boot_mem` para verificar folga maior antes do primeiro `WiFi.begin()` do que antes do `initMatrixDisplay()`.
3. Se ainda houver falha de memoria no boot provisionado, usar esses logs para decidir um ajuste isolado no perfil HUB75 ou nos buffers do Wi-Fi.
