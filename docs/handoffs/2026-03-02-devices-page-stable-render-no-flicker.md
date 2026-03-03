# DevicesPage Stable Render No Flicker

## Objetivo

Eliminar a sensacao de "recarregamento" visual da `DevicesPage`, removendo o rebuild total da lista em refresh normal e evitando restart desnecessario do preview maior do device selecionado.

## Escopo classificado

- Classificacao: funcional
- Area principal: `src/App.WinUI`
- Fora de escopo: protocolo, firmware, telemetria, reescrita da pagina em XAML declarativo

## Arquivos alterados

- `src/App.WinUI/Services/Devices/DeviceListRenderDiff.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/Controls/DeviceListRowControl.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/DeviceListRenderDiffTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/device-operations-coordinator.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- `DeviceListChanged` passou a ser a fonte principal de refresh da lista apos a carga inicial.
- `ApplyState(...)` nao reconstrui mais a lista de devices.
- A lista agora usa diff incremental por `DeviceId`, com assinatura estavel para no-op rapido quando o snapshot nao mudou.
- `ClearRenderedItems()` ficou restrito a teardown/descarte, nao a refresh normal.
- `DeviceListRowControl.Bind(...)` e `SetSelected(...)` ficaram idempotentes para evitar rebind visual e invalidacao desnecessaria.
- O preview maior so reinicia quando muda a identidade do device selecionado ou do app exibido.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build MicaAudio.sln -c Debug`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Devices|FullyQualifiedName~WinUiBootstrap"`

## Riscos e rollback

- Risco principal: regressao na preservacao de selecao ao inserir/remover/reordenar items.
- Risco secundario: item visual ficar stale se a assinatura de lista nao cobrir algum campo relevante.
- Rollback: restaurar o rebuild completo da lista e remover os guardas de rebind (nao recomendado, mas simples de fazer).

## Proximos passos

- Validar manualmente a `DevicesPage` com refresh continuo por alguns minutos.
- Se ainda houver sensacao de flicker, inspecionar se a assinatura inclui algum campo que muda sem necessidade e reduzir o churn do token.
