# Handoff: Serial monitor copy/export + A3 extracao de funcoes do loop

## Objetivo

Adicionar botao "Copiar tudo" ao serial monitor do app WinUI e extrair as tres secoes principais do `loop()` do firmware em funcoes nomeadas (fase A3 da preparacao FreeRTOS).

## Escopo classificado

- Tipo: funcional (serial monitor) + firmware/protocolo (A3 preparacao FreeRTOS)
- Criterio de aceite: build WinUI sem erros, testes de SerialMonitor passando, builds `esp32s3_devkitc1_dma_exp` e `esp32s3_devkitc1_dma_diag` compilando sem warnings adicionais.

## Arquivos alterados

- `src/App.WinUI/Infrastructure/Serial/SerialMonitorService.cs`
- `src/App.WinUI/Views/SettingsPage.Observability.cs`
- `firmware/esp32s3-devkitc1/src/main.cpp`

## Decisoes tomadas

1. **ExportAllText() no service, nao na UI.** O join de linhas fica em `SerialMonitorService.ExportAllText()` (dentro de lock) para manter a UI desacoplada da logica de formatacao.
2. **Botao "Copiar tudo" entre Conectar e Limpar.** Ordem natural: `[Porta ▼] [Conectar] [Copiar tudo] [Limpar]`. Grid expandido de 3 para 4 colunas.
3. **`IsTextSelectionEnabled = true` em cada linha.** Permite selecionar e copiar texto de linhas individuais sem precisar de modo de selecao no ListView (que quebraria o auto-scroll).
4. **A3 usa anonymous namespace.** As tres funcoes extraidas (`processNetworkPoll`, `processSignalTimeout`, `processRenderFrame`) foram posicionadas dentro do anonymous namespace existente, antes do `}  // namespace`. Isso mantem a mesma visibilidade das demais funcoes internas e evita linkage externo desnecessario.
5. **Timing simplificado no loop().** Substituicao das expressoes `elapsedMicrosSince(a) - elapsedMicrosSince(b)` por subtracao direta de timestamps capturados antes de cada fase (`serialDoneUs - loopStartedUs`, `renderStartUs - networkStartUs`, `loopEndUs - renderStartUs`). Semanticamente equivalente, mais legivel.
6. **A3 nao muda semantica.** A extracao e puramente estrutural: mesma ordem de operacoes, mesmas variaveis, mesmos paths de render. Nenhum comportamento alterado.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug      -> 0 errors, 5 warnings (Magick.NET pre-existentes)
dotnet test (SerialMonitorService tests)  -> 7/7 passed
pio run -e esp32s3_devkitc1_dma_exp      -> SUCCESS (RAM 37.8%, Flash 38.0%)
pio run -e esp32s3_devkitc1_dma_diag     -> SUCCESS
```

## Riscos e rollback

- Risco principal: nenhum para serial monitor (feature aditiva isolada). Para o firmware, a extracao e mecanicamente segura; risco residual seria um erro de escopo de namespace, mas a compilacao confirma que isso nao ocorreu.
- Como reverter: `git revert` de cada commit individualmente. A3 e um commit atomico que pode ser desfeito sem afetar A1/A4.

## Proximos passos

1. **Testes manuais do firmware (baseline A3).** Executar T04 (bins), T05 (frame), T07 (wifi disconnect), T10 (brightness) e verificar que os logs `[perf]` continuam saindo normalmente.
2. **Fase B - baseline FreeRTOS.** Com A1+A3+A4 em producao, capturar metricas de loop_max_us, hub75_fps e loop_health durante streaming continuo por 5+ minutos.
3. **Fase C - FreeRTOS explicito.** Criar `networkTask` (Core 0) + `renderTask` (Core 1) + `gRenderQueue`. Pre-requisito: baseline da Fase B documentado.
4. **Plano de referencia.** Ver `C:\Users\eliels\.claude\plans\crystalline-nibbling-pixel.md` para checklist completo de invariantes (INV-01..INV-20) e testes manuais (T01..T18).
