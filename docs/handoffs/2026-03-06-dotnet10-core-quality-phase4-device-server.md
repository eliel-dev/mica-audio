# Handoff - Fase 4 de qualidade .NET 10 (core-first em Device/Server)

## Objetivo

Melhorar a arquitetura e a testabilidade do core em `Device.Server`, `Device.Protocol` e `Output` sem alterar o wire HTTP/WS nem o contrato com o firmware.

## Escopo classificado

- Classificacao: estrutural.
- Escopo desta fase:
  - decompor o `DeviceServerHost` em colaboradores internos nomeados;
  - centralizar `ServerConfig` em um runtime config normalizado;
  - mover estado temporal e transicoes de `DeviceRecord` para tipos internos testaveis;
  - extrair a decisao de deduplicacao/envio RGB565 do `Esp32S3LedOutput`;
  - adicionar testes deterministas com `TimeProvider`.
- Fora desta fase:
  - mudanca de wire HTTP/WS;
  - mudanca de DTOs em `Device.Protocol`;
  - mudanca de firmware;
  - refactor em `Audio.Loopback`, `Analyzer.Dsp` ou `App.WinUI`.

## Arquivos alterados

- Host de device / protocolo:
  - `src/Device.Server/Hosting/DeviceServerHost.cs`
  - `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs`
  - `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
  - `src/Device.Server/Hosting/DeviceServerRuntimeConfig.cs`
  - `src/Device.Server/Hosting/DevicePairingState.cs`
  - `src/Device.Server/Hosting/DeviceRecordMutations.cs`
  - `src/Device.Server/Hosting/DeviceSession.cs`
  - `src/Device.Server/Hosting/DeviceSessionRegistry.cs`
  - `src/Device.Server/Hosting/PendingTrackedCommand.cs`
  - `src/Device.Server/Hosting/PendingTrackedCommandStore.cs`
  - `src/Device.Server/Properties/AssemblyInfo.cs`
- Output ESP32:
  - `src/Output/Led/Esp32S3LedOutput.cs`
  - `src/Output/Led/LedFrameDeduplicator.cs`
  - `src/Output/Properties/AssemblyInfo.cs`
- Testes:
  - `tests/Output.Tests/Output.Tests.csproj`
  - `tests/Output.Tests/ManualTimeProvider.cs`
  - `tests/Output.Tests/DeviceServerRuntimeConfigTests.cs`
  - `tests/Output.Tests/DevicePairingStateTests.cs`
  - `tests/Output.Tests/DeviceSessionTests.cs`
  - `tests/Output.Tests/PendingTrackedCommandTests.cs`
  - `tests/Output.Tests/LedFrameDeduplicatorTests.cs`
  - `tests/Output.Tests/LedOutputLifecycleTests.cs`
  - `tests/Output.Tests/DeviceServerHostTimeProviderTests.cs`
- Documentacao:
  - `docs/wiki/modules/device-server-protocol.md`
  - `docs/wiki/modules/output-led.md`
  - `docs/wiki/reference/code-index.md`

## Decisoes tomadas

- `DeviceServerHost` ficou como orquestrador fino:
  - sobe o host ASP.NET Core;
  - aplica middleware/rate limit;
  - delega pair, sessao e comandos tracked para colaboradores internos.
- `ServerConfig` continua como contrato publico, mas a execucao do host passou a consumir `DeviceServerRuntimeConfig`:
  - clamps e limites antes espalhados ficaram centralizados;
  - parsing de CIDR passou a acontecer uma vez por start;
  - a politica de fallback para CIDRs invalidos foi preservada.
- O tempo sensivel do host agora usa `TimeProvider`:
  - `CreatePairingCode`;
  - consumo/expiracao de pairing;
  - grace period de detach;
  - snapshots online/offline;
  - espera tracked fora do caminho `TimeProvider.System`.
- A mutacao de `DeviceRecord` foi consolidada em `DeviceRecordMutations`, removendo reconstrucoes manuais repetidas do host.
- `Esp32S3LedOutput` deixou de decidir inline a deduplicacao de frame:
  - `LedFrameDeduplicator` passou a codificar RGB565 e comparar frame+brilho;
  - o output agora atua como adaptador fino para `BroadcastFrame`.

## Compatibilidade e contratos

- Mantidos sem mudanca:
  - `IDeviceServerHost`
  - endpoints `/api/v1/*`
  - endpoint `/ws/v1/stream`
  - DTOs de `Device.Protocol`
  - `ILedOutput`
  - `StreamFrameV2`
- Mudanca interna adicionada:
  - sobrecarga `DeviceServerHost(TimeProvider timeProvider)`, preservando o construtor default.

## Validacoes executadas

- `dotnet build MicaAudio.sln -c Debug --no-restore -m:1`
  - OK
  - baseline final: `0 warnings`
- `dotnet test MicaAudio.sln -c Debug --no-build -m:1`
  - OK
  - `208` aprovados
  - `1` ignorado

## Cobertura nova desta fase

- `DeviceServerRuntimeConfigTests`
  - clamp de limites;
  - parse de CIDR;
  - timeout offline normalizado.
- `DevicePairingStateTests`
  - expiracao de pairing code por tempo controlado;
  - janela de tentativas com reset apos avancar o tempo.
- `DeviceSessionTests`
  - `MarkAuthenticated`;
  - `MarkTelemetry`;
  - grace period de detach com snapshot deterministico.
- `PendingTrackedCommandTests`
  - add/get/remove/drain;
  - timeout;
  - cancelamento;
  - completacao.
- `DeviceServerHostTimeProviderTests`
  - expiracao de pairing via host real e HTTP real com `TimeProvider` injetado.
- `LedFrameDeduplicatorTests` e `LedOutputLifecycleTests`
  - codificacao RGB565;
  - deduplicacao por frame+brilho;
  - lifecycle de `NullLedOutput`, `SimulatorLedOutput` e `Esp32S3LedOutput`.

## Riscos e rollback

- Risco funcional principal:
  - qualquer bug residual agora tende a ficar confinado aos colaboradores internos (`DeviceSession`, `DevicePairingState`, `PendingTrackedCommand`).
- Como o wire permaneceu congelado, rollback e direto:
  - restaurar `DeviceServerHost.cs`/`DeviceServerHost.Advanced.cs` monoliticos;
  - remover os novos colaboradores internos;
  - restaurar a deduplicacao inline do `Esp32S3LedOutput`.

## Estado final da fase

- O rebuild da solucao continua em `0 warnings` no `.NET 10`.
- O core de `Device.Server` ficou menor, mais testavel e com responsabilidades separadas.
- A logica temporal sensivel passou a ser exercitavel sem `Task.Delay` real.
- O caminho ESP32 manteve o wire e ganhou deduplicacao extraida e coberta por testes.

## Proximos passos

- Proxima onda natural de lapidacao:
  - revisar `Audio.Loopback` e `Analyzer.Dsp` com o mesmo criterio core-first;
  - reduzir complexidade residual em services de app que ainda concentram orquestracao demais;
  - se necessario, abrir fase especifica para logging estruturado e ProblemDetails no host, sem tocar no wire do firmware.
