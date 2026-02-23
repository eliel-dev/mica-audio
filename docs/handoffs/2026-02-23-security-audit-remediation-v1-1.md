## Objetivo

Aplicar hardening de seguranca no servidor de dispositivos e no cliente WinUI, corrigindo autenticacao/token, limites de input, processamento WS fragmentado e timeout explicito de HTTP client.

## Escopo classificado

Estrutural (seguranca de protocolo/host, contratos de configuracao e testes de regressao de seguranca).

## Arquivos alterados

- `src/Device.Protocol/Contracts/ServerConfig.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/App.WinUI/Views/MainPage.xaml.cs`
- `src/App.WinUI/Services/Apps/GifCatalogAppRuntimeService.cs`
- `tests/Output.Tests/DeviceServerHostSecurityTests.cs`
- `docs/adr/0006-device-auth-hardening-and-input-limits.md`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/reference/http-api-v1.md`
- `docs/wiki/reference/ws-protocol-v1.md`
- `docs/wiki/reference/troubleshooting-matrix.md`

## Decisoes tomadas

1. Token por query em HTTP foi bloqueado definitivamente.
2. Token por query no WS ficou transitório, controlado por `AllowLegacyWebSocketQueryToken=true` (compatibilidade legado).
3. Body JSON e mensagem WS passaram a ter limites configuraveis com defaults de 64KB.
4. Receive loop WS passou a remontar mensagens fragmentadas ate `EndOfMessage`.
5. Headers HTTP defensivos foram adicionados globalmente.
6. `HttpClient` de GIF no WinUI recebeu timeout explicito de 15s.

## Validacoes executadas

- `dotnet test tests/Output.Tests --filter "FullyQualifiedName~DeviceServerHostSecurityTests" --no-restore`
- `dotnet build src/App.WinUI/App.WinUI.csproj -c Debug`
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`

## Riscos e rollback

- Firmware legado pode falhar se `AllowLegacyWebSocketQueryToken=false` antes da migracao de headers.
- Limites muito baixos de payload podem bloquear clientes validos.
- Rollback: reverter commit de hardening no host e restaurar fallback anterior (somente se necessario).

## Proximos passos

1. Release N+1: mudar default de `AllowLegacyWebSocketQueryToken` para `false`.
2. Release N+2: remover fallback legado de query token no WS.
3. Evoluir firmware para enviar token em header no handshake WS.
