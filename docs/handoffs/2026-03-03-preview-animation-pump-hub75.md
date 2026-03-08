# Handoff - Preview animado + pump de frame real HUB75

## Objetivo

Implementar politica de animacao diferenciada entre as telas de dispositivos e apps, e alimentar frames reais do `SimulatorLedOutput` nas miniaturas de devices com visualizer ativo.

## Escopo classificado

- Tipo: funcional + UX
- Abrangencia: `src/App.WinUI/Views`, `src/App.WinUI/Views/Controls`, `src/App.WinUI/App.xaml.cs`, docs.

## Arquivos alterados

- src/App.WinUI/Views/DevicesPage.xaml.cs
- src/App.WinUI/Views/Controls/DeviceListRowControl.cs
- src/App.WinUI/Views/Controls/AppCatalogCardControl.cs
- src/App.WinUI/Views/AppsPage.xaml.cs
- src/App.WinUI/App.xaml.cs
- docs/wiki/modules/app-winui.md
- docs/wiki/guides/setup-new-device.md

## Decisoes tomadas

1. **DevicesPage — sempre animado**: `DeviceListRowControl.Bind()` chama `preview.Start()` automaticamente. Miniaturas ficam sempre vivas enquanto a pagina esta carregada.
2. **AppsPage — hover-only**: `AppCatalogCardControl` liga animacao em `PointerEntered` e desliga em `PointerExited`. O auto-start que existia em `RefreshPreviewPlayback` foi removido.
3. **Preview pump**: timer UI de 8 Hz (`DispatcherQueueTimer`, 125ms) na `DevicesPage` alimenta frames reais do `SimulatorLedOutput` para linhas cujo `AppId == visualizer-hub75`.
4. **Fetch lazy**: o frame do simulador so e lido se existir ao menos uma linha com visualizer ativo, evitando custo desnecessario.
5. **Guard de concorrencia**: o tick do pump respeita `isApplyingDeviceList` para nao competir com o diff incremental. O pump opera em snapshot estavel de `renderedItemsByDeviceId.Values`.
6. **Lifecycle**: pump inicia em `OnLoaded`, para em `OnUnloaded`. `ClearRenderedItems` chama `StopPreview` em todos os itens.
7. **Injecao**: `SimulatorLedOutput` (singleton, ja registrado) foi injetado na `DevicesPage` via construtor.

## Justificativa da mudanca em relacao ao handoff anterior

O handoff de 2026-03-02 mantinha miniaturas estaticas na DevicesPage. A decisao foi revisada porque:
- Miniaturas animadas dao feedback imediato sobre o estado do device sem exigir selecao.
- O custo e controlado: cada miniatura usa timer proprio de 30fps, mas o pump de frame real roda a 8Hz e so acessa o simulador quando necessario.
- Na AppsPage, hover-only reduz custo de CPU quando muitos cards estao visiveis (cenario de catalogo expandido).

## Validacoes executadas

- `dotnet build MicaAudio.sln -c Debug` — sucesso.
- Testes `Hub75VisualizerSessionServiceTests` — 3/3 passaram.
