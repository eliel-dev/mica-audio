# Handoff - Polar Arcs 2D Renderer

## Objetivo

Adicionar o preset `Polar Arcs` como renderer 2D classico em Win2D, sem shader GPU, integrado ao fluxo atual de presets.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: o renderer aparece na engine e no `PresetCombo`, renderiza sem excecao com frame valido/vazio e os gates de docs/governanca continuam verdes.

## Arquivos alterados

- `src/Visual.Win2D/Renderers/PolarArcsRenderer.cs`
- `src/Visual.Win2D/Engine/RendererIds.cs`
- `src/Visual.Win2D/Engine/VisualizerEngine.cs`
- `src/App.WinUI/Services/DefaultPresets.cs`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/guides/add-new-renderer.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. O port foi implementado como renderer CPU 2D para manter simplicidade e alinhamento com os demais renderers Win2D classicos.
2. A traducao do shader foi feita por geometria vetorial (aneis/arcos concentricos), sem SDF por pixel, porque isso entrega o comportamento visual esperado com custo menor e manutencao mais previsivel.
3. O preset entrou com palette monocromatica dedicada e sem painel novo de configuracao, preservando o fluxo atual de selecao por preset.
4. O mapeamento de audio foi mantido interno ao renderer, com 12 faixas low->high e smoothing leve por frame para evitar jitter excessivo.

## Validacoes executadas

```text
dotnet build src/Visual.Win2D/Visual.Win2D.csproj -c Debug -> OK
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug -> OK
```

## Riscos e rollback

- Risco principal: a leitura visual dos arcos pode precisar de ajuste fino de sweep/espessura para ficar mais proxima da referencia original.
- Como reverter: remover o registro `RendererIds.PolarArcs`, o preset `spectrum-polar-arcs` e o arquivo `PolarArcsRenderer.cs`.

## Proximos passos

1. Validar visualmente o sweep e a espessura dos 12 arcos com musica real.
2. Se necessario, expor ajuste fino apenas via `RendererParameters`, sem criar UI dedicada.
