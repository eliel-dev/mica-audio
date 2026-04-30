using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Logging;
using App.WinUI.Views.Controls;

namespace App.WinUI.Views;

// DOCS: docs/wiki/guides/troubleshoot-city-autocomplete.md#passos
// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
public sealed partial class AppsPage
{
    private async Task LoadModifierEditorAsync()
    {
        var item = selectedItem;
        IReadOnlyDictionary<string, string>? values = null;

        if (item is not null)
        {
            var draft = await modifierStore.GetDraftAsync(LocalDraftScope, item.Id).ConfigureAwait(false);
            values = draft?.Values;
        }

        await modifierEditor.LoadAsync(
            item,
            ModifiersPanel,
            ModifiersHintText,
            values,
            emptySelectionHint: "Selecione um app para editar modificadores.",
            configuredHint: "Salvar atualiza o preview local. Instalar envia a configuracao atual para o dispositivo selecionado.",
            noModifiersHint: "Este app nao possui modificadores configuraveis.")
            .ConfigureAwait(false);

        UpdateGifOpenFileButtonVisibility();
        UpdateActionButtonsEnabled();
    }

    private bool TryBuildConfigFromEditor(AppCatalogItem item, out Dictionary<string, object?> jsonValues, out Dictionary<string, string> rawValues, out string error)
    {
        return modifierEditor.TryBuildConfig(item, out jsonValues, out rawValues, out error);
    }

    private static void ParseCityConfig(string? raw, out string displayName, out CitySuggestion? suggestion)
    {
        AppModifierEditorHost.ParseCityConfig(raw, out displayName, out suggestion);
    }

    private static bool TryBuildConfigJsonFromDraft(AppCatalogItem item, IReadOnlyDictionary<string, string> rawValues, out string configJson, out string error)
    {
        return AppModifierEditorHost.TryBuildConfigJsonFromDraft(item, rawValues, out configJson, out error);
    }

    private static string BuildCityAutocompleteFailureMessage(string query, CityAutocompleteService.CitySearchResult searchResult)
    {
        return AppModifierEditorHost.BuildCityAutocompleteFailureMessage(query, searchResult);
    }

    private void ApplyPreviewDraftToCard(string appId, IReadOnlyDictionary<string, string>? values)
    {
        var card = catalogCards.FirstOrDefault(card => string.Equals(card.Item.Id, appId, StringComparison.OrdinalIgnoreCase));
        if (card is null)
        {
            return;
        }

        card.SetPreviewConfig(WeatherAppFixedLocation.NormalizeRawValues(card.Item, values));
    }

    private async Task RefreshPreviewDraftsAsync()
    {
        var perAppValues = new Dictionary<string, IReadOnlyDictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in catalogCards)
        {
            var draft = await modifierStore.GetDraftAsync(LocalDraftScope, card.Item.Id).ConfigureAwait(false);
            perAppValues[card.Item.Id] = WeatherAppFixedLocation.NormalizeRawValues(card.Item, draft?.Values);
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var card in catalogCards)
            {
                card.SetPreviewConfig(perAppValues.TryGetValue(card.Item.Id, out var values) ? values : null);
            }
        });
    }

    private void RecordCityAutocompleteEvent(string message, LogSeverity severity)
    {
        appLogStore.Append(LogCategory.App, severity, message, selectedItem?.Id);
    }
}
