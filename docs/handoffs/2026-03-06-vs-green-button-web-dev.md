# 2026-03-06 - VS Green Button Web Dev

## Objetivo

Retomar a etapa pendente do fluxo `Web.Headless + App.Headless` para permitir subir o stack de desenvolvimento com um clique no botao verde do Visual Studio Community.

## Escopo classificado

- Classificacao: `estrutural`
- Motivo: muda a orquestracao de execucao no Visual Studio, adiciona projeto novo na solucao, perfil compartilhado `.slnLaunch`, ajustes no runner PowerShell e evidencia nova em wiki/handoff.

## Arquivos alterados

- `src/App.DevLauncher/App.DevLauncher.csproj`
- `src/App.DevLauncher/Program.cs`
- `scripts/headless-web-run.ps1`
- `MicaAudio.sln`
- `MicaAudio.slnLaunch`
- `docs/wiki/guides/visual-studio-web-headless-dev.md`
- `docs/wiki/README.md`
- `docs/handoffs/2026-03-06-vs-green-button-web-dev.md`

## Decisoes tomadas

1. Mantido `scripts/headless-web-run.ps1` como fonte unica de orquestracao; o Visual Studio agora chama esse runner por meio de `App.DevLauncher`, sem duplicar a logica de subir backend/frontend.
2. O launcher usa `Windows Job Object` com `kill-on-close` para que parar o debug no Visual Studio derrube tambem `powershell`, `dotnet`, `npm` e `node`.
3. O `App.DevLauncher` foi alinhado ao SDK atual do repositorio (`net10.0-windows10.0.19041.0`) em vez do `net8` proposto no plano original, para evitar desvio em relacao ao estado real da solucao.
4. O runner mudou de install web "sempre" para install "auto", rodando `npm ci` apenas quando `node_modules` nao existe ou quando `package.json`/`package-lock.json` estiverem mais novos que `node_modules/.package-lock.json`.
5. O perfil compartilhado `MicaAudio.slnLaunch` foi mantido com um unico projeto (`App.DevLauncher`), porque o objetivo do botao verde e iniciar o stack completo, nao depurar backend/frontend separadamente.
6. Portas e contratos do stack foram preservados:
   - frontend Vite em `5173`
   - backend HTTP em `5175`
   - device server em `5174`

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build MicaAudio.sln -c Debug --configfile NuGet.config -m:1 -> OK (1 warning preexistente WIN2D0001 em Integration.Smoke)
```

Observacao:
- `dotnet build MicaAudio.sln -c Debug` em modo paralelo reproduziu lock transiente do XAML compiler em `src/App.WinUI/obj/.../input.json`; a rerun serial com `-m:1` concluiu com sucesso.

## Riscos e rollback

- Risco principal: a visibilidade de perfis `.slnLaunch` depende da versao/configuracao do Visual Studio; se o perfil nao aparecer, o suporte a shared multi-project launch profiles precisa estar habilitado.
- Risco principal: a heuristica de install automatico depende de `node_modules/.package-lock.json`; se esse stamp local sumir, o runner vai reinstalar dependencias na proxima execucao.

Rollback:
1. Remover `src/App.DevLauncher/`.
2. Reverter `scripts/headless-web-run.ps1`, `MicaAudio.sln` e `MicaAudio.slnLaunch`.
3. Remover o guia/handoff desta mudanca.
4. Reexecutar as validacoes obrigatorias.

## Proximos passos

1. Validar manualmente o perfil `Web Headless Dev` dentro do Visual Studio Community.
2. Se o VS ainda mostrar warning de configuration mappings desconhecidos e isso afetar o perfil, normalizar os mappings relevantes no `MicaAudio.sln`.
3. Considerar um segundo perfil compartilhado para `prod` apenas se o fluxo de release realmente passar a depender do Visual Studio.