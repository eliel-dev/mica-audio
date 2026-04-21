# Guia - Setup New Device

## Objetivo

Documentar o fluxo oficial atual de onboarding do Mica sem flash USB interno no desktop.

Fluxo oficial:

1. Baixar o firmware oficial no app.
2. Gravar o BIN com ferramenta externa.
3. Gerar `pair code` no desktop com `Parear`.
4. Copiar o host LAN com `Copiar host`.
5. Concluir o provisioning no AP `MicaAudio-Setup-xxxx`.

## Passos

1. Abrir `Dispositivos`.
2. Clicar em `Baixar firmware`.
3. Salvar o BIN oficial sugerido pelo app.
4. Gravar o arquivo com a ferramenta externa de sua escolha.
5. Voltar ao app e clicar em `Parear`.
6. Copiar o `pair code` exibido no banner inline.
7. Clicar em `Copiar host`.
8. No celular, conectar ao Wi-Fi `MicaAudio-Setup-xxxx`.
9. Abrir o portal do ESP32 e preencher:
   - `Servidor`
   - `Codigo pareamento`
   - `Nome dispositivo` opcional
10. Confirmar que o device aparece na `DevicesPage`.

## Fonte do firmware mais atual

1. O desktop continua tratando `PrecompiledFirmwareService` como fonte unica do "ultimo firmware".
2. Em workspace/dev, o app valida se o pacote oficial local ficou stale em relacao aos fontes do firmware e tenta regenerar o artefato oficial.
3. Fora do workspace/dev, vale o pacote oficial embarcado no app.
4. O app nao consulta manifesto remoto nem release externo para decidir `Ultimo release`.

## O que mudou

1. O botao `Novo dispositivo` foi removido.
2. O desktop nao executa mais `esptool`, wizard USB, recaptura de boot ou logs seriais de onboarding.
3. O banner inline de `Parear` virou a superficie oficial para obter o `pair code`.
4. `Copiar host` virou o apoio oficial para preencher o portal AP manualmente.

## OTA e dashboard

1. O dashboard continua mostrando `Firmware atual` e `Ultimo release` mesmo para device offline.
2. O CTA `Atualizar firmware` existe apenas quando:
   - ha pacote oficial compativel;
   - a versao atual difere do ultimo release;
   - o device esta online para OTA agora.
3. Device offline nao oferece reflash pelo desktop; o caminho oficial passa a ser download manual + ferramenta externa.

## Checklist rapido

1. `Baixar firmware` salva um BIN oficial.
2. O nome sugerido inclui a versao do manifesto oficial.
3. `Parear` exibe um `pair code` no banner inline.
4. `Copiar host` entrega um endereco LAN valido, nao loopback.
5. O portal AP aceita `Servidor` + `Codigo pareamento`.
6. O device aparece no dashboard depois do pareamento.

## Troubleshooting rapido

1. Se `Baixar firmware` falhar, validar os arquivos em `src/App.WinUI/AppData/Firmware/` e o estado do `PrecompiledFirmwareService`.
2. Se o device continuar offline depois do flash manual, confirmar no portal AP se o `Servidor` aponta para `http://<IP-do-PC>:5272`.
3. Se o device nao anunciar `Ultimo release`, o app nao conseguiu provar que o pacote oficial local esta fresco.

## Referencias de codigo

- [DevicesPage code-behind](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DevicesPage download/pairing](../../../src/App.WinUI/Views/DevicesPage.Onboarding.cs#L1)
- [DevicesPage OTA](../../../src/App.WinUI/Views/DevicesPage.FirmwareUpdate.cs#L1)
- [DevicesPage UI](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)
- [Firmware main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
