# Troubleshooting Matrix

| Sintoma | Diagnostico rapido | Causa comum | Acao recomendada |
|---|---|---|---|
| App abre sem visualizacao | Ver `MainPage` + estado de sessao | pipeline pausado apos navegacao | validar ativacao/pausa da sessao de visualizacao |
| Preview HUB75 128x64 nao aparece no Visualizador | conferir se `Modo HUB75` esta ativo e se `HubPreviewPanel` esta visivel | preview 128x64 segue o mesmo toggle do 64x32 e nao tem controle proprio | ativar `Modo HUB75`; validar `OnHubCanvas128Draw` e `InvalidateHubPreviews()` em `MainPage` |
| Startup falha com Unable to resolve service for type ... | conferir construtor da pagina/use case e registros no App.BuildServiceProvider() | dependencia nao registrada no DI | registrar servico faltante e manter construtor publico DI-friendly |
| Debug/F5 falha com DEP0840 no VS Community | verificar WindowsPackageType e EnableMsixTooling no Debug | deploy empacotado exigindo pacotes WinAppRuntime locais | usar perfil local unpackaged (Debug) e abrir MicaAudio.Dev.slnf; manter MSIX em Release/CI |
| Build falha com `CS1503` em settings/presets | verificar assinatura dos construtores e registro no container | migracao parcial para `IOptions<MicaAudioOptions>` | registrar `services.Configure<MicaAudioOptions>(...)` e remover construtor por `string appDataRoot` |
| git push falha com APPX3217 no pre-push local | verificar hook `.githooks/pre-push` e log do build local | maquina sem SDK/UAP para Integration.Smoke | usar gate local leve (`scripts/local-prepush-gate.ps1`) e manter build completo no CI; no VS usar `MicaAudio.Dev.slnf` |
| Comando de device retorna timeout | ver status por dispositivo selecionado em `Dispositivos` | device offline ou WS sem resposta | confirmar online, repetir comando e revisar conectividade LAN |
| Dois comandos em devices diferentes parecem conflitar | verificar se ambos comandos foram disparados para IDs distintos | concorrencia por device permite paralelo, mas bloqueia 2 comandos no mesmo device | disparar 1 comando por device e acompanhar `CommandByDevice` |
| Pair retorna `429` | verificar burst de requests por mesmo IP | rate limit de pareamento ativo | aguardar janela expirar ou reduzir retries |
| API retorna `403 network_not_allowed` | conferir IP do cliente e politica em `ServerConfig` | origem fora da rede permitida/CIDR | ajustar `AllowedCidrs` ou manter cliente na LAN privada |
| WS retorna `401` apos hardening de token | validar como o firmware envia token no handshake | query token legado desabilitado (`AllowLegacyWebSocketQueryToken=false`) | migrar para header `X-Device-Token` ou reabilitar legado temporariamente |
| Endpoint retorna `413 Payload Too Large` | verificar tamanho do JSON enviado | body acima de `MaxJsonBodyBytes` | reduzir payload ou ajustar limite de servidor de forma controlada |
| Download de firmware falha no wizard | ver logs na aba `Dispositivos` | BIN ausente no pacote para placa/perfil selecionado | validar assets em `AppData/Firmware` e repetir salvar |
| Botao de salvar abre e cancela | ver status `Download: cancelado` | usuario cancelou `FileSavePicker` | comportamento esperado |
| Texto ilegivel em tema | comparar tema sistema e brushes | style sem recurso semantico | revisar Fluent2 tokens e bindings |
| Preset `Blob Neon`, `Orbit Rings` ou `Polar Arcs` nao aparece | validar pasta `%AppData%/MicaAudio/presets` e schema dos defaults | catalogo local antigo sem merge de defaults | abrir app novamente para migracao automatica; se persistir, remover somente presets default antigos e reiniciar |
| Preset legado de Hyper Tunnel nao abre mais | comportamento esperado na politica 2D-only | renderer antigo foi aposentado e migrado para 2D | reabrir o app para migracao automatica; presets antigos passam a usar `AudioMotion Clone` |
| Grid de presets nao aparece | conferir `PresetGalleryPanel` e se a pagina concluiu `InitializeAsync()` | falha na carga de presets ou erro de XAML/visual tree | validar `PopulatePresetGallery()`, `PresetGalleryGridView` e se `PresetRepository.LoadOrSeedAsync()` retornou itens |
| Hover no card nao anima | conferir foco/hover e se outro card ficou ativo | timer do preview nao iniciou ou card anterior nao foi parado corretamente | validar `StartPresetPreview()`, `StopPresetPreview()` e `PresetPreviewThumbnailControl.StartPreview()` |
| Card anima, mas o preset principal nao muda | diferenciar hover de clique | hover so anima miniatura; selecao real depende de click/Enter/Space | clicar no card ou usar teclado; validar `OnPresetGalleryItemClick()` e `OnPresetGalleryKeyDown()` |
| Miniatura parece diferente da visualizacao principal | comparar configuracao atual da UI e largura real da miniatura | preview nao reconstruido com o analyzer real ou snapshot desatualizado | validar `VisualizerAnalyzerConfigFactory.Build()`, `PresetPreviewSignalFactory.CreatePcmFrame()` e `PresetPreviewThumbnailControl.ApplyPreviewSettings()` |
| Consumo alto de CPU ao passar o mouse | conferir se mais de um card esta animando ao mesmo tempo | preview simultaneo em multiplos cards ou timer nao foi parado | validar que apenas `activePreviewCard` fica ativo e que `StopPreview()` e chamado em `PointerExited/LostFocus` |
| Polar Arcs parece estatico | conferir `Quantidade de barras`, sensibilidade e o preset local | preset local defasado ou nivel de entrada muito baixo | reiniciar o app para reaplicar defaults; se persistir, remover apenas `spectrum-polar-arcs.json` e reabrir o app |
| FPS cai ao usar renderers Vizzy | comparar `blobPointCount/orbitPointCount` e `glowPasses` no preset ativo | complexidade alta de geometria + glow | reduzir `pointCount` e `glowPasses` para 1-2 |

## Token criptografado no devices.json

- A partir do hardening de seguranca, o token e salvo em `TokenProtected` com prefixo `dpapi:v1:`.
- Em leitura, formato legado em texto puro ainda e aceito para migracao.
- Se a descriptografia falhar no usuario atual, o token e tratado como invalido e o device deve re-parear.

## Preview HUB75 128x64 (simulado)

- O preview 128x64 no Visualizador e apenas simulacao local.
- A fonte de dados e o mesmo snapshot 64x32 do simulador (`SimulatorLedOutput.GetFrameSnapshot()`).
- O desenho usa mapeamento 2x nearest-neighbor (`x128/2`, `y128/2`) para manter fidelidade de pixel HUB75.

## Renderers 2D do Visualizador

- `Blob Neon`, `Orbit Rings` e `Polar Arcs` sao renderers Win2D 2D.
- `Polar Arcs` opera no modo apenas-barras e depende do `ReactiveBandSampler` para resposta ao audio.
- O controle nesta fase e por presets (`RendererParameters`), sem painel dedicado na UI.
- Mudancas extremas de parametros podem impactar frame time; use clamps recomendados.
- O modulo visual e oficialmente 2D-only; o caminho antigo de shader/GPU foi aposentado para manter consistencia com HUB75.
- A selecao de preset agora usa uma galeria visual; `PresetCombo` nao e mais o fluxo principal.
- Os cards usam `PcmFrame` sintetico, mas o preview passa pelo `SpectrumAnalyzer` real; hover/foco nao alteram a visualizacao principal.
- Se `AudioMotion Clone` parecer diferente no card, valide `DisplayViewportWidthPx` e a recriacao do analyzer ao mudar largura/configuracao.

## Referencias de codigo

- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1)
- [DevicesPage.ShowNewDeviceSetupDialogAsync](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L163)
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L8)
- [JsonDeviceRegistryStore](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [VizzyBlobNeonRenderer](../../../src/Visual.Win2D/Renderers/VizzyBlobNeonRenderer.cs#L1)
- [VizzyOrbitRingsRenderer](../../../src/Visual.Win2D/Renderers/VizzyOrbitRingsRenderer.cs#L1)
- [PolarArcsRenderer](../../../src/Visual.Win2D/Renderers/PolarArcsRenderer.cs#L1)
- [AudioMotionCloneRenderer](../../../src/Visual.Win2D/Renderers/AudioMotionCloneRenderer.cs#L1)
- [PresetPreviewSignalFactory](../../../src/App.WinUI/Services/Visualizer/PresetPreviewSignalFactory.cs#L1)
- [VisualizerAnalyzerConfigFactory](../../../src/App.WinUI/Services/Visualizer/VisualizerAnalyzerConfigFactory.cs#L1)
- [PresetPreviewThumbnailControl](../../../src/App.WinUI/Views/Controls/PresetPreviewThumbnailControl.cs#L1)
- [PresetGalleryCardControl](../../../src/App.WinUI/Views/Controls/PresetGalleryCardControl.cs#L1)

## Push local x CI (gate local leve)

- O hook local pre-push roda validacoes de docs/governanca, build do App.WinUI e Output.Tests.
- O build completo da solucao (`MicaAudio.sln`) continua obrigatorio no CI (`governance-ai-guardrails` e `governance-build-debug`).
- Resultado: push local destravado em maquinas sem SDK UAP, sem reduzir rigor para merge na main.

## Renderers reativos (bridge incremental)

- Se um renderer novo parecer pouco reativo, valide primeiro a saida do `ReactiveBandSampler` antes de inspecionar pixels.
- Se a lateral de configuracao parecer ignorada, confira `VisualizerEngine.GetCapabilities(...)` e `MainPage.ApplyRendererControlState()`.
- No contrato atual, `AudioMotion Clone` esconde `Quantidade de barras` por design, porque a geometria continua dependente da largura do layout.
- A sensibilidade (dB) nao e mais configuravel; a faixa fixa e `-85/-25` para todas as visualizacoes.
- Se a visualizacao parecer mais fraca ou mais forte, ajuste `Linear Boost`, `FFT Smoothing`, `Weighting Filter` ou `Faixa de frequencia`, nao dB.




