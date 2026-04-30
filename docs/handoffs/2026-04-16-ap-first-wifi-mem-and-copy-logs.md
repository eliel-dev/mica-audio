# Handoff - 2026-04-16 - ap-first-wifi-mem-and-copy-logs

## Objetivo

Restaurar o boot AP-first estavel no ESP32-S3 quando a flash estiver limpa, removendo a disputa de RAM interna entre Wi-Fi e HUB75 no `setup()`, e completar o diagnostico do wizard USB com um caminho oficial para copiar toda a sessao serial.

## Escopo classificado

- Classificacao: `firmware_protocolo`.
- Inclui: firmware ESP32-S3 (`setup`, provisioning e leitura de `Preferences`), wizard WinUI, smoke/unit tests e documentacao operacional.
- Nao inclui: refactor para portal nao-bloqueante, retorno do provisioning serial como caminho feliz ou teardown dinamico do HUB75 em runtime.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/src/mica_ota.cpp`
- `firmware/esp32s3-devkitc1/src/mica_prefs.cpp`
- `firmware/esp32s3-devkitc1/src/mica_prefs.h`
- `firmware/esp32s3-devkitc1/src/mica_provisioning.cpp`
- `src/App.WinUI/Views/DevicesPage.Ui.cs`
- `src/App.WinUI/Views/DevicesPage.Onboarding.cs`
- `src/App.WinUI/Views/DevicesPage.WizardSerial.cs`
- `tests/Integration.Smoke/DevicesPageSmokeTests.cs`
- `tests/Output.Tests/SerialMonitorServiceTests.cs`
- `docs/wiki/guides/setup-new-device.md`
- `docs/wiki/modules/app-winui.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/handoffs/2026-04-16-ap-first-wifi-mem-and-copy-logs.md`

## Decisoes tomadas

1. O boot incompleto passou a priorizar `Preferences -> decidir provisioning -> abrir AP`, deixando `initMatrixDisplay()` para depois da decisao de Wi-Fi.
2. O conserto de RAM foi limitado ao boot limpo; nao foi introduzido teardown novo do HUB75 no runtime.
3. Leituras de `Preferences` no boot/provisioning/OTA passaram a usar `isKey()` antes de `get*()`, com defaults seguros e sem cascata `NOT_FOUND`.
4. O wizard USB manteve o `ListView` de logs, mas ganhou `Copiar logs` reaproveitando `ISerialMonitorService.ExportAllText()`.
5. O primeiro portal bloqueante pode abrir sem `SETUP WIFI` no HUB75; a prioridade travada nesta correcao e o AP confiavel.

## Validacoes executadas

- `dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~SerialMonitorServiceTests|FullyQualifiedName~MicaSerialProtocolTests|FullyQualifiedName~WizardSerialMonitorPolicyTests"` -> OK.
- `dotnet test tests/Integration.Smoke/Integration.Smoke.csproj -c Debug --filter "FullyQualifiedName~DevicesPageSmokeTests"` -> OK.
- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` -> OK.
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` -> OK.
- `powershell -ExecutionPolicy Bypass -File .\scripts\mvvm-validate.ps1` -> OK.
- `dotnet build MicaAudio.sln -c Debug` -> OK.
- `platformio run -e esp32s3_devkitc1_dma_exp -d firmware/esp32s3-devkitc1` -> OK.
- Launch check WinUI: `src/App.WinUI/bin/x64/Debug/net10.0-windows10.0.22621.0/win-x64/App.WinUI.exe` abriu com janela principal `WinUI Desktop`.
- Observacao: `dotnet restore/build` continua emitindo warnings `NU190x` preexistentes de `Magick.NET-Q8-AnyCPU 14.11.1`; nenhum erro novo ficou pendente.

## Riscos e rollback

- Risco: com Wi-Fi priorizado antes do display no boot limpo, o primeiro portal pode nao mostrar feedback visual no HUB75.
  - Mitigacao: o wizard agora captura/copiа a serial inteira e o fallback `SETUP WIFI` continua valendo quando o display ja estiver ativo.
- Risco: inicializar o HUB75 depois do Wi-Fi ainda pode falhar em cenarios de memoria mais apertados.
  - Mitigacao: o AP passa a subir primeiro; em falha residual, o log serial do wizard fica copiavel para diagnostico de bancada.
- Rollback:
  1. remover `mica_prefs.*` e voltar aos `gPrefs.get*()` diretos;
  2. recolocar `initMatrixDisplay()` antes da decisao de provisioning em `setup()`;
  3. retirar `Copiar logs` do wizard e manter apenas `Recapturar boot`/`Limpar`.

## Proximos passos

1. Validar em bancada com `erase-all + flash` se o SSID `MicaAudio-Setup-xxxx` aparece sem `ESP_ERR_NO_MEM`.
2. Confirmar se o HUB75 volta ao runtime normal apos concluir o portal AP em hardware real.
3. Se ainda houver falha de memoria com Wi-Fi + HUB75 ja provisionados, medir heap interna/largest block no boot para decidir um ajuste isolado de buffers ou do perfil DMA.
