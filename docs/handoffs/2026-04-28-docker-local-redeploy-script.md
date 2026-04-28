# Docker Local Redeploy Script

## Objetivo

Criar um caminho operacional de um comando para rebuild/redeploy local do `MicaAudio.Server` em Docker, com endpoints LAN corretos para ESP32.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: `scripts/docker-server-redeploy.ps1` rebuilda a imagem, para/remove somente o container alvo, sobe a nova versao com storage persistente, publica HTTP/MQTT/UDP e anuncia o IP LAN correto.

## Arquivos alterados

- `scripts/docker-server-redeploy.ps1`
- `docs/wiki/modules/server-build-and-artifacts.md`
- `docs/wiki/reference/code-index.md`
- `docs/handoffs/2026-04-28-docker-local-redeploy-script.md`

## Decisoes tomadas

1. Usar PowerShell em vez de Docker Compose para manter o fluxo alinhado aos scripts existentes e resolver automaticamente o IP LAN anunciado ao firmware.
2. Usar imagem `micaaudio-server:dev`, container `mica-audio-server` e volume nomeado `mica-audio-server-data` como defaults oficiais locais, preservando dados entre rebuilds.
3. Mascarar `MICA_SERVER__ADMINTOKEN` na exibicao do comando `docker run`, mantendo o valor real apenas no processo Docker.
4. Bloquear `localhost`/`127.x` como `PublicHost`, porque ESP fisico nao consegue usar loopback do PC.

## Validacoes executadas

```text
powershell -NoProfile -Command "`$null = [scriptblock]::Create((Get-Content .\scripts\docker-server-redeploy.ps1 -Raw))" -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\docker-server-redeploy.ps1 -DryRun -PublicHost 192.168.1.50 -AdminToken test-token -> aprovado; comandos exibidos sem executar e token mascarado
powershell -ExecutionPolicy Bypass -File .\scripts\docker-server-redeploy.ps1 -AdminToken dev-token -> aprovado; container subiu com health HTTP 200 e LAN base http://192.168.1.16:5272
docker ps --filter "name=^/mica-audio-server$" -> aprovado; container mica-audio-server Up com portas 5272/tcp, 5273/tcp, 5274-5275/udp
powershell -NoProfile -Command "(Invoke-WebRequest -Uri http://127.0.0.1:5272/api/v1/health -UseBasicParsing -TimeoutSec 5).StatusCode" -> 200
docker logs --tail 80 mica-audio-server -> aprovado; HTTP anunciado http://192.168.1.16:5272, MQTT mqtt://192.168.1.16:5273, discovery udp://0.0.0.0:5275, storage /data
docker inspect mica-audio-server --format "{{range .Mounts}}{{.Name}} -> {{.Destination}}{{end}}" -> mica-audio-server-data -> /data
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> aprovado
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> aprovado
dotnet build MicaAudio.sln -c Debug -> primeira tentativa falhou por lock transitorio do XamlCompiler; apos dotnet build-server shutdown, aprovado com 42 warnings NU1902 preexistentes de OpenTelemetry e 0 erros
```

## Riscos e rollback

- Risco principal: Docker Desktop/firewall/rede guest pode bloquear UDP discovery mesmo com `5275/udp` publicado.
- Como reverter: remover `scripts/docker-server-redeploy.ps1`, voltar a documentacao para o comando `docker run` manual e subir o servidor standalone diretamente com `docker run`.

## Proximos passos

1. Executar dry run e smoke real Docker local.
2. Validar se o ESP descobre o servidor automaticamente em LAN real; se UDP for bloqueado, usar `Servidor=http://<IP_DO_PC>:5272` como fallback tecnico.
