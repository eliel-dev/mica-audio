# Handoff - upgrade estrutural para .NET 10

## Objetivo

Migrar a solucao Mica Audio para `.NET 10`, alinhar todos os TFMs/lockfiles, corrigir scripts locais e atualizar release/installer para entregar `.NET Desktop Runtime 10 x64`.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: solucao compila e testa em `.NET 10`, benchmark lane continua valida, publish Release funciona, MSI/bundle WiX sobem com runtime 10 e a documentacao operacional reflete o novo contrato de plataforma.

## Arquivos alterados

- `global.json`
- `README.md`
- `.github/workflows/release.yml`
- `scripts/dev-run.ps1`
- `scripts/sign-dev.ps1`
- `installer/MicaAudio.Bundle/Bundle.wxs`
- `installer/MicaAudio.Bundle/MicaAudio.Bundle.wixproj`
- `installer/MicaAudio.Bundle/packages.lock.json`
- `installer/MicaAudio.Installer/packages.lock.json`
- `docs/wiki/guides/release-1.0-installer.md`
- `BenchmarkSuite1/BenchmarkSuite1.csproj`
- `src/App.WinUI/App.WinUI.csproj`
- `src/Audio.Loopback/Audio.Loopback.csproj`
- `src/Visual.Win2D/Visual.Win2D.csproj`
- `src/Analyzer.Dsp/Analyzer.Dsp.csproj`
- `src/Device.Protocol/Device.Protocol.csproj`
- `src/Device.Server/Device.Server.csproj`
- `src/MicaAudio.Core/MicaAudio.Core.csproj`
- `src/Output/Output.csproj`
- `tests/Analyzer.Dsp.Tests/Analyzer.Dsp.Tests.csproj`
- `tests/Integration.Smoke/Integration.Smoke.csproj`
- `tests/Output.Tests/Output.Tests.csproj`
- todos os `packages.lock.json` da solucao
- `docs/handoffs/2026-03-06-dotnet-10-upgrade.md`

## Decisoes tomadas

1. Todos os projetos foram migrados para `net10.0` ou `net10.0-windows10.0.22621.0`, padronizando o piso Windows em `22621` para eliminar o drift residual com `19041`.
2. `global.json` foi alinhado para `10.0.103`, mantendo `rollForward=latestFeature`.
3. `Microsoft.WindowsAppSDK` foi mantido em `1.8.260209005`; o lote focou em TFM/toolchain e nao precisou subir o Windows App SDK para estabilizar o build.
4. Pacotes diretos `System.IO.Ports`, `System.Management` e `System.Drawing.Common` foram alinhados para `10.0.0` onde permaneceram necessarios.
5. `scripts/dev-run.ps1` e `scripts/sign-dev.ps1` deixaram de depender de path hardcoded de TFM e passaram a resolver o `TargetFramework` a partir do `App.WinUI.csproj`.
6. O pipeline de release e o bundle WiX passaram a referenciar `.NET Desktop Runtime 10.0.3 x64`, incluindo busca/detect do runtime major `10` no Burn.
7. Os projetos WiX passaram a gerar `packages.lock.json` locais, em linha com `RestorePackagesWithLockFile=true` definido no repositório.
8. O build do bundle continua dependendo do payload offline do runtime; para validacao local foi necessario baixar `windowsdesktop-runtime-10.0.3-win-x64.exe` para `artifacts/dotnet/` e depois remover o artefato do worktree.

## Validacoes executadas

```text
dotnet restore MicaAudio.sln --configfile NuGet.config -m:1 -> OK
dotnet restore src/Analyzer.Dsp/Analyzer.Dsp.csproj --configfile NuGet.config --force-evaluate -m:1 -> OK

dotnet build MicaAudio.sln -c Debug --configfile NuGet.config -m:1 -> OK (423 warnings, 0 erro)
dotnet test MicaAudio.sln -c Debug --no-build -m:1 -> OK (183 aprovados, 1 ignorado)

dotnet build BenchmarkSuite1/BenchmarkSuite1.csproj -c Debug --configfile NuGet.config -m:1 -> OK
dotnet run --project BenchmarkSuite1/BenchmarkSuite1.csproj -c Debug --framework net10.0-windows10.0.22621.0 -- --list flat -> OK (BenchmarkDotNet validou entrada; avisou sobre build Debug/non-optimized)

dotnet publish src/App.WinUI/App.WinUI.csproj -c Release -p:Platform=x64 -p:PublishProfile=win-x64 --configfile NuGet.config -m:1 -> OK
dotnet build installer/MicaAudio.Installer/MicaAudio.Installer.wixproj -c Release --configfile NuGet.config -m:1 -> OK
Invoke-WebRequest https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/10.0.3/windowsdesktop-runtime-10.0.3-win-x64.exe -> OK
dotnet build installer/MicaAudio.Bundle/MicaAudio.Bundle.wixproj -c Release --configfile NuGet.config -m:1 -> OK

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
```

## Riscos e rollback

- Risco principal: o lote nao resolveu warnings historicos/analyzer backlog; o build continua verde, mas a base ainda carrega warnings de qualidade e `MVVMTK0045`/`WIN2D0001` ja existentes.
- Risco principal: o bundle local depende do payload offline em `artifacts/dotnet/`; sem esse arquivo o WiX falha com `WIX0103`.
- Como reverter:
  - restaurar os TFMs anteriores (`net8.0*`) nos `.csproj` afetados;
  - restaurar `global.json` para `10.0.102`;
  - recolocar `release.yml`, `Bundle.wxs` e `MicaAudio.Bundle.wixproj` apontando para `.NET Desktop Runtime 8.0.24`;
  - rerodar `dotnet restore MicaAudio.sln --configfile NuGet.config -m:1` para ressincronizar os lockfiles;
  - rerodar `dotnet build MicaAudio.sln -c Debug --configfile NuGet.config -m:1` e `dotnet test MicaAudio.sln -c Debug --no-build -m:1`.

## Proximos passos

1. Avaliar um lote separado para backlog de warnings/analyzers que ficou mais visivel no build `.NET 10`.
2. Decidir se `tests/Integration.Smoke` deve fixar `PlatformTarget/RID` para eliminar definitivamente o warning `WIN2D0001`.
3. Repetir o fluxo de release com assinatura real em CI para validar o bundle final em ambiente limpo.