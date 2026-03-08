# Handoff - Entrega 2 Logs por dispositivo e formatacao de metricas

## Objetivo

Preparar a camada de aplicacao para observabilidade por dispositivo, com buffer de logs segmentado por `DeviceId` e formatacao consistente de metricas para uso na UI.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: coordenador expoe logs por dispositivo sem depender de estado global de logs; formatador converte snapshot bruto em apresentacao com semantica correta de loop/heap/psram/rede.

## Arquivos alterados

- src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs
- src/App.WinUI/Services/Devices/DeviceMetricsPresentation.cs
- src/App.WinUI/Services/Devices/DeviceMetricsFormatter.cs
- tests/Output.Tests/DeviceOperationsCoordinatorDeviceLogsTests.cs
- tests/Output.Tests/DeviceMetricsFormatterTests.cs
- tests/Output.Tests/Output.Tests.csproj

## Decisoes tomadas

1. O `DeviceOperationsCoordinator` passou a manter `deviceLogsById` com capacidade fixa (100 eventos por dispositivo), evitando crescimento ilimitado e isolando historico por device.
2. Foi introduzida a abstracao `IDeviceOperationsRuntime` para desacoplar o coordenador do runtime real e permitir testes unitarios deterministas de transicoes e logs.
3. A formatacao foi isolada em `DeviceMetricsFormatter` + `DeviceMetricsPresentation`, mantendo regras de dominio fora da View: label de loop como "Carga do loop", semantica de PSRAM guiada por `psramAvailable` e barras derivadas apenas com dados coerentes.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File ./scripts/docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File ./scripts/ai-governance-check.ps1 -> OK
powershell -ExecutionPolicy Bypass -File ./scripts/mvvm-validate.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK (com warnings pre-existentes)
dotnet test tests/Output.Tests/Output.Tests.csproj --no-build -v q -> OK (105 aprovados)
python -m platformio run -e esp32s3_devkitc1_dma_exp (firmware/matrixportal-s3) -> OK
```

## Riscos e rollback

- Risco principal: divergencia entre a semantica exibida na UI futura e os textos/status calculados pelo formatter pode exigir ajuste de copy sem alterar regra de negocio.
- Como reverter: remover os novos tipos (`DeviceMetrics*`), restaurar o coordenador para logs globais e reverter os testes associados no mesmo commit.

## Proximos passos

1. Integrar `DeviceMetricsFormatter` e `GetDeviceLogs(deviceId)` na `DevicesPage` (Entrega 3) preservando comportamento de selecao atual.
2. Adicionar cobertura de teste de integracao UI para evitar regressao de flicker e fallback de metricas quando o snapshot estiver incompleto.
