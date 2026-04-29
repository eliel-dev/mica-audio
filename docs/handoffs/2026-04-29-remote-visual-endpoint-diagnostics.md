# Handoff - Diagnostico de endpoint visual remoto

## Objetivo

Explicar e tornar visivel a causa do visualizador Remote nao usar UDP direto quando o servidor nao expoe `/api/v1/admin/visual-endpoints`.

## Escopo classificado

- Tipo: funcional + estrutural
- Criterio de aceite:
  - O WinUI nao reporta mais `404` generico para endpoints visuais remotos.
  - O diagnostico informa quando o servidor/container esta sem a rota atual de endpoints visuais.
  - `DevicesPage` mantem `Remover dispositivo` e remove `Reprovisionar Wi-Fi` da UI principal.

## Arquivos alterados

- `src/App.WinUI/Services/Devices/RemoteDeviceServerConnectionTester.cs`
- `src/Device.Client.Remote/RemoteDeviceFrameTransport.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/DevicesPage.Selection.cs`
- `tests/Output.Tests/RemoteDeviceServerConnectionTesterTests.cs`
- `tests/Output.Tests/RemoteDeviceServerClientTests.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. O erro `HTTP 404` em `/api/v1/admin/visual-endpoints` passa a ser tratado como servidor/container antigo sem a rota atual, porque nesse estado o cliente nao descobre IP/porta/token visual do ESP e nunca tenta UDP direto.
2. O transporte remoto agora limpa endpoints locais em falha HTTP explicita de discovery visual e grava mensagem operacional com acao de redeploy.
3. O botao `Reprovisionar Wi-Fi` saiu do card de resumo; o metodo tecnico `ExecuteReprovisionWifiAsync` foi preservado sem surface de usuario.
4. `Remover dispositivo` permanece visivel no card de resumo, com confirmacao e tentativa de revogacao quando online.

## Validacoes executadas

```text
dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj -c Debug --filter "DevicesPageShouldDeclareEmbeddedDashboardAndPairingFields|DevicesPageShouldKeepWebViewDashboardBridgeMethods" -> aprovado
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --filter "RemoteDeviceServerConnectionTesterTests|RemoteDeviceFrameTransport_ShouldExplainMissingVisualEndpointsRoute" -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\docker-server-redeploy.ps1 -AdminToken dev-token -> aprovado; endpoint visual passou de 404 para 200
GET http://127.0.0.1:5272/api/v1/admin/visual-endpoints -> 200; 1 endpoint: 192.168.1.34:5274 bins128
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> aprovado
dotnet build .\MicaAudio.sln -c Debug -> primeira tentativa falhou por lock temporario do XAML compiler em input.json; apos dotnet build-server shutdown, aprovado com 0 avisos/0 erros
git diff --check -> aprovado
```

## Riscos e rollback

- Risco principal: redeploy do Docker ainda e necessario para o servidor real passar a expor a rota; a mudanca do cliente so torna o erro claro.
- Como reverter: restaurar o uso de `GetFromJsonAsync` direto no transporte remoto e recolocar `ReprovisionWifiButton` na `DevicesPage.Ui.cs`.

## Proximos passos

1. Recriar o container local com `scripts/docker-server-redeploy.ps1`.
2. Validar `GET /api/v1/admin/visual-endpoints` retornando `200` e pelo menos um device quando o ESP estiver online/MQTT.
3. Reiniciar o WinUI em modo Remote e confirmar `UDP direto enviados > 0`.
