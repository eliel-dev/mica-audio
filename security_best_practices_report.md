# Varredura Geral de Seguranca

Data: 2026-03-23  
Modelo de ameaca base: `LAN confiavel`  
Escopo auditado: `src/Device.Server`, `src/Device.Protocol`, `src/App.WinUI`, `firmware/esp32s3-devkitc1/src/main.cpp`  
Exclusoes: `.pio/libdeps`, `.pio/build`, `bin/`, `obj/`, `BenchmarkDotNet.Artifacts` e demais artefatos gerados

## Resumo executivo

Foram priorizados 5 riscos no worktree atual. No baseline de `LAN confiavel`, nao identifiquei um achado `Critico`, mas ha dois itens `Alto` que merecem correcao primeiro:

1. O dashboard em `/ws/device/{deviceId}` aceita conexao sem autenticacao e a UX atual compartilha links contendo apenas `deviceId`.
2. O ESP32 abre um AP de provisioning sem senha e sem timeout, com reentrada automatica quando faltam credenciais.
3. A cadeia `pair -> HTTP API -> /ws/v1/stream -> MQTT -> OTA` opera em `http://`, `ws://` e MQTT sem protecao criptografica fim a fim; o SHA-256 atual valida integridade, mas nao ancora autenticidade fora do mesmo canal.
4. O token do dispositivo permanece em NVS em texto claro no ESP32, enquanto o lado Windows ja usa DPAPI para proteger segredo em repouso.
5. `/api/v1/server/info` permanece anonimo e expoe topologia operacional suficiente para facilitar enumeracao local.

Sob `LAN hostil`, os itens 1, 2 e 3 sobem de severidade de forma material.

## Revalidacao do baseline nao mutavel

Os checks pedidos para a etapa de auditoria passaram sem achados bloqueantes:

| Validacao | Resultado |
| --- | --- |
| `powershell -ExecutionPolicy Bypass -File .\scripts\dependency-vulnerability-gate.ps1 -ProjectOrSolution MicaAudio.sln` | `OK`, sem vulnerabilidades conhecidas reportadas |
| `dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter "FullyQualifiedName~DeviceServerHostSecurityTests" --no-restore` | `OK`, 24 testes aprovados |
| `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` | `OK` |
| `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` | `OK` |

## Cobertura de superficies

- `/api/v1/pair`
  - Protecao atual: codigo de pareamento de uso unico, limitacao de taxa por IP e limite de payload.
  - Evidencia: `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:16-17`, `src/Device.Server/Hosting/DeviceServerHost.cs:472-543`, `firmware/esp32s3-devkitc1/src/main.cpp:2613-2653`.
  - Observacao: a resposta entrega `deviceId`, `token`, `httpBase`, `mqttHost`, `mqttPort`, `mqttRootTopic` e `wsPath` via `http://`.

- `/api/v1/server/info`
  - Protecao atual: nenhuma autenticacao.
  - Evidencia: `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:20`, `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:46-58`.
  - Observacao: expoe `HttpBase`, `MqttHost`, `MqttPort`, `MqttRootTopic`, `WsPath`, `MdnsService` e `MaxDevices`.

- `/api/v1/device/config`
  - Protecao atual: autenticacao por `X-Device-Id` + `X-Device-Token` ou `Authorization: Bearer`.
  - Evidencia: `src/Device.Server/Hosting/DeviceServerHost.cs:546-561`, `src/Device.Server/Hosting/DeviceServerHost.cs:707-752`.

- `/api/v1/device/firmware/latest`
  - Protecao atual: mesma autenticacao do HTTP API.
  - Evidencia: `src/Device.Server/Hosting/DeviceServerHost.Firmware.cs:11-25`, `src/Device.Server/Hosting/DeviceServerHost.cs:707-752`.
  - Observacao: o manifesto traz `sha256` e `downloadPath`, mas nao assinatura criptografica do release.

- `/api/v1/device/firmware/download`
  - Protecao atual: mesma autenticacao do HTTP API.
  - Evidencia: `src/Device.Server/Hosting/DeviceServerHost.Firmware.cs:28-56`, `src/Device.Server/Hosting/DeviceServerHost.Firmware.cs:90-103`.

- `/api/v1/device/command-ack`
  - Protecao atual: autenticacao, limitacao de taxa e limite de payload.
  - Evidencia: `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:26-27`, `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs:232-293`, `src/Device.Server/Hosting/DeviceServerHost.cs:707-752`.

- `/ws/v1/stream`
  - Protecao atual: autenticacao por headers no handshake e limitacao de taxa.
  - Evidencia: `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:29-31`, `src/Device.Server/Hosting/DeviceServerHost.cs:569-583`, `src/Device.Server/Hosting/DeviceServerHost.cs:707-752`, `firmware/esp32s3-devkitc1/src/main.cpp:3218-3223`.
  - Observacao: o transporte segue em `ws://`.

- `/ws/device/{deviceId}`
  - Protecao atual: apenas `deviceId` na rota e limitacao de taxa.
  - Evidencia: `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:33-34`, `src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs:32-57`, `src/Device.Server/wwwroot/dashboard/dashboard.js:50-53`, `src/Device.Server/wwwroot/dashboard/dashboard.js:543-556`, `src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs:117-133`.

- MQTT `mica/v1/devices/{deviceId}/...`
  - Protecao atual: autenticacao por `deviceId` e `token` no broker, sem evidencia de transporte criptografado.
  - Evidencia: `firmware/esp32s3-devkitc1/src/main.cpp:1923-1925`, `firmware/esp32s3-devkitc1/src/main.cpp:1955`, `firmware/esp32s3-devkitc1/src/main.cpp:1989`, `firmware/esp32s3-devkitc1/src/main.cpp:2010`, `firmware/esp32s3-devkitc1/src/main.cpp:2053`, `firmware/esp32s3-devkitc1/src/main.cpp:3074-3076`, `firmware/esp32s3-devkitc1/src/main.cpp:3248-3257`.
  - Observacao: o firmware publica `stats`, `logs`, `presence`, `command-events` e consome `commands`.

- Provisioning AP, NVS e OTA
  - Evidencia principal: `firmware/esp32s3-devkitc1/src/main.cpp:2796-2818`, `firmware/esp32s3-devkitc1/src/main.cpp:3719-3726`, `firmware/esp32s3-devkitc1/src/main.cpp:2652-2653`, `firmware/esp32s3-devkitc1/src/main.cpp:3711-3712`, `firmware/esp32s3-devkitc1/src/main.cpp:1623-1652`, `firmware/esp32s3-devkitc1/src/main.cpp:1750-1884`.

- WinUI / WebView2
  - Revisao concluida sem achado prioritario no baseline atual.
  - Evidencia de reducao de risco atual: o dashboard embutido e fixado em `127.0.0.1` antes da navegacao, reduzindo a superficie do bridge para o servidor local (`src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs:107-114`), e o dashboard standalone entra em modo somente leitura quando `HOST_BRIDGE_AVAILABLE` nao existe (`src/Device.Server/wwwroot/dashboard/dashboard.js:62-68`).
  - Backlog de hardening: validar `Source`/origem da mensagem antes de executar acoes no `WebMessageReceived` (`src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs:145-191`).

## Critico

Nenhum finding `Critico` no baseline de `LAN confiavel`.

## Alto

### SBP-001 - Dashboard WebSocket compartilhavel apenas por `deviceId`

- Classificacao: `bug de implementacao`
- Impacto:
  - Qualquer cliente com acesso HTTP ao servidor e conhecimento do `deviceId` consegue assinar o dashboard e receber telemetria, estado de firmware, RSSI, memoria, FPS e metadados operacionais do dispositivo.
  - O fluxo atual de compartilhamento gera exatamente esse link, sem segredo adicional.
- Evidencia:
  - A rota publica existe sem wrapper de autenticacao: `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:33-34`.
  - O handler aceita o WebSocket depois de validar apenas `deviceId` e existencia do snapshot: `src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs:41-57`.
  - O payload inclui dados operacionais detalhados: `src/Device.Server/Hosting/DeviceServerHost.Dashboard.cs:336-410`.
  - O cliente web monta `ws(s)://.../ws/device/{deviceId}` sem token: `src/Device.Server/wwwroot/dashboard/dashboard.js:50-53`, `src/Device.Server/wwwroot/dashboard/dashboard.js:543-556`.
  - O link de compartilhamento no WinUI carrega apenas `deviceId`: `src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs:117-133`.
  - Os testes validam a conexao anonima so com `deviceId`: `tests/Output.Tests/DeviceServerHostDashboardTests.cs:155-159`, `tests/Output.Tests/DeviceServerHostDashboardTests.cs:270-275`, `tests/Output.Tests/DeviceServerHostDashboardTests.cs:358-362`.
- Avaliacao no baseline:
  - Em `LAN confiavel`, o risco principal e exposicao indevida de observabilidade e enumeracao de dispositivos para qualquer usuario local que receba ou descubra o `deviceId`.
- Agravante em `LAN hostil`:
  - Sobe porque scripts em origens nao confiaveis ainda podem tentar abrir o WebSocket; nao encontrei verificacao explicita de origem antes de `AcceptWebSocketAsync`.
- Recomendacao:
  - Exigir autenticacao/autorizacao no dashboard.
  - Se o produto precisa de compartilhamento facil, trocar `deviceId` puro por URL assinada de vida curta, token de dashboard separado ou sessao autenticada.
  - Adicionar validacao explicita de origem para o canal WebSocket de observabilidade.
- Risco residual:
  - Mesmo com autenticacao, o dashboard continuara contendo dados sensiveis de operacao; manter TTL curto para links, revogacao e escopo de leitura.

### SBP-002 - Provisioning AP aberto, sem senha e sem timeout

- Classificacao: `lacuna de hardening`
- Impacto:
  - Um atacante em alcance de radio pode ingressar no AP de provisioning, alterar configuracao de rede/servidor e induzir novo pareamento.
  - Como o portal nao expira, a janela de exposicao permanece aberta indefinidamente ate acao manual ou sucesso do fluxo.
- Evidencia:
  - O portal entra em modo bloqueante e sem timeout: `firmware/esp32s3-devkitc1/src/main.cpp:2796-2799`.
  - O SSID e previsivel a partir do MAC e `autoConnect` e chamado sem senha: `firmware/esp32s3-devkitc1/src/main.cpp:2812-2814`.
  - O portal abre automaticamente no boot se faltar host, porta, `deviceId` ou `token`: `firmware/esp32s3-devkitc1/src/main.cpp:3719-3726`.
  - O firmware ainda permite reentrada remota em provisioning por comando: `firmware/esp32s3-devkitc1/src/main.cpp:2846-2854`, `firmware/esp32s3-devkitc1/src/main.cpp:2866-2869`.
- Avaliacao no baseline:
  - Este risco independe em parte da confianca da LAN, porque a superficie mais exposta e o radio local do AP temporario.
- Agravante em `LAN hostil`:
  - Um atacante que primeiro capture ou reuse o token do dispositivo consegue forcar reentrada em provisioning via plano de controle e depois reconfigurar o equipamento.
- Recomendacao:
  - Fechar o AP com senha unica por dispositivo, prova de presenca fisica e timeout finito.
  - Preferir o stack oficial de provisioning do ESP-IDF com mecanismo de seguranca documentado pela Espressif, em vez de um captive portal aberto.
  - Separar provisioning inicial de re-provisioning remoto; o segundo deve exigir confirmacao local.
- Risco residual:
  - Provisioning sempre amplia a superficie temporariamente; o objetivo e torna-lo curto, autenticado e fisicamente controlado.

## Medio

### SBP-003 - Cadeia `pair/HTTP/WS/MQTT/OTA` em claro e OTA sem ancora criptografica independente

- Classificacao: `risco arquitetural`
- Impacto:
  - `deviceId`, `token`, telemetria e comandos trafegam em `http://`, `ws://` e MQTT sem protecao de canal.
  - O mecanismo de OTA atual verifica `sha256`, mas esse hash chega pelo mesmo canal HTTP nao protegido que entrega `downloadPath`; isso protege integridade acidental, nao autenticidade forte contra MITM do canal.
- Evidencia:
  - O servidor sobe apenas em `http://`: `src/Device.Server/Hosting/DeviceServerHost.cs:102-106`.
  - O servidor anuncia `HttpBase` em claro em `/server/info` e no pareamento: `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:46-58`, `src/Device.Server/Hosting/DeviceServerHost.cs:533-541`.
  - O cliente WinUI tambem trata o servidor publico como `http://`: `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs:58`, `src/App.WinUI/Services/Devices/DeviceIntegrationService.cs:92-95`.
  - O pareamento do firmware e feito por `http://.../api/v1/pair`: `firmware/esp32s3-devkitc1/src/main.cpp:2613-2633`.
  - O HTTP autenticado do dispositivo reaproveita `X-Device-Id` e `X-Device-Token` sobre `http://`: `firmware/esp32s3-devkitc1/src/main.cpp:1601-1619`.
  - O fluxo `/ws/v1/stream` usa `ws://` com headers de autenticacao, sem TLS: `firmware/esp32s3-devkitc1/src/main.cpp:3218-3223`.
  - O MQTT usa `WiFiClient` simples e se conecta como `mqtt://`: `firmware/esp32s3-devkitc1/src/main.cpp:216-217`, `firmware/esp32s3-devkitc1/src/main.cpp:3248-3257`.
  - O manifesto OTA traz `sha256` e `downloadPath`: `firmware/esp32s3-devkitc1/src/main.cpp:1623-1652`, `src/Device.Server/Hosting/DeviceServerHost.Firmware.cs:90-103`.
  - O binario OTA e baixado do mesmo servidor HTTP autenticado e comparado apenas contra o hash desse manifesto: `firmware/esp32s3-devkitc1/src/main.cpp:1750-1884`.
- Avaliacao no baseline:
  - Em `LAN confiavel`, a arquitetura pode ser operacionalmente aceitavel em laboratorio, mas continua fragil contra redes Wi-Fi mal segmentadas, switches espelhados, captive portals indevidos e qualquer host local comprometido.
- Agravante em `LAN hostil`:
  - Este finding sobe para `Alto` ou `Critico`, porque captura e replay de token passam a ser plausiveis e o OTA fica dependente de um canal sem autenticidade de transporte.
- Recomendacao:
  - Migrar HTTP e WebSocket para TLS e MQTT para transporte equivalente seguro.
  - Introduzir autenticidade de release independente do canal, por exemplo assinatura do manifesto/pacote validada por chave publica embutida no firmware.
  - Reduzir a reutilizacao do mesmo token em todos os canais ou segmentar credenciais por finalidade.
- Risco residual:
  - Mesmo com TLS, segredos reutilizados continuam ampliando blast radius; a remediacao ideal inclui separacao de credenciais e assinatura de release.

### SBP-004 - `deviceId` e `token` persistidos em NVS sem protecao em repouso no ESP32

- Classificacao: `lacuna de hardening`
- Impacto:
  - Quem obtiver acesso fisico, serial, dump de flash ou execucao arbitraria no dispositivo consegue extrair o token e clonar a identidade do equipamento.
  - O impacto cresce porque o mesmo token autentica HTTP, WebSocket e MQTT.
- Evidencia:
  - O firmware salva `deviceId` e `token` em NVS imediatamente apos o pareamento: `firmware/esp32s3-devkitc1/src/main.cpp:2643-2653`.
  - O firmware le ambos em claro na inicializacao: `firmware/esp32s3-devkitc1/src/main.cpp:3711-3712`, `firmware/esp32s3-devkitc1/src/main.cpp:3734-3735`.
  - O WinUI ja protege o token do lado desktop com DPAPI: `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs:216`, `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs:241`.
- Avaliacao no baseline:
  - O vetor exige acesso ao dispositivo ou ao armazenamento, mas o resultado e takeover completo do endpoint.
- Agravante em `LAN hostil`:
  - Com transporte em claro, o adversario nao precisa nem chegar ao storage; pode simplesmente capturar o token em transito.
- Recomendacao:
  - Planejar `Secure Boot` e `Flash Encryption` para a plataforma ESP32-S3, avaliando impacto operacional de fabrica, update e recovery.
  - Enquanto isso nao existir, reduzir superficie de debug e considerar tokens rotativos e revogacao simples.
- Risco residual:
  - Protecao em repouso em microcontrolador e sempre dependente do modo de producao; sem secure boot e flash encryption, o risco permanece relevante.

## Baixo

### SBP-005 - `/api/v1/server/info` expoe topologia sem autenticacao

- Classificacao: `lacuna de hardening`
- Impacto:
  - Facilita descoberta de portas, topicos MQTT, caminho WebSocket e capacidade maxima do host.
  - Isoladamente nao compromete autenticacao, mas reduz custo de reconhecimento local.
- Evidencia:
  - A rota e publicada sem autenticacao: `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:19-20`.
  - O retorno inclui `HttpBase`, `MqttHost`, `MqttPort`, `MqttRootTopic`, `WsPath`, `MdnsService` e `MaxDevices`: `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:46-58`.
- Avaliacao no baseline:
  - Em `LAN confiavel`, e principalmente uma perda de minimizacao de superficie, nao um bypass direto.
- Recomendacao:
  - Restringir a endpoint a onboarding controlado, loopback, autenticacao ou resposta minimizada.
  - Se a descoberta automatica for indispensavel, reduzir o payload ao minimo necessario.
- Risco residual:
  - Mesmo autenticada, a informacao continua valiosa para diagnostico; o objetivo e limitar quem pode obte-la.

## Boas praticas ja atendidas

- Restricao de rede privada por padrao no servidor: `src/Device.Protocol/Contracts/ServerConfig.cs:20-23`, `src/Device.Server/Hosting/DeviceServerHost.cs:156-166`.
- Rate limiting em pareamento, `command-ack` e handshakes WebSocket: `src/Device.Server/Hosting/DeviceServerHost.cs:107-143`, `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:16-17`, `src/Device.Server/Hosting/DeviceServerHost.Routes.cs:26-34`.
- Limites de payload HTTP e WebSocket definidos por configuracao: `src/Device.Protocol/Contracts/ServerConfig.cs:40-43`, `src/Device.Server/Hosting/DeviceServerHost.Advanced.cs:239-241`, `src/Device.Server/Hosting/DeviceServerHost.cs:772-779`.
- Comparacao de token em tempo constante no servidor: `src/Device.Server/Hosting/DeviceServerHost.cs:782-791`.
- Query-string token legado de WebSocket permanece desabilitado por default: `src/Device.Protocol/Contracts/ServerConfig.cs:37-38`.
- Headers basicos de hardening no host HTTP: `src/Device.Server/Hosting/DeviceServerHost.cs:147-153`.
- Registro local no Windows com DPAPI para segredo em repouso: `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs:216`, `src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs:241`.
- Dashboard standalone entra em modo somente leitura quando nao ha bridge do host: `src/Device.Server/wwwroot/dashboard/dashboard.js:62-68`.
- Dashboard embutido no WinUI fixa navegacao em `127.0.0.1`, reduzindo exposicao do bridge a conteudo remoto direto: `src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs:107-114`.

## Backlog recomendado

1. Proteger `/ws/device/{deviceId}` com autenticacao real e remover URLs compartilhaveis baseadas apenas em `deviceId`.
2. Endurecer o provisioning do ESP32: senha unica, timeout, prova de presenca e, idealmente, migracao para o provisioning seguro documentado pela Espressif.
3. Evoluir o plano de controle para transporte criptografado e acrescentar assinatura de release/manifests para OTA.
4. Planejar `Secure Boot` + `Flash Encryption` no ESP32-S3 e reduzir dependencia de token unico de longa duracao.
5. Reduzir ou autenticar `/api/v1/server/info`.
6. Hardening adicional no WinUI: validar a origem em `WebMessageReceived` antes de executar comandos do dashboard, mesmo com a navegacao atual presa a loopback.

## Referencias externas

- Microsoft Learn, autenticacao em ASP.NET Core:
  - https://learn.microsoft.com/en-us/aspnet/core/security/authentication/?view=aspnetcore-10.0
- Microsoft Learn, seguranca para SignalR/WebSockets:
  - https://learn.microsoft.com/en-us/aspnet/core/signalr/security?view=aspnetcore-9.0
- Espressif, ESP-IDF v5.5.3 para ESP32-S3:
  - https://docs.espressif.com/projects/esp-idf/en/v5.5.3/esp32s3/index.html
- Espressif, seguranca no ESP32-S3:
  - https://docs.espressif.com/projects/esp-idf/en/v5.5.3/esp32s3/security/security.html
- Espressif, provisioning Wi-Fi:
  - https://docs.espressif.com/projects/esp-idf/en/v5.5.3/esp32s3/api-reference/provisioning/wifi_provisioning.html
- Espressif, provisioning unificado:
  - https://docs.espressif.com/projects/esp-idf/en/v5.5.3/esp32s3/api-reference/provisioning/provisioning.html

## Conclusao

No worktree auditado, a base esta razoavelmente endurecida para APIs autenticadas do dispositivo, mas o desenho ainda depende demais da confianca do ambiente local. Se houver uma segunda passada de remediacao, a ordem correta e: provisioning/AP, dashboard WS, cadeia OTA/plano de controle, e por fim protecao de segredo em repouso no ESP32.
