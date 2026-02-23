# Handoff — Apps UX Brasília + Clima Open-Meteo

## Objetivo
Finalizar a aba `Apps` em PT-BR com foco em dois apps (`Clima` e `Relógio`), removendo histórico/autor da UI, aplicando relógio fixo em Brasília com watchfaces e usando clima real da API Open-Meteo no preview HUB75.

## Escopo classificado
Estrutural (governança local considera alterações em `src/` como estruturais).

## Arquivos alterados
- `src/App.WinUI/Views/AppsPage.Ui.cs`
- `src/App.WinUI/Views/AppsPage.xaml.cs`
- `src/App.WinUI/AppData/apps-catalog.seed.json`
- `src/App.WinUI/Services/Apps/AppCatalogService.cs`
- `src/App.WinUI/Views/Controls/Renderers/ClockPreviewRenderer.cs`
- `src/App.WinUI/Views/Controls/Renderers/Hub75PreviewHelper.cs`
- `src/App.WinUI/Views/Controls/Renderers/WeatherPreviewRenderer.cs`
- `src/App.WinUI/Services/Apps/WeatherPreviewDataService.cs`
- `src/App.WinUI/Services/Apps/WeatherPreviewSnapshot.cs`
- `src/App.WinUI/Views/Controls/Renderers/ClockFontRenderer.cs`

## Decisoes tomadas
1. A aba Apps ficou sem card de histórico; status/progresso foi mantido.
2. A UI de detalhes removeu referência a autor e passou para `categoria | intervalo recomendado`.
3. O catálogo permanece com apenas 2 apps (`Clima` e `Relógio`) com modificadores alinhados ao escopo atual.
4. O relógio usa timezone fixa de Brasília e passou a aceitar `format24h`, `watchfaceStyle` e `fontColor`.
5. O preview de clima passou a buscar dados reais no Open-Meteo com cache e atualização automática (janela alvo de 5 min).
6. A seleção de cidade foi reforçada para renderização determinística de nomes no autocomplete.
7. O preview local continua atualizando imediatamente após `Salvar`.

## Validacoes executadas
- `dotnet build MicaAudio.sln -c Debug` (sucesso)

## Riscos e rollback
- Risco: indisponibilidade da API Open-Meteo pode exibir preview neutro temporário (`--C/--F`).
- Mitigação: cache local mantém último snapshot válido quando houver.
- Rollback: reverter os arquivos da lista deste handoff para o estado anterior.

## Proximos passos
1. Validar manualmente os três estilos de relógio em tema claro/escuro.
2. Validar manualmente autocomplete de cidade com diferentes capitais brasileiras.
3. Adicionar testes unitários para `WeatherPreviewDataService` e parse de modificadores de relógio.
