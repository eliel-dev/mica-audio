# Guia - Setup New Device (USB end-to-end)

## Objetivo

Documentar o fluxo oficial de onboarding de novo dispositivo usando WinUI em 2 etapas:

1. Wi-Fi (SSID + senha)
2. USB/COM (flash + provisionamento + pareamento automatico)

## Passos

1. Abrir `Dispositivos`.
2. Clicar em `Novo dispositivo` no rodape da lista.
3. Etapa 1: informar `SSID` e `senha Wi-Fi`.
4. Etapa 2: selecionar porta COM detectada automaticamente (com opcao `Mostrar todas as portas`).
5. Confirmar `Iniciar onboarding`.

## Tela Dispositivos

Pipeline executado pelo app:

1. Resolve firmware oficial `esp32s3-devkitc1-128x64-dma_exp_merged.bin`.
2. Flasha o ESP32-S3 via `esptool`.
3. Gera `pair code` internamente (oculto na UI).
4. Envia request serial `mica.serial.v1` com:
   - `ssid`
   - `password`
   - `serverBaseUrl`
   - `pairCode`
5. Aguarda `result` serial.
6. Verifica entrada online no dashboard por timeout deterministico.

## Contrato serial `mica.serial.v1`

- `hello`
  - `{"type":"hello","protocol":"mica.serial.v1","deviceId":"...","firmwareVersion":"...","capabilities":["provision"]}`
- `provision` (host -> firmware)
  - `{"type":"provision","requestId":"...","ssid":"...","password":"...","serverBaseUrl":"...","pairCode":"..."}`
- `progress`
  - `{"type":"progress","requestId":"...","stage":"wifi_connecting|wifi_connected|pairing|done|error","message":"..."}`
- `result`
  - `{"type":"result","requestId":"...","ok":true|false,"errorCode":"...","message":"...","deviceId":"..."}`

## Politica de seguranca para credenciais

1. Senha Wi-Fi e efemera.
2. Nao persistir senha em `settings.json`.
3. Nao gravar senha em logs/handoffs.

## Fallback operacional

Se onboarding USB falhar:

1. Validar porta COM e cabo.
2. Atualizar lista de portas.
3. Repetir onboarding.
4. Em ultimo caso, usar provisioning por portal AP (fluxo manual legado).

## Checklist rapido

1. Botao `Novo dispositivo` visivel no rodape da lista.
2. Wizard abre com 2 etapas (`Wi-Fi` e `Porta USB`).
3. Porta COM detectada automaticamente (ou via `Mostrar todas as portas`).
4. Onboarding conclui com device online no dashboard.

## Referencias de codigo

- [DevicesPage UI](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DevicesPage code-behind](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DeviceUsbOnboardingService](../../../src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs#L1)
- [SerialPortCatalogService](../../../src/App.WinUI/Infrastructure/Serial/SerialPortCatalogService.cs#L1)
- [SerialProvisioningClient](../../../src/App.WinUI/Infrastructure/Serial/SerialProvisioningClient.cs#L1)
- [EspToolFlashService](../../../src/App.WinUI/Services/Devices/Onboarding/EspToolFlashService.cs#L1)
- [Firmware main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
