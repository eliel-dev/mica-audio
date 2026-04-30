# Handoff - 2026-03-05 - devices-page-html-parity

## Objetivo
Implementar paridade visual 1:1 da `DevicesPage` e do wizard de `Novo dispositivo` com o contrato canonico `C:\Users\eliels\Pictures\nice\mica-dashboard.html`, preservando o fluxo tecnico ja funcional (onboarding USB, comandos e telemetria).

## Escopo classificado
- Classificacao: `funcional` (App.WinUI) com impacto visual amplo.
- Incluido:
  - reestruturacao visual completa da `DevicesPage`;
  - wizard custom em overlay (2 etapas);
  - restauracao da hierarquia visual do mock (header, brilho, metricas, tendencia, ESP-DASH, conectividade e logs);
  - atualizacao de smoke tests de contrato visual.
- Nao incluido:
  - mudancas de protocolo wire (HTTP/WS/serial);
  - mudancas em firmware;
  - mudancas de backend/servidor.

## Arquivos alterados
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/guides/setup-new-device.md`

## Decisoes tomadas
1. O HTML local passou a ser contrato visual direto para a composicao da tela.
2. O wizard foi mantido em 2 passos com overlay custom para controlar medidas/padding/radius.
3. O fluxo tecnico do onboarding nao foi alterado (apenas a camada visual).
4. O brilho permaneceu com faixa segura visivel `30..160`.
5. O detalhe do dispositivo manteve a ordem canonica: brilho -> metricas -> tendencia -> ESP-DASH -> conectividade -> logs.

## Validacoes executadas
1. `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug` -> OK.
2. `dotnet build MicaAudio.sln -c Debug` -> OK (na segunda execucao; a primeira falhou por lock transitorio de arquivo do XAML compiler).
3. `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests"` -> OK (4/4).
4. `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceOperationsCoordinator|FullyQualifiedName~DeviceServerHostSecurityTests"` -> OK (28/28).
5. `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK.
6. `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> OK apos criacao deste handoff.

## Riscos e rollback
- Risco: aumento de complexidade de UI programatica e maior custo de manutencao visual.
- Risco: drift futuro entre HTML canonico e WinUI.
- Mitigacao: manter constantes de spec (dimensoes/paddings/radius) e smoke test de contrato visual.
- Rollback: reverter commit desta entrega para voltar ao layout anterior, sem impacto em dados/protocolo.

## Proximos passos
1. Validacao manual lado a lado em DPI 100% com o HTML canonico (wizard passo 1/2 + detalhe online + sem selecao).
2. Se houver ajustes finos de pixel, concentrar apenas em constantes visuais e estilos auxiliares.
3. Opcional: consolidar uma checklist visual fixa para reduzir regressao em futuras alteracoes da `DevicesPage`.
