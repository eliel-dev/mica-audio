# Modulo Settings, Presets e Persistencia

## Objetivo

Centralizar configuracoes de sessao, presets e armazenamento em `%AppData%`.

## Responsabilidades

- Modelo de configuracao global (`AppSettings`).
- Migracao/coercao de valores antigos.
- Load/save de `settings.json`.
- Load/seed/save de presets em JSON.

## Fluxo

1. `MainPage.InitializeAsync` carrega settings e presets.
2. `AppSettingsDomainService.Migrate` normaliza valores.
3. Handlers de UI atualizam `appSettings` em memoria.
4. `OnUnloaded` salva snapshot atual no disco.

## Pontos de alteracao frequente

- Novo campo de settings: atualizar `AppSettings`, `AppSettingsDomainService`, `SettingsRepository`.
- Novo preset builtin: atualizar `DefaultPresets` e seed em repositiorio.

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
- [SettingsRepository](../../../src/App.WinUI/Services/SettingsRepository.cs#L6) - assinatura: `internal sealed class SettingsRepository`
- [PresetRepository](../../../src/App.WinUI/Services/PresetRepository.cs#L6) - assinatura: `internal sealed class PresetRepository`
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

## Migracao de Registro de Devices

O registro persistido de devices agora usa dupla protecao para evitar falso `Nunca conectado`:

- script explicito: `scripts/migrate-device-registry-presence-v1.ps1`
- fallback automatico em runtime: `DeviceRegistryPresenceNormalizer` + `JsonDeviceRegistryStore`

