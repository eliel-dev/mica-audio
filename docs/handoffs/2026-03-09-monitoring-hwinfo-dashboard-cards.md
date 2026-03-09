# Handoff de mudanca estrutural

## Objetivo

Introduzir a sessao `Monitoramento` no app com leitura local do HWiNFO64 via Shared Memory e resumir os sensores em um dashboard inspirado no InfoPanel, agora com 6 cards compostos orientados a hardware.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - a shell expõe `Monitoramento` como sessao primaria;
  - o topo do dashboard mostra 6 cards fixos (`Uso total`, `Temperatura geral`, `Memoria RAM`, `VRAM GPU`, `Consumo`, `Frequencia`);
  - cada card preserva o nome do hardware detectado quando houver leitura elegivel;
  - a secao do sensor (`C-State Ocupacao`, `DTS`, etc.) nao pode substituir o modelo da CPU/GPU no card;
  - labels localizados do HWiNFO para `RAM/VRAM` passam a ser reconhecidos;
  - RAM/VRAM `Disponivel` pode ser derivada de `Used + Total`;
  - se `RAM/VRAM` nao forem resolvidas pelo HWiNFO, o app pode usar fallback local do Windows;
  - a lista completa de sensores continua abaixo dos cards, com busca local.

## Arquivos alterados

- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/Views/ShellPage.xaml`
- `src/App.WinUI/Views/ShellPage.xaml.cs`
- `src/App.WinUI/Views/ShellPageContentFactory.cs`
- `src/App.WinUI/Views/MonitoringPage.xaml.cs`
- `src/App.WinUI/Views/MonitoringPage.Ui.cs`
- `src/App.WinUI/ViewModels/MonitoringPageViewModel.cs`
- `src/App.WinUI/Services/Monitoring/MonitoringContracts.cs`
- `src/App.WinUI/Services/Monitoring/MonitoringHardwareResolver.cs`
- `src/App.WinUI/Services/Monitoring/MonitoringTextNormalization.cs`
- `src/App.WinUI/Services/Monitoring/WindowsMemoryFallbackProvider.cs`
- `src/App.WinUI/Services/Monitoring/MonitoringSnapshotProjector.cs`
- `src/App.WinUI/Services/Monitoring/MonitoringKpiSelector.cs`
- `src/App.WinUI/Services/Monitoring/HwinfoSharedMemoryBinaryParser.cs`
- `src/App.WinUI/Services/Monitoring/HwinfoSharedMemorySource.cs`
- `tests/Output.Tests/HwinfoMonitoringTests.cs`
- `tests/Integration.Smoke/MonitoringPageSmokeTests.cs`
- `tests/Integration.Smoke/ShellPageContentFactoryTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

1. O dashboard superior ficou com 6 cards compostos fixos, em vez de widgets dinamicos, para manter o v1 denso e previsivel.
2. `MonitoringKpi` deixou de representar um tile de valor unico e passou a carregar duas metricas internas (`MonitoringKpiMetric`) com contexto de hardware por linha e origem (`Hwinfo` ou `WindowsFallback`).
3. A resolucao de nome do hardware foi separada da categoria do sensor, evitando que secoes como `C-State Ocupacao` virem o nome da CPU no card.
4. A selecao dos cards usa heuristicas opinadas por tipo de leitura com matching accent-insensitive e termos em ingles + PT-BR para memoria.
5. Quando `Available/Free` nao existe para RAM ou VRAM, o app primeiro deriva `Disponivel` a partir de `Used + Total`; se ainda assim nao resolver, pode cair em fallback local do Windows.
6. A UI de `MonitoringPage` permaneceu em WinUI nativo, com grid responsivo `3/2/1` e indicacao discreta quando uma metrica veio do fallback do Windows.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> sucesso
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> sucesso
dotnet build MicaAudio.sln -c Debug -> sucesso
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter HwinfoMonitoringTests -> sucesso
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "ShellPageContentFactoryTests|MonitoringPageSmokeTests" -> sucesso
```

## Riscos e rollback

- Risco principal: instalacoes com labels muito diferentes do padrao do HWiNFO64 ainda podem deixar um ou mais slots em `Indisponivel`, e o fallback de VRAM continua conservador para nao casar adaptador errado.
- Como reverter:
  - remover a aba `Monitoramento` da shell;
  - excluir `Views/MonitoringPage*`, `ViewModels/MonitoringPageViewModel.cs` e `Services/Monitoring/`;
  - restaurar a documentacao e os smoke tests ao estado anterior sem a sessao.

## Proximos passos

1. Validar manualmente com uma maquina real que tenha CPU + GPU dedicadas para confirmar os nomes detectados e as leituras de VRAM.
2. Se houver casos recorrentes de labels nao reconhecidos, ampliar a tabela de heuristicas no `MonitoringKpiSelector` com fixtures reais capturadas do HWiNFO64.
3. Quando houver demanda por historico, adicionar isso como trilha separada sem desmontar o contrato snapshot-only atual.
