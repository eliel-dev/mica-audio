# Handoff - Galeria de presets com preview animado em hover

## Objetivo

Substituir o `PresetCombo` do Visualizador por uma galeria visual em grid com todos os presets disponiveis, usando miniaturas animadas em hover/foco sem trocar a visualizacao principal.

## Escopo classificado

1. `PresetCombo` saiu da UX principal da `MainPage`.
2. `MainPage` ganhou `PresetGalleryPanel` com `GridView` horizontal.
3. Foram adicionados controles dedicados para miniatura e card:
   - `PresetPreviewThumbnailControl`
   - `PresetGalleryCardControl`
4. Foi adicionada uma fonte deterministica de `SpectrumFrame` demo:
   - `PresetPreviewSignalFactory`
5. Clique em card seleciona preset e persiste `ActivePresetId`.
6. Hover/foco apenas anima a miniatura do card.

## Arquivos alterados

- `src/App.WinUI/Views/MainPage.xaml`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Views/Controls/PresetPreviewThumbnailControl.cs`
- `src/App.WinUI/Views/Controls/PresetGalleryCardControl.cs`
- `src/App.WinUI/Services/Visualizer/PresetPreviewSignalFactory.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/PresetPreviewSignalFactoryTests.cs`
- `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`
- `docs/wiki/modules/visual-win2d.md`
- `docs/wiki/guides/change-visualizer-settings.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. A galeria substitui permanentemente o `PresetCombo` como fluxo principal de selecao.
2. O preview usa `VisualizerEngine` real com `SpectrumFrame` sintetico, nao audio ao vivo.
3. Apenas um card pode animar por vez.
4. Hover/foco nao alteram `activePreset`; apenas click/Enter/Space selecionam.
5. O preview e deterministico por `PresetId`, para manter consistencia entre sessoes.

## Validacoes executadas

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Visualizer|FullyQualifiedName~WinUiBootstrap"
```

## Riscos e rollback

1. `GridView` com `UserControl` como item pode exigir ajuste fino visual de spacing/focus no runtime.
2. Em maquinas muito fracas, o hover rapido entre muitos cards pode evidenciar custo de criacao de frame demo.
3. A miniatura usa frame demo sintetico; alguns presets podem parecer menos expressivos do que com audio real.

### Rollback

1. Reverter os arquivos de `MainPage` para restaurar `PresetCombo`.
2. Remover `PresetPreviewThumbnailControl`, `PresetGalleryCardControl` e `PresetPreviewSignalFactory`.
3. Reverter os testes/documentacao deste handoff.

## Proximos passos

1. Ajustar visual fino dos cards (spacing, densidade, estados de foco) com validacao manual.
2. Se necessario, adicionar filtro/busca de presets em uma entrega separada.


