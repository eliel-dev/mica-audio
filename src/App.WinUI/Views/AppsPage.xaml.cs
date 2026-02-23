
using System.Globalization;
using System.Text.Json;
using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Gif;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI;
using WinRT.Interop;

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
    private readonly GifCatalogAppRuntimeService gifRuntimeService;
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly IAppCatalogService catalogService;
    private readonly IAppDeploymentService deploymentService;
    private readonly IAppModifierStateStore modifierStore;
    private readonly CityAutocompleteService cityService;

    private AppCatalogItem? selectedItem;
    private DeviceOperationsState currentState = new();
    private ScrollViewer? catalogScrollViewer;
    private bool pendingRuntimeAutostart;
    private bool catalogReloadInProgress;
    private RgbaColor[] gifPreviewFrame = Enumerable.Repeat(new RgbaColor(0, 0, 0, 255), LedDefaults.MatrixWidth * LedDefaults.MatrixHeight).ToArray();
    private string lastRuntimeStatus = "Selecione um app com runtime local para iniciar.";
    private readonly AppRuntimeProviderRegistry runtimeProviderRegistry;
    private IAppRuntimeProvider? activeRuntimeProvider;

    internal AppsPage(
        DeviceOperationsCoordinator deviceOps,
        IAppCatalogService catalogService,
        IAppDeploymentService deploymentService,
        IAppModifierStateStore modifierStore,
        CityAutocompleteService cityService,
        DeviceIntegrationService deviceIntegration)
    {
        this.deviceOps = deviceOps;
        this.catalogService = catalogService;
        this.deploymentService = deploymentService;
        this.modifierStore = modifierStore;
        this.cityService = cityService;

        var host = deviceIntegration.Host;
        gifRuntimeService = new GifCatalogAppRuntimeService(
            matrixOutput: new MatrixPortalLedOutput(host),
            simulatorOutput: new SimulatorLedOutput(),
            decoder: new Hub75GifDecoder(Hub75GifDecoder.DefaultMaxGifFrames),
            formatter: new Hub75FrameFormatter(),
            player: new Hub75GifPlayer(TimeSpan.FromMilliseconds(1000d / GifCatalogAppRuntimeService.TargetFps)));
        runtimeProviderRegistry = new AppRuntimeProviderRegistry([new GifHub75RuntimeProvider()]);

        InitializeComponent();
        AttachRuntimeProviders();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        deviceOps.StateChanged += OnDeviceOpsStateChanged;
        currentState = deviceOps.GetStateSnapshot();
        ApplyDevices(currentState.DeviceListSnapshot);
        ApplyState(currentState);

        CatalogGrid.SizeChanged += OnCatalogViewportChanged;
        EnsureCatalogScrollViewer();
UpdateRuntimePanel();
        await ReloadCatalogFromDiskAsync().ConfigureAwait(false);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        deviceOps.StateChanged -= OnDeviceOpsStateChanged;

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
runtimeProviderRegistry.Dispose();
    }

    // DOCS: docs/wiki/guides/add-app-catalog-item.md#passos
    private async Task LoadCatalogAsync()
    {
        var service = catalogService;

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
        var state = deviceOps.GetStateSnapshot();
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

        pendingRuntimeAutostart = runtimeProviderRegistry.Resolve(card.Item) is not null;
        if (ReferenceEquals(CatalogGrid.SelectedItem, card))
        {
            SetSelectedItem(card.Item);
            return;
        }

        CatalogGrid.SelectedItem = card;
    }

    public async Task ReloadCatalogFromDiskAsync()
    {
        if (catalogReloadInProgress)
        {
            return;
        }

        catalogReloadInProgress = true;
        try
        {
            await LoadCatalogAsync().ConfigureAwait(false);
        }
        finally
        {
            catalogReloadInProgress = false;
        }
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
        var previousProvider = activeRuntimeProvider;
        selectedItem = item;
        activeRuntimeProvider = runtimeProviderRegistry.Resolve(item);

        SelectedAppNameText.Text = item.Name;
        SelectedAppMetaText.Text = $"{item.Category} | Intervalo recomendado: {item.RecommendedIntervalMinutes} min";
        SelectedAppDescriptionText.Text = item.Description;

        _ = LoadModifierEditorAsync();

        if (previousProvider is not null && !ReferenceEquals(previousProvider, activeRuntimeProvider))
        {
            previousProvider.OnDeselected(item);
        }

        _ = EvaluateRuntimeAutostartAsync();

        UpdateRuntimePanel();
        UpdateSelectionVisuals();
        RefreshPreviewPlayback();
        UpdateActionButtonsEnabled();
    }

    private void ClearSelectedItem()
    {
        var previousProvider = activeRuntimeProvider;
        selectedItem = null;
        activeRuntimeProvider = null;

        SelectedAppNameText.Text = "Selecione um app";
        SelectedAppMetaText.Text = "-";
        SelectedAppDescriptionText.Text = "Nenhum app selecionado.";
        ModifiersHintText.Text = "Selecione um app e um dispositivo para editar modificadores.";
        ModifiersPanel.Children.Clear();
        modifierBindings.Clear();

        previousProvider?.OnDeselected(new AppCatalogItem());

        UpdateRuntimePanel();
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

        var deployment = deploymentService;

        if (!TryBuildConfigFromEditor(item, out var values, out var rawValues, out var validationError))
        {
            AppendLog(validationError);
            return;
        }

        var configJson = JsonSerializer.Serialize(values);
        await modifierStore.SetDraftAsync(LocalDraftScope, item.Id, new AppConfigDraft { Values = rawValues }).ConfigureAwait(false);

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

        await modifierStore.SetDraftAsync(LocalDraftScope, item.Id, new AppConfigDraft { Values = rawValues }).ConfigureAwait(false);
        if (activeRuntimeProvider is not null && selectedItem is not null)
        {
            await activeRuntimeProvider.OnConfigSavedAsync(selectedItem, rawValues, CancellationToken.None).ConfigureAwait(false);
        }

        _ = DispatcherQueue.TryEnqueue(() =>
        {
            ApplyPreviewDraftToCard(item.Id, rawValues);
            AppendLog($"Modificações salvas localmente para {item.Name}.");
        });
    }

    private void UpdateRuntimePanel()
    {
        GifRuntimeStatusText.Text = lastRuntimeStatus;
        GifRuntimePanel.Visibility = activeRuntimeProvider is null ? Visibility.Collapsed : Visibility.Visible;
        GifRuntimeCanvas.Invalidate();
    }

    private void SetRuntimeStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return;
        }

        lastRuntimeStatus = status;
        _ = DispatcherQueue.TryEnqueue(() => GifRuntimeStatusText.Text = status);
    }

    private async Task EvaluateRuntimeAutostartAsync()
    {
        if (activeRuntimeProvider is null || selectedItem is null)
        {
            pendingRuntimeAutostart = false;
            return;
        }

        activeRuntimeProvider.OnSelected(selectedItem);
        if (!pendingRuntimeAutostart)
        {
            return;
        }

        pendingRuntimeAutostart = false;
        var values = await ResolveRuntimeValuesAsync().ConfigureAwait(false);
        await activeRuntimeProvider.OnConfigSavedAsync(selectedItem, values, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<IReadOnlyDictionary<string, string>> ResolveRuntimeValuesAsync()
    {
        var item = selectedItem;
        if (item is null)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (TryBuildConfigFromEditor(item, out _, out var rawValues, out _))
        {
            return rawValues;
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in item.Modifiers.Where(static modifier => modifier.IsValid()))
        {
            values[modifier.Key] = modifier.Type == AppModifierFieldType.Toggle
                ? (modifier.DefaultToggle is true ? "true" : "false")
                : (modifier.DefaultValue ?? string.Empty);
        }

        var draft = await modifierStore.GetDraftAsync(LocalDraftScope, item.Id).ConfigureAwait(false);
        if (draft?.Values is not null)
        {
            foreach (var pair in draft.Values)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return values;
    }

    private void AttachRuntimeProviders()
    {
        var host = new AppRuntimeHost
        {
            RuntimePanel = GifRuntimePanel,
            RuntimeStatusText = GifRuntimeStatusText,
            OpenFileButton = GifOpenFileButton,
            FirmwareWarningText = GifFirmwareWarningText,
            RuntimeCanvas = GifRuntimeCanvas,
            DispatcherQueue = DispatcherQueue,
            GifRuntimeService = gifRuntimeService,
            PickGifFileAsync = PickGifFileAsync,
            ResolveScaleMode = () => selectedItem is null || !TryBuildConfigFromEditor(selectedItem, out _, out var values, out _) ? GifScaleMode.Fit : ParseGifScaleMode(values.TryGetValue("scaleMode", out var mode) ? mode : null),
            ResolveCurrentValuesAsync = ResolveRuntimeValuesAsync,
            PersistCurrentValuesAsync = () => Task.CompletedTask,
            UpdateFrame = frame => gifPreviewFrame = frame,
            SetStatus = SetRuntimeStatus,
        };

        foreach (var provider in runtimeProviderRegistry.Providers)
        {
            provider.Attach(host);
        }
    }

    private static GifScaleMode ParseGifScaleMode(string? raw)
    {
        return raw?.Trim().ToLowerInvariant() switch
        {
            "fill" => GifScaleMode.Fill,
            "stretch" => GifScaleMode.Stretch,
            _ => GifScaleMode.Fit,
        };
    }

    private static async Task<StorageFile?> PickGifFileAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
        };

        picker.FileTypeFilter.Add(".gif");

        if (App.MainWindow is not null)
        {
            var hwnd = WindowNative.GetWindowHandle(App.MainWindow);
            InitializeWithWindow.Initialize(picker, hwnd);
        }

        return await picker.PickSingleFileAsync();
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
        var draft = await modifierStore.GetDraftAsync(LocalDraftScope, item.Id).ConfigureAwait(false);
        if (draft is not null)
        {
            values = draft.Values;
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
        if (query.Length < 2)
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
            var suggestions = await cityService.SearchAsync(query, cts.Token).ConfigureAwait(false);
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
        var perAppValues = new Dictionary<string, IReadOnlyDictionary<string, string>?>(StringComparer.OrdinalIgnoreCase);
        foreach (var card in catalogCards)
        {
            var draft = await modifierStore.GetDraftAsync(LocalDraftScope, card.Item.Id).ConfigureAwait(false);
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

    private void OnGifRuntimeCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var drawWidth = (float)Math.Max(1d, sender.ActualWidth);
        var drawHeight = (float)Math.Max(1d, sender.ActualHeight);

        var ds = args.DrawingSession;
        ds.Clear(Color.FromArgb(255, 5, 8, 12));

        var frame = gifPreviewFrame;
        if (frame.Length != LedDefaults.MatrixWidth * LedDefaults.MatrixHeight)
        {
            return;
        }

        var pitch = MathF.Min((drawWidth - 10f) / LedDefaults.MatrixWidth, (drawHeight - 10f) / LedDefaults.MatrixHeight);
        pitch = MathF.Max(1f, pitch);
        var ledSize = MathF.Max(1f, pitch * 0.82f);
        var panelWidth = LedDefaults.MatrixWidth * pitch;
        var panelHeight = LedDefaults.MatrixHeight * pitch;
        var ox = (drawWidth - panelWidth) * 0.5f;
        var oy = (drawHeight - panelHeight) * 0.5f;

        ds.FillRoundedRectangle(ox - 2f, oy - 2f, panelWidth + 4f, panelHeight + 4f, 3f, 3f, Color.FromArgb(255, 8, 12, 18));
        ds.DrawRoundedRectangle(ox - 1f, oy - 1f, panelWidth + 2f, panelHeight + 2f, 2f, 2f, Color.FromArgb(255, 35, 48, 62), 1f);

        for (var y = 0; y < LedDefaults.MatrixHeight; y++)
        {
            for (var x = 0; x < LedDefaults.MatrixWidth; x++)
            {
                var px = frame[(y * LedDefaults.MatrixWidth) + x];
                if (px.A == 0)
                {
                    continue;
                }

                var left = ox + (x * pitch) + ((pitch - ledSize) * 0.5f);
                var top = oy + (y * pitch) + ((pitch - ledSize) * 0.5f);
                var color = Color.FromArgb(px.A, px.R, px.G, px.B);
                ds.FillRectangle(left, top, ledSize, ledSize, color);
            }
        }
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
        await ReloadCatalogFromDiskAsync().ConfigureAwait(false);
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
























