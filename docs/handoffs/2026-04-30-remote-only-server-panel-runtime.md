# Handoff - Remote-Only Server Panel Runtime

## Objetivo

Remover o modo embedded do WinUI e mover o runtime autoritativo de paineis para o `MicaAudio.Server`, mantendo o ESP32-S3 como runtime de display conectado ao servidor remoto.

## Escopo classificado

- Mudanca estrutural.
- Mudanca de protocolo/admin API.
- Mudanca de arquitetura de runtime de paineis.
- Firmware/ESP32-S3 dentro do contrato existente, sem renderizador nativo de widgets nesta etapa.

## Arquivos alterados

- `src/App.WinUI/App.xaml.cs`
- `src/App.WinUI/App.WinUI.csproj`
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs`
- `src/App.WinUI/Services/Panels/PanelsFrameComposer.cs`
- `src/App.WinUI/Services/Panels/PanelsStore.cs`
- `src/App.WinUI/Views/SettingsPage.xaml.cs`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
- `src/Device.Client.Remote/RemoteDeviceServerClient.cs`
- `src/Device.Client.Abstractions/IDeviceServerClient.cs`
- `src/Device.Protocol/Models/PanelRuntimeStateDocument.cs`
- `src/Device.Protocol/Models/PanelRuntimeStatusDocument.cs`
- `src/Device.Protocol/Models/PanelWidgetItem.cs`
- `src/Device.Server.Abstractions/Hosting/IPanelRuntimeStateStore.cs`
- `src/Device.Server.Abstractions/Hosting/IPanelRuntimeStatusStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.*`
- `src/MicaAudio.Panels/*`
- `src/MicaAudio.Server/*Panel*`
- `tests/Integration.Smoke/*`
- `tests/Output.Tests/*`
- `docs/adr/0010-client-owned-lan-data-plane.md`
- `docs/wiki/architecture/01-system-overview.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/paineis.md`
- `docs/wiki/reference/code-index.md`

## Decisoes tomadas

1. `App.WinUI` passa a registrar apenas client remoto, frame transport remoto e runtime remoto.
2. `Device.Client.Embedded` foi removido da solution e do disco.
3. `DeviceServerMode` foi removido de settings; JSON legado com `Embedded` e ignorado.
4. `MicaAudio.Panels` concentra compositor, helpers, decoder e encoder sem WinUI e sem `System.Drawing`.
5. `MicaAudio.Server` assume `clientId = server-panels` para heartbeat/session context de paineis autonomos.
6. O WinUI ativa/desativa paineis salvando `PanelRuntimeStateDocument`; ele nao agenda nem envia frames ao device.
7. Widgets client-only sao omitidos no runtime autonomo e reportados no status.
8. O ESP continua recebendo batches/frames pelo servidor, sem renderizador nativo de widgets complexos.

## Validacoes executadas

- `dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter FullyQualifiedName~PanelRuntimeApiTests --no-restore` -> PASS (2/2).
- `dotnet test .\tests\Integration.Smoke\Integration.Smoke.csproj --filter FullyQualifiedName~WinUiBootstrapSmokeTests --no-restore` -> PASS (8/8).
- `dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter "FullyQualifiedName~ServerAbstractionBoundaryTests|FullyQualifiedName~PanelRuntimeApiTests" --no-restore` -> PASS (11/11).

Validacoes finais obrigatorias ainda devem ser registradas neste handoff antes do fechamento:

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `dotnet build MicaAudio.sln -c Debug`

## Riscos e rollback

- Risco: painel contendo apenas widgets client-only renderiza fallback simples no servidor. Mitigacao: status expõe `skippedWidgets`.
- Risco: midias locais antigas precisam ser salvas uma vez para upload e publicacao de `mediaId`/`mediaIds`.
- Risco: ambiente sem `MicaAudio.Server` standalone rodando abre WinUI offline. Mitigacao: client remoto falha de forma recuperavel e o app nao inicia server in-process.
- Rollback tecnico exigiria restaurar `Device.Client.Embedded`, `DeviceServerMode` e composition root antigo; nao recomendado porque contradiz a direcao remote-only.

## Proximos passos

1. Expandir renderers server-capable para clima real e status detalhado de device.
2. Adicionar UI de status do runtime remoto em `PanelsPage`.
3. Remover mencoes historicas antigas fora da documentacao ativa se elas passarem a confundir o fluxo atual.
4. Revisar firmware para expor telemetria mais precisa de consumo de batches no contrato ESP-IDF v5.5.4.
