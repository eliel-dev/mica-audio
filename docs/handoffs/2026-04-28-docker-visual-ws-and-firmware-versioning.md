# Docker Visual WS + Firmware Timestamp Versioning

## Objetivo

Restaurar o caminho visual confiavel via WS no Docker local e voltar o firmware oficial para versao com timestamp UTC em repos sem tag.

## Escopo classificado

- Tipo: firmware/protocolo
- Criterio de aceite: Docker local sobe com UDP visual desabilitado por default, `-PreferVisualUdp` habilita o experimento UDP, e o build oficial do firmware gera `vyyyy.MM.dd-HHmmssZ-<tag-or-untagged>-<sha>` sem `v0.0.0`.

## Arquivos alterados

- `scripts/docker-server-redeploy.ps1`
- `scripts/build-precompiled-firmware.ps1`
- `tests/Output.Tests/MicaAudioServerStandaloneTests.cs`
- `docs/wiki/modules/server-build-and-artifacts.md`
- `docs/wiki/modules/firmware-esp32s3-devkitc1.md`
- `docs/wiki/guides/build-export-firmware.md`
- `docs/wiki/reference/code-index.md`
- `docs/wiki/reference/troubleshooting-matrix.md`
- `docs/handoffs/2026-04-28-docker-visual-ws-and-firmware-versioning.md`

## Decisoes tomadas

1. WS voltou a ser o default do helper Docker local porque o ESP registra e conecta por MQTT, mas o caminho UDP em Docker gerava `streamInvalidFrameCount` alto no HUB75 fisico.
2. UDP visual permanece disponivel por `-PreferVisualUdp`, publicando `5274/udp` somente quando o teste fisico pede esse caminho.
3. `Resolve-FirmwareVersion` voltou para timestamp UTC para evitar `v0.0.0` em repos sem tag e diferenciar geracoes do pacote oficial.
4. Sem tag Git, o token de versao passa a ser `untagged`; tags existentes continuam aparecendo normalizadas na string oficial.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docker-server-redeploy.ps1 -AdminToken dev-token
OK: container mica-audio-server recriado, health 200, Visual transport: WS.
OK: docker ps publicou 5272/tcp, 5273/tcp e 5275/udp; 5274/udp nao foi publicado no default.
OK: docker logs mostrou "UDP visual LAN desabilitado (PreferLanUdpVisualTransport=false)."

powershell -ExecutionPolicy Bypass -File .\scripts\docker-server-redeploy.ps1 -DryRun -PreferVisualUdp -PublicHost 192.168.1.50 -AdminToken test-token
OK: dry-run publicou 5274/udp somente com -PreferVisualUdp e anunciou Visual transport: UDP opt-in.

powershell -ExecutionPolicy Bypass -File .\scripts\build-precompiled-firmware.ps1
OK: firmwareVersion gerado como v2026.04.28-222740Z-untagged-aac170d.
OK: manifesto AppData/Firmware atualizado sem v0.0.0.

python -m platformio run -d firmware\esp32s3-devkitc1 -e esp32s3_devkitc1_dma_exp
OK: SUCCESS em 00:00:59.773.

dotnet test tests\Output.Tests\Output.Tests.csproj -c Debug --filter "FullyQualifiedName~DockerRedeployScript_ShouldDefaultToWsAndKeepVisualUdpOptIn|FullyQualifiedName~FirmwareBuildScript_ShouldUseTimestampVersionWhenRepoHasNoTags"
OK: 2 testes aprovados.

powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
OK: nenhuma falha encontrada.

powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
OK: governanca IA valida.

dotnet build MicaAudio.sln -c Debug
OK: compilacao com exito; 42 avisos NU1902 ja existentes de OpenTelemetry, 0 erros.
```

## Riscos e rollback

- Risco principal: duas geracoes do mesmo commit voltam a produzir versoes diferentes, entao a UI pode indicar update disponivel apos rebuild oficial sem mudanca funcional.
- Como reverter: voltar `Resolve-FirmwareVersion` para `git describe`, remover `-PreferVisualUdp` do helper e restaurar `MICA_SERVER__PREFERLANUDPVISUALTRANSPORT=true` no Docker local.

## Proximos passos

1. Validar HUB75 fisico com Docker local padrao via WS.
2. Revalidar UDP visual fisico separadamente usando `-PreferVisualUdp`.
