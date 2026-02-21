# Handoff - Security/Quality Hardening (Fases 1-4)

## Objetivo

Implementar hardening imediato de seguranca e qualidade no servidor/protocolo/firmware com compatibilidade preservada, mais gates de supply chain no fluxo local e CI.

## Escopo classificado

- `estrutural`
- `firmware/protocolo`

## Arquivos alterados

- `src/Device.Protocol/Contracts/ServerConfig.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
- `firmware/matrixportal-s3/platformio.ini`
- `firmware/matrixportal-s3/src/main.cpp`
- `.github/dependabot.yml`
- `.github/workflows/governance.yml`
- `.github/workflows/release.yml`
- `.github/workflows/codeql.yml`
- `.github/workflows/dependency-review.yml`
- `scripts/sign-release.ps1`
- `scripts/dependency-vulnerability-gate.ps1`
- `Directory.Build.props`
- `NuGet.config`
- `.editorconfig`
- `tests/Output.Tests/DeviceServerHostSecurityTests.cs`
- `docs/wiki/modules/device-server-protocol.md`
- `docs/wiki/modules/firmware-matrixportal-s3.md`
- `docs/wiki/modules/settings-presets-persistence.md`
- `docs/wiki/reference/http-api-v1.md`
- `docs/wiki/reference/ws-protocol-v1.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/wiki/reference/docs-health.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/README.md`
- `docs/wiki/guides/security-quality-hardening.md`
- `README.md`

## Decisoes tomadas

1. Transporte permanece `HTTP local + hardening` nesta etapa.
2. Autenticacao prioriza headers e preserva fallback por query para compatibilidade legada.
3. Pairing ganhou anti-abuso por IP/janela sem quebrar fluxo atual de pareamento.
4. Tokens de device em repouso passam a DPAPI (`TokenProtected`) com leitura backward-compatible.
5. Supply chain endurecido com Dependabot, CodeQL, Dependency Review e gate de vulnerabilidade no CI.
6. Firmware ganhou validacao minima de frame (`version` + `messageType`) e perfis `dev/release` no PlatformIO.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\dependency-vulnerability-gate.ps1 -ProjectOrSolution MicaAudio.sln` -> OK
- `dotnet build MicaAudio.sln -c Debug` -> OK (com warnings de analyzer existentes)
- `dotnet test MicaAudio.sln -c Debug --no-build` -> OK (31 + 13 + 1 aprovados, 1 skip manual)

## Riscos e rollback

- Risco: limites de rate limiting agressivos podem bloquear dispositivos validos em redes ruidosas.
- Mitigacao: ajustar `ServerConfig` (`PairRequestsPerMinute`, `PairingAttemptsPerWindow`, `AllowedCidrs`).
- Risco: token protegido por DPAPI nao e compartilhavel entre usuarios Windows diferentes.
- Mitigacao: fallback de migracao + re-pareamento controlado quando necessario.
- Rollback rapido: reverter alteracoes de `DeviceServerHost` e `JsonDeviceRegistryStore` para politica anterior.

## Proximos passos

1. Ajustar branch protection para exigir checks de seguranca adicionados.
2. Definir threshold operacional de rate limits por ambiente (dev/lab/producao local).
3. Planejar ativacao progressiva de hardening de firmware release (secure boot/flash encryption) em hardware real.
4. Incluir testes adicionais de estresse de reconexao e concorrencia em broadcast/shutdown.
