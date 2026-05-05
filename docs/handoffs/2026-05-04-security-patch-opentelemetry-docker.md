# Handoff - Correcao de vulnerabilidades OpenTelemetry e imagem base Docker

## Objetivo

Eliminar as 5 vulnerabilidades corrigiveis (CVEs) detectadas pelo Docker Scout na imagem `mica-audio-server:latest`, abrangendo pacotes NuGet OpenTelemetry e pacotes de sistema Ubuntu (`libcap2`, `sed`).

## Escopo classificado

- Tipo: estrutural
- Criterio de aceite: CVEs fixaveis removidas da imagem via atualizacao de dependencias e patch do sistema, build da solucao passando, validacoes estruturais passando.

## Arquivos alterados

- `src/Device.Server/Device.Server.csproj`
- `src/App.WinUI/App.WinUI.csproj`
- `tests/Output.Tests/Output.Tests.csproj`
- `src/MicaAudio.Server/Dockerfile`

## Decisoes tomadas

1. Manter compatibilidade com a linha de versao `1.15.x` dos pacotes OpenTelemetry, aplicando apenas patches de seguranca:
   - `OpenTelemetry.Exporter.OpenTelemetryProtocol`: `1.15.0` -> `1.15.3`
   - `OpenTelemetry.Extensions.Hosting`: `1.15.0` -> `1.15.3`
   - `OpenTelemetry.Instrumentation.AspNetCore`: `1.15.0` -> `1.15.2`
   - `OpenTelemetry.Instrumentation.Http`: `1.15.0` -> `1.15.1`
2. Adicionar `RUN apt-get update && apt-get upgrade -y --no-install-recommends` na stage `runtime` do `Dockerfile` para aplicar patches de seguranca da imagem base `mcr.microsoft.com/dotnet/aspnet:10.0` (Ubuntu 24.04) sem manter cache de apt.
3. Nao alterar contratos publicos, APIs ou comportamento funcional; apenas atualizacao de dependencias transdutoras de telemetria.

## Validacoes executadas

```text
powershell -ExecutionPolicy Bypass -File .\scripts\docs-validate.ps1 -> OK
powershell -ExecutionPolicy Bypass -File .\scripts\ai-governance-check.ps1 -> OK (apos handoff)
dotnet build MicaAudio.sln -c Debug -> OK (0 erros, 0 avisos)
```

## Riscos e rollback

- Risco principal: diferenca de comportamento sutil nas bibliotecas de instrumentacao OpenTelemetry apos patch; regressao tipica seria falha de exportacao OTLP ou metricas.
- Como reverter:
  - Restaurar as versoes `1.15.0` nos tres `.csproj`;
  - Remover o passo `apt-get upgrade` do `Dockerfile`.

## Proximos passos

1. Rebuildar a imagem Docker `mica-audio-server` a partir do `Dockerfile` atualizado.
2. Re-executar o Docker Scout na nova imagem para confirmar que as 5 CVEs fixaveis nao constam mais no relatorio.
3. Se permanecerem CVEs nao-fixaveis, avaliar se aceitacao de risco ou mudanca de imagem base e necessaria.
