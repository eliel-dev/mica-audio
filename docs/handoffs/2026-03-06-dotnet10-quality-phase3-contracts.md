# Handoff - Fase 3 de qualidade .NET 10 (contratos e baseline zero)

## Objetivo

Fechar os dois grupos finais do backlog de qualidade (`CA1822` e `CA1716`) sem abrir refactor artificial, deixando a baseline do rebuild da solucao em zero warnings no `.NET 10`.

## Escopo classificado

- Classificacao: estrutural.
- Escopo desta fase:
  - promover `CA1822` e `CA1716` para `error` em `.editorconfig`;
  - zerar os casos residuais com supressoes direcionadas e justificadas por design/contrato;
  - manter a politica de qualidade com enforcement real, sem `NoWarn` amplo.
- Fora desta fase:
  - refactor de DI para transformar services em `static`;
  - renomeacao do contrato publico `ILedOutput.Stop()`;
  - qualquer mudanca funcional em runtime, firmware, protocolo ou UX.

## Arquivos alterados

- Enforcement:
  - `.editorconfig`
- Supressoes documentadas de design:
  - `src/App.WinUI/Services/AppSettingsDomainService.cs`
  - `src/App.WinUI/Services/Apps/CityAutocompleteService.cs`
  - `src/App.WinUI/Services/Apps/UseCases/AppConfigValidationUseCase.cs`
  - `src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs`
  - `src/App.WinUI/Services/Gif/Hub75FrameFormatter.cs`
  - `src/Output/Led/ILedOutput.cs`

## Decisoes tomadas

- `CA1822` passou a ser `error`, mas os casos residuais do repositorio foram tratados com supressao local e justificativa explicita porque representam contracts/services intencionais:
  - `AppSettingsDomainService.Migrate/Copy`
  - `CityAutocompleteService.SearchAsync`
  - `AppConfigValidationUseCase.TryBuildPayload`
  - `PrecompiledFirmwareService.GetOptions/TryGetOption`
  - `Hub75FrameFormatter.Format`
- A decisao de design foi manter esses membros por instancia para preservar:
  - consistencia de DI;
  - contratos internos/publicos ja usados no repo;
  - espaco para evolucao stateful futura sem churn desnecessario.
- `CA1716` passou a ser `error`, mas o contrato `ILedOutput.Stop()` foi mantido e documentado com supressao direcionada.
- A decisao para `ILedOutput.Stop()` foi de compatibilidade:
  - o verbo `Stop` ja e o contrato de ciclo de vida usado entre assemblies;
  - renomear agora geraria churn publico sem ganho funcional.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
  - OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
  - OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
  - OK
- `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1`
  - OK
  - baseline inicial desta fase: `40` warnings
  - baseline final desta fase: `0` warnings
  - categorias zeradas nesta fase: `CA1822`, `CA1716`
- `dotnet test MicaAudio.sln -c Debug --no-build -m:1`
  - OK
  - `191` aprovados
  - `1` ignorado
- Validacao manual WinUI:
  - `src\App.WinUI\bin\x64\Debug\net10.0-windows10.0.22621.0\win-x64\App.WinUI.exe`
  - iniciou e permaneceu em execucao apos 5s (`PID 15976`, `MainWindowTitle=WinUI Desktop`)

## Riscos e rollback

- Esta fase nao altera comportamento funcional.
- O maior risco e apenas de manutencao: novos casos de `CA1822` e `CA1716` agora falham no build se nao vierem com correcao real ou justificativa explicita.
- Rollback rapido:
  - remover `CA1822`/`CA1716` como `error` em `.editorconfig`;
  - remover as supressoes locais desta fase.

## Estado final da fase

- Baseline de analyzer do rebuild da solucao em `.NET 10`: `0 warnings`.
- Qualidade agora protegida por enforcement real para:
  - `CA1068`
  - `CA1716`
  - `CA1725`
  - `CA1805`
  - `CA1822`
  - `CA1826`
  - `CA1852`
  - `CA1859`
  - `CA1861`
  - `CA1865`
  - `CA2016`
  - `CA2263`
  - `xUnit1030`

## Proximos passos

- Proxima onda recomendada nao e mais "limpeza de backlog", e sim manutencao:
  - revisar periodicamente se alguma supressao local ainda faz sentido;
  - quando houver mudanca de arquitetura/DI, reavaliar se algum caso suprimido de `CA1822` pode virar `static` sem churn;
  - manter a politica obrigatoria para IA e a baseline de analyzers sincronizadas com a data corrente e com a documentacao oficial da stack alvo.
