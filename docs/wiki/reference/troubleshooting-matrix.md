# Troubleshooting Matrix

| Sintoma | Diagnostico rapido | Causa comum | Acao recomendada |
|---|---|---|---|
| App abre sem visualizacao | Ver `MainPage` + estado de sessao | pipeline pausado apos navegacao | validar ativacao/pausa da sessao de visualizacao |
| Startup falha com `Unable to resolve service for type ...` | conferir construtor da pagina/use case e registros no `App.BuildServiceProvider()` | dependencia nao registrada no DI | registrar servico faltante e manter construtor publico DI-friendly |
| Build falha com `CS1503` em settings/presets | verificar assinatura dos construtores e registro no container | migracao parcial para `IOptions<MicaAudioOptions>` | registrar `services.Configure<MicaAudioOptions>(...)` e remover construtor por `string appDataRoot` |
| git push falha com APPX3217 no pre-push local | verificar hook .githooks/pre-push e log do build local | maquina sem SDK/UAP para Integration.Smoke | usar gate local leve (scripts/local-prepush-gate.ps1) e manter build completo no CI |
| Comando device timeout | Ver status em DevicesPage/ServerPage | device offline ou WS sem resposta | confirmar online, repetir comando, revisar timeout |
| Pair retorna `429` | verificar burst de requests por mesmo IP | rate limit de pareamento ativo | aguardar janela expirar ou reduzir retries |
| API retorna `403 network_not_allowed` | conferir IP do cliente e politica em `ServerConfig` | origem fora da rede permitida/CIDR | ajustar `AllowedCidrs` ou manter cliente na LAN privada |
| WS retorna `401` apos hardening de token | validar como o firmware envia token no handshake | query token legado desabilitado (`AllowLegacyWebSocketQueryToken=false`) | migrar para header `X-Device-Token` ou reabilitar legado temporariamente |
| Endpoint retorna `413 Payload Too Large` | verificar tamanho do JSON enviado | body acima de `MaxJsonBodyBytes` | reduzir payload ou ajustar limite de servidor de forma controlada |
| Download de firmware falha | Ver logs da aba Servidor | BIN ausente no pacote ou falha de permissao no destino | validar assets em `AppData/Firmware` e repetir salvar |
| Botao de salvar abre e cancela | Ver status `Download: cancelado` | usuario cancelou FileSavePicker | comportamento esperado |
| Texto ilegivel em tema | comparar tema sistema e brushes | style sem recurso semantico | revisar Fluent2 tokens e bindings |

## Token criptografado no devices.json

- A partir do hardening de seguranca, o token e salvo em `TokenProtected` com prefixo `dpapi:v1:`.
- Em leitura, formato legado em texto puro ainda e aceito para migracao.
- Se a descriptografia falhar no usuario atual, o token e tratado como invalido e o device deve re-parear.

## Referencias de codigo

- [DeviceOperationsCoordinator logs](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1)
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L1)
- [ServerPage.SaveFirmwareAsync](../../../src/App.WinUI/Views/ServerPage.xaml.cs#L1)
- [DevicesPage.ApplyState](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [JsonDeviceRegistryStore](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)

## Push local x CI (gate local leve)

- O hook local pre-push roda validacoes de docs/governanca, build do App.WinUI e Output.Tests.
- O build completo da solucao (MicaAudio.sln) continua obrigatorio no CI (governance-ai-guardrails e governance-build-debug).
- Resultado: push local destravado em maquinas sem SDK UAP, sem reduzir rigor para merge na main.

