# Handoff - Devices Page Active App Preview Fix

## Objetivo

Corrigir a `DevicesPage` para deixar de exibir o nome do tipo interno na lista e passar a mostrar um preview do app ativo do device, com miniatura pequena por linha e preview maior para o item selecionado.

## Escopo classificado

- Classificacao: funcional
- Modulo principal: `App.WinUI`
- Sem mudancas em protocolo, firmware ou telemetria

## Arquivos alterados

- `src/App.WinUI/Services/Devices/DevicePreviewResolver.cs`
- `src/App.WinUI/Views/Controls/DeviceListRowControl.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/App.xaml.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `tests/Output.Tests/DevicePreviewResolverTests.cs`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

- A UI ativa da tela continua sendo a montagem programatica em `DevicesPage.Ui.cs`.
- A lista de devices agora usa um controle visual proprio por linha (`DeviceListRowControl`) em vez de depender de `ToString()` do item.
- As miniaturas da lista ficam estaticas para reduzir custo.
- O preview maior do painel da direita e o unico preview animado da pagina.
- Quando nao ha selecao, o preview maior fica em placeholder vazio.
- O preview tenta usar o app real via `ActiveAppId` + catalogo de apps; sem match, cai em fallback heuristico.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build MicaAudio.sln -c Debug`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~WinUiBootstrap"`

## Riscos e rollback

- Risco principal: a `DevicesPage` e montada em codigo; alterar o XAML nao corrige o runtime.
- Risco secundario: se o catalogo de apps falhar ao carregar, os previews ainda dependem do fallback heuristico.
- Rollback: restaurar `DevicesPage.Ui.cs` / `DevicesPage.xaml.cs` ao estado anterior e remover `DeviceListRowControl` + `DevicePreviewResolver`.

## Proximos passos

- Validar em runtime com devices reais e confirmar que `ActiveAppId` resolve corretamente para os apps do catalogo.
- Se aparecerem apps custom frequentes sem match, ampliar o mapeamento de fallback do `DevicePreviewResolver`.
