# Handoff: Fix slider de brilho sempre mostrando 100%

## Objetivo

Corrigir o controle de brilho da DevicesPage que exibia sempre 100% (valor maximo) tanto no slider quanto no texto, independente do valor real aplicado no dispositivo.

## Escopo classificado

- Tipo: bug fix visual (UI apenas)
- Criterio de aceite: slider reflete `BrightnessApplied` do snapshot, texto mostra porcentagem correta, build 0 errors

## Causa raiz (corrigida apos segunda iteracao)

O firmware ESP32 define `gAppliedBrightness = min(gStreamBrightness, gBrightnessCap)`. Em operacao
normal (sem ajuste de brilho via stream de audio), `gStreamBrightness` e inicializado igual ao
`gBrightnessCap` no boot — portanto `brightnessApplied == brightnessCap` sempre no telemetry.

A formula do label era `applied / cap * 100`. Como os dois valores sao sempre iguais, o resultado
era invariavelmente `100%`, independente do valor real configurado pelo usuario.

O slider se posicionava corretamente (value=80 com max=160 -> thumb a 50%), mas o label ao lado
dizia "100%", dando a impressao de que a barra "voltou para 100%".

## Arquivos alterados

- `src/App.WinUI/Views/DevicesPage.Dashboard.cs`
  - `BuildBrightnessValueLabel()`: formula corrigida para `cap / SafeBrightnessMax * 100`; exibe `-%` se nao houver dado
  - Dois blocos onde `DashboardBrightnessSlider.Value` e definido (em `ApplyDashboard` path online e em `ApplySafeDashboardFallback`): usam `BrightnessApplied` com fallback para `BrightnessCap`
- `src/App.WinUI/Views/DevicesPage.xaml.cs`
  - `OnBrightnessSliderValueChanged()`: label durante arraste usa `normalized / SafeBrightnessMax * 100` (consistente com label pos-telemetry)

## Decisoes tomadas

1. **`BrightnessCap / SafeBrightnessMax` como formula do label.** O slider controla o cap; mostrar cap como fracao do maximo possivel e o que o usuario espera. Consistente com o handler de arraste.
2. **`BrightnessApplied` para posicao do slider, fallback para `BrightnessCap`.** Na pratica valores sao iguais; em condicoes futuras onde stream abaixe o brilho abaixo do cap, o slider refletiria o brilho real.
3. **`-%` quando sem dados.** Evita mostrar `0%` enganosamente para dispositivos sem telemetria.

## Validacoes executadas

```text
dotnet build src/App.WinUI/App.WinUI.csproj -c Debug  -> 0 errors
```

## Riscos e rollback

- Risco: nenhum. Mudanca puramente visual, nao afeta logica de envio de comando.
- O comando de brilho continua usando o valor do slider (`DashboardBrightnessSlider.Value`) clampeado entre `SafeBrightnessMin` e `SafeBrightnessMax` — nao foi alterado.
- Como reverter: `git revert` do commit.

## Proximos passos

1. Testar com dispositivo fisico: ajustar brilho, desconectar, reconectar, verificar que o slider restaura o valor anterior correto.
2. Verificar estado offline: dispositivo sem telemetria deve mostrar `-%` e slider no fallback do `BrightnessCap`.
