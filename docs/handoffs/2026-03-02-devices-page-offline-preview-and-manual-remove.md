# Handoff - DevicesPage Offline Preview and Manual Remove

## Objetivo

Ajustar a `DevicesPage` para refletir melhor o estado real dos devices: trocar o icone de offline para uma semantica passiva, ocultar preview visual quando o device estiver offline e expor a remocao manual do registro local via botao `Remover`.

## Escopo classificado

- Classificacao: funcional
- Modulo principal: `App.WinUI`
- Sem mudancas em protocolo, firmware ou telemetria

## Arquivos alterados

- `src/App.WinUI/Services/Devices/DeviceLifecycleIcon.cs`
- `src/App.WinUI/Services/Devices/DeviceLifecyclePolicy.cs`
- `src/App.WinUI/Services/Devices/DevicePreviewVisibilityPolicy.cs`
- `src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Views/Controls/DeviceListRowControl.cs`
- `tests/Output.Tests/Output.Tests.csproj`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/modules/app-winui.md`

## Decisoes tomadas

- `Offline | Configurado` deixou de usar a semantica visual de sincronizacao e passou a usar `Pause`.
- Devices offline continuam visiveis, mas sem miniatura do app na lista.
- O painel da direita so mostra preview grande para devices online.
- Para offline, a UI mostra apenas placeholder (`Dispositivo offline`) e o texto `Ultimo app conhecido: ...`.
- A decisao de exibir preview ficou centralizada em `DevicePreviewVisibilityPolicy`.
- A remocao manual foi exposta via `DeviceOperationsCoordinator.RemoveDevice(...)`, mantendo a UI desacoplada do `DeviceIntegrationService`.
- `Remover` significa exclusao apenas do registro local; `Revogar` continua sendo a acao remota para devices online.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug`
- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-restore`
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --no-restore --filter "FullyQualifiedName~Devices|FullyQualifiedName~WinUiBootstrap"`

## Riscos e rollback

- Risco principal: usuarios podem estranhar a ausencia de preview visual para offline; isso e intencional para nao sugerir atividade inexistente.
- Risco secundario: a remocao local pode ser confundida com uma revogacao remota; a confirmacao deixa esse limite explicito.
- Rollback: restaurar o icone antigo, permitir preview offline novamente e remover o botao `Remover` + `DeviceOperationsCoordinator.RemoveDevice(...)`.

## Proximos passos

- Validar manualmente com um device offline e um online, confirmando a diferenca entre `App ativo` e `Ultimo app conhecido`.
- Se o fluxo de remocao local estiver claro na pratica, manter; se ainda gerar duvida, reforcar o texto explicativo do dialogo.
