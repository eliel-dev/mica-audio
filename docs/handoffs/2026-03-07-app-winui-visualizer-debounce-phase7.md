# Handoff - Visualizador fluido com debounce

## Objetivo

Reduzir microtravadas e rebuilds redundantes no `Visualizador`, mantendo o escopo restrito ao core da `MainPage` e preservando o hardening de startup ja estabilizado.

## Escopo classificado

- Classificacao: estrutural curta com impacto funcional no `App.WinUI`.
- Escopo desta rodada:
  - separar runtime pendente do runtime aplicado do visualizer;
  - introduzir debounce unico de `150 ms` para ajustes finos do analyzer;
  - consolidar preset/renderer em um apply imediato unico;
  - impedir persistencia/rebuild redundante quando nao houver delta real.
- Fora desta rodada:
  - refactor novo em `AudioPipelineCoordinator`;
  - mudancas em `DevicesPage`, `Device.Server`, `Device.Protocol` ou firmware;
  - redesign visual do XAML.

## Arquivos alterados

- Runtime do visualizador:
  - `src/App.WinUI/Views/MainPage.xaml.cs`
  - `src/App.WinUI/Views/MainPage.Startup.cs`
  - `src/App.WinUI/Views/MainPage.VisualizerRuntime.cs`
  - `src/App.WinUI/Views/MainPage.Dispose.cs`
- Testes:
  - `tests/Integration.Smoke/MainPageStartupHelpersTests.cs`
- Documentacao:
  - `docs/wiki/modules/app-winui.md`
  - `docs/wiki/modules/settings-presets-persistence.md`
  - `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- O `Visualizador` passou a trabalhar com dois estados internos:
  - `pending/draft`, alterado imediatamente pela UI;
  - `applied`, usado pelo analyzer, render e output HUB75 ate o proximo apply valido.
- Ajustes finos (`LinearBoost`, `BarCount`, `FFT`, `Smoothing`, `Weighting` e `Frequency`) agora entram em debounce unico de `150 ms`.
- Preset e renderer continuam imediatos, mas usam o mesmo caminho consolidado de apply, sem cascata de rebuilds.
- O render (`MainCanvas`) e o `PumpHubFrameOutput()` passaram a consumir o runtime realmente aplicado, evitando inconsistencias durante a janela de debounce.
- Persistencia do runtime visualizer agora ocorre apenas quando o apply do analyzer realmente entra no alvo pedido; fallback seguro nao grava estado potencialmente invalido.
- O hardening existente foi preservado:
  - `crash.log` continua obrigatorio em arquivo;
  - a shell continua viva se a `MainPage` falhar;
  - `TryRebuildAnalyzer()` ainda degrade para fallback seguro ou preservacao do analyzer anterior.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1`
- `dotnet test MicaAudio.sln -c Debug --no-build -m:1`
- Validacao manual rapida:
  - `App.WinUI.exe` sobe e permanece viva por pelo menos 5 segundos;
  - processo verificado em execucao apos a mudanca de debounce.

## Riscos e rollback

- Risco principal:
  - algum ajuste fino aparentar atraso maior que o desejado para usuarios que esperavam rebuild por tick de slider.
- Risco residual:
  - `Resetar padrao` ainda depende do preset/render atual ser rebuildavel para confirmar a mensagem de sucesso.
- Rollback:
  - remover `MainPage.VisualizerRuntime.cs`;
  - voltar handlers do `MainPage` para `RebuildAnalyzer() + PersistCurrentVisualizerSettings()` imediatos;
  - restaurar `TryRebuildAnalyzer()` sem estado `pending/applied`.

## Proximos passos

- Validar manualmente a sensacao de fluidez ao arrastar `LinearBoost` e `FFT Smoothing`.
- Medir se `150 ms` entrega a melhor sensacao; se necessario, lapidar apenas esse valor, sem reabrir a arquitetura.
- Se a UX estabilizar, a proxima rodada natural e polir a configuracao da `MainPage` com foco em ergonomia e menor complexidade residual do code-behind.
