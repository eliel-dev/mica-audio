# Handoff - DevKitC-1 BINs embarcados e wizard simplificado

## Objetivo
Fechar o hotfix final da branch `hub75` com BINs DevKitC-1 embarcados e simplificacao do setup em `Dispositivos`, removendo `Copiar host` e pinagem manual da UX.

## Escopo classificado
- estrutural
- firmware/protocolo

## Arquivos alterados
- scripts/build-precompiled-firmware.ps1
- src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-stable_merged.bin
- src/App.WinUI/AppData/Firmware/esp32s3-devkitc1-dma_exp_merged.bin
- src/App.WinUI/Views/DevicesPage.Ui.cs
- src/App.WinUI/Views/DevicesPage.xaml.cs
- src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs
- tests/Integration.Smoke/FirmwareCatalogSmokeTests.cs
- docs/wiki/guides/setup-new-device.md
- docs/wiki/modules/server-build-and-artifacts.md

## Decisoes tomadas
1. BIN fonte nao e selecionado manualmente na UI; a resolucao e automatica por `boardModel + panelType + profile`.
2. O wizard `Novo dispositivo` ficou com tres seletores fixos (`Placa`, `Painel`, `Firmware`) e tres acoes (`Baixar firmware`, `Gerar pareamento`, `Fechar`).
3. `Copiar host` foi removido da barra de `Dispositivos` e do wizard.
4. A pinagem da placa e fixa por variante de firmware (preset DevKitC-1 v1.0), sem configuracao manual nesta fase.
5. Script oficial de build precompilado gera os dois BINs DevKitC-1 (`stable` e `dma_exp`) e faz merge deterministico com `esptool`.

## Validacoes executadas
1. `powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1 -SkipToolInstall`
2. `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
3. `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
4. `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug`
5. `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug`

## Riscos e rollback
1. Se um BIN embarcado for removido/renomeado, o wizard falha com erro de artefato ausente para a combinacao selecionada.
2. O fluxo de flash continua manual por ferramenta externa; erro de gravacao nao e coberto pelo app.
3. Rollback rapido: reverter alteracoes de `DevicesPage` e manter apenas o catalogo anterior de firmware.

## Proximos passos
1. Validar em hardware real DevKitC-1 (`stable` e `dma_exp`) o boot com painel HUB75 64x32.
2. Expandir testes de service para cobrir `TryResolveSource` com cenarios de arquivo ausente.
3. Atualizar guia de troubleshooting com mensagens reais coletadas em testes de campo.
