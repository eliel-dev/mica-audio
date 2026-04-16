# Guia - Setup New Device (AP-first estavel)

## Objetivo

Documentar o fluxo oficial e estavel de onboarding:

1. selecionar porta COM no wizard
2. gravar firmware com `erase-all`
3. exibir `pair code` no desktop
4. conectar o celular ao AP `MicaAudio-Setup-xxxx`
5. concluir provisioning no portal AP com `Servidor` + `pair code`

## Logs seriais no wizard

1. O fluxo continua sequencial:
   - `esptool` usa a porta COM durante o flash;
   - ao terminar, o wizard reabre a mesma trilha USB a `115200`;
   - o monitor serial passa a acompanhar o boot do ESP32-S3 sem disputar a COM com o flash.
2. O bloco `Ver mais` fica recolhido por padrao e mostra:
   - status atual da serial apos o flash;
   - terminal monoespacado com buffer circular;
   - acoes `Copiar logs`, `Recapturar boot` e `Limpar`.
3. O painel se expande automaticamente quando:
   - o flash falha;
   - a porta nao reaparece depois do flash;
   - o monitor reabre a COM, mas nao recebe linhas de boot dentro da janela diagnostica.
4. `Recapturar boot` nao reflasha o device:
   - ele apenas faz um reset controlado nas linhas da porta ja aberta;
   - isso permite repetir o boot com o monitor ja anexado.
5. Quando o wizard identifica um `hello` valido de `mica.serial.v1` com `deviceId` e esse device fica `Online` na UI, a sessao serial e encerrada automaticamente.

## Passos

1. Abrir `Dispositivos`.
2. Clicar em `Novo dispositivo` no rodape da lista.
3. Selecionar a porta COM do ESP32 (`Atualizar portas` quando necessario).
4. Conferir o `Servidor local para preencher no portal AP`.
5. Clicar em `Concluir` e aguardar o flash.
6. Ao final, copiar o `pair code` exibido pelo app.
7. No celular, conectar ao Wi-Fi `MicaAudio-Setup-xxxx`.
8. Abrir o portal do ESP32 e preencher:
   - `Servidor`
   - `Codigo pareamento`
   - `Nome dispositivo` opcional
9. Confirmar que o device aparece online no dashboard.

## Observacao operacional

1. Em flash limpo, ou sempre que faltarem `host/porta/deviceId/token`, o firmware abre o AP de setup imediatamente no `setup()`.
2. Depois que o display estiver inicializado, o HUB75 continua priorizando `SETUP WIFI` sempre que o portal estiver ativo.
3. O fallback por queda prolongada de Wi-Fi continua ativo para devices ja provisionados.
4. No boot limpo, o AP-first pode abrir o portal antes da inicializacao do HUB75 para priorizar RAM interna do Wi-Fi; por isso, o primeiro portal bloqueante pode aparecer sem `SETUP WIFI` na matriz.
5. `mica.serial.v1` continua no firmware e no desktop apenas como compatibilidade/diagnostico, fora do caminho oficial do wizard.
6. Leitura de `Preferences` ausente em flash limpo passou a usar defaults seguros; `NOT_FOUND` em massa nao deve mais ser tratado como falha critica.

## Campo Servidor no portal AP

1. O portal AP expõe um campo editavel `Servidor`.
2. Formatos aceitos:
   - `http://192.168.1.16:5272`
   - `192.168.1.16:5272`
   - `192.168.1.16`
3. Quando a porta nao for informada, o firmware assume `5272`.
4. Se ja existir host salvo no ESP, o campo abre preenchido com esse valor.
5. Se o valor digitado for invalido, o firmware nao apaga um host valido ja salvo; registra o erro em serial e reaproveita a configuracao anterior quando possivel.

## Observacao de protocolo

1. O `pair code` voltou a ser parte visivel do onboarding do desktop.
2. O backend continua usando `/api/v1/pair`.
3. A resposta de pareamento continua entregando `mqttHost`, `mqttPort` e `mqttRootTopic`.
4. O firmware persiste esses campos automaticamente e usa MQTT como control plane apos concluir o onboarding.
5. O WS permanece reservado ao stream visual binario.

## Contrato visual do wizard

1. Fonte canonica: `C:\Users\eliels\Pictures\nice\mica-dashboard.html`.
2. Especificacao aplicada na WinUI:
   - card `560px` (margem lateral `14px`);
   - head/body/footer com paddings `14/16`, `14/16`, `10/16`;
   - barra de etapas com altura `4px` e gap `8px`;
   - controles com altura `34px`;
   - botao `Concluir` no rodape a direita.
3. O wizard da WinUI e um overlay custom e nao depende de `ContentDialog`.

## Tela Dispositivos

### Pipeline executado pelo app

1. Resolve firmware oficial `esp32s3-devkitc1-128x64-dma_exp_merged.bin`.
2. Em workspace/dev, executa preflight de frescor do release oficial:
   - se o pacote oficial local estiver stale, roda `scripts/build-precompiled-firmware.ps1`;
   - o frescor considera toda a arvore `firmware/esp32s3-devkitc1/src`.
3. Valida manifesto sidecar `esp32s3-devkitc1-128x64-dma_exp_merged.manifest.json`.
4. So prossegue com flash quando o manifesto declarar `controlPlane = mqtt`.
5. Valida que o `Servidor local` do desktop nao caiu em loopback.
6. Flasha o ESP32-S3 via `esptool`, sempre com apagamento total da flash antes da gravacao.
7. Gera um `pair code` de curta duracao e o exibe ao usuario.
8. Orienta o usuario a concluir o provisioning manual no AP do ESP32.
9. O status `Online` continua dependendo do control plane MQTT estar conectado.
10. Depois do flash, o wizard passa a exibir logs seriais de boot sob demanda em `Ver mais`.

## Perfil oficial do comando de flash

Comando canonico usado no onboarding (bundle local ou fallback `python -m esptool`):

```powershell
python -m esptool --chip esp32s3 --port COMx --baud 115200 --before default_reset --after hard_reset write_flash --erase-all --no-compress 0x0 firmware.bin
```

Regras fechadas:

1. Usa `--before default_reset` e `--after hard_reset`.
2. Usa `write_flash --erase-all` para apagar toda a flash antes de gravar.
3. Usa `--no-compress` (nao usa `-z`).
4. O wipe total e obrigatorio no wizard USB, mesmo que o device ja tenha configuracao anterior.

Consequencia operacional:

1. Credenciais, host salvo, pareamento e qualquer configuracao local anterior do ESP32 sao apagados em todo flash iniciado pelo wizard.
2. O processo pode demorar mais do que um write sem erase, por desenho.

## Progresso de flash no wizard

1. Durante `Flashing`, o wizard exibe barra `0..100` + `%`.
2. O percentual exibido vem da saida do `esptool` (`NN%` ou `NN %`).
3. Antes da escrita, o wizard informa que a flash inteira esta sendo apagada.
4. Em sucesso, o app passa a mostrar o `pair code` e as instrucoes do AP.
5. Em falha, o ultimo percentual permanece visivel junto da mensagem de erro.
6. O mesmo card passa a monitorar a serial a `115200` assim que a COM e liberada pelo flash.

## Contrato serial `mica.serial.v1` (compatibilidade)

1. O protocolo serial nao faz mais parte do caminho oficial do wizard.
2. O cliente serial e mantido apenas para compatibilidade futura e diagnostico de bancada.
3. O AP `MicaAudio-Setup-xxxx` voltou a ser o baseline oficial de provisioning.

## Politica de seguranca para credenciais

1. O app nao coleta mais `SSID` e `senha Wi-Fi` no wizard oficial.
2. O `pair code` continua efemero.
3. Nao gravar senhas do portal AP em logs/handoffs.

## Fallback operacional

Se onboarding USB falhar:

1. Validar porta COM e cabo.
2. Confirmar que o `Servidor local` mostrado no wizard nao caiu em loopback.
3. Atualizar lista de portas.
4. Repetir o flash.
5. Se o firmware abrir o AP `MicaAudio-Setup-xxxx`, usar esse portal como caminho principal de provisioning.
6. Se o AP nao aparecer, abrir `Ver mais`, usar `Copiar logs` para guardar a sessao inteira e depois `Recapturar boot` para repetir o boot com o monitor serial ja conectado.

## Diagnostico de firmware legado

1. Se o card do device mostrar `Firmware legado`, o device ainda esta falando pelo caminho passivo de WS-texto/HTTP sem control plane MQTT.
2. Nessa situacao o stream visual pode continuar funcionando, mas comandos e o status `Online` nao serao ativados.
3. A correcao oficial e regravar o firmware precompilado atual pelo wizard USB.

## Checklist rapido

1. Botao `Novo dispositivo` visivel no rodape da lista.
2. Wizard abre com selecao de porta COM + `Servidor local` em modo leitura.
3. `SSID`, `Senha Wi-Fi` e `Nome do dispositivo` nao fazem parte do wizard oficial.
4. Ao fim do flash, o app mostra o `pair code` e orienta o uso do AP `MicaAudio-Setup-xxxx`.
5. O bloco `Ver mais` fica recolhido por padrao e expande sozinho em erro/no-boot.
6. `Copiar logs` exporta a sessao serial inteira sem depender de selecao manual linha a linha.
7. `Recapturar boot` repete o reset com a serial ja anexada a `115200`.
8. Em boot limpo, o firmware abre o AP imediatamente antes de inicializar o HUB75.
9. Portal AP continua mostrando o campo `Servidor`, aceitando URL completa ou `host[:porta]`.
10. Device conecta MQTT + WS automaticamente apos provisioning via AP.
11. Device novo nao deve aparecer como `Firmware legado`; se aparecer, refazer o flash com o pacote atualizado.

## Referencias de codigo

- [DevicesPage UI](../../../src/App.WinUI/Views/DevicesPage.Ui.cs#L1)
- [DevicesPage code-behind](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L1)
- [DeviceUsbOnboardingService](../../../src/App.WinUI/Services/Devices/Onboarding/DeviceUsbOnboardingService.cs#L1)
- [SerialPortCatalogService](../../../src/App.WinUI/Infrastructure/Serial/SerialPortCatalogService.cs#L1)
- [EspToolFlashService](../../../src/App.WinUI/Services/Devices/Onboarding/EspToolFlashService.cs#L1)
- [Firmware main.cpp](../../../firmware/esp32s3-devkitc1/src/main.cpp#L1)
