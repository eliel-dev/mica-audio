# Handoff - Hyper Tunnel audio reactivity and preset combo hotfix

## Objetivo

Ajustar a reatividade de audio do renderer `Hyper Tunnel` e corrigir a selecao de presets na `MainPage` para restaurar a troca de presets sem regressao visual.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `Hyper Tunnel` responde de forma perceptivel a `bass`, `mid` e `high`, e o `PresetCombo` volta a permitir a troca de predefinicoes no visualizador.

## Arquivos alterados

- `src/Visual.Win2D/Renderers/VizzyHyperTunnelShaderRenderer.cs`
- `src/Visual.Win2D/Shaders/HyperTunnelShadertoyShader.cs`
- `src/App.WinUI/Views/MainPage.xaml`
- `src/App.WinUI/Views/MainPage.xaml.cs`

## Decisoes tomadas

1. Mantive o caminho de audio existente (`SpectrumFrame -> HyperTunnelAudioMapper -> uniforms do shader`) e apenas aumentei os pesos de influencia para tornar a resposta ao audio perceptivel sem reescrever o pipeline.
2. Corrigi o `PresetCombo` usando `DisplayMemberPath` e `SelectedValuePath`, com leitura por `SelectedValue`, para evitar dependencia fragil de `SelectedItem` com objetos `ComboOption` no WinUI.
3. Preservei o comportamento atual dos demais `ComboBox` reutilizando `SelectComboOption` com fallback automatico para `SelectedItem` quando o controle nao tiver `SelectedValuePath`.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> pendente apos criacao deste handoff
dotnet build src/Visual.Win2D/Visual.Win2D.csproj -c Debug -> OK
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug -> OK
dotnet build MicaAudio.sln -c Debug -> OK
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~Visualizer" --no-restore -> OK
```

## Riscos e rollback

- Risco principal: o hotfix do `PresetCombo` muda a forma de selecao e pode expor algum comportamento diferente em outros fluxos se o controle passar a operar com `SelectedValue` nulo em momentos de inicializacao.
- Como reverter: restaurar `PresetCombo` para a configuracao anterior sem `DisplayMemberPath`/`SelectedValuePath` e voltar o handler `OnPresetSelectionChanged` para o uso de `SelectedItem`.

## Proximos passos

1. Validar manualmente no app a troca entre pelo menos 3 presets distintos, incluindo `Hyper Tunnel` e `Hyper Tunnel Classic`.
2. Se a reatividade ainda estiver sutil, ajustar apenas os coeficientes de `bass`, `mid` e `high` no shader sem mexer no pipeline de mapeamento.