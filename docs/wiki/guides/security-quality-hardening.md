# Hardening de Seguranca e Qualidade

## Objetivo

Aplicar uma trilha `security-first` no projeto para reduzir risco real em servidor/protocolo/firmware, com gates de qualidade automatizados para fluxo solo + IA.

## Passos

1. Endurecer servidor e protocolo sem quebrar compatibilidade atual:
- adicionar rate limiting em `/api/v1/pair`, `/api/v1/device/command-ack` e handshake de `/ws/v1/stream`.
- priorizar autenticacao por header (`X-Device-Token` e `Authorization`) com fallback de query para compatibilidade.
- restringir acesso de rede por padrao a IP privado, com allowlist CIDR opcional.
- limitar tentativas de pareamento por IP/janela.

2. Proteger segredo em repouso:
- persistir token de device em `devices.json` usando DPAPI (CurrentUser) no Windows.
- manter leitura backward-compatible de token antigo em texto puro.

3. Endurecer supply chain e CI:
- habilitar Dependabot para NuGet e GitHub Actions.
- adicionar CodeQL e Dependency Review.
- incluir gate de vulnerabilidades de dependencia (`dotnet package list --vulnerable`) no CI.
- usar lock de dependencias (`packages.lock.json`) e `NuGet.config` com source mapping.
- usar TSA via HTTPS em assinatura de release.

4. Evoluir qualidade de engenharia:
- habilitar analyzers em `Directory.Build.props`.
- definir regras base em `.editorconfig`.
- adicionar testes de seguranca e concorrencia no host de dispositivos.

5. Hardening minimo de firmware:
- separar perfil `dev` e `release` em `platformio.ini`.
- validar cabecalho de frame (`version` e `messageType`) antes de aplicar `gBins/gLevel`.
- manter fallback seguro quando painel/stream nao estiverem disponiveis.

## Referencias oficiais

- ASP.NET Core Security Overview: https://learn.microsoft.com/en-us/aspnet/core/security/?view=aspnetcore-8.0
- ASP.NET Core HTTPS/HSTS: https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl?view=aspnetcore-10.0
- SignalR/WebSocket security: https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-9.0
- NuGet lock file: https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies
- NuGet package source mapping: https://learn.microsoft.com/en-us/nuget/consume-packages/package-source-mapping
- dotnet package list: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-package-list
- GitHub Actions secure use: https://docs.github.com/en/actions/reference/security/secure-use
- CodeQL: https://docs.github.com/en/code-security/concepts/code-scanning/codeql/about-code-scanning-with-codeql
- Dependabot: https://docs.github.com/en/code-security/concepts/supply-chain-security/about-dependabot-security-updates
- ESP32-S3 Secure Boot v2: https://docs.espressif.com/projects/esp-idf/en/stable/esp32s3/security/secure-boot-v2.html
- ESP32-S3 Flash Encryption: https://docs.espressif.com/projects/esp-idf/en/stable/esp32s3/security/flash-encryption.html
- NIST SSDF (SP 800-218): https://csrc.nist.gov/pubs/sp/800/218/final

## Referencias de codigo

- [ServerConfig](../../../src/Device.Protocol/Contracts/ServerConfig.cs#L1)
- [DeviceServerHost.StartAsync](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [JsonDeviceRegistryStore](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L1)
- [dependency-vulnerability-gate](../../../scripts/dependency-vulnerability-gate.ps1#L1)
- [governance workflow](../../../.github/workflows/governance.yml#L1)
- [codeql workflow](../../../.github/workflows/codeql.yml#L1)
- [dependency-review workflow](../../../.github/workflows/dependency-review.yml#L1)

## Checklist rapido

- Rodar `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`.
- Rodar `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`.
- Rodar `powershell -ExecutionPolicy Bypass -File .\scripts\dependency-vulnerability-gate.ps1 -ProjectOrSolution MicaAudio.sln`.
- Rodar `dotnet build MicaAudio.sln -c Debug`.
- Rodar `dotnet test MicaAudio.sln -c Debug --no-build`.
