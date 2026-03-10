using System.Globalization;
using System.Text;
using System.Text.Json;
using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace App.WinUI.Views.Controls;

// DOCS: docs/wiki/modules/paineis.md#editor-compartilhado-de-modifiers
// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
internal sealed class AppModifierEditorHost : IDisposable
{
    private readonly CityAutocompleteService cityService;
    private readonly Action<string>? diagnosticSink;
    private readonly Dictionary<string, AppModifierControlBinding> bindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<AutoSuggestBox, CancellationTokenSource> citySuggestCts = new();
    private readonly Dictionary<AutoSuggestBox, Dictionary<string, CitySuggestion>> citySuggestionLookup = new();
    private readonly Dictionary<AutoSuggestBox, TextBlock> citySuggestionFeedback = new();

    public AppModifierEditorHost(CityAutocompleteService cityService, Action<string>? diagnosticSink = null)
    {
        this.cityService = cityService;
        this.diagnosticSink = diagnosticSink;
    }

    public event EventHandler<string>? ModifierChanged;

    public Task LoadAsync(
        AppCatalogItem? item,
        StackPanel panelHost,
        TextBlock hintText,
        IReadOnlyDictionary<string, string>? rawValues,
        string emptySelectionHint,
        string configuredHint,
        string noModifiersHint)
    {
        ArgumentNullException.ThrowIfNull(panelHost);
        ArgumentNullException.ThrowIfNull(hintText);

        Cleanup();
        panelHost.Children.Clear();

        if (item is null)
        {
            hintText.Text = emptySelectionHint;
            return Task.CompletedTask;
        }

        var modifiers = item.Modifiers.Where(static modifier => modifier.IsValid()).ToArray();
        var values = WeatherAppFixedLocation.NormalizeRawValues(item, rawValues);
        if (modifiers.Length == 0)
        {
            if (WeatherAppFixedLocation.IsWeatherApp(item))
            {
                hintText.Text = configuredHint;
                AddStaticModifierInfo(panelHost, "Cidade", WeatherAppFixedLocation.FixedLocationLabel);
                AddStaticModifierInfo(panelHost, "Temperatura", WeatherAppFixedLocation.FixedUnitsLabel);
                return Task.CompletedTask;
            }

            hintText.Text = noModifiersHint;
            panelHost.Children.Add(new TextBlock
            {
                Text = "Sem parametros adicionais.",
                Opacity = 0.8,
            });
            return Task.CompletedTask;
        }

        hintText.Text = configuredHint;
        foreach (var modifier in modifiers)
        {
            var control = CreateModifierControl(modifier, values);
            bindings[modifier.Key] = new AppModifierControlBinding(modifier, control);

            panelHost.Children.Add(new TextBlock
            {
                Text = modifier.Label,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            });

            if (!string.IsNullOrWhiteSpace(modifier.Description))
            {
                panelHost.Children.Add(new TextBlock
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
                panelHost.Children.Add(inlineHost);
                continue;
            }

            panelHost.Children.Add(control);
        }

        return Task.CompletedTask;
    }

    public bool TryBuildConfig(
        AppCatalogItem item,
        out Dictionary<string, object?> jsonValues,
        out Dictionary<string, string> rawValues,
        out string error)
    {
        jsonValues = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        rawValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var modifier in item.Modifiers.Where(static modifier => modifier.IsValid()))
        {
            if (!bindings.TryGetValue(modifier.Key, out var binding))
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

    public bool TryGetCurrentRawValue(string key, out string rawValue)
    {
        rawValue = string.Empty;
        if (!bindings.TryGetValue(key, out var binding))
        {
            return false;
        }

        if (!TryReadModifierValue(binding, out _, out rawValue, out _))
        {
            rawValue = string.Empty;
            return false;
        }

        return true;
    }

    public void Cleanup()
    {
        foreach (var binding in bindings.Values)
        {
            switch (binding.Control)
            {
                case AutoSuggestBox suggest:
                    suggest.TextChanged -= OnCitySuggestTextChanged;
                    suggest.SuggestionChosen -= OnCitySuggestionChosen;
                    suggest.QuerySubmitted -= OnCitySuggestionQuerySubmitted;
                    break;
                case ComboBox combo:
                    combo.SelectionChanged -= OnModifierControlChanged;
                    break;
                case ToggleSwitch toggle:
                    toggle.Toggled -= OnModifierControlChanged;
                    break;
                case NumberBox number:
                    number.ValueChanged -= OnModifierNumberChanged;
                    break;
                case TextBox text:
                    text.TextChanged -= OnModifierControlChanged;
                    break;
            }

            if (binding.Control is AutoSuggestBox autoSuggest)
            {
                ClearCitySuggestions(autoSuggest);
                SetCityAutocompleteFeedback(autoSuggest, string.Empty);
                citySuggestionFeedback.Remove(autoSuggest);

                if (citySuggestCts.Remove(autoSuggest, out var pending))
                {
                    pending.Cancel();
                    pending.Dispose();
                }
            }
        }

        bindings.Clear();
        citySuggestionLookup.Clear();
        citySuggestionFeedback.Clear();
    }

    public void Dispose()
    {
        Cleanup();
        foreach (var cts in citySuggestCts.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }

        citySuggestCts.Clear();
    }

    public static void ParseCityConfig(string? raw, out string displayName, out CitySuggestion? suggestion)
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

    public static bool TryBuildConfigJsonFromDraft(AppCatalogItem item, IReadOnlyDictionary<string, string> rawValues, out string configJson, out string error)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in item.Modifiers.Where(static modifier => modifier.IsValid()))
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

    public static string BuildCityAutocompleteFailureMessage(string query, CityAutocompleteService.CitySearchResult searchResult)
    {
        var builder = new StringBuilder();
        builder.Append("Autocomplete de cidade indisponivel");

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

    private FrameworkElement CreateModifierControl(AppModifierDefinition definition, IReadOnlyDictionary<string, string> values)
    {
        values.TryGetValue(definition.Key, out var savedValue);
        var initialValue = !string.IsNullOrWhiteSpace(savedValue) ? savedValue : definition.DefaultValue;

        switch (definition.Type)
        {
            case AppModifierFieldType.Toggle:
                var toggle = new ToggleSwitch
                {
                    IsOn = bool.TryParse(initialValue, out var isOn) ? isOn : definition.DefaultToggle ?? false,
                };
                toggle.Toggled += OnModifierControlChanged;
                return toggle;
            case AppModifierFieldType.Select:
                var combo = new ComboBox();
                foreach (var option in definition.Options)
                {
                    combo.Items.Add(new ComboBoxItem { Content = option.Label, Tag = option.Value });
                }

                var selected = combo.Items.OfType<ComboBoxItem>().FirstOrDefault(item => string.Equals(item.Tag as string, initialValue, StringComparison.OrdinalIgnoreCase))
                    ?? combo.Items.OfType<ComboBoxItem>().FirstOrDefault();
                combo.SelectedItem = selected;
                combo.SelectionChanged += OnModifierControlChanged;
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
                if (definition.Min is not null)
                {
                    number.Minimum = definition.Min.Value;
                }

                if (definition.Max is not null)
                {
                    number.Maximum = definition.Max.Value;
                }

                number.SmallChange = definition.Step ?? 1d;
                number.ValueChanged += OnModifierNumberChanged;
                return number;
            default:
                var text = new TextBox
                {
                    PlaceholderText = definition.Placeholder ?? string.Empty,
                    Text = initialValue ?? string.Empty,
                };
                text.TextChanged += OnModifierControlChanged;
                return text;
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
        NotifyModifierChanged(sender);

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
                diagnosticSink?.Invoke(failureMessage);
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
            NotifyModifierChanged(sender);
            return;
        }

        ParseCityConfig(selectedText, out _, out var parsedSuggestion);
        sender.Tag = parsedSuggestion;
        NotifyModifierChanged(sender);
    }

    private void OnCitySuggestionQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is CitySuggestion chosenSuggestion)
        {
            sender.Tag = chosenSuggestion;
            sender.Text = chosenSuggestion.DisplayName;
            SetCityAutocompleteFeedback(sender, string.Empty);
            NotifyModifierChanged(sender);
            return;
        }

        var queryText = args.QueryText?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(queryText))
        {
            sender.Tag = null;
            SetCityAutocompleteFeedback(sender, string.Empty);
            NotifyModifierChanged(sender);
            return;
        }

        if (citySuggestionLookup.TryGetValue(sender, out var lookup)
            && lookup.TryGetValue(queryText, out var suggestion))
        {
            sender.Tag = suggestion;
            sender.Text = suggestion.DisplayName;
            SetCityAutocompleteFeedback(sender, string.Empty);
            NotifyModifierChanged(sender);
            return;
        }

        ParseCityConfig(queryText, out _, out var parsedSuggestion);
        sender.Tag = parsedSuggestion;
        SetCityAutocompleteFeedback(sender, string.Empty);
        NotifyModifierChanged(sender);
    }

    private void OnModifierNumberChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        NotifyModifierChanged(sender);
    }

    private void OnModifierControlChanged(object sender, object e)
    {
        if (sender is FrameworkElement element)
        {
            NotifyModifierChanged(element);
        }
    }

    private void NotifyModifierChanged(FrameworkElement control)
    {
        var key = bindings
            .Where(pair => ReferenceEquals(pair.Value.Control, control))
            .Select(pair => pair.Key)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(key))
        {
            ModifierChanged?.Invoke(this, key);
        }
    }

    private static bool TryReadModifierValue(AppModifierControlBinding binding, out object? typedValue, out string rawValue, out string error)
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
                    error = $"O campo '{definition.Label}' e obrigatorio.";
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
                    error = $"O campo '{definition.Label}' e obrigatorio.";
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
                    error = $"O campo '{definition.Label}' e obrigatorio.";
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
                    error = $"O campo '{definition.Label}' e obrigatorio.";
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
                    error = $"Valor invalido para '{modifier.Label}'.";
                    return false;
                }

                typedValue = boolValue;
                error = string.Empty;
                return true;
            case AppModifierFieldType.Number:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberValue))
                {
                    typedValue = null;
                    error = $"Valor numerico invalido para '{modifier.Label}'.";
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

    private static bool TryParseDouble(string? value, out double result)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static void AddStaticModifierInfo(StackPanel panelHost, string label, string value)
    {
        panelHost.Children.Add(new TextBlock
        {
            Text = label,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        panelHost.Children.Add(new TextBlock
        {
            Text = value,
            Opacity = 0.76,
            TextWrapping = TextWrapping.Wrap,
        });
    }

    private void ClearCitySuggestions(AutoSuggestBox sender)
    {
        citySuggestionLookup.Remove(sender);
        sender.ItemsSource = null;
        sender.IsSuggestionListOpen = false;
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

    private enum CityAutocompleteFeedbackState
    {
        None,
        Loading,
        Empty,
        Error,
    }
}

internal sealed class AppModifierControlBinding
{
    public AppModifierControlBinding(AppModifierDefinition definition, FrameworkElement control)
    {
        Definition = definition;
        Control = control;
    }

    public AppModifierDefinition Definition { get; }

    public FrameworkElement Control { get; }
}
