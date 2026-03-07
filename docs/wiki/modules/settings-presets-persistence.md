# Modulo Settings, Presets e Persistencia

## Objetivo

Centralizar configuracoes de sessao, presets e armazenamento em `%AppData%`.

## Responsabilidades

- Modelo de configuracao global (`AppSettings`).
- Migracao/coercao de valores antigos.
- Load/save de `settings.json`.
- Load/seed/save de presets em JSON.
- Derivacao do runtime visualizer a partir de settings persistidos.

## Fluxo

1. `MainPage.InitializeAsync` carrega settings e presets.
2. `AppSettingsDomainService.Migrate` normaliza valores e deriva o runtime visualizer/lifecycle.
3. `MainPage.Pipeline` aplica e persiste `VisualizerRuntimeSettings` sem duplicar clamp/default no code-behind.
4. `OnUnloaded` salva snapshot atual no disco.

## Pontos de alteracao frequente

- Novo campo de settings: atualizar `AppSettings`, `AppSettingsDomainService`, `SettingsRepository`.
- Novo preset builtin: atualizar `DefaultPresets` e seed em repositiorio.
- Novo default/coercao de visualizer: atualizar `VisualizerRuntimeSettings`, `AnalyzerRuntimeProfile` e `MainPage.Pipeline`.
- Novo payload de runtime HUB75: usar `LedPayloadFactory` em vez de montar `LedPayload` inline no app.

## Riscos

- Esquecer migracao pode quebrar usuario com settings antigos.
- Esquecer persistencia em `Copy/Build` causa perda de estado entre sessoes.

## Checklist

- Alterar opcao na UI, fechar app, abrir novamente.
- Confirmar que valor permaneceu igual.
- Testar arquivo `settings.json` limpo e legado.

## Tokens de dispositivo em repouso

- `devices.json` passa a persistir `TokenProtected` com DPAPI (`dpapi:v1:`).
- Leitura segue backward-compatible com formato legado em texto puro.
- Rotacao/repareamento deve sobrescrever token legado com formato protegido no proximo save.

## Referencias de codigo

- [AppSettings](../../../src/MicaAudio.Core/Presets/AppSettings.cs#L5) - assinatura: `public sealed class AppSettings`
- [AppSettingsDomainService](../../../src/App.WinUI/Services/AppSettingsDomainService.cs#L7) - assinatura: `internal sealed class AppSettingsDomainService`
- [VisualizerRuntimeSettings](../../../src/MicaAudio.Core/Config/VisualizerRuntimeSettings.cs#L1) - assinatura: `internal sealed class VisualizerRuntimeSettings`
- [AnalyzerRuntimeProfile](../../../src/MicaAudio.Core/Config/AnalyzerRuntimeProfile.cs#L1) - assinatura: `internal sealed class AnalyzerRuntimeProfile`
- [DeviceLifecycleSettings](../../../src/MicaAudio.Core/Config/DeviceLifecycleSettings.cs#L1) - assinatura: `internal readonly record struct DeviceLifecycleSettings`
- [LedPayloadFactory](../../../src/MicaAudio.Core/Led/LedPayloadFactory.cs#L1) - assinatura: `internal static class LedPayloadFactory`
- [SettingsRepository](../../../src/App.WinUI/Services/SettingsRepository.cs#L6) - assinatura: `internal sealed class SettingsRepository`
- [PresetRepository](../../../src/App.WinUI/Services/PresetRepository.cs#L6) - assinatura: `internal sealed class PresetRepository`
- [MainPage Pipeline helpers](../../../src/App.WinUI/Views/MainPage.Pipeline.cs#L1) - assinatura: `public partial class MainPage`
- [JsonDeviceRegistryStore](../../../src/App.WinUI/Services/Devices/JsonDeviceRegistryStore.cs#L1) - assinatura: `internal sealed class JsonDeviceRegistryStore`

## Backlinks no codigo

Os arquivos acima devem continuar apontando para esta pagina quando houver mudancas de schema.

## Thresholds de Presence de Device

`AppSettings` agora persiste thresholds leves de lifecycle para devices:

- `DeviceFreshThresholdSeconds`
- `DeviceStaleThresholdMinutes`
- `DeviceDormantThresholdHours`

Eles ainda nao tem UI dedicada, mas ja podem ser ajustados em config e passam por migracao/coercao segura em `AppSettingsDomainService`.
A regra de normalizacao garante sempre: `Fresh < Stale < Dormant`.

## Atualizacao 2026-03 - Runtime settings centralizados

- As invariantes de visualizer sairam do code-behind e passaram a ter um caminho unico no core:
  - `VisualizerRuntimeSettings` normaliza FFT, smoothing, weighting, faixa de frequencia e `linearBoost`;
  - `AnalyzerRuntimeProfile` deriva `AnalyzerConfig` de `AppSettings + PresetDefinition + viewport`;
  - `DeviceLifecycleSettings` concentra a regra `Fresh < Stale < Dormant`.
- `AppSettingsDomainService` continua sendo a fronteira de migracao/copia do app, mas deixou de ser a origem unica dessas regras.

## Atualizacao 2026-03 - Fase 6 runtime profile e persistencia

- A fase 6 consolidou o caminho de runtime em pontos de verdade reutilizaveis:
  - `VisualizerRuntimeSettings` para defaults/clamp do visualizer;
  - `AnalyzerRuntimeProfile` para compor `AnalyzerConfig` no core;
  - `DeviceLifecycleSettings` para thresholds de presence;
  - `LedPayloadFactory` para gerar `LedPayload` sem duplicacao.
- `MainPage.Pipeline` virou a borda de integracao da tela para esse runtime:
  - `BuildVisualizerRuntimeSettings()` gera o snapshot imutavel da sessao;
  - `ApplyVisualizerRuntimeSettings()` reaplica estado normalizado na UI/viewmodel;
  - `PersistCurrentVisualizerSettings()` persiste o runtime visualizer sem remontar `AppSettings` campo a campo.
- `AppSettingsDomainService` agora expõe `GetVisualizerRuntimeSettings()` e `SetVisualizerRuntimeSettings()` para manter migracao e persistencia no mesmo contrato central.

## Atualizacao 2026-03 - Draft aplicado no Visualizador

- A `MainPage` passou a manter dois estados para o runtime do visualizer:
  - `draft/pending`, alterado imediatamente pela UI;
  - `applied`, usado pelo analyzer e pelo render ate o proximo apply valido.
- O runtime pendente so entra em `AppSettings` quando ha apply real:
  - ajustes finos usam debounce unico de `150 ms`;
  - mudancas sem delta efetivo nao disparam rebuild nem persistencia;
  - preset/renderer continuam imediatos, mas no mesmo caminho consolidado de apply.

## Toggle operacional de auth WS legado

`AppSettings` persiste a flag `AllowLegacyWebSocketQueryToken` para controlar rollback emergencial de compatibilidade WS:

- default: `false` (seguro, sem query token no handshake WS);
- override emergencial: `true` em `%AppData%\\MicaAudio\\settings.json`;
- sem UI dedicada nesta fase; controle intencionalmente operacional.

## Migracao de Registro de Devices

O registro persistido de devices agora usa dupla protecao para evitar falso `Nunca conectado`:

- script explicito: `scripts/migrate-device-registry-presence-v1.ps1`
- fallback automatico em runtime: `DeviceRegistryPresenceNormalizer` + `JsonDeviceRegistryStore`

