# Display-state e paineis GIF autonomos

## Objetivo

Continuar a correcao de paineis autonomos para que:

- o visualizador suspenda painel server-side ja ativo quando o device conecta depois da pagina de Paineis carregar;
- o ESP32-S3 deixe de manter o ultimo frame congelado quando o servidor informa `first_run` ou `no_mode_active`;
- paineis `gifhub75`, sozinhos ou compostos com `analogclock`, possam continuar renderizando no servidor apos o cliente WinUI fechar.

## Escopo classificado

- Tipo: firmware/protocolo.
- Motivo: altera firmware ESP32-S3, rotas HTTP de device/admin, stores server-side e composicao de paineis.
- Validacoes obrigatorias: `docs-validate`, `ai-governance-check`, `dotnet build MicaAudio.sln -c Debug`.

## Arquivos alterados

- `firmware/esp32s3-devkitc1/src/main.cpp`
- `firmware/esp32s3-devkitc1/src/mica_display.cpp`
- `firmware/esp32s3-devkitc1/src/mica_network.cpp`
- `firmware/esp32s3-devkitc1/src/mica_network.h`
- `firmware/esp32s3-devkitc1/src/mica_types.h`
- `firmware/esp32s3-devkitc1/src/mica_globals.cpp`
- `firmware/esp32s3-devkitc1/src/mica_globals.h`
- `src/App.WinUI/Views/PanelsPage.xaml.cs`
- `src/App.WinUI/Services/Panels/PanelsPlaybackService.cs`
- `src/Device.Server/Hosting/DeviceServerHost.cs`
- `src/Device.Server/Hosting/DeviceServerHost.PanelStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.MediaStore.cs`
- `src/Device.Server/Hosting/DeviceServerHost.Routes.cs`
- `src/Device.Server/Hosting/InMemoryServerPanelStore.cs`
- `src/Device.Server.Abstractions/Hosting/IServerPanelStore.cs`
- `src/Device.Server.Abstractions/Hosting/IServerMediaStore.cs`
- `src/Device.Protocol/Models/DeviceDisplayStateResponse.cs`
- `src/Device.Protocol/Models/ServerPanelSnapshot.cs`
- `src/Device.Client.Abstractions/IDeviceServerClient.cs`
- `src/Device.Client.Remote/RemoteDeviceServerClient.cs`
- `src/MicaAudio.Server/FileServerPanelStore.cs`
- `src/MicaAudio.Server/FileServerMediaStore.cs`
- `src/MicaAudio.Server/MicaAudioServerBootstrap.cs`
- `src/MicaAudio.Server/PanelCompositorHostedService.cs`
- `src/Panels.Composition/ServerSide/PanelServerCapability.cs`
- `src/Panels.Composition/ServerSide/ServerGifWidgetRuntime.cs`
- `src/Panels.Composition/ServerSide/ServerMediaDecoder.cs`
- `src/Panels.Composition/ServerSide/ServerSidePanelCompositor.cs`
- `tests/Output.Tests/DeviceServerHostAdminApiTests.cs`
- `tests/Output.Tests/FirmwareBootSourceLayoutTests.cs`
- `tests/Output.Tests/MicaAudioServerStandaloneTests.cs`
- `tests/Output.Tests/ServerSideGifPanelTests.cs`

## Decisoes tomadas

1. O firmware faz polling HTTP em `GET /api/v1/device/display-state` apenas depois de timeout de frames e com Wi-Fi/WS autenticados. Isso evita disputar com o hot path de frames enquanto o servidor ainda esta enviando composicao.
2. `panel_active` volta para `Hub75FallbackState::None`; o ESP aguarda novos frames. `first_run` e `no_mode_active` viram telas locais de fallback.
3. `DELETE /api/v1/admin/devices/{deviceId}/panel` cria tombstone mesmo quando nao havia painel salvo. Um clear explicito do cliente significa `no_mode_active`, nao `first_run`.
4. GIF server-side depende de upload previo de midia por device. O cliente troca `sourcePath` por `mediaId`/`mediaIds` antes de enviar o painel ao servidor.
5. O limite global do Kestrel passa a aceitar payloads de midia/batch; os handlers JSON continuam aplicando `MaxJsonBodyBytes` localmente.

## Validacoes executadas

Executadas durante a implementacao:

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter "FullyQualifiedName~FirmwareBootSourceLayoutTests|FullyQualifiedName~DeviceServerHostAdminApiTests.DeviceDisplayState" --no-restore
```

Resultado inicial: falhou nos testes de polling/render fallback e clear explicito, confirmando regressao.

```text
dotnet test .\tests\Output.Tests\Output.Tests.csproj --filter "FullyQualifiedName~ServerSideGifPanelTests|FullyQualifiedName~FirmwareBootSourceLayoutTests|FullyQualifiedName~DeviceServerHostAdminApiTests.DeviceDisplayState|FullyQualifiedName~DeviceServerHostAdminApiTests.AdminMediaUpload|FullyQualifiedName~MicaAudioServerStandaloneTests.FileServerPanelStore" --no-restore
```

Resultado: aprovado, 13 testes, 0 falhas. Warnings NU1902 de OpenTelemetry pre-existentes.

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1
```

Resultado: aprovado. `wiki_to_code_links_validated: 652`, `docs_backlinks_found: 103`.

```text
$env:XDG_CONFIG_HOME=(Get-Location).Path; $env:GIT_CONFIG_GLOBAL='NUL'; powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1
```

Resultado: aprovado. `changed_files: 60`, `structural_changed: 40`, `docs_evidence: 12`, `handoffs_changed: 1`.

```text
dotnet vstest .\tests\Output.Tests\bin\Debug\net10.0\Output.Tests.dll /TestCaseFilter:"FullyQualifiedName~ServerSideGifPanelTests|FullyQualifiedName~FirmwareBootSourceLayoutTests|FullyQualifiedName~DeviceServerHostAdminApiTests.DeviceDisplayState|FullyQualifiedName~DeviceServerHostAdminApiTests.AdminMediaUpload|FullyQualifiedName~MicaAudioServerStandaloneTests.FileServerPanelStore"
```

Resultado: aprovado, 13 testes, 0 falhas.

```text
dotnet build MicaAudio.sln -c Debug
```

Resultado: bloqueado no sandbox por `NU1301` ao tentar acessar `https://api.nuget.org/v3/index.json` (`Foi feita uma tentativa de acesso a um soquete de uma maneira que e proibida pelas permissoes de acesso`). Um build focado com `--no-restore` tambem acionou o mesmo acesso ao feed e falhou antes de compilar.

Build de firmware: nao executado porque `platformio`/`pio` nao estao disponiveis neste ambiente.

## Riscos e rollback

- Risco: polling HTTP bloqueia o loop principal por ate 2 segundos quando o servidor esta lento. Mitigacao: so roda apos timeout de frames, com intervalo de 5 segundos e timeout menor que os fluxos OTA/panels ja existentes.
- Risco: GIFs grandes ainda podem exceder `MaxMediaBodyBytes` de 8 MiB. Rollback operacional: reduzir o GIF ou ajustar o limite em uma mudanca separada.
- Risco: `mediaId` estavel por widget/index pode manter frames antigos se o arquivo local mudar sem atualizar o painel. O upload regrava o arquivo; o compositor reconstruiu quando o painel muda, e testes cobrem o caminho de composicao server-side.
- Rollback: reverter este lote remove o polling de display-state e volta a depender do ultimo frame recebido no ESP; tambem remove suporte a GIF server-side persistido.

## Proximos passos

1. Validar em hardware ESP32-S3 com painel real: limpar painel ativo, aguardar timeout e confirmar tela `NENHUM MODO`.
2. Validar fluxo real WinUI: painel GIF-only e painel GIF+relogio continuam apos fechar o cliente.
3. Considerar mover polling HTTP para worker leve se o loop health mostrar degradacao em rede instavel.
