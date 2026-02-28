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
| Hyper Tunnel nao aparece no combo de presets | comportamento esperado nesta fase de teste | presets builtin de Hyper Tunnel foram desativados para evitar travamento em VM sem GPU dedicada | manter o renderer apenas para fallback tecnico/testes manuais; use outros presets no fluxo normal |
| Hyper Tunnel shader nao compila localmente | rodar `scripts/validate-shader-toolchain.ps1` | toolchain de shader incompleta (ComputeSharp/TFM/UAP) | alinhar TFM `net8.0-windows10.0.22621.0`, validar pacote ComputeSharp e usar script de preflight para diagnostico |
| Polar Arcs parece estatico ou sem as cores do visualizador | conferir se o preset local foi migrado e se a paleta foi atualizada | `spectrum-polar-arcs.json` antigo ainda carregado com defaults defasados | reiniciar o app para migracao; se persistir, remover apenas `spectrum-polar-arcs.json` e reabrir o app |
| FPS cai ao usar renderers Vizzy | comparar `blobPointCount/orbitPointCount/tunnelSliceCount` e `glowPasses` no preset ativo | complexidade alta de geometria + glow | reduzir `pointCount/sliceCount` e `glowPasses` para 1-2; no Hyper Tunnel a auto-qualidade ajusta complexidade em runtime |

## Token criptografado no devices.json

- A partir do hardening de seguranca, o token e salvo em `TokenProtected` com prefixo `dpapi:v1:`.
- Em leitura, formato legado em texto puro ainda e aceito para migracao.
- Se a descriptografia falhar no usuario atual, o token e tratado como invalido e o device deve re-parear.

## Preview HUB75 128x64 (simulado)

- O preview 128x64 no Visualizador e apenas simulacao local.
- A fonte de dados e o mesmo snapshot 64x32 do simulador (`SimulatorLedOutput.GetFrameSnapshot()`).
- O desenho usa mapeamento 2x nearest-neighbor (`x128/2`, `y128/2`) para manter fidelidade de pixel HUB75.

## Renderers Vizzy (blob/orbit/tunnel)

- `Blob Neon` e `Orbit Rings` sao renderers Win2D inspirados visualmente no estilo Vizzy.
- `Polar Arcs` e um renderer 2D classico com composicao inspirada em vinil, mas usando a paleta do preset e 12 pares de arcos espelhados mapeados do espectro.
- O controle nesta fase e por presets (`RendererParameters`), sem painel dedicado na UI.
- Presets builtin de Hyper Tunnel estao temporariamente ocultos no catalogo em funcao de estabilidade em ambientes sem GPU dedicada.`r`n- Mudancas extremas de parametros podem impactar frame time; use clamps recomendados.

## Referencias de codigo

- [DeviceOperationsCoordinator](../../../src/App.WinUI/Services/Devices/DeviceOperationsCoordinator.cs#L1)
- [DevicesPage.ShowNewDeviceSetupDialogAsync](../../../src/App.WinUI/Views/DevicesPage.xaml.cs#L163)
- [PrecompiledFirmwareService](../../../src/App.WinUI/Services/Firmware/PrecompiledFirmwareService.cs#L8)
- [JsonDeviceRegistryStore](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L1)
- [DeviceServerHost](../../../src/Device.Server/Hosting/DeviceServerHost.cs#L1)
- [VizzyBlobNeonRenderer](../../../src/Visual.Win2D/Renderers/VizzyBlobNeonRenderer.cs#L1)
- [VizzyOrbitRingsRenderer](../../../src/Visual.Win2D/Renderers/VizzyOrbitRingsRenderer.cs#L1)
- [VizzyHyperTunnelRenderer](../../../src/Visual.Win2D/Renderers/VizzyHyperTunnelRenderer.cs#L1)
- [VizzyHyperTunnelShaderRenderer](../../../src/Visual.Win2D/Renderers/VizzyHyperTunnelShaderRenderer.cs#L1)
- [PolarArcsRenderer](../../../src/Visual.Win2D/Renderers/PolarArcsRenderer.cs#L1)
- [HyperTunnelShadertoyShader](../../../src/Visual.Win2D/Shaders/HyperTunnelShadertoyShader.cs#L1)
- [validate-shader-toolchain](../../../scripts/validate-shader-toolchain.ps1#L1)

## Push local x CI (gate local leve)

- O hook local pre-push roda validacoes de docs/governanca, build do App.WinUI e Output.Tests.
- O build completo da solucao (`MicaAudio.sln`) continua obrigatorio no CI (`governance-ai-guardrails` e `governance-build-debug`).
- Resultado: push local destravado em maquinas sem SDK UAP, sem reduzir rigor para merge na main.





## Renderers reativos (bridge incremental)

- Se um renderer novo parecer pouco reativo, valide primeiro a saida do `ReactiveBandSampler` antes de inspecionar pixels.
- Se a lateral de configuracao parecer ignorada, confira `VisualizerEngine.GetCapabilities(...)` e `MainPage.ApplyRendererControlState()`.
- No contrato atual, `AudioMotion Clone` esconde `Quantidade de barras` por design, porque a geometria continua dependente da largura do layout.
