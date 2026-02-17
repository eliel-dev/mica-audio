# Handoff — Release 1.0 Setup assinado + pipeline

## Objetivo

Implementar distribuicao Release 1.0 para usuario final Windows 11 via setup EXE assinado, sem execucao de scripts no cliente final.

## Escopo classificado

estrutural

## Arquivos alterados

- `src/App.WinUI/Package.appxmanifest`
- `installer/MicaAudio.Installer/MicaAudio.Installer.wixproj`
- `installer/MicaAudio.Installer/Product.wxs`
- `installer/MicaAudio.Bundle/MicaAudio.Bundle.wixproj`
- `installer/MicaAudio.Bundle/Bundle.wxs`
- `scripts/sign-release.ps1`
- `.github/workflows/release.yml`
- `README.md`
- `docs/wiki/README.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/guides/release-1.0-installer.md`
- `docs/wiki/reference/ai-contract.v1.yaml`
- `scripts/docs-structural-gate.ps1`

## Decisoes tomadas

1. Formato final de distribuicao: `Setup EXE` (WiX Burn), x64.
2. Assinatura de release com certificado OV + timestamp DigiCert.
3. Runtime .NET entregue via bootstrapper (`.NET Desktop Runtime 8 x64`) no setup.
4. Atualizacao no 1.0 permanece manual por GitHub Releases.
5. Toolchain de firmware fica fora do instalador 1.0.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
- `dotnet build MicaAudio.sln -c Debug`

## Riscos e rollback

- Risco: variacoes de ambiente do GitHub Runner para compilar WiX/signing.
- Risco: URL do instalador offline do .NET 8 precisar ser atualizada em revisoes futuras.
- Rollback rapido: remover workflow `release.yml` e pasta `installer/`, manter apenas fluxo atual de publish/dev.

## Proximos passos

1. Configurar secrets de assinatura no GitHub.
2. Executar release de teste (`v1.0.0-rc1` em branch/tag de homologacao).
3. Validar instalacao em maquina Windows 11 limpa.
4. Promover tag oficial `v1.0.0`.
