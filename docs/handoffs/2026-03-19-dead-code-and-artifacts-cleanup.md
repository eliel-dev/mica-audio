# Handoff de mudanca estrutural

## Objetivo

Limpar artefatos indevidamente versionados, remover lixo temporario do repositorio e aposentar codigo legado sem uso no runtime ativo do Mica Audio.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - `tmpclaude-*` e conteudos versionados de `.codex-*` deixam de ser rastreados;
  - o stub `validate-shader-toolchain.ps1` sai do repositorio;
  - `SpectrumDownmixer` sai do codigo de producao sem afetar o pipeline atual;
  - o contrato legado de stream 64x32 sai do codigo, testes e documentacao ativa.

## Arquivos alterados

- `.gitignore`
- `.github/agents/mica-code.agent.md`
- `docs/wiki/architecture/03-data-contracts.md`
- `docs/wiki/guides/criticality-context7-audit.md`
- `docs/wiki/modules/output-led.md`
- `docs/wiki/README.md`
- `docs/wiki/reference/glossary.md`
- `docs/wiki/reference/linking-conventions.md`
- `docs/wiki/reference/ws-protocol-v1.md`
- `scripts/docs-validate.ps1`
- `scripts/validate-shader-toolchain.ps1`
- `src/Analyzer.Dsp/Analysis/SpectrumDownmixer.cs`
- `src/Device.Protocol/Models/DeviceCommandProgressMessage.cs`
- `src/Device.Protocol/Stream/StreamFrameV1.cs`
- `tests/Analyzer.Dsp.Tests/BandMappingTests.cs`
- `tests/Output.Tests/StreamFrameV1Tests.cs`
- `docs/handoffs/2026-03-19-dead-code-and-artifacts-cleanup.md`
- arquivos temporarios versionados `tmpclaude-*`
- conteudos versionados em `.codex-build-obj/` e `.codex-sln-obj/`

## Decisoes tomadas

1. Artefatos `tmpclaude-*` e `.codex-*` foram tratados como lixo operacional: saem do indice do git e passam a ser ignorados.
2. A limpeza dos diretorios `src/App.DevLauncher`, `src/App.Headless` e `src/Web.Headless` foi executada apenas localmente, sem tentar transformar sobra de disco em mudanca de produto.
3. `SpectrumDownmixer` foi removido junto do teste que existia apenas para exercitar esse helper legado.
4. O protocolo legado de stream 64x32 foi aposentado de forma explicita: codigo, testes e documentacao ativa saem; ADRs e handoffs historicos permanecem como registro.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
dotnet build MicaAudio.sln -c Debug -m:1 /nr:false -p:UseSharedCompilation=false
dotnet test tests/Analyzer.Dsp.Tests/Analyzer.Dsp.Tests.csproj -c Debug --no-build
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --no-build
```

## Riscos e rollback

- Risco principal: algum documento ou fluxo externo ainda depender do legado 64x32, embora o repositorio ativo nao o use mais.
- Risco secundario: ferramentas locais de IA/build podem voltar a recriar `.codex-*`; por isso o `.gitignore` foi ajustado neste lote.
- Como reverter:
  - restaurar `SpectrumDownmixer` e o teste correspondente;
  - restaurar `StreamFrameV1`, `StreamFrameV1Tests` e a documentacao `ws-protocol-v1`;
  - remover as entradas novas do `.gitignore`;
  - reverter a remocao de `tmpclaude-*` e `.codex-*` do indice apenas se houver justificativa operacional real.

## Proximos passos

1. Se houver incidente real de compatibilidade com hardware 64x32 antigo, restaurar o legado em lote separado e com criterio de suporte explicito.
2. Monitorar se mais artefatos locais estao escapando para o indice do git e ampliar o `.gitignore` apenas quando houver prova concreta.
3. Manter a documentacao ativa alinhada ao conjunto real de projetos da solucao e do produto.
