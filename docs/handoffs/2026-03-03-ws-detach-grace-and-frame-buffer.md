# Handoff - WS detach por identidade + grace e buffer de frame no firmware

## Objetivo

Reduzir flapping de online/offline em reconexao rapida de WebSocket, reforcando o detach por identidade de socket no servidor e adicionando grace curto de transicao, alem de aumentar margem de payload WS no firmware para frame 128x64.

## Escopo classificado

- Tipo: estrutural
- Abrangencia: `src/Device.Server`, `firmware/matrixportal-s3`, testes de host e docs operacionais.

## Arquivos alterados

- src/Device.Server/Hosting/DeviceServerHost.cs
- firmware/matrixportal-s3/platformio.ini
- tests/Output.Tests/DeviceServerHostSecurityTests.cs
- docs/wiki/modules/device-server-protocol.md
- docs/wiki/modules/firmware-matrixportal-s3.md
- docs/wiki/reference/troubleshooting-matrix.md
- docs/wiki/reference/code-index.md

## Decisoes tomadas

1. O detach de socket passou a validar identidade (`ReferenceEquals`) para evitar que conexoes antigas derrubem a sessao ativa de uma reconexao mais nova.
2. Foi aplicado grace period de 500ms apos detach valido antes de refletir offline no snapshot, para absorver reconexoes curtas sem flapping visual.
3. O firmware recebeu `WEBSOCKETS_MAX_DATA_SIZE=32768` no build para suportar payloads binarios do stream 128x64 com margem no cliente WS.
4. Cobertura de regressao adicionada com testes de host para grace period e para fechamento de socket antigo sem derrubar socket novo.

## Validacoes executadas

```text
dotnet test tests/Output.Tests/Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DeviceServerHostSecurityTests" -v q -> OK (18 aprovados)
python -m platformio run -e esp32s3_devkitc1_dma_exp (firmware/matrixportal-s3) -> FALHOU (PlatformIO indisponivel no ambiente)
powershell -ExecutionPolicy Bypass -File ./scripts/docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File ./scripts/ai-governance-check.ps1 -> OK
powershell -ExecutionPolicy Bypass -File ./scripts/mvvm-validate.ps1 -> OK
dotnet build MicaAudio.sln -c Debug -> OK (0 erros)
```

## Riscos e rollback

- Risco principal: o grace de 500ms pode atrasar em fração de segundo a sinalizacao de offline real em desconexao genuina.
- Rollback: remover grace e retornar detach imediato em `DeviceServerHost`, revertendo tambem o teste de regressao correspondente.

## Proximos passos

1. Revalidar firmware local com PlatformIO instalado na maquina de desenvolvimento para confirmar build do novo flag.
2. Monitorar logs de campo para confirmar reducao do padrao de flapping online/offline em ciclo curto.
