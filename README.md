# Mica Audio

Visualizador de audio para Windows (WinUI 3 + Win2D) com captura WASAPI loopback em tempo real, foco em visual "bonito na tela" e arquitetura em transicao para `server + firmware + clients`.

Escopo atual: Windows-first, pipeline modular (`PCM -> FFT/bandas -> render -> output opcional`), preset padrao `AudioMotion Clone`, preview HUB75 via simulador interno e control plane separado via `MicaAudio.Server`.

## Direcao oficial

- `server` = control plane + storage + catalogo + estado duravel.
- `cliente Windows` = primeiro edge client oficial e dono do data plane LAN.
- `ESP32` = runtime de execucao/render com ownership explicito por device.
- `visualizador` e `Paineis` passam a ser oficialmente client-driven; o servidor deixa de ser o hot path visual alvo.

## Status atual

- V1 focada em visualizacao em tempo real no desktop.
- Preset padrao no primeiro run: `AudioMotion Clone`.
- Output atual:
- `NullLedOutput` (no-op)
- `SimulatorLedOutput` (preview 64x32)

Comportamento do simulador: o frame mais recente e mantido internamente e o preview HUB75 da UI consome esse snapshot por polling no ciclo de render (timer a 60 FPS). Nao existe evento de frame atualizado no simulador.
- Saida UDP real para controlador externo: planejada para etapa futura.

## Wiki tecnica

A documentacao tecnica detalhada do projeto fica em `docs/wiki/README.md`.

Validacao local dos links/referencias da wiki:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
```
### Documentacao operacional (Fase 2)

- Guia operacional de dispositivos/servidor/firmware pre-compilado: `docs/wiki/modules/server-build-and-artifacts.md`
- Operacao de dispositivos em campo: `docs/wiki/guides/operate-device-lifecycle.md`
- Matriz de troubleshooting: `docs/wiki/reference/troubleshooting-matrix.md`
- Saude da documentacao e metricas: `docs/wiki/reference/docs-health.md`

Fluxo recomendado antes de commit relevante:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug
```
### Governanca de documentacao (Fase 3)

- ADRs: `docs/adr/README.md`
- Workflow CI: `.github/workflows/governance.yml`
- Workflow de release: `.github/workflows/release.yml`
- Gate estrutural: `scripts/docs-structural-gate.ps1`
- Template de PR: `.github/PULL_REQUEST_TEMPLATE.md`

Politica resumida:

1. Mudanca estrutural (`src/`, `firmware/`, `scripts/`, `installer/`, `MicaAudio.sln`, `global.json`) exige update de docs (`docs/wiki/`, `docs/adr/`, `docs/handoffs/` ou `README.md`).
2. Em PR, a label `docs-exempt` permite bypass controlado (com justificativa).
3. Em push para `main`, o gate estrutural nao aceita bypass por label.

Validacao local recomendada:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
dotnet build MicaAudio.sln -c Debug
```
### Modo solo com IA (Fase 4)

- Contrato canonico para agentes: `AGENTS.md`
- Politica machine-readable: `docs/wiki/reference/ai-contract.v1.yaml`
- Schema do contrato: `docs/wiki/reference/ai-contract.schema.json`
- Guardrail local: `scripts/ai-governance-check.ps1`
- Hooks versionados: `.githooks/pre-commit`, `.githooks/pre-push`
- Bootstrap de hooks: `scripts/git-hooks-bootstrap.ps1`
- Handoff estrutural: `docs/handoffs/YYYY-MM-DD-<slug>.md`

Bootstrap local recomendado:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\git-hooks-bootstrap.ps1
```

Validacao completa recomendada antes de push:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug
```
### Arquitetura remote-only e widgets autonomos no servidor (2026-05)

A partir de 2026-05 o WinUI nao hospeda mais o `DeviceServer` em-processo. Toda
comunicacao com o ESP32 passa pelo `MicaAudio.Server` standalone (Docker ou
`dotnet run`). O firmware ESP32-S3 conecta direto em modo STA com credenciais
hardcoded no codigo-fonte e se auto-registra via `POST /api/v1/auto-register`,
recebendo `deviceId` e `token` deterministicos por MAC.

Widgets autonomos (relogio hoje, clima/GIFs/imagens em iteracoes futuras) sao
renderizados pelo proprio `MicaAudio.Server` em background, mantendo o painel
ativo no LED mesmo apos o WinUI fechar. Widgets dependentes do cliente
(visualizador de audio, metricas do PC) so funcionam enquanto o WinUI esta
aberto.

- Componentes principais:
  - `src/Panels.Composition/` (modelos, drawing helpers, compositor server-side)
  - `src/MicaAudio.Server/PanelCompositorHostedService.cs` (loop 30 FPS server-side)
  - `src/MicaAudio.Server/FileServerPanelStore.cs` (persistencia por `{StorageRoot}/panels/{deviceId}.json`)
  - `src/Device.Server/Hosting/DeviceServerHost.AutoRegister.cs` (endpoint auto-register)
  - `src/Device.Server/Hosting/DeviceServerHost.PanelStore.cs` (PUT/GET/DELETE `/api/v1/admin/devices/{deviceId}/panel`)
- Configuracao do firmware (Wi-Fi/servidor): copie
  `firmware/esp32s3-devkitc1/src/mica_config.example.h` para
  `firmware/esp32s3-devkitc1/src/mica_config.h` (gitignored) e edite
  `MICA_WIFI_SSID`, `MICA_WIFI_PASSWORD`, `MICA_SERVER_HOST`,
  `MICA_SERVER_PORT` antes do `pio run`.
- Decisao registrada em `docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md`.
- Handoff de migracao: `docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md`.

### Seguranca e qualidade (security-first)

Hardening aplicado no runtime e no pipeline:

- Rate limiting em endpoints criticos (`pair`, `command-ack`, handshake WS).
- Politica de rede privada por padrao no servidor de dispositivos (`ServerConfig`).
- Pareamento com limite anti-abuso por IP/janela.
- Token de dispositivo criptografado em repouso (DPAPI) no `devices.json`.
- Dependabot (NuGet + Actions), CodeQL e Dependency Review no GitHub.
- Gate local/CI para vulnerabilidades de dependencias.

Comando local recomendado:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dependency-vulnerability-gate.ps1 -ProjectOrSolution MicaAudio.sln
```

Guia operacional:
- `docs/wiki/guides/security-quality-hardening.md`

## Principais recursos

- Captura de audio do dispositivo de saida padrao via WASAPI loopback.
- Normalizacao interna para `48 kHz`, mono, `float`.
- Pipeline com contratos estaveis:
- `PcmFrame` (entrada de audio)
- `SpectrumFrame` (bands display + bands64 + level)
- FFT fixa em `2048`, com hop fixo para boa responsividade e menor carga operacional no runtime.
- Escalas de frequencia: `Logarithmic`, `Mel`, `Bark`.
- Weighting filter por bin: `Off`, `A`, `B`, `C`, `D`, `468`.
- Controles de sensibilidade em dB (`Min dB` / `Max dB`) com `Linear Boost` fixo em `1.3`.
- Tela cheia com atalhos:
- `F11` alterna fullscreen
- `Esc` sai do fullscreen
- Painel lateral de configuracoes (estilo hamburger).
- Modo HUB75 com preview dedicado sem quebrar o canvas principal.
- Persistencia de configuracoes e presets em `%AppData%`.

## Arquitetura da solucao

```text
Loopback PCM -> Analyzer (FFT/bandas) -> Visual (Win2D) -> Output opcional (ILedOutput)
```

### Estrutura de projetos

```text
MicaAudio.sln
src/
  App.WinUI
  Audio.Loopback
  Analyzer.Dsp
  MicaAudio.Core
  Output
  Visual.Win2D
tests/
  Analyzer.Dsp.Tests
  Output.Tests
  Integration.Smoke
scripts/
  dev-run.ps1
  dev-doctor.ps1
  sign-dev.ps1
```

### Responsabilidades por modulo

- `src/App.WinUI`: shell da aplicacao, UI, navegacao, estado, presets, persistencia e orquestracao do pipeline.
- `src/Audio.Loopback`: captura WASAPI loopback, reconexao de device, normalizacao para formato interno.
- `src/Analyzer.Dsp`: janela Hann, FFT, mapeamento de bandas (inclui mode0), smoothing, weighting e level.
- `src/Visual.Win2D`: engine/renderers Win2D, incluindo renderer dedicado `AudioMotion Clone`.
- `src/Output`: contrato de output LED + implementacoes `Null` e `Simulator`.
- `src/MicaAudio.Core`: modelos/contratos compartilhados entre modulos.

## Contratos estaveis (public APIs)

### Audio input

- `ILoopbackCapture.StartAsync(CaptureConfig, CancellationToken)`
- `ILoopbackCapture.StopAsync()`
- `ILoopbackCapture.Frames : ChannelReader<PcmFrame>`

`PcmFrame`
- `float[] SamplesMono`
- `long TimestampQpc`

### DSP

- `IAnalyzer.Process(in PcmFrame frame) -> SpectrumFrame?`

`SpectrumFrame`
- `float[] BandsDisplay`
- `float[] Bands64`
- `float Level`
- `long TimestampQpc`
- `float[]? DisplayBarX` (opcional)
- `float[]? DisplayBarWidth` (opcional)

Regra importante: `Bands64` ÃƒÂ© derivado do mesmo espectro calculado no frame (sem segunda FFT).

### Output

- `ILedOutput.Start(LedOutputConfig config)`
- `ILedOutput.Stop()`
- `ILedOutput.Send(LedPayload payload)`
- `ILedOutput.SetBrightness(float value)`
- `ILedOutput.IsAvailable`

## Tecnologias e requisitos

- Windows 10/11 x64.
- .NET SDK conforme `global.json`:
- `10.0.103`
- Target principal da app: `net10.0-windows10.0.22621.0`.
- Bibliotecas e testes sem dependencia de Windows: `net10.0`.
- Visual Studio local: usar versao compativel com `net10.0` (baseline `18.0+`) ou CLI `dotnet` 10.
- Release bundle: instala/verifica `.NET Desktop Runtime 10 x64`.
- UI: WinUI 3 (`Microsoft.WindowsAppSDK` 1.8.x).
- Render: Win2D.
- Captura de audio: NAudio (WASAPI loopback).
- Scripts auxiliares: PowerShell.
## Instalacao para usuario final (Windows 11)

Para usuarios finais (sem scripts PowerShell), a distribuicao oficial do 1.0 e feita por setup assinado no GitHub Releases.

1. Baixe `MicaAudio-Setup-x64-vX.Y.Z.exe` na pagina de Releases.
2. Execute o instalador com duplo clique.
3. O setup instala o app em `%ProgramFiles%\MicaAudio` e aplica pre-requisitos automaticamente.
4. Abra pelo Menu Iniciar (`Mica Audio`).

Atualizacao no 1.0: manual (baixar e executar a versao mais recente do setup).
### Visual Studio Community (fluxo local leve)

- Abra `MicaAudio.Dev.slnf` para desenvolvimento diario.
- O filtro remove `tests/Integration.Smoke` do build local para evitar APPX3217 em maquinas sem SDK UAP.
- O CI continua validando `MicaAudio.sln` completo, sem relaxamento de gate.


### 1) Restore e build

```powershell
dotnet restore MicaAudio.sln
dotnet build MicaAudio.sln -c Debug
```

### 2) Rodar pelo script recomendado

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-run.ps1 -Configuration Debug
```

Observacao: o `RunMode` default atual do script e `publish`.

### 3) Modos de execucao

- Modo publish (padrao):

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-run.ps1 -Configuration Debug -RunMode publish
```

- Modo dotnet run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-run.ps1 -Configuration Debug -RunMode dotnet
```

### 4) Execucao manual alternativa

```powershell
dotnet run --project .\src\App.WinUI\App.WinUI.csproj -c Debug -p:Platform=x64
```

## Scripts de desenvolvimento

| Script | Objetivo | Exemplo |
|---|---|---|
| `scripts/dev-run.ps1` | Build/publish, assinatura dev opcional, launch e diagnostico rapido | `powershell -ExecutionPolicy Bypass -File .\scripts\dev-run.ps1 -Configuration Debug` |
| `scripts/dev-doctor.ps1` | Diagnostico do ambiente (SDK, WinAppRuntime, politica, crash log) | `powershell -ExecutionPolicy Bypass -File .\scripts\dev-doctor.ps1` |
| `scripts/sign-dev.ps1` | Cria/usa certificado dev e assina binarios publicados | `powershell -ExecutionPolicy Bypass -File .\scripts\sign-dev.ps1 -Configuration Debug -SkipPublish` |

### Parametros uteis do `dev-run.ps1`

- `-NoLaunch`: prepara build/publish sem abrir a app.
- `-NoSign`: pula assinatura dev.
- `-SkipDoctor`: pula diagnostico inicial.
- `-SkipPublish`: reutiliza publish existente (quando aplicavel).
- `-SingleFile`: tenta publicacao single-file.
- `-RunMode dotnet|publish|auto`: escolhe estrategia de execucao.
- `-ValidateDocs`: executa `scripts/docs-validate.ps1` antes do build/publish e falha cedo se houver link/referencia quebrado.

## Testes

Rodar suite completa:

```powershell
dotnet test MicaAudio.sln -c Debug
```

Cobertura por projeto:

- `tests/Analyzer.Dsp.Tests`
- Testes de mapeamento de bandas (Log/Mel/Bark).
- Testes de mode0 layout/contagem/monotonicidade.
- `tests/Output.Tests`
- Testes do `SimulatorLedOutput` (frame gerado, clamp de brilho, e ausencia do evento `FrameUpdated`).
- `tests/Integration.Smoke`
- Smoke de pipeline Analyzer -> Output.
- Caso manual de loopback real marcado como `Skip` (execucao assistida).

## Configuracao e persistencia

Arquivos de runtime:

- Settings:
- `%AppData%\MicaAudio\settings.json`
- Presets:
- `%AppData%\MicaAudio\presets\*.json`
- Crash log:
- `%LocalAppData%\MicaAudio\crash.log`

Defaults relevantes atuais (primeiro run):

- Preset: `AudioMotion Clone`
- FFT size: `2048`
- FFT smoothing: `0.75`
- Weighting: `B`
- Frequency scale: `Bark`
- Range: `20 Hz` a `1000 Hz`
- Sensibilidade: `Min -85 dB`, `Max -25 dB`
- Linear boost: `1.3` (fixo)

## Troubleshooting

### 1) PowerShell bloqueia scripts (`PSSecurityException`)

Sintoma:
- `a execucao de scripts foi desabilitada neste sistema`

Use:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-run.ps1 -Configuration Debug
```

### 2) Prompt de Windows App Runtime ausente/incompativel

Sintoma:
- Mensagem pedindo Windows App Runtime compativel.

Acao:
- Preferir fluxo `publish` com configuracao atual do projeto (`WindowsAppSDKSelfContained=true`).
- Validar ambiente com:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev-doctor.ps1
```

### 3) App nao abre e nao mostra erro na UI

- Verifique o log:
- `%LocalAppData%\MicaAudio\crash.log`
- O app tem fallback de startup e grava stack trace nesse arquivo.

### 4) Bloqueio por App Control / SAC / WDAC (`0x800711C7`)

Sintoma tipico:
- `Could not load file or assembly ...` + codigo `0x800711C7`
- DLL bloqueada por politica do Windows App Control.

Impacto:
- Modulos carregados em runtime podem ser bloqueados mesmo em ambiente dev.

Mitigacao em ambiente de desenvolvimento:
- Rodar em VM dev sem politica restritiva.
- Usar certificacao aceita pela politica corporativa.
- Conferir diagnostico do estado de politica via `dev-doctor.ps1`.

### 5) Diferenca entre `RunMode publish` e `RunMode dotnet`

- `publish`: gera executavel e usa saida publicada (default atual, mais previsivel para WinUI/Runtime).
- `dotnet`: executa via `dotnet run`, util para ciclo rapido de desenvolvimento.
- Se um modo falhar, teste o outro e compare logs/doctor.

## Roadmap curto

Ja existe:
- Captura loopback robusta com reconexao de dispositivo.
- Renderer dedicado `AudioMotion Clone`.
- Configuracoes de sensibilidade dB, smoothing, weighting, escala/range.
- Preview HUB75 64x32 via simulador.

Proximos passos naturais:
- `UdpLedOutput` real para hardware externo.
- Expansao de presets/renderers adicionais.
- Ajustes finos de paridade perceptual com referencia audioMotion.
- Pipeline de release (assinatura/distribuicao) para maquinas fora do ambiente dev.

## Contribuicao

Fluxo sugerido:

1. Criar branch para a feature/bugfix.
2. Rodar build e testes localmente.
3. Validar manualmente execucao da app (incluindo fullscreen e modo HUB75 quando aplicavel).
4. Abrir PR com descricao objetiva das mudancas e passos de validacao.

Checklist minimo antes do PR:

```powershell
dotnet build MicaAudio.sln -c Debug
dotnet test MicaAudio.sln -c Debug
```

## Creditos e referencias

Inspiracao de UX/comportamento:

- `audioMotion-analyzer`
- `audiomotion.app`

Implementacao deste projeto segue arquitetura propria (sem port direto de codigo).

## Licenca

Ainda nao definida neste repositorio. Recomendado adicionar `LICENSE` antes da publicacao oficial no GitHub.
