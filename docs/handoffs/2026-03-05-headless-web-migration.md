# 2026-03-05 - Headless Web Migration

## Objetivo

Migrar o runtime headless + UI web como implementacao real (nao POC), mantendo o `App.WinUI` sem regressao e entregando nesta etapa paridade alta da tela Visualizador (MainPage) no web.

## Escopo classificado

- Classificacao: `estrutural`
- Motivo: evolucao de arquitetura (backend headless + frontend web), novos contratos REST/WS, novos servicos de estado/persistencia de visualizador e runner unificado de execucao.

## Arquivos alterados

- `scripts/headless-web-run.ps1`
- `src/App.Headless/App.Headless.csproj`
- `src/App.Headless/Program.cs`
- `src/App.Headless/UiApiEndpoints.cs`
- `src/App.Headless/UiContracts.cs`
- `src/App.Headless/Services/DeviceStateService.cs`
- `src/App.Headless/Services/HeadlessAudioPipeline.cs`
- `src/App.Headless/Services/PreviewBroadcaster.cs`
- `src/App.Headless/Services/HeadlessVisualizerSettingsStore.cs`
- `src/App.Headless/Services/HeadlessVisualizerSettingsDomainService.cs`
- `src/App.Headless/Services/HeadlessHub75SessionService.cs`
- `src/App.Headless/Services/VisualizerModels.cs`
- `src/App.Headless/Services/Gif/GifContentSourceMode.cs`
- `src/App.Headless/Services/Gif/GifScaleMode.cs`
- `src/App.Headless/Services/Gif/Hub75FrameFormatter.cs`
- `src/App.Headless/Services/Gif/Hub75GifDecoder.cs`
- `src/App.Headless/Services/Gif/Hub75GifPlayer.cs`
- `src/Web.Headless/src/App.svelte`
- `src/Web.Headless/src/pages/VisualizerPage.svelte`
- `src/Web.Headless/src/pages/DevicesPage.svelte`
- `src/Web.Headless/src/pages/AppsPage.svelte`
- `src/Web.Headless/src/lib/api.ts`
- `src/Web.Headless/src/lib/ws.ts`
- `src/Web.Headless/src/tests/api.test.ts`
- `src/Web.Headless/dist/*` (gerado por build)

## Decisoes tomadas

1. Backend e frontend continuam no mesmo repositorio, em diretorios separados:
   - `src/App.Headless`
   - `src/Web.Headless`
2. Stack web mantida em `Svelte 5 + TypeScript + Vite + npm`.
3. `App.Headless` passa a ter estado dinamico do visualizador com persistencia local:
   - linear boost
   - bar count
   - fft size
   - fft smoothing
   - weighting filter
   - frequency scale
   - faixa min/max
   - hub75 enabled
   - brightness
4. Pipeline headless evoluido para:
   - aplicacao de configuracao em runtime (thread-safe)
   - troca de modo `audio|gif`
   - snapshot de espectro para UI
   - emissao de estado do visualizador em tempo real
5. Sessao HUB75 portada para headless:
   - ao habilitar, ativa `visualizer-hub75` em devices online
   - ao desabilitar, tenta restaurar app anterior
6. API expandida sem quebra dos endpoints existentes:
   - `GET /api/ui/visualizer/state`
   - `PUT /api/ui/visualizer/settings`
   - `POST /api/ui/visualizer/hub75`
   - `POST /api/ui/visualizer/content-mode`
   - `POST /api/ui/visualizer/gif/url`
   - `POST /api/ui/visualizer/gif/play`
   - `POST /api/ui/visualizer/gif/pause`
   - `POST /api/ui/visualizer/gif/stop`
7. WS `/ws/ui/preview` expandido com:
   - `visualizer-spectrum`
   - `visualizer-status`
   - `visualizer-settings-changed`
   - mantendo `frame`, `heartbeat`, `command-progress`, `onboarding-progress`, `onboarding-result`
8. Frontend reestruturado para shell com navegacao lateral:
   - Visualizador (default)
   - Dispositivos
   - Apps
9. Visualizador web implementado com:
   - barra superior (config, modo, toggle HUB75, status)
   - painel lateral de configuracoes
   - canvas principal em tempo real a partir de `visualizer-spectrum`
   - preview HUB75 (128x64 escalado)
   - controles GIF (URL + play/pause/stop + feedback)
   - fullscreen com atalhos `F11` e `Esc`
10. Runner unico implementado em `scripts/headless-web-run.ps1`:
    - `-Mode dev` (backend + Vite juntos, abre `http://127.0.0.1:5173`)
    - `-Mode prod` (build web + backend servindo `dist`, abre `http://127.0.0.1:5175`)
    - flags `-NoOpen` e `-SkipInstall`
    - encerramento de processos filhos no fim da execucao

## Validacoes executadas

- `npm.cmd run lint` (em `src/Web.Headless`) -> OK
- `npm.cmd run typecheck` -> OK
- `npm.cmd run test:unit` -> OK
- `npm.cmd run build` -> OK
- `dotnet build src/App.Headless/App.Headless.csproj -c Debug` -> OK
- `dotnet build MicaAudio.sln -c Debug` -> OK (warnings preexistentes em outros projetos)
- `dotnet test MicaAudio.sln -c Debug --no-build` -> OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> OK

## Riscos e rollback

- Risco: decode GIF depende de `System.Drawing` e de fonte HTTP valida.
- Risco: envio WS de espectro/frame pode crescer em custo com muitos clientes simultaneos.
- Risco: sessao HUB75 em reconcilicao ainda e baseline (nao inclui estrategia completa de retries/backoff da camada WinUI).

Rollback:
1. Reverter alteracoes em `src/App.Headless` e `src/Web.Headless`.
2. Reverter `scripts/headless-web-run.ps1`.
3. Reexecutar validacoes obrigatorias da solucao.

## Proximos passos

1. Endurecer autenticacao/autorizacao dos endpoints web antes de exposicao fora da LAN.
2. Cobrir novas rotas de visualizador com testes de integracao (inclusive fluxo GIF e HUB75 session).
3. Fechar paridade da pagina Apps (layout e operacoes de deploy/ativacao).
4. Refinar politica de retry/telemetria da sessao HUB75 para cenario de alta instabilidade de rede.
