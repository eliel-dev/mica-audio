# Release 1.0 com instalador assinado (Windows 11)

## Objetivo

Documentar o fluxo oficial para gerar e publicar o instalador `Setup EXE` assinado do Mica Audio sem scripts manuais para usuario final.

## Passos

1. Configure os secrets no GitHub:
   - `SIGNING_CERT_PFX_BASE64`
   - `SIGNING_CERT_PASSWORD`
   - `SIGNING_CERT_SUBJECT` (opcional, para auditoria)
2. Garanta que o branch principal esta verde no workflow de governanca.
3. Crie uma tag de release no formato `vX.Y.Z`:
   - Exemplo: `git tag v1.0.0` e `git push origin v1.0.0`.
4. O workflow `release.yml` executa em sequencia:
   - validacao documental + build debug,
   - publish `Release` do `App.WinUI` (x64),
   - assinatura dos binarios da app,
   - build MSI (WiX), assinatura MSI e build do bundle EXE,
   - assinatura final do setup EXE,
   - geracao de checksum `.sha256` e publicacao no GitHub Release.
5. Valide em maquina limpa Windows 11:
   - instala sem .NET Desktop Runtime 10 pre-instalado,
   - instala com runtime ja presente,
   - abre via Menu Iniciar,
   - desinstala por Apps e Recursos.
6. Valide upgrade:
   - instalar `v1.0.0`, depois `v1.0.1` sobreposto,
   - confirmar sem quebra de atalho/configuracao.

## Referencias de codigo

- [Workflow de release](../../../.github/workflows/release.yml#L1) - assinatura: `name: release`
- Projeto MSI historico: `installer/MicaAudio.Installer/MicaAudio.Installer.wixproj` - assinatura: `<Project Sdk="WixToolset.Sdk/5.0.2">`
- Manifesto MSI historico: `installer/MicaAudio.Installer/Product.wxs` - assinatura: `<Package Name="Mica Audio"`
- Projeto Bundle historico: `installer/MicaAudio.Bundle/MicaAudio.Bundle.wixproj` - assinatura: `<OutputType>Bundle</OutputType>`
- Manifesto Bundle historico: `installer/MicaAudio.Bundle/Bundle.wxs` - assinatura: `<Bundle Name="Mica Audio"`
- [Assinatura de release](../../../scripts/sign-release.ps1#L1) - assinatura: `param(`
- [Publish profile x64](../../../src/App.WinUI/Properties/PublishProfiles/win-x64.pubxml#L1) - assinatura: `<PublishProtocol>FileSystem</PublishProtocol>`

## Checklist rapido

- [ ] Secrets de assinatura configurados no repositorio
- [ ] Tag no formato `vX.Y.Z`
- [ ] `governance.yml` passando antes da release
- [ ] Release publica com:
  - [ ] `MicaAudio-Setup-x64-vX.Y.Z.exe`
  - [ ] `MicaAudio-Setup-x64-vX.Y.Z.sha256`
- [ ] Setup assinado com timestamp valido
- [ ] Instalacao validada em Windows 11 limpo
- [ ] Upgrade `vX.Y.Z -> vX.Y.(Z+1)` validado
- [ ] Rollback documentado (remocao de tag/release se necessario)
