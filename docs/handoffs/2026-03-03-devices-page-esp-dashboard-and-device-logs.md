# Handoff - Entrega 3 DevicesPage dashboard ESP e logs por device

## Objetivo

Substituir o card de logs gerais da `DevicesPage` por dashboard de metricas do ESP e logs do device selecionado, consumindo os contratos das entregas 1 e 2 sem alterar o design base da pagina.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `DevicesPage` mostra dashboard e logs por `deviceId`, offline exibe ultimo snapshot conhecido, sem selecao usa placeholders estaveis e atualizacao com assinatura/cache para reduzir flicker.

## Arquivos alterados

- src/App.WinUI/Views/DevicesPage.Ui.cs
- src/App.WinUI/Views/DevicesPage.xaml.cs
- tests/Integration.Smoke/DevicesPageSmokeTests.cs
- docs/wiki/modules/app-winui.md
- docs/wiki/modules/device-server-protocol.md
- docs/wiki/reference/code-index.md
- docs/wiki/reference/troubleshooting-matrix.md
- docs/wiki/guides/setup-new-device.md
- docs/wiki/reference/device-telemetry-v2-fields.md
- docs/wiki/README.md

## Decisoes tomadas

1. O dashboard da `DevicesPage` usa `DeviceMetricsFormatter` como unica fonte de semantica para labels e derivacoes, evitando logica de metricas na View.
2. Logs passaram a ser exibidos por `deviceId` com `DeviceOperationsCoordinator.GetDeviceLogs(deviceId)`, removendo dependencia visual de logs globais.
3. Para reduzir flicker, foram adicionadas assinaturas/caches especificas para dashboard e logs do device selecionado, alem do diff incremental ja existente na lista.
4. Em offline, a UI exibe aviso explicito de ultimo snapshot conhecido sem apagar os dados persistidos.

## Validacoes executadas

```text
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests|FullyQualifiedName~WinUiBootstrapSmokeTests" -v q -> OK (7 aprovados)
dotnet test tests/Integration.Smoke/Integration.Smoke.csproj --no-build --filter "FullyQualifiedName~DevicesPageSmokeTests|FullyQualifiedName~WinUiBootstrapSmokeTests" -v q -> OK (7 aprovados)
powershell -ExecutionPolicy Bypass -File ./scripts/docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File ./scripts/ai-governance-check.ps1 -> OK
powershell -ExecutionPolicy Bypass -File ./scripts/mvvm-validate.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK (0 erros)
```

## Riscos e rollback

- Risco principal: estados raros de snapshot parcial podem exibir placeholders de metrica em momentos de reconexao rapida.
- Como reverter: restaurar `DevicesPage.Ui.cs` e `DevicesPage.xaml.cs` para o layout anterior de logs gerais e remover o smoke test/atualizacoes de docs desta entrega.

## Proximos passos

1. Adicionar smoke funcional de interacao (selecao alternando dois devices) para validar troca de logs/metricas sem regressao.
2. Considerar promover o dashboard para componente reutilizavel caso a mesma apresentacao seja necessaria em outras telas.
