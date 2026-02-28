# Handoff Estrutural — 2026-02-28 — audiomotion-reactivity-tuning

## Objetivo

Restaurar a reatividade visual do `AudioMotion Clone` alinhando o comportamento ao branch `main`, onde o preset ainda estava no ponto esperado.

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
3. O `AnalyzerConfig` do clone foi alinhado ao `main`, mantendo o envelope padrao:
   - `DisplaySmoothingRise = 0.82`
   - `DisplaySmoothingFall = 0.06`
   - `DisplayMotionDamping = 0.30`
4. Os mesmos valores padrao foram mantidos em `OutputSmoothing*` para preservar o comportamento do preview HUB75.
5. A documentacao do modulo foi atualizada para registrar que o clone usa bandas cruas e envelope padrao do analyzer, alinhado ao `main`.

## Validacoes executadas

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Visualizer"
```

## Riscos e rollback

- Risco: a reatividade continuar abaixo do esperado por outro fator fora do clone (preset salvo, configuracao local ou analyzer global).
- Rollback:
  1. restaurar o envelope especial introduzido neste branch
  2. reavaliar o preset builtin salvo em disco antes de qualquer novo smoothing no renderer

## Proximos passos

1. Validar visualmente se o `AudioMotion Clone` voltou ao comportamento do `main`.
2. Se ainda estiver “mole”, comparar o preset salvo em `%AppData%\MicaAudio` com o seed do `main`.
3. So depois disso decidir se `FftSmoothing` default precisa mudar.
