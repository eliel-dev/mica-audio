# Guia - Rodar Web Headless pelo botao verde do Visual Studio

## Objetivo

Subir o stack `App.Headless + Web.Headless` em modo dev pelo perfil compartilhado `Web Headless Dev`, usando o Visual Studio como launcher sem deixar processos orfaos ao parar o debug.

## Passos

1. Confirmar no Visual Studio Community os workloads:
   - `ASP.NET e desenvolvimento Web`
   - `Desenvolvimento em node.js`
   - `Desenvolvimento para desktop com .NET`
   - `Desenvolvimento de aplicativo WinUI`
2. Abrir `MicaAudio.sln`.
3. Se o dropdown de debug nao mostrar perfis compartilhados, habilitar o suporte a `Multi-Project Launch Profiles` na instalacao/preview correspondente do Visual Studio e reabrir a solucao.
4. Selecionar `Web Headless Dev` no dropdown de execucao.
5. Clicar no botao verde.
6. Confirmar que:
   - o launcher sobe `scripts/headless-web-run.ps1 -Mode dev`;
   - o browser abre em `http://127.0.0.1:5173`;
   - o backend responde em `http://127.0.0.1:5175/api/ui/health`.
7. Ao clicar em `Stop` no Visual Studio, confirmar que `powershell`, `dotnet`, `npm` e `node` do stack foram encerrados junto com o launcher.

## Referencias de codigo

- [App.DevLauncher Program](../../../src/App.DevLauncher/Program.cs#L1) - launcher do VS que encapsula o runner PowerShell e usa Job Object para encerrar a arvore de processos.
- [headless-web-run.ps1](../../../scripts/headless-web-run.ps1#L1) - runner unico do stack web/headless, com instalacao automatica de dependencias e modos `dev|prod`.
- [MicaAudio.slnLaunch](../../../MicaAudio.slnLaunch#L1) - perfil compartilhado `Web Headless Dev` carregado pelo Visual Studio.
- [App.Headless Program](../../../src/App.Headless/Program.cs#L1) - backend HTTP em `5175`, WS e fallback para assets do frontend.

## Checklist rapido

- [ ] O dropdown do Visual Studio mostra `Web Headless Dev`.
- [ ] O botao verde abre `http://127.0.0.1:5173`.
- [ ] `http://127.0.0.1:5175/api/ui/health` responde com sucesso.
- [ ] Parar o debug encerra a arvore `powershell` + `dotnet` + `npm` + `node`.