# Handoff - limpeza conservadora da solucao

## Objetivo

Reduzir lixo e codigo nao usado na base da `MicaAudio.sln` com foco em higiene de repositorio, remocao de codigo morto provado e eliminacao de supressoes locais redundantes, sem abrir refactor cosmetico ou mudar contratos funcionais.

## Escopo classificado

- Tipo: estrutural
- Escopo efetivo: projetos da `MicaAudio.sln`, testes, benchmark e documentacao de referencia associada.
- Fora desta rodada: `firmware/`, contratos wire, backlog amplo de `CA1707`, `CA1416`, `MVVMTK0045` e projetos/pastas locais fora da solucao (`src/App.DevLauncher`, `src/App.Headless`, `src/Web.Headless`).

## Arquivos alterados

- `.gitignore`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `tests/Output.Tests/DeviceIntegrationServiceLegacyWsSettingTests.cs`
- `tests/Output.Tests/DeviceOperationsCoordinatorBrightnessTests.cs`
- `tests/Output.Tests/Esp32S3LedOutputTests.cs`
- `tests/Integration.Smoke/DeviceUsbOnboardingServiceTests.cs`
- `docs/wiki/reference/code-index.md`
- `docs/handoffs/2026-03-06-solution-cleanup-pass1.md`

## Decisoes tomadas

1. O baseline desta rodada foi tratado como limpeza conservadora: manter o backlog amplo fora do lote e atacar apenas residuos com prova forte de obsolescencia.
2. `BenchmarkDotNet.Artifacts/` passou a ser ignorado explicitamente porque o repositorio ja gera esse artefato em fluxos locais de benchmark e ele nao faz parte do produto.
3. O remanescente do dashboard avancado da `DevicesPage` foi removido do codigo porque o caminho seguro ja havia substituido integralmente a renderizacao ESP-Dash no fluxo padrao e nao restavam referencias vivas na pagina.
4. As supressoes locais de `CS0067` em doubles de teste foram substituidas por eventos vazios (`add/remove`) para manter o contrato de interface sem carregar pragmas redundantes.
5. Membros privados comprovadamente sem estado foram convertidos para `static` apenas quando isso nao alterava DI, consumo publico interno nem semantica de instancia.
6. As pastas locais `src/App.DevLauncher`, `src/App.Headless` e `src/Web.Headless` foram mantidas fora do lote de produto; o achado fica registrado como sobra operacional fora da solucao rastreada.

## Validacoes executadas

```text
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1 -> OK
dotnet build MicaAudio.sln -c Debug --configfile NuGet.config -m:1 -> OK (warnings de analyzer existentes, 0 erro)
dotnet test MicaAudio.sln -c Debug --no-build -m:1 -> OK (189 aprovados, 1 ignorado)
dotnet build MicaAudio.sln -c Debug --no-restore -t:Rebuild -m:1 -> OK
```

### Baseline observado

- Build atual da solucao continua com backlog amplo de analyzers, dominado por categorias fora desta rodada como `CA1707`, `MVVMTK0045`, `CA1416`, `CA2000`, `CA1861` e avisos pontuais como `WIN2D0001`.
- Nao havia arquivos temporarios/versionados (`*.log`, `*.tmp`, `*.bak`, `*.orig`, `*.rej`) rastreados pelo Git nesta rodada.
- O backlog historico de analyzers citado em handoffs anteriores continua fora deste lote.
- A verificacao dirigida apos o rebuild mostrou `CS0067=0` e `CA1822=36`; os `CA1822` remanescentes concentram-se em services internos compartilhados entre app/testes e ficam explicitados abaixo.

## Riscos e rollback

- Risco principal: algum fluxo futuro querer reusar visual antigo da `DevicesPage`; nesta rodada ele foi tratado como codigo morto porque o caminho seguro ja e o contrato ativo.
- Risco residual: `WIN2D0001` continua aparecendo em `Integration.Smoke` e permanece como proximo candidato de higiene, mas nao foi alterado para evitar mudar configuracao de build/teste fora do necessario.
- Debito assumido: permanecem `CA1822` em `AppSettingsDomainService`, `Hub75FrameFormatter`, `AppConfigValidationUseCase`, `PrecompiledFirmwareService` e `CityAutocompleteService`, pois a conversao para `static` exigiria refatorar DI/call sites fora da limpeza conservadora. O lote atual zerou `CS0067`, mas nao tenta zerar `CA1822` com refactor de arquitetura.
- Como reverter:
  - restaurar os blocos removidos da `DevicesPage`;
  - recolocar os pragmas `CS0067` nos doubles de teste, se necessario;
  - remover a linha `BenchmarkDotNet.Artifacts/` do `.gitignore`;
  - rerodar `dotnet build MicaAudio.sln -c Debug` e `dotnet test MicaAudio.sln -c Debug`.

## Proximos passos

1. Decidir em lote separado se `Integration.Smoke` deve fixar plataforma/RID para eliminar `WIN2D0001`.
2. Avaliar limpeza operacional das pastas locais fora da solucao (`App.DevLauncher`, `App.Headless`, `Web.Headless`) antes que virem nova fonte de confusao.
3. Abrir outra rodada somente se houver interesse em backlog de analyzers reais, sem misturar com higiene de codigo morto.
