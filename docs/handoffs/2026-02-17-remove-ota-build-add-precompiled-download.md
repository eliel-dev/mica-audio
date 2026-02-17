# Handoff - Remocao de build/OTA e download de BINs pre-compilados

## Objetivo

Remover totalmente do app o fluxo de build/export e OTA de firmware, substituindo por uma UX simples de download local de dois BINs pre-compilados (`stable` e `dma_exp`).

## Escopo classificado

- Estrutural
- Firmware/protocolo
- Documentacao operacional

## Arquivos alterados

- App/UI: `src/App.WinUI/App.xaml.cs`, `src/App.WinUI/Views/ServerPage.*`, `src/App.WinUI/Views/DevicesPage.*`
- Servicos: `src/App.WinUI/Services/Firmware/*`, `src/App.WinUI/Services/Devices/*`
- Protocolo/servidor: `src/Device.Protocol/Models/DeviceCommandType.cs`, `src/Device.Server/Hosting/*`
- Firmware: `firmware/matrixportal-s3/src/main.cpp`
- Documentacao: `README.md`, `docs/wiki/**`, `scripts/docs-validate.ps1`

## Decisoes tomadas

1. Firmware do app agora vem somente como asset pre-compilado (`AppData/Firmware/*.bin`).
2. Fluxo OTA foi removido de app, protocolo, servidor e firmware.
3. Build/export local de firmware foi removido da interface do app.
4. A aba `Servidor` passou a oferecer somente download/salvamento local dos BINs.
5. O codigo-fonte de firmware permanece no repositorio, mas fora do fluxo da UI.

## Validacoes executadas

1. `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK
2. `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> OK
3. `dotnet build MicaAudio.sln -c Debug` -> OK
4. `dotnet test MicaAudio.sln -c Debug --no-build` -> OK

## Riscos e rollback

- Risco: links/documentacao desatualizados apos remocao de OTA/build.
  - Mitigacao: `docs-validate` atualizado e executado.
- Risco: regressao na tela de dispositivos/servidor por remocao de estado de build.
  - Mitigacao: build/test e validacao manual do fluxo de comandos.
- Rollback: restaurar commit anterior e reintroduzir `FirmwareBuildService` + endpoints OTA removidos.

## Proximos passos

1. Validar manualmente em UI: baixar `stable` e `dma_exp`, cancelar e salvar.
2. Atualizar os dois BINs em `src/App.WinUI/AppData/Firmware/` sempre que houver nova versao de firmware.
3. Opcional: adicionar checksum visivel na UI para o BIN salvo.
