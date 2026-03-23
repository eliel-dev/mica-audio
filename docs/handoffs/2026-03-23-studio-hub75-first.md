# Handoff - Studio HUB75-First

## Objetivo

Transformar o Studio do Visualizador em uma bancada de preset HUB75-first, com preview dominante, biblioteca compacta e inspetor enxuto, mantendo o modo `Painel HUB75` alinhado ao mesmo preview shipping do `Visualizador`.

## Escopo classificado

- Tipo: funcional
- Criterio de aceite:
  - abrir `Editar preset` a partir do `Visualizador` mantendo a rota oculta na shell;
  - mostrar breadcrumb `Visualizador / Studio de preset`, preview `Painel HUB75` por padrao e alternancia para `Canvas`;
  - preservar rename de built-in, variante `user-{presetId}` para cor de built-in e clone local por `Salvar como novo`;
  - validar `docs-validate`, `ai-governance-check` e `dotnet build`.

## Arquivos alterados

- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/MainPage.xaml`
- `src/App.WinUI/Views/MainPage.VisualEditor.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs`
- `src/App.WinUI/Views/VisualizerStudioPage.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/settings-presets-persistence.md`
- `docs/wiki/modules/visual-win2d.md`

## Decisoes tomadas

1. O Studio passou a usar o mesmo caminho shipping do `Visualizador` para o modo `Painel HUB75`: `AudioPipelineCoordinator` + `AudioPipelineFrameProcessor` resolvem o transporte e alimentam o `SimulatorLedOutput` compartilhado.
2. O desenho do frame 128x64 continua centralizado em `Hub75PreviewHelper.DrawFrame(...)`, evitando drift visual entre `MainPage` e `VisualizerStudioPage`.
3. O modo `Canvas` virou a superficie fiel do working copy em edicao; o modo `Painel HUB75` deixa de prometer projeção fiel de gradientes arbitrarios fora do contrato `Bins128`.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK
```

## Riscos e rollback

- Risco principal: a UX do Studio agora depende explicitamente de duas leituras diferentes do preset, `Canvas` para edicao fiel e `Painel HUB75` para comportamento shipping, o que exige microcopy clara para evitar expectativa errada.
- Como reverter:
  - restaurar o envio manual de frame 128x64 do working copy em `VisualizerStudioPage`;
  - recolocar `Hub75VisualizerFrameRenderer` como caminho principal do modo HUB75 do Studio;
  - atualizar a wiki para voltar a descrever o preview fiel local do working copy.

## Proximos passos

1. Fazer uma passada manual de UX no app para conferir breakpoint, scroll e legibilidade do rail compacto abaixo de `1280`.
2. Cobrir com smoke test o caminho `Editar preset -> Salvar -> Voltar ao visualizador`.
