# Handoff - HUB75 60 FPS com pacing fisico correto

## Objetivo

Corrigir o pacing de apresentacao do HUB75 para que o firmware nao flippe o `double buffer` acima da taxa fisica do painel por causa de arredondamento em milissegundos, preservando o canvas unico `128x64`, o transporte `Frame128x64Rgb565` e a fidelidade visual do painel oficial.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite:
  - o pacing do `flipDMABuffer()` passa a usar microssegundos com arredondamento para cima;
  - o firmware respeita `effective_present_interval_us = max(target_60fps, physical_refresh)` em vez de truncar `1000 / refreshRate`;
  - `hub75PresentFrames` passa a contar presents reais do HUB75;
  - `hub75Fps` do dashboard passa a derivar de `hub75PresentFrames`;
  - `Bins128` e `Frame128x64` deixam de depender de redraw bruto da matriz inteira em todo present.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `src/Device.Protocol/Models/DeviceTelemetryMessage.cs`
- `src/Device.Protocol/Models/DeviceSnapshot.cs`
- `src/Device.Protocol/Models/DeviceRecord.cs`
- `src/Device.Server/Hosting/DeviceRecordMutations.cs`
- `src/Device.Server/Hosting/DeviceSession.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs`
- `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs`
- `src/App.WinUI/Services/Devices/DeviceRefreshCoordinator.cs`
- `tests/Output.Tests/DeviceTelemetryMessageTests.cs`
- `tests/Output.Tests/DeviceServerHostDashboardTests.cs`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/device-telemetry-v2-fields.md`
- `docs/wiki/reference/device-observability-dashboard.md`
- `docs/handoffs/2026-03-13-hub75-60fps-physical-pacing.md`

## Decisoes tomadas

1. O pacing do HUB75 deixou de usar milissegundos truncados e passou a usar microssegundos:
   - `physical_present_interval_us = ceil(1_000_000 / calculated_refresh_rate)`
   - `target_present_interval_us = ceil(1_000_000 / 60)`
   - `effective_present_interval_us = max(target, physical)`
2. `hub75PresentFrames` foi adicionado como contador monotono de presents reais. `streamFramesApplied` foi preservado, mas ficou com a semantica de payload novo efetivamente exibido ao menos uma vez.
3. O caminho `Frame128x64` passou a usar buffer sombra por DMA buffer e diff por linha/pixel em SRAM interna, evitando `clearScreen() + redraw completo` como unico caminho de render.
4. O caminho `Bins128` passou a usar cache de alturas por buffer e diff por segmento/coluna, preservando apresentacao continua quando o painel suporta essa cadencia.
5. LUTs pequenas em SRAM (`RGB565 -> RGB888`) foram adicionadas para reduzir custo aritmetico repetido por pixel, sem mover framebuffer/DMA para PSRAM.
6. O host/dashboard passou a calcular `hub75Fps` por `hub75PresentFrames`, que mede a taxa fisica do painel, e nao por `streamFramesApplied`.

## Validacoes executadas

```text
pio run -d firmware/esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp -> OK
dotnet test .\tests\Output.Tests\Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceServerHostDashboardTests|FullyQualifiedName~DeviceTelemetryMessageTests" -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet build .\src\App.WinUI\App.WinUI.csproj -c Debug -p:Platform=x64 -> OK
dotnet build MicaAudio.sln -c Debug -> falhou fora do escopo desta entrega, com `APPX0002/MSB3030` no pipeline WinUI/Integration.Smoke procurando `.xbf` ausentes (`MainPage.xbf`, `ShellPage.xbf`, `SettingsPage.xbf`, `Fluent2Controls.xbf`, `Fluent2Tokens.xbf`)
```

## Riscos e rollback

- Risco principal: mesmo com pacing fisico correto, o painel real ainda pode continuar limitado por custo de render ou comportamento eletrico especifico do combo `ICN6124D + FM7258E`.
- Risco secundario: os buffers sombra em SRAM aumentam consumo de RAM interna; o firmware continua dentro da margem observada, mas isso deve ser monitorado junto com `freeHeapBytes` e `largestHeapBlockBytes`.
- Como reverter:
  - voltar o scheduler para o pacing anterior;
  - remover `hub75PresentFrames` do protocolo/host;
  - restaurar o caminho de redraw integral da matriz.

## Proximos passos

1. Executar as validacoes obrigatorias restantes (`docs-validate`, `ai-governance-check`, `dotnet build`) e atualizar este handoff se houver bloqueio local.
2. Investigar separadamente por que a solucao completa ainda quebra no caminho WinUI/Integration.Smoke por artefatos `.xbf` ausentes, apesar de `App.WinUI.csproj` isolado compilar.
3. Gravar o firmware no hardware e validar no serial boot os valores de `calculated_refresh_rate`, `physical_present_interval_us`, `target_present_interval_us` e `effective_present_interval_us`.
4. Medir no painel real se `hub75Fps` converge para `~60` quando o painel suporta essa cadencia, sem aumento de flicker.
5. Se ainda houver oscilacao forte no hardware apos esta entrega, abrir uma fase focada em custo de render restante e em ajuste fino do driver/timing do painel, sem reabrir o pacing truncado em milissegundos.
