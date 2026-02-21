
using System.Globalization;
using System.Text.Json;
using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#modulo-apps-catalog-and-deployment
public sealed partial class AppsPage : Page
{
    private const string LocalDraftScope = "__local__";
    private readonly List<AppCatalogItem> allItems = new();
    private readonly List<AppCatalogItem> filteredItems = new();
    private readonly List<AppCatalogCardControl> catalogCards = new();
    private readonly HashSet<AppCatalogCardControl> activePreviewCards = new();
    private readonly Dictionary<string, ModifierControlBinding> modifierBindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<AutoSuggestBox, CancellationTokenSource> citySuggestCts = new();
    private readonly Dictionary<AutoSuggestBox, Dictionary<string, CitySuggestion>> citySuggestionLookup = new();

    private AppCatalogItem? selectedItem;
    private DeviceOperationsState currentState = new();
    private ScrollViewer? catalogScrollViewer;

    public AppsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private DeviceOperationsCoordinator? DeviceOps => App.DeviceOps;
    private AppCatalogService? CatalogService => App.AppCatalog;
    private AppDeploymentService? DeploymentService => App.AppDeployment;
    private AppModifierStateStore? ModifierStore => App.AppModifierStore;
    private CityAutocompleteService? CityService => App.CityAutocomplete;

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is not null)
        {
            DeviceOps.StateChanged += OnDeviceOpsStateChanged;
            currentState = DeviceOps.GetStateSnapshot();
            ApplyDevices(currentState.DeviceListSnapshot);
            ApplyState(currentState);
        }

        CatalogGrid.SizeChanged += OnCatalogViewportChanged;
        EnsureCatalogScrollViewer();
        await LoadCatalogAsync().ConfigureAwait(false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DeviceOps is not null)
        {
            DeviceOps.StateChanged -= OnDeviceOpsStateChanged;
        }

        CatalogGrid.SizeChanged -= OnCatalogViewportChanged;
        if (catalogScrollViewer is not null)
        {
            catalogScrollViewer.ViewChanged -= OnCatalogScrollViewChanged;
        }

        foreach (var pending in citySuggestCts.Values)
        {
            pending.Cancel();
            pending.Dispose();
        }

        citySuggestCts.Clear();
        citySuggestionLookup.Clear();
        foreach (var card in catalogCards)
        {
            card.Preview.Stop();
        }

        activePreviewCards.Clear();
    }

    // DOCS: docs/wiki/guides/add-app-catalog-item.md#passos
    private async Task LoadCatalogAsync()
    {
        var service = CatalogService;
        if (service is null)
        {
            AppendLog("Serviço de catálogo indisponível.");
            return;
        }

        try
        {
            var catalog = await service.LoadCatalogAsync().ConfigureAwait(false);
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                allItems.Clear();
                allItems.AddRange(catalog);
                ApplyFilter();
                AppendLog($"Catálogo carregado: {allItems.Count} apps.");
            });
        }
        catch (Exception ex)
        {
            _ = DispatcherQueue.TryEnqueue(() => AppendLog($"Falha ao carregar catálogo: {ex.Message}"));
        }
    }

    private void OnDeviceOpsStateChanged(object? sender, EventArgs e)
    {
        var ops = DeviceOps;
        if (ops is null)
        {
            return;
        }

        var state = ops.GetStateSnapshot();
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            currentState = state;
            ApplyState(state);
            ApplyDevices(state.DeviceListSnapshot);
        });
    }

    private void ApplyState(DeviceOperationsState state)
    {
        OperationProgressRing.IsActive = state.CommandInProgress;
        OperationProgressRing.Visibility = state.CommandInProgress ? Visibility.Visible : Visibility.Collapsed;
        OperationStatusText.Text = state.CommandStatus;
        OperationPercentText.Text = $"{Math.Clamp(state.CommandPercent, 0, 100)}%";
        UpdateActionButtonsEnabled();
    }

    private void ApplyDevices(IReadOnlyList<DeviceSnapshot> devices)
    {
        var currentSelection = TargetDeviceCombo.SelectedItem as ComboBoxItem;
        var selectedId = currentSelection?.Tag as string;

        TargetDeviceCombo.Items.Clear();
        foreach (var device in devices.Where(static d => d.Status == DeviceStatus.Online))
        {
            TargetDeviceCombo.Items.Add(new ComboBoxItem
            {
                Content = $"{device.Name} ({device.DeviceId})",
                Tag = device.DeviceId,
            });
        }

        if (!string.IsNullOrWhiteSpace(selectedId))
        {
            foreach (var item in TargetDeviceCombo.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag as string, selectedId, StringComparison.OrdinalIgnoreCase))
                {
                    TargetDeviceCombo.SelectedItem = item;
                    break;
                }
            }
        }

        UpdateActionButtonsEnabled();
        _ = RefreshPreviewDraftsAsync();
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        filteredItems.Clear();

        IEnumerable<AppCatalogItem> source = allItems;
        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(item =>
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                
                || item.Summary.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        filteredItems.AddRange(source);
        RebuildCatalogCards();
    }
    private void RebuildCatalogCards()
    {
        foreach (var card in catalogCards)
        {
            card.Preview.Stop();
        }

        catalogCards.Clear();
        activePreviewCards.Clear();
        CatalogGrid.Items.Clear();

        foreach (var item in filteredItems)
        {
            var card = new AppCatalogCardControl(item);
            catalogCards.Add(card);
            CatalogGrid.Items.Add(card);
        }

        EnsureCatalogScrollViewer();

        if (selectedItem is not null)
        {
            var card = catalogCards.FirstOrDefault(c => string.Equals(c.Item.Id, selectedItem.Id, StringComparison.OrdinalIgnoreCase));
            if (card is not null)
            {
                CatalogGrid.SelectedItem = card;
                SetSelectedItem(card.Item);
            }
            else if (filteredItems.Count == 0)
            {
                ClearSelectedItem();
            }
        }
        else if (catalogCards.Count > 0)
        {
            CatalogGrid.SelectedItem = catalogCards[0];
            SetSelectedItem(catalogCards[0].Item);
        }
        else
        {
            ClearSelectedItem();
        }

        UpdateSelectionVisuals();
        RefreshPreviewPlayback();
        _ = RefreshPreviewDraftsAsync();
    }

    private void OnCatalogItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is not AppCatalogCardControl card)
        {
            return;
        }

        CatalogGrid.SelectedItem = card;
        SetSelectedItem(card.Item);
    }

    private void OnCatalogSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CatalogGrid.SelectedItem is AppCatalogCardControl card)
        {
            SetSelectedItem(card.Item);
            return;
        }

        if (selectedItem is not null)
        {
            UpdateSelectionVisuals();
        }
    }

    private void SetSelectedItem(AppCatalogItem item)
    {
        selectedItem = item;
        SelectedAppNameText.Text = item.Name;
        SelectedAppMetaText.Text = $"{item.Category} | Intervalo recomendado: {item.RecommendedIntervalMinutes} min";
        SelectedAppDescriptionText.Text = item.Description;

        _ = LoadModifierEditorAsync();
        UpdateSelectionVisuals();
        RefreshPreviewPlayback();
        UpdateActionButtonsEnabled();
    }

    private void ClearSelectedItem()
    {
        selectedItem = null;
        SelectedAppNameText.Text = "Selecione um app";
        SelectedAppMetaText.Text = "-";
        SelectedAppDescriptionText.Text = "Nenhum app selecionado.";
        ModifiersHintText.Text = "Selecione um app e um dispositivo para editar modificadores.";
        ModifiersPanel.Children.Clear();
        modifierBindings.Clear();
        UpdateSelectionVisuals();
        RefreshPreviewPlayback();
        UpdateActionButtonsEnabled();
    }

    private void UpdateSelectionVisuals()
    {
        var selectedId = selectedItem?.Id;
        foreach (var card in catalogCards)
        {
            card.SetSelectedVisual(string.Equals(card.Item.Id, selectedId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private void EnsureCatalogScrollViewer()
    {
        if (catalogScrollViewer is not null)
        {
            return;
        }

        catalogScrollViewer = FindDescendant<ScrollViewer>(CatalogGrid);
        if (catalogScrollViewer is null)
        {
            return;
        }

        catalogScrollViewer.ViewChanged += OnCatalogScrollViewChanged;
    }

    private void OnCatalogViewportChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshPreviewPlayback();
    }

    private void OnCatalogScrollViewChanged(object? sender, ScrollViewerViewChangedEventArgs e)
    {
        RefreshPreviewPlayback();
    }

    private void RefreshPreviewPlayback()
    {
        if (catalogCards.Count == 0)
        {
            return;
        }

        var shouldRun = new HashSet<AppCatalogCardControl>();

        if (catalogScrollViewer is not null)
        {
            var viewportTop = catalogScrollViewer.VerticalOffset;
            var viewportBottom = viewportTop + catalogScrollViewer.ViewportHeight;
            var contentElement = catalogScrollViewer.Content as UIElement;

            foreach (var card in catalogCards)
            {
                if (contentElement is null || card.ActualWidth <= 0 || card.ActualHeight <= 0)
                {
                    continue;
                }

                var transform = card.TransformToVisual(contentElement);
                var bounds = transform.TransformBounds(new Rect(0, 0, card.ActualWidth, card.ActualHeight));
                if (bounds.Bottom >= viewportTop && bounds.Top <= viewportBottom)
                {
                    shouldRun.Add(card);
                    if (shouldRun.Count >= 6)
                    {
                        break;
                    }
                }
            }
        }

        if (shouldRun.Count == 0)
        {
            foreach (var card in catalogCards.Take(6))
            {
                shouldRun.Add(card);
            }
        }

        if (selectedItem is not null)
        {
            var selectedCard = catalogCards.FirstOrDefault(c => string.Equals(c.Item.Id, selectedItem.Id, StringComparison.OrdinalIgnoreCase));
            if (selectedCard is not null)
            {
                shouldRun.Add(selectedCard);
            }
        }

        foreach (var card in catalogCards)
        {
            if (shouldRun.Contains(card))
            {
                if (activePreviewCards.Add(card))
                {
                    card.Preview.Start();
                }
            }
            else if (activePreviewCards.Remove(card))
            {
                card.Preview.Stop();
            }
        }
    }
    private async void OnInstallClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelection(out var deviceId, out var item, out var error))
        {
            AppendLog(error);
            return;
        }

        var deployment = DeploymentService;
        if (deployment is null)
        {
            AppendLog("Serviço de deploy indisponível.");
            return;
        }

        if (!TryBuildConfigFromEditor(item, out var values, out var rawValues, out var validationError))
        {
            AppendLog(validationError);
            return;
        }

        var configJson = JsonSerializer.Serialize(values);
        var store = ModifierStore;
        if (store is not null)
        {
            await store.SetDraftAsync(LocalDraftScope, item.Id, new AppConfigDraft { Values = rawValues }).ConfigureAwait(false);
        }

        var result = await deployment.InstallAsync(deviceId, item, configJson).ConfigureAwait(false);
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ApplyPreviewDraftToCard(item.Id, rawValues);
            AppendLog(RenderResult("instalar", item.Name, result));
        });
    }

    
    private async void OnSaveModifiersClicked(object sender, RoutedEventArgs e)
    {
        if (!TryGetSelectedItem(out var item, out var error))
        {
            AppendLog(error);
            return;
        }

        if (!TryBuildConfigFromEditor(item, out _, out var rawValues, out var validationError))
        {
            AppendLog(validationError);
            return;
        }

        var store = ModifierStore;
        if (store is null)
        {
            AppendLog("Repositório de modificadores indisponível.");
            return;
        }

        await store.SetDraftAsync(LocalDraftScope, item.Id, new AppConfigDraft { Values = rawValues }).ConfigureAwait(false);
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ApplyPreviewDraftToCard(item.Id, rawValues);
            AppendLog($"Modificações salvas localmente para {item.Name}.");
        });
    }

    
    
    private void OnTargetDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateActionButtonsEnabled();
    }

    private async Task LoadModifierEditorAsync()
    {
        var item = selectedItem;

        ModifiersPanel.Children.Clear();
        modifierBindings.Clear();

        if (item is null)
        {
            ModifiersHintText.Text = "Selecione um app para editar modificadores.";
            return;
        }

        var modifiers = item.Modifiers.Where(static modifier => modifier.IsValid()).ToArray();
        if (modifiers.Length == 0)
        {
            ModifiersHintText.Text = "Este app não possui modificadores configuráveis.";
            ModifiersPanel.Children.Add(new TextBlock
            {
                Text = "Sem parâmetros adicionais.",
                Opacity = 0.8,
            });
            UpdateActionButtonsEnabled();
            return;
        }

        IReadOnlyDictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (ModifierStore is not null)
        {
            var draft = await ModifierStore.GetDraftAsync(LocalDraftScope, item.Id).ConfigureAwait(false);
            if (draft is not null)
            {
                values = draft.Values;
            }
        }

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

            ModifiersPanel.Children.Add(control);
        }

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
                return combo;
            case AppModifierFieldType.CityAutocomplete:
                ParseCityConfig(initialValue, out var cityDisplay, out var citySuggestion);
                var suggest = new AutoSuggestBox
                {
                    PlaceholderText = definition.Placeholder ?? "Digite a cidade",
                    Text = cityDisplay,
                };
                if (citySuggestion is not null)
                {
                    suggest.Tag = citySuggestion;
                }

                suggest.TextChanged += OnCitySuggestTextChanged;
                suggest.SuggestionChosen += OnCitySuggestionChosen;
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
        citySuggestionLookup.Remove(sender);

        var query = sender.Text.Trim();
        if (query.Length < 2 || CityService is null)
        {
            sender.ItemsSource = null;
            return;
        }

        if (citySuggestCts.Remove(sender, out var previous))
        {
            previous.Cancel();
            previous.Dispose();
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        citySuggestCts[sender] = cts;

        try
        {
            var suggestions = await CityService.SearchAsync(query, cts.Token).ConfigureAwait(false);
            var lookup = new Dictionary<string, CitySuggestion>(StringComparer.OrdinalIgnoreCase);
            foreach (var suggestion in suggestions)
            {
                if (!lookup.ContainsKey(suggestion.DisplayName))
                {
                    lookup[suggestion.DisplayName] = suggestion;
                }
            }

            var displayItems = lookup.Keys.ToArray();
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                citySuggestionLookup[sender] = lookup;
                sender.ItemsSource = displayItems;
            });
        }
        catch
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                citySuggestionLookup.Remove(sender);
                sender.ItemsSource = null;
            });
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
        if (citySuggestionLookup.TryGetValue(sender, out var lookup)
            && lookup.TryGetValue(selectedText, out var mappedSuggestion))
        {
            sender.Tag = mappedSuggestion;
            return;
        }

        ParseCityConfig(selectedText, out _, out var parsedSuggestion);
        sender.Tag = parsedSuggestion;
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
        card?.SetPreviewConfig(values);
    }

    private async Task RefreshPreviewDraftsAsync()
    {
        var store = ModifierStore;

        if (store is null)
        {
            _ = DispatcherQueue.TryEnqueue(() =>
            {
                foreach (var card in catalogCards)
                {
                    card.SetPreviewConfig(null);
                }
            });

            return;
        }

        var perAppValues = new Dictionary<string, IReadOnlyDictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in catalogCards)
        {
            var draft = await store.GetDraftAsync(LocalDraftScope, card.Item.Id).ConfigureAwait(false);
            perAppValues[card.Item.Id] = draft?.Values;
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            foreach (var card in catalogCards)
            {
                card.SetPreviewConfig(perAppValues.TryGetValue(card.Item.Id, out var values) ? values : null);
            }
        });
    }

    private bool TryGetSelection(out string deviceId, out AppCatalogItem item, out string error)
    {
        deviceId = string.Empty;
        item = selectedItem ?? new AppCatalogItem();

        if (selectedItem is null)
        {
            error = "Selecione um app antes de continuar.";
            return false;
        }

        if (TargetDeviceCombo.SelectedItem is not ComboBoxItem selectedDevice || selectedDevice.Tag is not string selectedDeviceId)
        {
            error = "Selecione um dispositivo online.";
            return false;
        }

        deviceId = selectedDeviceId;
        item = selectedItem;
        error = string.Empty;
        return true;
    }

    private bool TryGetSelectedItem(out AppCatalogItem item, out string error)
    {
        if (selectedItem is null)
        {
            item = new AppCatalogItem();
            error = "Selecione um app antes de continuar.";
            return false;
        }

        item = selectedItem;
        error = string.Empty;
        return true;
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

    private void UpdateActionButtonsEnabled()
    {
        var hasSelection = selectedItem is not null;
        var hasDevice = TargetDeviceCombo.SelectedItem is ComboBoxItem;
        var busy = currentState.CommandInProgress;

        InstallButton.IsEnabled = hasSelection && hasDevice && !busy;
        SaveModifiersButton.IsEnabled = hasSelection;
    }
    private static bool TryParseDouble(string? value, out double result) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) return match;
            var nested = FindDescendant<T>(child);
            if (nested is not null) return nested;
        }

        return null;
    }

    private static string RenderResult(string verb, string appName, CommandDispatchResult result)
    {
        return result.Success
            ? $"App '{appName}' {verb} com sucesso em {result.DeviceId}."
            : $"Falha ao {verb} '{appName}' em {result.DeviceId}: {result.Message ?? result.ErrorCode ?? "erro"}.";
    }

    private async void OnReloadCatalogClicked(object sender, RoutedEventArgs e)
    {
        await LoadCatalogAsync().ConfigureAwait(false);
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        OperationStatusText.Text = $"Operações: {message}";
        if (!currentState.CommandInProgress)
        {
            OperationPercentText.Text = "0%";
        }
    }

    private sealed class ModifierControlBinding
    {
        public ModifierControlBinding(AppModifierDefinition definition, FrameworkElement control)
        {
            Definition = definition;
            Control = control;
        }

        public AppModifierDefinition Definition { get; }
        public FrameworkElement Control { get; }
    }
}






















