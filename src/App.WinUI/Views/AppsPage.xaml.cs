using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Apps.UseCases;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Gif;
using App.WinUI.ViewModels;
using App.WinUI.Views.Controls;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#modulo-apps-catalog-and-deployment
// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03---fase-9-wave-2-e-wave-3-monolitos-do-app-decompostos
public sealed partial class AppsPage : Page, IDisposable
{
    private const string LocalDraftScope = "__local__";
    private readonly List<AppCatalogItem> allItems = new();
    private readonly List<AppCatalogItem> filteredItems = new();
    private readonly List<AppCatalogCardControl> catalogCards = new();
    private readonly HashSet<AppCatalogCardControl> activePreviewCards = new();
    private readonly Dictionary<string, ModifierControlBinding> modifierBindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<AutoSuggestBox, CancellationTokenSource> citySuggestCts = new();
    private readonly Dictionary<AutoSuggestBox, Dictionary<string, CitySuggestion>> citySuggestionLookup = new();
    private readonly AppsPageViewModel viewModel;
    private readonly GifCatalogAppRuntimeService gifRuntimeService;
    private readonly DeviceOperationsCoordinator deviceOps;
    private readonly IAppCatalogService catalogService;
    private readonly IAppModifierStateStore modifierStore;
    private readonly CityAutocompleteService cityService;
    private readonly SaveAppConfigUseCase saveAppConfigUseCase;
    private readonly DeployAppUseCase deployAppUseCase;
    private readonly AppConfigValidationUseCase appConfigValidationUseCase;

    private AppCatalogItem? selectedItem;
    private DeviceOperationsState currentState = new();
    private ScrollViewer? catalogScrollViewer;
    private const string GifAppId = "gifhub75";
    private bool pendingRuntimeAutostart;
    private bool catalogReloadInProgress;
    private RgbaColor[]? latestGifRuntimeFrame;
    private readonly GifHub75RuntimeProvider gifRuntimeProvider;
    private readonly AppRuntimeProviderRegistry runtimeProviderRegistry;
    private IAppRuntimeProvider? activeRuntimeProvider;

    internal AppsPage(
        AppsPageViewModel viewModel,
        DeviceOperationsCoordinator deviceOps,
        IAppCatalogService catalogService,
        IAppModifierStateStore modifierStore,
        CityAutocompleteService cityService,
        SaveAppConfigUseCase saveAppConfigUseCase,
        DeployAppUseCase deployAppUseCase,
        AppConfigValidationUseCase appConfigValidationUseCase,
        DeviceIntegrationService deviceIntegration)
    {
        this.viewModel = viewModel;
        this.deviceOps = deviceOps;
        this.catalogService = catalogService;
        this.modifierStore = modifierStore;
        this.cityService = cityService;
        this.saveAppConfigUseCase = saveAppConfigUseCase;
        this.deployAppUseCase = deployAppUseCase;
        this.appConfigValidationUseCase = appConfigValidationUseCase;

        var host = deviceIntegration.Host;
        gifRuntimeService = new GifCatalogAppRuntimeService(
            matrixOutput: new Esp32S3LedOutput(host),
            simulatorOutput: new SimulatorLedOutput(),
            decoder: new Hub75GifDecoder(Hub75GifDecoder.DefaultMaxGifFrames),
            formatter: new Hub75FrameFormatter(),
            player: new Hub75GifPlayer(TimeSpan.FromMilliseconds(1000d / GifCatalogAppRuntimeService.TargetFps)));
        gifRuntimeProvider = new GifHub75RuntimeProvider();
        runtimeProviderRegistry = new AppRuntimeProviderRegistry([gifRuntimeProvider]);

        this.viewModel.ConfigureCommands(ReloadCatalogFromDiskAsyncCommandAsync, SaveSelectedModifierDraftAsync, InstallSelectedAppAsync);

        InitializeComponent();
        DataContext = viewModel;
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
        await ReloadCatalogFromDiskAsync().ConfigureAwait(false);
        await EnsureGifRuntimeWhileAppsPageIsVisibleAsync().ConfigureAwait(false);
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

    public void Dispose()
    {
        runtimeProviderRegistry.Dispose();
        gifRuntimeService.Dispose();
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
