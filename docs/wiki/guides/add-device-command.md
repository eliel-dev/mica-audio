# Guia - Adicionar novo comando de dispositivo

## Objetivo

Adicionar um comando tracked ponta-a-ponta (app -> servidor -> firmware -> ACK/progresso).

## Passos

1. Definir tipo no `DeviceCommandType`.
2. Atualizar mapeamento wire em `CommandTypeToWire`.
3. Enviar comando via `SendCommandTrackedAsync`.
4. Tratar comando no firmware (`onWsEvent`).
5. Enviar progresso/ACK de volta.

## Referencias de codigo

- [DeviceCommandType](../../../src/Device.Protocol/Models/DeviceCommandType.cs#L1) - assinatura: enum de comandos
- [DeviceCommandRequest](../../../src/Device.Protocol/Models/DeviceCommandRequest.cs#L3) - assinatura: envelope de comando
- [DeviceServerHost.SendCommandTrackedAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L186) - assinatura: `SendCommandTrackedAsync(...)`
- [SendTrackedCommandCoreAsync](../../../src/Device.Server/Hosting/DeviceServerHost.Advanced.cs#L22) - assinatura: correlacao por `commandId`
- [onWsEvent firmware](../../../firmware/matrixportal-s3/src/main.cpp#L434) - assinatura: parser de comandos no device

## Checklist rapido

- Comando chega ao device.
- UI recebe progresso.
- Timeout offline continua funcionando.
