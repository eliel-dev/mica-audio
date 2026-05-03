# Guia - Download de firmware pre-compilado

## Objetivo

Baixar o release oficial fresco do firmware e salvar um BIN no local escolhido para flash manual externo.

Este e o caminho oficial de campo quando voce nao vai usar OTA: o app so garante o BIN oficial mais atual; a gravacao fica a cargo de ferramenta externa.

## Passos

1. Abrir a aba `Dispositivos` ou `Servidor`.
2. Clicar em `Baixar firmware`.
3. O app executa um preflight do release oficial com `EnsureOfficialFirmwareFreshAsync(...)`.
4. Se o release oficial estiver stale e o script oficial conseguir regenerar o pacote, o download segue com o novo artefato.
5. Se o app nao conseguir provar que o release oficial esta fresco, o download falha sem fallback silencioso para um `merged.bin` potencialmente stale.
6. Escolher o destino no `FileSavePicker` e confirmar.
7. Validar no log da tela que o download foi concluido.

## Nome do arquivo exportado

- O nome interno do artefato oficial continua estavel:
  - `esp32s3-devkitc1-128x64-dma_exp_merged.bin`
- O nome sugerido ao usuario no save picker inclui a versao do manifesto oficial:
  - formato: `<base>_<firmwareVersion>.bin`
  - exemplo: `esp32s3-devkitc1-128x64-dma_exp_merged_v0.0.0-8-g4f86ce0-dirty.bin`
- O rename vale apenas para o arquivo exportado pelo usuario; o pipeline interno de build, manifesto e OTA continua usando o nome interno estavel.

## Referencias de codigo

- [DevicesPage.SaveFirmwareAsync](../../../src/App.WinUI/Views/DevicesPage.Onboarding.cs#L1)
- [PrecompiledFirmwareService.PrepareOfficialFirmwareExportAsync](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)
- [PrecompiledFirmwareService.CopyArtifactToAsync](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)

## Checklist rapido

- [ ] O botao `Baixar firmware` funciona em `Dispositivos` e `Servidor`.
- [ ] O download usa o release oficial fresco, nao uma copia bruta stale.
- [ ] O nome sugerido inclui a versao oficial mostrada na UI.
- [ ] Cancelar o FileSavePicker nao gera erro.
- [ ] O arquivo salvo possui tamanho maior que zero.
- [ ] O log informa caminho final salvo.
