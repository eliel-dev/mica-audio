using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace App.WinUI.Views;

// DOCS: docs/wiki/guides/troubleshoot-city-autocomplete.md#passos
// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
public sealed partial class AppsPage
{
    private async Task LoadModifierEditorAsync()
    {
        var item = selectedItem;

        CleanupCityAutocompleteControls();
        ModifiersPanel.Children.Clear();
        modifierBindings.Clear();

        if (item is null)
        {
            ModifiersHintText.Text = "Selecione um app para editar modificadores.";
            UpdateGifOpenFileButtonVisibility();
            UpdateActionButtonsEnabled();
            return;
        }

        var modifiers = item.Modifiers.Where(static modifier => modifier.IsValid()).ToArray();
        if (modifiers.Length == 0)
        {
            if (WeatherAppFixedLocation.IsWeatherApp(item))
            {
                ModifiersHintText.Text = "Salvar atualiza o preview local. Instalar envia a configuração atual para o dispositivo selecionado.";
                AddFixedWeatherInfo();
            }
            else
            {
                ModifiersHintText.Text = "Este app não possui modificadores configuráveis.";
                ModifiersPanel.Children.Add(new TextBlock
                {
                    Text = "Sem parâmetros adicionais.",
                    Opacity = 0.8,
                });
            }

            UpdateGifOpenFileButtonVisibility();
            UpdateActionButtonsEnabled();
            return;
        }

        IReadOnlyDictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var draft = await modifierStore.GetDraftAsync(LocalDraftScope, item.Id);
        if (draft is not null)
        {
            values = draft.Values;
        }
        values = WeatherAppFixedLocation.NormalizeRawValues(item, values);

        ModifiersHintText.Text = "Salvar atualiza o preview local. Instalar envia a configuração atual para o dispositivo selecionado.";

        foreach (var modifier in modifiers)
        {
            var control = CreateModifierControl(modifier, values);
            modifierBindings[modifier.Key] = new ModifierControlBinding(modifier, control);

            ModifiersPanel.Children.Add(new TextBlock
            {
                Text = modifier.Label,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });

            if (!string.IsNullOrWhiteSpace(modifier.Description))
            {
                ModifiersPanel.Children.Add(new TextBlock
                {
                    Text = modifier.Description,
                    Opacity = 0.76,
                    TextWrapping = TextWrapping.Wrap,
                });
            }

            if (control is AutoSuggestBox suggest
                && citySuggestionFeedback.TryGetValue(suggest, out var feedback))
            {
                var inlineHost = new StackPanel { Spacing = 6 };
                inlineHost.Children.Add(control);
                inlineHost.Children.Add(feedback);
                ModifiersPanel.Children.Add(inlineHost);
                continue;
            }

            ModifiersPanel.Children.Add(control);
        }

        UpdateGifOpenFileButtonVisibility();
        UpdateActionButtonsEnabled();
    }

    private FrameworkElement CreateModifierControl(AppModifierDefinition definition, IReadOnlyDictionary<string, string> values)
    {
        values.TryGetValue(definition.Key, out var savedValue);
        var initialValue = !string.IsNullOrWhiteSpace(savedValue) ? savedValue : definition.DefaultValue;

        switch (definition.Type)
        {
            case AppModifierFieldType.Toggle:
                return new ToggleSwitch
                {
                    IsOn = bool.TryParse(initialValue, out var isOn) ? isOn : definition.DefaultToggle ?? false,
                };
            case AppModifierFieldType.Select:
                var combo = new ComboBox();
                foreach (var option in definition.Options)
                {
                    combo.Items.Add(new ComboBoxItem { Content = option.Label, Tag = option.Value });
                }

                var selected = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(i => string.Equals(i.Tag as string, initialValue, StringComparison.OrdinalIgnoreCase))
                    ?? combo.Items.OfType<ComboBoxItem>().FirstOrDefault();
                combo.SelectedItem = selected;
                if (string.Equals(definition.Key, "sourceType", StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectionChanged += OnSourceTypeSelectionChanged;
                }

                return combo;
            case AppModifierFieldType.CityAutocomplete:
                ParseCityConfig(initialValue, out var cityDisplay, out var citySuggestion);
                var suggest = new AutoSuggestBox
                {
                    PlaceholderText = definition.Placeholder ?? "Digite a cidade",
                    Text = cityDisplay,
                };
                citySuggestionFeedback[suggest] = new TextBlock
                {
                    Opacity = 0.82,
                    TextWrapping = TextWrapping.Wrap,
                    Visibility = Visibility.Collapsed,
                };
                if (citySuggestion is not null)
                {
                    suggest.Tag = citySuggestion;
                }

                suggest.TextChanged += OnCitySuggestTextChanged;
                suggest.SuggestionChosen += OnCitySuggestionChosen;
                suggest.QuerySubmitted += OnCitySuggestionQuerySubmitted;
                return suggest;
            case AppModifierFieldType.Number:
                var number = new NumberBox
                {
                    PlaceholderText = definition.Placeholder ?? "0",
                    SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden,
                    Value = TryParseDouble(initialValue, out var parsed) ? parsed : definition.Min ?? 0d,
                };
                if (definition.Min is not null) number.Minimum = definition.Min.Value;
                if (definition.Max is not null) number.Maximum = definition.Max.Value;
                number.SmallChange = definition.Step ?? 1d;
                return number;
            default:
                return new TextBox
                {
                    PlaceholderText = definition.Placeholder ?? string.Empty,
                    Text = initialValue ?? string.Empty,
                };
        }
    }

    private async void OnCitySuggestTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        sender.Tag = null;
        ClearCitySuggestions(sender);

        var query = sender.Text.Trim();
        if (query.Length < CityAutocompleteService.MinQueryLength)
        {
            SetCityAutocompleteFeedback(sender, string.Empty);
            return;
        }

        if (citySuggestCts.Remove(sender, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        citySuggestCts[sender] = cts;
        SetCityAutocompleteFeedback(sender, $"Buscando cidades brasileiras para '{query}'...", CityAutocompleteFeedbackState.Loading);

        try
        {
            var searchResult = await cityService.SearchWithDiagnosticsAsync(query, cts.Token);
            if (!citySuggestCts.TryGetValue(sender, out var active) || !ReferenceEquals(active, cts))
            {
                return;
            }

            if (searchResult.IsCancelled)
            {
                return;
            }

            if (searchResult.HasFailure)
            {
                var failureMessage = BuildCityAutocompleteFailureMessage(query, searchResult);
                SetCityAutocompleteFeedback(sender, failureMessage, CityAutocompleteFeedbackState.Error);
                RecordCityAutocompleteEvent(failureMessage, LogSeverity.Warning);
                return;
            }

            var lookup = new Dictionary<string, CitySuggestion>(StringComparer.OrdinalIgnoreCase);
            foreach (var suggestion in searchResult.Suggestions)
            {
                if (!lookup.ContainsKey(suggestion.DisplayName))
                {
                    lookup[suggestion.DisplayName] = suggestion;
                }
            }

            if (lookup.Count == 0)
            {
                SetCityAutocompleteFeedback(sender, $"Nenhuma cidade brasileira encontrada para '{query}'.", CityAutocompleteFeedbackState.Empty);
                return;
            }

            citySuggestionLookup[sender] = lookup;
            sender.ItemsSource = lookup.Keys.ToArray();
            sender.IsSuggestionListOpen = true;
            SetCityAutocompleteFeedback(sender, string.Empty);
            sender.UpdateLayout();
        }
        finally
        {
            if (citySuggestCts.TryGetValue(sender, out var active) && ReferenceEquals(active, cts))
            {
                citySuggestCts.Remove(sender);
            }

            cts.Dispose();
        }
    }

    private void OnCitySuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        var selectedText = args.SelectedItem switch
        {
            CitySuggestion suggestion => suggestion.DisplayName,
            string text => text,
            _ => string.Empty,
        };

        if (string.IsNullOrWhiteSpace(selectedText))
        {
            return;
        }

        sender.Text = selectedText;
        SetCityAutocompleteFeedback(sender, string.Empty);
        if (citySuggestionLookup.TryGetValue(sender, out var lookup)
            && lookup.TryGetValue(selectedText, out var mappedSuggestion))
        {
            sender.Tag = mappedSuggestion;
            return;
        }

        ParseCityConfig(selectedText, out _, out var parsedSuggestion);
        sender.Tag = parsedSuggestion;
    }

    private void OnCitySuggestionQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is CitySuggestion chosenSuggestion)
        {
            sender.Tag = chosenSuggestion;
            sender.Text = chosenSuggestion.DisplayName;
            SetCityAutocompleteFeedback(sender, string.Empty);
            return;
        }

        var queryText = args.QueryText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(queryText))
        {
            sender.Tag = null;
            SetCityAutocompleteFeedback(sender, string.Empty);
            return;
        }

        if (citySuggestionLookup.TryGetValue(sender, out var lookup)
            && lookup.TryGetValue(queryText, out var suggestion))
        {
            sender.Tag = suggestion;
            sender.Text = suggestion.DisplayName;
            SetCityAutocompleteFeedback(sender, string.Empty);
            return;
        }

        ParseCityConfig(queryText, out _, out var parsedSuggestion);
        sender.Tag = parsedSuggestion;
        SetCityAutocompleteFeedback(sender, string.Empty);
    }

    private static void ParseCityConfig(string? raw, out string displayName, out CitySuggestion? suggestion)
    {
        displayName = string.Empty;
        suggestion = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var parts = raw.Split('|', StringSplitOptions.TrimEntries);
        displayName = parts[0].Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = raw.Trim();
        }

        var labels = displayName.Split(',', StringSplitOptions.TrimEntries);
        var name = labels.Length > 0 ? labels[0] : displayName;
        var region = labels.Length > 1 ? labels[1] : string.Empty;
        var country = labels.Length > 2 ? labels[2] : "Brasil";

        if (parts.Length >= 3
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            suggestion = new CitySuggestion
            {
                Name = name,
                Region = region,
                Country = country,
                Latitude = lat,
                Longitude = lon,
            };
            return;
        }

        suggestion = new CitySuggestion
        {
            Name = name,
            Region = region,
            Country = country,
        };
    }

    private void ApplyPreviewDraftToCard(string appId, IReadOnlyDictionary<string, string>? values)
    {
        var card = catalogCards.FirstOrDefault(c => string.Equals(c.Item.Id, appId, StringComparison.OrdinalIgnoreCase));
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

    private bool TryBuildConfigFromEditor(AppCatalogItem item, out Dictionary<string, object?> jsonValues, out Dictionary<string, string> rawValues, out string error)
    {
        jsonValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        rawValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var modifier in item.Modifiers.Where(static m => m.IsValid()))
        {
            if (!modifierBindings.TryGetValue(modifier.Key, out var binding))
            {
                continue;
            }

            if (!TryReadModifierValue(binding, out var typedValue, out var rawValue, out error))
            {
                return false;
            }

            jsonValues[modifier.Key] = typedValue;
            rawValues[modifier.Key] = rawValue;
        }

        WeatherAppFixedLocation.NormalizePayloadInPlace(item, jsonValues);
        WeatherAppFixedLocation.NormalizeRawValuesInPlace(item, rawValues);
        error = string.Empty;
        return true;
    }

    private static bool TryBuildConfigJsonFromDraft(AppCatalogItem item, IReadOnlyDictionary<string, string> rawValues, out string configJson, out string error)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in item.Modifiers.Where(static m => m.IsValid()))
        {
            rawValues.TryGetValue(modifier.Key, out var rawValue);
            if (!TryParseRawValue(modifier, rawValue, out var typedValue, out error))
            {
                configJson = string.Empty;
                return false;
            }

            data[modifier.Key] = typedValue;
        }

        WeatherAppFixedLocation.NormalizePayloadInPlace(item, data);
        configJson = JsonSerializer.Serialize(data);
        error = string.Empty;
        return true;
    }

    private static bool TryReadModifierValue(ModifierControlBinding binding, out object? typedValue, out string rawValue, out string error)
    {
        var definition = binding.Definition;
        switch (definition.Type)
        {
            case AppModifierFieldType.Toggle:
                var toggle = (ToggleSwitch)binding.Control;
                typedValue = toggle.IsOn;
                rawValue = toggle.IsOn ? "true" : "false";
                error = string.Empty;
                return true;
            case AppModifierFieldType.Select:
                if (((ComboBox)binding.Control).SelectedItem is ComboBoxItem selected && selected.Tag is string selectedValue)
                {
                    typedValue = selectedValue;
                    rawValue = selectedValue;
                    error = string.Empty;
                    return true;
                }

                if (definition.Required)
                {
                    typedValue = null;
                    rawValue = string.Empty;
                    error = $"O campo '{definition.Label}' é obrigatório.";
                    return false;
                }

                typedValue = string.Empty;
                rawValue = string.Empty;
                error = string.Empty;
                return true;
            case AppModifierFieldType.Number:
                var number = ((NumberBox)binding.Control).Value;
                if (double.IsNaN(number))
                {
                    typedValue = null;
                    rawValue = string.Empty;
                    error = $"O campo '{definition.Label}' é obrigatório.";
                    return false;
                }

                typedValue = number;
                rawValue = number.ToString(CultureInfo.InvariantCulture);
                error = string.Empty;
                return true;
            case AppModifierFieldType.CityAutocomplete:
                var suggest = (AutoSuggestBox)binding.Control;
                var cityRaw = suggest.Tag is CitySuggestion city ? city.ToConfigValue() : suggest.Text.Trim();
                if (definition.Required && string.IsNullOrWhiteSpace(cityRaw))
                {
                    typedValue = null;
                    rawValue = string.Empty;
                    error = $"O campo '{definition.Label}' é obrigatório.";
                    return false;
                }

                typedValue = cityRaw;
                rawValue = cityRaw;
                error = string.Empty;
                return true;
            default:
                var text = ((TextBox)binding.Control).Text.Trim();
                if (definition.Required && string.IsNullOrWhiteSpace(text))
                {
                    typedValue = null;
                    rawValue = string.Empty;
                    error = $"O campo '{definition.Label}' é obrigatório.";
                    return false;
                }

                typedValue = text;
                rawValue = text;
                error = string.Empty;
                return true;
        }
    }

    private static bool TryParseRawValue(AppModifierDefinition modifier, string? rawValue, out object? typedValue, out string error)
    {
        var value = rawValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            typedValue = modifier.Type == AppModifierFieldType.Toggle ? false : string.Empty;
            error = string.Empty;
            return !modifier.Required;
        }

        switch (modifier.Type)
        {
            case AppModifierFieldType.Toggle:
                if (!bool.TryParse(value, out var boolValue))
                {
                    typedValue = null;
                    error = $"Valor inválido para '{modifier.Label}'.";
                    return false;
                }

                typedValue = boolValue;
                error = string.Empty;
                return true;
            case AppModifierFieldType.Number:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberValue))
                {
                    typedValue = null;
                    error = $"Valor numérico inválido para '{modifier.Label}'.";
                    return false;
                }

                typedValue = numberValue;
                error = string.Empty;
                return true;
            default:
                typedValue = value;
                error = string.Empty;
                return true;
        }
    }

    private static bool TryParseDouble(string? value, out double result) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private void AddFixedWeatherInfo()
    {
        AddStaticModifierInfo("Cidade", WeatherAppFixedLocation.FixedLocationLabel);
        AddStaticModifierInfo("Temperatura", WeatherAppFixedLocation.FixedUnitsLabel);
    }

    private void AddStaticModifierInfo(string label, string value)
    {
        ModifiersPanel.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        ModifiersPanel.Children.Add(new TextBlock
        {
            Text = value,
            Opacity = 0.76,
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private void CleanupCityAutocompleteControls()
    {
        foreach (var binding in modifierBindings.Values)
        {
            if (binding.Control is not AutoSuggestBox suggest)
            {
                continue;
            }

            suggest.TextChanged -= OnCitySuggestTextChanged;
            suggest.SuggestionChosen -= OnCitySuggestionChosen;
            suggest.QuerySubmitted -= OnCitySuggestionQuerySubmitted;
            ClearCitySuggestions(suggest);
            SetCityAutocompleteFeedback(suggest, string.Empty);
            citySuggestionFeedback.Remove(suggest);

            if (citySuggestCts.Remove(suggest, out var pending))
            {
                pending.Cancel();
                pending.Dispose();
            }
        }

        citySuggestionFeedback.Clear();
    }

    private void ClearCitySuggestions(AutoSuggestBox sender)
    {
        citySuggestionLookup.Remove(sender);
        sender.ItemsSource = null;
        sender.IsSuggestionListOpen = false;
    }

    private static string BuildCityAutocompleteFailureMessage(string query, CityAutocompleteService.CitySearchResult searchResult)
    {
        var builder = new StringBuilder();
        builder.Append("Autocomplete de cidade indisponível");

        if (!string.IsNullOrWhiteSpace(query))
        {
            builder.Append(" para '");
            builder.Append(query);
            builder.Append('\'');
        }

        builder.Append(": ");
        builder.Append(searchResult.FailureMessage);
        return builder.ToString();
    }

    private void SetCityAutocompleteFeedback(AutoSuggestBox sender, string message, CityAutocompleteFeedbackState state = CityAutocompleteFeedbackState.None)
    {
        if (!citySuggestionFeedback.TryGetValue(sender, out var feedback))
        {
            return;
        }

        feedback.Text = message;
        feedback.Visibility = string.IsNullOrWhiteSpace(message)
            ? Visibility.Collapsed
            : Visibility.Visible;
        feedback.Opacity = state == CityAutocompleteFeedbackState.Loading ? 0.72 : 0.9;
    }

    private void RecordCityAutocompleteEvent(string message, LogSeverity severity)
    {
        appLogStore.Append(LogCategory.App, severity, message, selectedItem?.Id);
    }

    private enum CityAutocompleteFeedbackState
    {
        None,
        Loading,
        Empty,
        Error,
    }

    private void OnSourceTypeSelectionChanged(object sender, SelectionChangedEventArgs e)
        => UpdateGifOpenFileButtonVisibility();
}
