# Handoff: Versionamento SemVer via git describe + stages OTA ausentes

## Objetivo

1. Substituir o versionamento CalVer/timestamp do firmware por SemVer determinisico
   baseado em `git describe`, eliminando falsos positivos de "atualizacao disponivel".
2. Adicionar stages OTA ausentes ("queued", "sent") no mapeamento de texto da UI.

## Escopo classificado

- Tarefa 1 (stages OTA): funcional (1 arquivo C#)
- Tarefa 2 (SemVer): estrutural (1 arquivo em scripts/)

## Arquivos alterados

### scripts/build-precompiled-firmware.ps1
- `Resolve-FirmwareVersion` reescrita: usa `git describe --tags --long --always`
  em vez de timestamp UTC.
- Formato de saida:
  - Tag no HEAD: `v1.0.0`
  - N commits apos tag: `v1.0.0-14-gb116aea`
  - Repo sem tags: `v0.0.0-0-g<sha>`
- `Normalize-VersionToken` e `Resolve-GitSha` mantidos (usados em outros pontos).
- `builtAtUtc` no manifesto continua sendo timestamp (e a semantica correta para freshness check).

### src/App.WinUI/Services/Devices/DeviceOperationsText.cs
- `DescribeStage()`: adicionados cases `"queued" => "na fila"` e `"sent" => "enviado"`
  antes de `"received"`, seguindo a ordem do ciclo de vida OTA.

## Decisoes tomadas

1. **git describe em vez de CalVer.** O formato anterior (`vyyyy.MM.dd-HHmmssZ-tag-sha`)
   gerava uma versao unica por build mesmo sem mudanca de codigo, causando falsos
   positivos em `IsFirmwareUpdateAvailable()` (simples `string.Equals`). Com `git describe`,
   dois builds do mesmo commit geram a mesma versao, eliminando o problema.

2. **Sem parsing de versao semantica no comparador.** `IsFirmwareUpdateAvailable` usa
   `string.Equals` — isso funciona corretamente com git describe porque a mesma posicao
   no grafo git sempre gera o mesmo string. Nao ha necessidade de comparacao numerica
   (maior/menor) porque o build script gera o binario mais recente; se o string difere
   do dispositivo, o binario local e mais novo por definicao.

3. **Compatibilidade com builds antigos.** Dispositivos flashados com o formato CalVer
   (`v2026.04.14-...`) terao uma versao diferente do novo formato SemVer, portanto
   `IsFirmwareUpdateAvailable` retornara `true` — o que e correto, pois o usuario deve
   atualizar para a versao compilada. Nao ha risco de downgrade acidental.

4. **Fallback sem tags: `v0.0.0-0-g<sha>`.** Quando o repo nao possui nenhuma tag,
   o build gera uma versao valida que permite o fluxo funcionar. Ao criar a primeira
   tag (`git tag v1.0.0`), o versionamento passa a ser automaticamente SemVer completo.

## Validacoes executadas

```text
dotnet build MicaAudio.sln -c Debug           -> 0 Erro(s), 35 Aviso(s) (Magick.NET pre-existentes)
powershell scripts/docs-validate.ps1           -> OK: nenhuma falha encontrada
powershell scripts/ai-governance-check.ps1     -> OK: governanca IA valida
git describe --tags --long --always            -> b116aea (sem tags -> v0.0.0-0-gb116aea esperado)
```

## Riscos e rollback

- **Risco baixo.** A mudanca de formato de versao causa uma unica atualizacao "forçada"
  para dispositivos com CalVer antigo. Apos essa primeira atualizacao, o versionamento
  estabiliza.
- **Rollback:** `git revert` do commit. Dispositivos que ja receberam firmware com versao
  SemVer continuam funcionando; o app simplesmente voltaria a gerar versoes CalVer, e a
  proxima rebuild seria detectada como atualizacao (o que e inofensivo).

## Proximos passos

1. Criar primeira tag SemVer no repo: `git tag v1.0.0` (ou versao adequada).
2. Rodar `build-precompiled-firmware.ps1` para gerar binario com versao SemVer.
3. Testar ciclo: flash via USB -> verificar versao no app -> alterar main.cpp -> rebuild
   -> confirm que `IsFirmwareUpdateAvailable` detecta diferenca -> OTA -> confirmar versao
   atualizada no dispositivo.
