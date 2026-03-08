# Guia - Setup New Device (USB + AP)

## Objetivo

Documentar o fluxo oficial e estavel de onboarding:

1. selecionar porta COM no wizard
2. gravar firmware
3. exibir codigo de pareamento
4. configurar Wi-Fi no portal AP do ESP32

## Passos

1. Abrir `Dispositivos`.
2. Clicar em `Novo dispositivo` no rodape da lista.
3. Selecionar a porta COM do ESP32 (`Atualizar portas` quando necessario).
4. Clicar em `Concluir` para gravar o firmware.
5. Anotar o codigo de pareamento exibido ao fim do flash.
6. Conectar no AP `MicaAudio-Setup-xxxx`.
7. No portal do ESP32, informar Wi-Fi/servidor e o codigo de pareamento.

## Contrato visual do wizard

1. Fonte canonica: `C:\Users\eliels\Pictures\nice\mica-dashboard.html`.
2. Especificacao aplicada na WinUI:
   - card `560px` (margem lateral `14px`);
   - head/body/footer com paddings `14/16`, `14/16`, `10/16`;
   - barra de etapas com altura `4px` e gap `8px`;
   - controles com altura `34px`;
   - botao `Concluir` no rodape a direita.
3. O wizard da WinUI e um overlay custom e nao depende de `ContentDialog`.

## Tela Dispositivos

### Pipeline executado pelo app

1. Resolve firmware oficial `esp32s3-devkitc1-128x64-dma_exp_merged.bin`.
2. Flasha o ESP32-S3 via `esptool`.
3. Gera `pair code` e mostra em modal.
4. Finaliza wizard e orienta provisioning via AP.
5. O device fica online no dashboard apos configuracao no portal.

## Perfil oficial do comando de flash

Comando canonico usado no onboarding (bundle local ou fallback `python -m esptool`):

```powershell
python -m esptool --chip esp32s3 --port COMx --baud 115200 --before default_reset --after hard_reset write_flash --no-compress 0x0 firmware.bin
```

Regras fechadas:

1. Usa `--before default_reset` e `--after hard_reset`.
2. Usa `--no-compress` (nao usa `-z`).
3. Nao executa `erase_flash` automatico.

## Progresso de flash no wizard

1. Durante `Flashing`, o wizard exibe barra `0..100` + `%`.
2. O percentual exibido vem da saida do `esptool` (`NN%` ou `NN %`).
3. Em falha, o ultimo percentual permanece visivel junto da mensagem de erro.

## Contrato serial `mica.serial.v1` (compatibilidade)

1. O protocolo serial permanece no firmware e no app para compatibilidade/futuro.
2. O onboarding oficial nao depende mais de handshake serial para concluir o fluxo.

## Politica de seguranca para credenciais

1. Senha Wi-Fi e efemera.
2. Nao persistir senha em `settings.json`.
3. Nao gravar senha em logs/handoffs.

## Fallback operacional

Se onboarding USB falhar:

1. Validar porta COM e cabo.
2. Atualizar lista de portas.
3. Repetir onboarding.
4. Provisionar manualmente pelo AP e repetir somente pareamento.

## Checklist rapido

1. Botao `Novo dispositivo` visivel no rodape da lista.
2. Wizard abre com selecao de porta COM + progresso de flash.
3. Porta COM detectada automaticamente (ou via `Atualizar portas`).
4. Ao fim do flash, app mostra codigo de pareamento.
5. Device entra online apos provisioning via AP.

## Referencias de codigo

- [DevicesPage UI](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DevicesPage code-behind](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DeviceUsbOnboardingService](../../../src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs#L1)
- [SerialPortCatalogService](../../../src/App.WinUI/Infrastructure/Serial/SerialPortCatalogService.cs#L1)
- [EspToolFlashService](../../../src/App.WinUI/Services/Devices/Onboarding/EspToolFlashService.cs#L1)
- [Firmware main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
