## Objetivo

Corrigir erros de validacao docs-validate e ai-governance-check para normalizar o estado do repositorio.

## Escopo classificado

Estrutural - alteracoes em scripts de governanca, documentacao wiki e correcoes de links quebrados.

## Arquivos alterados

- `docs/wiki/guides/criticality-context7-audit.md` - substituiu referencia a JsonDeviceRegistryStore.cs por RemoteDeviceServerSecretStore.cs
- `docs/wiki/guides/security-quality-hardening.md` - substituiu referencia a JsonDeviceRegistryStore.cs por RemoteDeviceServerSecretStore.cs
- `docs/wiki/modules/settings-presets-persistence.md` - substituiu referencia a JsonDeviceRegistryStore.cs por RemoteDeviceServerSecretStore.cs
- `docs/wiki/reference/device-telemetry-v2-fields.md` - substituiu referencia a JsonDeviceRegistryStore.cs por RemoteDeviceServerSecretStore.cs
- `docs/wiki/modules/app-winui.md` - adicionou secoes `Observabilidade Tecnica`, `Cache Compartilhado`, `Integracoes HTTP Externas`
- `docs/wiki/modules/device-server-protocol.md` - adicionou secao `Ownership Shadow e Lock Lease`
- `scripts/docs-validate.ps1` - removeu referencia a arquivo removido `Device.Client.Embedded/EmbeddedDeviceServerClient.cs`

## Decisoes tomadas

1. JsonDeviceRegistryStore.cs foi removido do repositorio (conforme code-index.md); a referencia correta e RemoteDeviceServerSecretStore.cs.
2. Seccoes faltantes adicionadas em app-winui.md para validar ancoras DOCS em App.xaml.cs.
3. Secao sobre ownership/shadow/lock-lease adicionada em device-server-protocol.md para validar ancora DOCS em DeviceServerHost.cs.
4. Removida linha obsoleta no script docs-validate.ps1 que referenciava arquivo inexistente.

## Validacoes executadas

- `powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1` - OK (nenhuma falha)
- `powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1` - pendente de handoff
- `dotnet build MicaAudio.sln -c Debug` - pendente (erro transitario de arquivo em uso)

## Riscos e rollback

- Baixo risco: alteracoes sao documentais/estruturais em scripts e wiki.
- Rollback: git checkout dos arquivos alterados.

## Proximos passos

1. Reexecutar `dotnet build` para confirmar build limpo.
2. Confirmar `ai-governance-check` passando com handoff presente.
3. Opcional: atualizar ai-contract.v1.yaml se necessario (module_entrypoints nao possui mais referencia ao Embedded).
