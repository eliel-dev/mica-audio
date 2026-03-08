# Handoff - Fase 2 de qualidade .NET 10 (baixo risco)

## Objetivo

Reduzir o backlog residual da baseline `.NET 10` com correcoes locais e baratas, sem abrir refactor de arquitetura, DI ou contrato publico.

## Escopo classificado

- Classificacao: estrutural.
- Escopo desta fase:
  - zerar `xUnit1030`, `CA1805`, `CA1725`, `CA1826`, `CA1865`, `CA1068` e `CA1852`;
  - promover essas categorias para `error` quando a baseline ficou zerada;
  - manter fora da rodada os warnings que ainda exigem decisao de contrato ou churn de DI (`CA1822`, `CA1716`).
- Fora desta fase:
  - conversao de services compartilhados para `static`;
  - renomeacao de contratos publicos de `Output`;
  - qualquer refactor de arquitetura em `App.WinUI`, `Device.Server` ou `Audio.Loopback`.

## Arquivos alterados

- Baseline e enforcement:
  - `.editorconfig`
- Codigo:
  - `BenchmarkSuite1/Program.cs`
  - `src/App.WinUI/Views/Controls/AppPreviewDrawHelpers.cs`
  - `src/App.WinUI/Views/DevicesPage.xaml.cs`
  - `src/Audio.Loopback/Capture/WasapiLoopbackCaptureService.cs`
  - `src/Device.Server/Hosting/DeviceServerHost.cs`
  - `src/MicaAudio.Core/Config/AnalyzerConfig.cs`
  - `tests/Integration.Smoke/VisualizerPresetSmokeTests.cs`

## Decisoes tomadas

- `xUnit1030` foi tratado como regra de teste obrigatoria e promovido para `error` em `tests/**/*.cs`.
- `CA1805`, `CA1725`, `CA1826`, `CA1852`, `CA1865` e `CA1068` foram promovidos para `error` em `[*.cs]` depois de zerarem no rebuild.
- `CA1068` entrou nesta fase porque o caso residual era um metodo `private` em `DeviceServerHost`, sem impacto de contrato publico.
- `CA1822` ficou de fora porque os casos remanescentes estao concentrados em services/UseCases usados por DI ou por contratos internos amplos; transformar isso em `static` agora aumentaria churn sem ganho proporcional.
- `CA1716` ficou de fora porque hoje depende de renomear o contrato publico `ILedOutput.Stop()`.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1`
  - OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1`
  - OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1`
  - OK
- `dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1`
  - OK
  - baseline inicial desta fase: `86` warnings
  - baseline final desta fase: `40` warnings
  - categorias zeradas nesta fase: `xUnit1030`, `CA1805`, `CA1725`, `CA1826`, `CA1865`, `CA1068`, `CA1852`
  - backlog residual final: `CA1822 (36)` e `CA1716 (4)`
- `dotnet test MicaAudio.sln -c Debug --no-build -m:1`
  - OK
  - `191` aprovados
  - `1` ignorado
- Validacao manual WinUI:
  - `src\App.WinUI\bin\x64\Debug\net10.0-windows10.0.22621.0\win-x64\App.WinUI.exe`
  - iniciou e permaneceu em execucao apos 5s (`PID 6384`, `MainWindowTitle=WinUI Desktop`)

## Riscos e rollback

- Esta fase nao muda comportamento funcional, protocolo ou API publica.
- O maior risco e somente de enforcement: novos casos das categorias promovidas agora falham no build em vez de virarem warning silencioso.
- Rollback rapido:
  - remover as severidades promovidas em `.editorconfig`;
  - restaurar as assinaturas/trechos locais alterados nesta fase.

## Proximos passos

- Fase 3 recomendada:
  - decidir uma politica para `CA1822` em services e use cases: manter por instancia e documentar, ou reduzir DI/localmente onde o ganho justificar o churn;
  - avaliar `CA1716` como decisao de API para `ILedOutput.Stop()`, com plano de compatibilidade se houver renomeacao;
  - se a meta for baseline zero, abrir uma rodada pequena e explicitamente orientada a contrato para fechar esses dois grupos finais.
