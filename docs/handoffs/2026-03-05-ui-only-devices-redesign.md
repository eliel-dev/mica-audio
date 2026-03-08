# Handoff - UI-only Devices Redesign (Fase 1)

## Objetivo

Aplicar o espelho visual da `DevicesPage` para reduzir ruido operacional sem alterar wire protocol, servidor ou firmware.

## Escopo classificado

- Classificacao: `funcional` (UI WinUI).
- Entrega: remocao da busca na lista, novo botao `Novo dispositivo` no rodape, ocultacao do painel de detalhes sem selecao, acoes `Testar LED` e `Remover` em pilha vertical, RSSI no topo.

## Arquivos alterados

- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/guides/setup-new-device.md`

## Decisoes tomadas

1. Busca removida da coluna de lista para simplificar operacao diaria.
2. Painel da direita ocultado quando nenhum device esta selecionado.
3. `Novo dispositivo` fica no rodape da lista para acao primaria de onboarding.
4. Acao de LED mantida como `Testar LED` (momentaneo), sem toggle continuo na UI.

## Validacoes executadas

1. `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests"` -> OK.
2. `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug` -> OK.
3. `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK.

## Riscos e rollback

- Risco: usuarios acostumados com busca podem sentir perda de filtro local.
- Rollback: reverter alteracoes de `DevicesPage.Ui.cs`/`DevicesPage.xaml.cs` para restaurar busca e layout anterior.

## Proximos passos

1. Validacao visual manual com dispositivo online/offline.
2. Seguir para fase 2 (onboarding USB end-to-end) com testes de integracao.
