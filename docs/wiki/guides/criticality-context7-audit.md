# Guia - Criticality + Context7 audit

## Objetivo

Executar auditoria tecnica completa do projeto (app + servidor + firmware) priorizada por risco operacional, validando atualizacao de implementacao com Context7 e gerando backlog priorizado.

## Escopo desta fase

1. Inclui auditoria documental e tecnica (sem alteracao de runtime).
2. Inclui build/test, dependencia .NET, dependencia firmware e validacao de API/docs com Context7.
3. Nao inclui refatoracao de codigo, upgrade automatico de libs ou mudanca de protocolo wire.

## Modelo de criticidade

1. Eixos (1 a 5):
   - Impacto operacional.
   - Exposicao (externo/rede/superficie).
   - Probabilidade de falha.
   - Lacuna de teste (5 = sem protecao suficiente).
2. Formula:
   - `score = 0.4*impacto + 0.3*exposicao + 0.2*probabilidade + 0.1*lacuna_teste`.
3. Classe:
   - `>= 4.2`: Critico
   - `3.4 - 4.19`: Alto
   - `2.6 - 3.39`: Medio
   - `< 2.6`: Baixo

## Inventario tecnico consolidado

1. Fluxo critico principal:
   - `AudioPipelineCoordinator` -> `Esp32S3LedOutput` -> `DeviceServerHost` -> firmware `main.cpp`.
2. Contrato ativo de stream:
   - `StreamFrameV2` com `bins128` (145 bytes) e `frame128x64 RGB565` (16400 bytes).
3. Seguranca operacional:
   - rate limiting e limites de payload no host.
   - persistencia de token criptografado com DPAPI.
   - fallback legado de token em query WS disponivel, mas OFF por default e controlado por `settings.json`.
4. Firmware oficial:
   - perfil unico `esp32s3_devkitc1_dma_exp`.
   - dependencia detectada desatualizada: `ArduinoJson 7.4.2 -> 7.4.3`.

## Passos

1. Rodar validacao de runtime:

```powershell
dotnet build MicaAudio.sln -c Debug
dotnet test MicaAudio.sln -c Debug --no-build
```

2. Rodar diagnostico de dependencias .NET:

```powershell
dotnet list MicaAudio.sln package --outdated --include-transitive
dotnet list MicaAudio.sln package --vulnerable --include-transitive
```

3. Rodar diagnostico de dependencias firmware:

```powershell
$env:PYTHONIOENCODING='utf-8'
platformio pkg list -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp
platformio pkg outdated -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp
```

4. Validar implementacao via Context7 (fonte primaria de docs):
   - Windows App SDK: `/websites/learn_microsoft_en-us_windows_windows-app-sdk_api_winrt`
   - Win2D WinUI3: `/websites/microsoft_github_io_win2d_winui3_html`
   - ArduinoJson: `/bblanchon/arduinojson`
   - ESP32 HUB75 DMA: `/mrcodetastic/esp32-hub75-matrixpanel-dma`

5. Preencher backlog priorizado com score, evidencia, acao, teste minimo e rollback.

## Resultado da rodada 2026-03-03

1. Build:
   - `dotnet build MicaAudio.sln -c Debug` -> OK (1 warning WIN2D0001 em Integration.Smoke AnyCPU).
2. Testes:
   - `dotnet test MicaAudio.sln -c Debug --no-build` -> FALHOU em 2 testes:
   - `Disable_ShouldRestoreAfterReconnect_WhenDeviceReturnsOnline`
   - `Disable_ShouldRestorePreviousApp_WhenDeviceIsOnline`
3. Dependencias .NET:
   - sem vulnerabilidades conhecidas nesta rodada;
   - drift de versao relevante em `Microsoft.Extensions.*`, `WebView2` e pacotes de teste.
4. Dependencias firmware:
   - `ArduinoJson` desatualizado em patch (`7.4.2` para `7.4.3`);
   - demais libs principais sem alerta nesta rodada.
5. Consistencia protocolo:
   - layout `StreamFrameV2` e parsing no firmware estao alinhados em versao, tipo e tamanho.

## Atualizacao RSK-001 2026-03-03

1. Correcao aplicada em `Hub75VisualizerSessionService`:
   - restore imediato ao desativar HUB75 sem herdar cooldown da ativacao anterior;
   - scheduler interno para retry autonomo sem depender de `DevicesChanged`.
2. Validacao de testes apos correcao:
   - `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~Hub75VisualizerSessionServiceTests"` -> OK (4/4).
   - `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests"` -> OK.
3. Resultado:
   - item `RSK-001` saiu de falha ativa para mitigado/corrigido nesta fase.

## Atualizacao RSK-003 2026-03-03

1. Trilha faseada executada:
   - Fase 1 (runtime): `App.WinUI` atualizado em `Microsoft.Extensions.DependencyInjection/Logging/Logging.Debug` para `10.0.3` e `Microsoft.Web.WebView2` para `1.0.3800.47`.
   - Fase 2 (test toolchain): `Microsoft.NET.Test.Sdk` para `18.3.0`, `coverlet.collector` para `8.0.0`, `xunit.runner.visualstudio` para `3.1.5` nos 3 projetos de teste.
   - Fase 3 (benchmark): `BenchmarkDotNet` para `0.15.8`.
2. Validacao da trilha:
   - `dotnet build MicaAudio.sln -c Debug` -> OK.
   - `dotnet test MicaAudio.sln -c Debug --no-build` -> OK (172 pass, 1 skip manual em smoke).
   - `dotnet list ... --vulnerable --include-transitive` -> sem vulnerabilidades conhecidas.
3. Resultado:
   - item `RSK-003` saiu de `Parcial` para `Corrigido` nesta rodada (com backlog transitive residual fora do escopo P1).

## Validacao Context7 por componente

1. Win2D for WinUI3:
   - status: aderente.
   - evidencia: uso de `CanvasControl` + namespace `Microsoft.Graphics.Canvas.UI.Xaml` no app.
   - observacao: implementacao nao usa `CanvasAnimatedControl` (decisao de arquitetura atual).
2. Windows App SDK:
   - status: aderente parcial.
   - evidencia: app usa WindowsAppSDK 1.8 no projeto principal e self-contained.
   - risco: drift de dependencia transitive e necessidade de revisao coordenada por modulo.
3. ArduinoJson:
   - status: aderente com atualizacao pendente.
   - evidencia: firmware usa `JsonDocument`, `deserializeJson` e `serializeJson`, alinhado ao guia v7.
   - risco: patch desatualizado.
4. ESP32 HUB75 DMA:
   - status: aderente.
   - evidencia: fluxo de `MatrixPanel_I2S_DMA` com `begin`, `setBrightness8`, `clearScreen` e pin mapping explicito.

Observacao importante:
os status acima incluem inferencia tecnica a partir das referencias Context7 e do codigo local, sem benchmark de hardware em bancada nesta fase.

## Backlog priorizado (risco operacional)

| ID | Componente | Evidencia | Score | Classe | Status atualizacao | Acao recomendada | Teste minimo | Prioridade | Rollback |
|---|---|---|---:|---|---|---|---|---|---|
| RSK-001 | Session restore HUB75 | falha intermitente de restore (corrigida em 2026-03-03) | 3.7 | Alto | Corrigido | manter monitoramento de regressao em testes de sessao HUB75 | rerun da suite `Output.Tests` e smoke de devices | P0 | reverter alteracao de session policy |
| RSK-002 | Auth WS legado | query token legado habilitado por default (corrigido em 2026-03-03) | 4.1 | Alto | Corrigido | manter auth WS por header; usar flag de rollback somente em incidente | testes de auth WS com token em header/query | P0 | reativar flag em release emergencial |
| RSK-003 | Drift de pacotes .NET | backlog de `Microsoft.Extensions.*` e `WebView2` (trilha faseada executada em 2026-03-03) | 3.4 | Alto | Corrigido | monitorar drift transitive fora do escopo P1 e abrir lote futuro dedicado | build + test completo por fase | P1 | pin de versoes anteriores no csproj |
| RSK-004 | Firmware versioning | `kFirmwareVersion` ainda fixo por release (sem automacao por tag/commit) | 2.9 | Medio | Parcial | adotar versionamento de build (tag/commit/date) no firmware | telemetria valida em device snapshot | P1 | fallback para string estatica |
| RSK-005 | Firmware deps | `ArduinoJson` patch desatualizado | 2.6 | Medio | Parcial | atualizar para `7.4.3` e validar regressao de parse | build firmware + smoke de pair/telemetria | P1 | restaurar lock anterior |
| RSK-006 | Artefato precompilado | risco de bin stale sem carimbo/hash no processo | 3.2 | Medio | Parcial | registrar hash + metadata de build no fluxo de export | validar hash no download local | P2 | manter fluxo atual de copia |
| RSK-007 | Cobertura de firmware secundario | `firmware/matrixportal-s3/src/main.cpp` vazio | 2.9 | Medio | N/A | marcar oficialmente como experimental ou remover da trilha ativa | docs + gate de coverage por firmware ativo | P2 | manter pasta isolada sem build |
| RSK-008 | Legado de protocolo | `StreamFrameV1` ainda presente no codigo/testes | 2.5 | Baixo | Parcial | definir politica de deprecacao e remover quando seguro | testes de compatibilidade antes da remocao | P3 | manter V1 isolado por compatibilidade |
| RSK-009 | Win2D warning em teste | WIN2D0001 em Integration.Smoke AnyCPU | 2.3 | Baixo | N/A | fixar plataforma de teste ou suprimir warning com criterio | build limpo sem warning alvo | P3 | retornar configuracao atual |

## Checklist rapido

- [ ] `dotnet build MicaAudio.sln -c Debug` executado
- [ ] `dotnet test MicaAudio.sln -c Debug --no-build` executado
- [ ] `dotnet list ... --outdated --include-transitive` executado
- [ ] `dotnet list ... --vulnerable --include-transitive` executado
- [ ] `platformio pkg list` e `platformio pkg outdated` executados
- [ ] backlog priorizado atualizado com score e acao
- [ ] handoff estrutural publicado em `docs/handoffs/`

## Referencias externas (Context7 / oficial)

- Win2D for WinUI3 docs: https://microsoft.github.io/Win2D/WinUI3/html/Introduction.htm
- Win2D API reference: https://microsoft.github.io/Win2D/WinUI3/html/APIReference.htm
- Windows App SDK API reference: https://learn.microsoft.com/en-us/windows/windows-app-sdk/api/winrt/
- ArduinoJson (v7): https://github.com/bblanchon/arduinojson
- ESP32 HUB75 DMA: https://github.com/mrcodetastic/ESP32-HUB75-MatrixPanel-DMA

## Referencias de codigo

- [AudioPipelineCoordinator.cs](../../../src/App.WinUI/Services/AudioPipelineCoordinator.cs#L12) - assinatura: `internal sealed class AudioPipelineCoordinator`
- [Esp32S3LedOutput.cs](../../../src/Output/Led/Esp32S3LedOutput.cs#L11) - assinatura: `public sealed class Esp32S3LedOutput`
- [DeviceServerHost.cs](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L20) - assinatura: `public sealed partial class DeviceServerHost`
- [ServerConfig.cs](../../../src/Device.Protocol/Contracts/ServerConfig.cs#L34) - assinatura: `AllowLegacyWebSocketQueryToken`
- [StreamFrameV2.cs](../../../src/Device.Protocol/Stream/StreamFrameV2.cs#L13) - assinatura: `PayloadSizeBins128 = 145`
- [main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L17) - assinatura: `kStreamFrameSize = 145`
- [main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L51) - assinatura: `kFirmwareVersion = "v2026.03.03-rsk002-ws-header"`
- [platformio.ini](../../../firmware/esp32s3-devkitc1/platformio.ini#L2) - assinatura: `default_envs = esp32s3_devkitc1_dma_exp`
- [build-precompiled-firmware.ps1](../../../scripts/build-precompiled-firmware.ps1#L37) - assinatura: `Resolve-PlatformIoCommand`
- [PrecompiledFirmwareService.cs](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L21) - assinatura: `FileName = "esp32s3-devkitc1-128x64-dma_exp_merged.bin"`
- [JsonDeviceRegistryStore.cs](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L13) - assinatura: `TokenCipherPrefix = "dpapi:v1:"`
- [StreamFrameV2Tests.cs](../../../tests/Output.Tests/StreamFrameV2Tests.cs#L8) - assinatura: `CreateBins128_ShouldGenerateExpectedLayout`
- [Hub75VisualizerSessionServiceTests.cs](../../../tests/Output.Tests/Hub75VisualizerSessionServiceTests.cs#L33) - assinatura: `Disable_ShouldRestorePreviousApp_WhenDeviceIsOnline`
