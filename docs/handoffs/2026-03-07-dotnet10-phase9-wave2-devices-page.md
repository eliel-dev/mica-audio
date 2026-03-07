## Objetivo

Reduzir o monolito de `DevicesPage` em blocos menores por responsabilidade, preservando a UX atual de lista incremental, dashboard seguro, preview pump e onboarding USB.

## Escopo classificado

- Classificacao: estrutural
- Stack alvo: `.NET 10` / `C# 14`
- Limites mantidos:
  - sem mudanca visual intencional
  - sem mexer em `Device.Server`, `Device.Protocol` ou firmware
  - sem alterar `DevicesPage.Ui.cs` alem do consumo existente

## Arquivos alterados

- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/DevicesPage.Onboarding.cs`
- `src/App.WinUI/Views/DevicesPage.ListState.cs`
- `src/App.WinUI/Views/DevicesPage.PreviewPump.cs`
- `src/App.WinUI/Views/DevicesPage.Dashboard.cs`
- `src/App.WinUI/Views/DevicesPage.Selection.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- O arquivo principal da pagina ficou restrito a:
  - campos/estado
  - construtor
  - lifecycle
  - handlers gerais
- Os blocos pesados foram movidos para partials focados:
  - onboarding
  - state/list diff
  - preview pump
  - dashboard
  - selecao/detalhes
- O objetivo foi decomposicao mecanica com risco baixo:
  - sem renomear handlers
  - sem alterar assinaturas
  - sem criar novo contrato de UI

## Validacoes executadas

- `dotnet build MicaAudio.sln -c Debug --no-restore -m:1`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug -m:1`
- Validacao cumulativa final da fase 9 registrada na onda 3

## Riscos e rollback

- Risco principal: falha de wiring apos mover metodos entre partials.
- Sinais de problema:
  - eventos nao conectados
  - selecao nao restaurada
  - onboarding sem resposta
  - preview pump parado
- Rollback seguro:
  - restaurar `DevicesPage.xaml.cs` para a versao monolitica anterior
  - remover os partials desta onda

## Proximos passos

- Aplicar a mesma estrategia em `AppsPage`.
- Consolidar a validacao final da fase 9 e atualizar docs modulares.
