# Handoff - Remediacao de seguranca: SHA pinning de GitHub Actions e remocao de artefato residual

## Objetivo

Corrigir brechas de supply chain fixando GitHub Actions a commits SHA especificos, e remover arquivo residual de desenvolvimento.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: todos os usos de actions nos workflows fixados a SHA; `test.txt` removido.

## Arquivos alterados

- `.github/workflows/codeql.yml`
- `.github/workflows/dependency-review.yml`
- `.github/workflows/governance.yml`
- `.github/workflows/release.yml`
- `src/Device.Server/Hosting/test.txt` (removido)

## Decisoes tomadas

1. **SHA pinning de GitHub Actions**: tags flutuantes como `@v4` sao mutaveis e podem ser redirecionadas para codigo malicioso sem aviso (supply chain attack). Fixar a SHA imutavel garante que o workflow executa exatamente o codigo auditado. Adicionado comentario de versao legivel (`# v4.3.1`) ao lado de cada SHA.
2. **`softprops/action-gh-release` nao alterado**: ja estava fixado a SHA `a06a81a03ee405af7f2048a818ed3f03bbf83c7b` desde a fase anterior de hardening.
3. **Remocao de `test.txt`**: arquivo sem valor funcional em `src/Device.Server/Hosting/` — artefato residual de desenvolvimento que nao deve constar no repositorio.

## Validacoes executadas

```text
grep -n "uses:" .github/workflows/codeql.yml
  -> actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5 # v4.3.1
  -> actions/setup-dotnet@67a3573c9a986a3f9c594539f4ab511d57bb3ce9 # v4.3.1
  -> github/codeql-action/init@0d579ffd059c29b07949a3cce3983f0780820c98 # v4.32.6
  -> github/codeql-action/analyze@0d579ffd059c29b07949a3cce3983f0780820c98 # v4.32.6

grep -n "uses:" .github/workflows/dependency-review.yml
  -> actions/checkout@34e114876b0b11c390a56381ad16ebd13914f8d5 # v4.3.1
  -> actions/dependency-review-action@2031cfc080254a8a887f58cffee85186f0e49e48 # v4.9.0

grep -n "uses:" .github/workflows/governance.yml
  -> 6x referencias fixadas a SHA (3 jobs x checkout+setup-dotnet)

grep -n "uses: actions/" .github/workflows/release.yml
  -> 11x referencias fixadas a SHA (checkout, setup-dotnet, upload-artifact, download-artifact)

ls src/Device.Server/Hosting/test.txt
  -> arquivo nao existe (removido com sucesso)
```

## Riscos e rollback

- Risco: SHAs precisam de atualizacao manual quando uma nova versao da action e lancada (diferente de tags flutuantes que atualizam automaticamente). Mitigacao: Dependabot (`github-actions` ecosystem) monitorara e criara PRs automaticamente para atualizar as SHAs.
- Rollback: reverter os commits de alteracao dos workflows para restaurar tags flutuantes (nao recomendado).

## Proximos passos

1. Confirmar que o Dependabot esta configurado para `github-actions` (ja esta em `.github/dependabot.yml`).
2. Atualizar SHAs periodicamente conforme Dependabot abrir PRs de atualizacao.
3. Considerar adicionar `permissions: {}` ou escopo minimo de permissoes nos jobs que nao precisam de `contents: write`.
