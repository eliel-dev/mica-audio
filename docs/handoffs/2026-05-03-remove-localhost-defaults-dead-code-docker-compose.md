# Handoff - 2026-05-03-remove-localhost-defaults-dead-code-docker-compose

## Objetivo

Remover codigo morto relacionado a servidor local/embedded, corrigir defaults de localhost no WinUI para suportar servidor remoto/Docker, e adicionar docker-compose.yml para facilitar uso do servidor standalone.

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite:
  - WinUI nao assume mais 127.0.0.1:5272 como default
  - WebViewDashboard usa o endereco do servidor configurado em vez de hardcoded localhost
  - Codigo morto (AppsPage, ServerPage, servicos de apps removidos) deletado fisicamente
  - Build limpo apos `dotnet clean`
  - docs-validate, ai-governance-check e dotnet build passam

## Arquivos alterados

### Codigo WinUI (defaults e hardcodes)
- `src/App.WinUI/App.xaml.cs` - `StartupDeviceServerSettings.Default` agora usa `string.Empty` em vez de `http://127.0.0.1:5272`
- `src/App.WinUI/Services/AppSettingsDomainService.cs` - `NormalizeRemoteServerBaseAddress` fallback retorna `string.Empty` em vez de localhost
- `src/App.WinUI/Services/Devices/DeviceOperationsState.cs` - `ServerBaseAddress` default agora `string.Empty`
- `src/App.WinUI/Views/SettingsPage.xaml.cs` - placeholder atualizado para `http://<host>:<porta>` e descricao removida mencao de localhost/standalone
- `src/App.WinUI/Views/DevicesPage.WebViewDashboard.cs` - `BuildDashboardWebViewUri` nao mais forca `Host = "127.0.0.1"`, usa o endereco configurado

### Remocao de codigo morto
- Deletados: `src/App.WinUI/Views/AppsPage*.cs`, `src/App.WinUI/Views/AppsPage.xaml`
- Deletados: `src/App.WinUI/Views/ServerPage*.cs`, `src/App.WinUI/Views/ServerPage.xaml`
- Deletados: `src/App.WinUI/Services/Apps/AppDeploymentService.cs`, `IAppDeploymentService.cs`, `AppRuntimeHost.cs`, `AppRuntimeProviderRegistry.cs`, `GifCatalogAppRuntimeService.cs`, `GifHub75RuntimeProvider.cs`, `IAppRuntimeProvider.cs`
- Deletados: `src/App.WinUI/Services/Apps/UseCases/*.cs`
- Deletado: `src/App.WinUI/ViewModels/AppsPageViewModel.cs`
- Removido diretorio vazio: `src/App.WinUI/Services/Apps/UseCases/`

### Projeto e configuracao
- `src/App.WinUI/App.WinUI.csproj` - removidos `<Compile Remove>` e `<Page Remove>` de arquivos deletados; mantido apenas `<Page Remove="Views\DevicesPage.xaml" />`
- `docker-compose.yml` (novo) - compose para subir `mica-audio-server` com ports 8080/5273/5274udp/5275udp e volume para `/data`

### Scripts e docs ajustados por impacto da remocao
- `scripts/docs-validate.ps1` - removidos `AppDeploymentService.cs`, `AppsPage.xaml.cs`, `ServerPage.xaml.cs` da lista `$docsCoverageTargets`
- `docs/wiki/reference/ai-contract.v1.yaml` - removido `module_entrypoint` de `AppDeploymentService.cs`
- `docs/wiki/guides/build-export-firmware.md` - removida referencia de codigo a `ServerPage.xaml.cs`
- `docs/wiki/ai/README.md` - adicionado link para `engineering-advisor.md`
- `docs/wiki/README.md` - adicionado link para `engineering-advisor.md`

## Decisoes tomadas

1. **Manter `DevicesPage.xaml` com `<Page Remove>`**: a DevicesPage ainda e a pagina principal de operacao de dispositivos, mas seu XAML precisa ser restaurado/reescrito em outro momento. Por enquanto, o build continua funcionando via code-behind.
2. **Nao renomear `render.yaml`**: ele e a config de deploy na plataforma Render.com, distinta do docker-compose local. Mantive os dois arquivos.
3. **Deletar fisicamente em vez de apenas excluir da compilacao**: o repo estava acumulando arquivos mortos que confundiam buscas e validacoes. Deletar reduz noise.
4. **Fallback vazio em vez de manter localhost**: como o app e remote-only, nao faz sentido default para localhost. Usuario deve configurar o servidor.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK
dotnet clean MicaAudio.sln -> OK
dotnet build MicaAudio.sln -c Debug -> OK (0 erros)
```

## Riscos e rollback

- **Risco principal**: usuarios que nao tinham servidor configurado agora veem campo vazio. O app precisa tratar `ServerBaseAddress` vazio graciosamente (ja trata: nao conecta e mostra status apropriado).
- **Como reverter**: restaurar os defaults de localhost nos 4 arquivos de settings; os arquivos deletados precisariam ser recuperados do git history.

## Proximos passos

1. Restaurar `DevicesPage.xaml` para remover a dependencia de `<Page Remove>` e stale `.g.cs`
2. Verificar se o app lida bem com `ServerBaseAddress` vazio na UI (desabilitar botoes, mostrar hint)
3. Considerar adicionar discovery/zero-code LAN onboarding para preencher o endereco automaticamente
