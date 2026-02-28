# Handoff Estrutural — 2026-02-28 — audiomotion-reactivity-tuning

## Objetivo

Restaurar a reatividade visual do `AudioMotion Clone` para um comportamento mais proximo do audioMotion, evitando empilhamento excessivo de smoothing.

## Escopo classificado

- Classificacao: funcional com impacto estrutural leve no modulo visual.
- Escopo: `AudioMotionCloneRenderer`, criacao do `AnalyzerConfig` em `MainPage` e documentacao do modulo visual.
- Fora de escopo: migrar outros renderers, alterar protocolo, firmware ou o pipeline base de audio.

## Arquivos alterados

- `src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `docs/wiki/modules/visual-win2d.md`

## Decisoes tomadas

1. O `AudioMotionCloneRenderer` voltou a consumir `BandsDisplay` diretamente, sem passar por `ReactiveBandSampler`.
2. O contrato `IRendererCapabilitiesProvider` foi mantido no renderer.
3. Quando o preset ativo e `AudioMotion Clone` e `FftSmoothing > 0.30`, o analyzer usa envelope leve:
   - `DisplaySmoothingRise = 1.00`
   - `DisplaySmoothingFall = 0.18`
   - `DisplayMotionDamping = 1.00`
4. O mesmo envelope leve e propagado para `OutputSmoothing*` para manter consistencia com HUB75.
5. A documentacao do modulo foi atualizada para registrar que o clone usa bandas cruas e envelope leve quando o smoothing FFT esta alto.

## Validacoes executadas

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
(dotnet build/test executados apos este handoff)
```

## Riscos e rollback

- Risco: o `AudioMotion Clone` pode ficar mais agressivo do que alguns presets abstratos.
- Risco: o envelope leve foi aplicado apenas ao clone; outros renderers continuam usando a estrategia anterior.
- Rollback:
  1. restaurar `AudioMotionCloneRenderer` para a versao com `ReactiveBandSampler`
  2. remover o branch `usesFftDrivenEnvelope` em `MainPage.CreateAnalyzer(...)`

## Proximos passos

1. Validar visualmente se o ataque voltou ao nivel esperado com o preset `AudioMotion Clone`.
2. Se ainda estiver “mole”, reduzir o default de `FftSmoothing` em uma entrega separada, com migracao explicita de defaults.
3. Se ficar bom, aplicar a mesma logica de envelope leve para outros renderers que precisem de resposta mais seca.
