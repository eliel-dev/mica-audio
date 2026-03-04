# Guia - Operar ciclo de vida de dispositivo

## Objetivo

Executar rotina operacional completa: parear, validar online, enviar comandos, acompanhar logs e confirmar reconexao.

## Passos

1. Gerar codigo de pareamento em Dispositivos.
2. Provisionar firmware com host correto e pairing code.
3. Confirmar device online na lista.
4. Validar controles operacionais:
- acionar `Testar LED` (envia `test_led` sem parametros);
- ajustar slider de brilho (envia `set_brightness` no commit);
- opcionalmente validar compat legado com `test_led` + `enabled=true|false` em ambiente tecnico.
5. Testar comando administrativo `enter_provisioning`.
6. Validar logs, progresso e status final.
7. Simular queda de rede e confirmar reconnect.

## Referencias de codigo

- [DeviceIntegrationService.CreatePairingCode](../../../src/App.WinUI/Services/Devices/DeviceIntegrationService.cs#L82) - assinatura: `PairingCodeInfo CreatePairingCode(TimeSpan ttl)`
- [DevicesPage.OnGeneratePairingCodeClicked](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L84) - assinatura: `private void OnGeneratePairingCodeClicked(...)`
- [DeviceOperationsCoordinator.RunCommandAsync](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L104) - assinatura: `public async Task<CommandDispatchResult> RunCommandAsync(...)`
- [DeviceServerHost.GetDevicesSnapshot](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L156) - assinatura: `IReadOnlyList<DeviceSnapshot> GetDevicesSnapshot()`

## Checklist rapido

- [ ] Pareamento finaliza com token valido.
- [ ] Device aparece online.
- [ ] Comando retorna ACK/progresso.
- [ ] Heartbeat (`telemetrySequence`) avanca no dashboard.
- [ ] Brilho aplicado/limite e disponibilidade de LED de teste atualizam na UI.
- [ ] Reconnect apos queda de rede funciona.
