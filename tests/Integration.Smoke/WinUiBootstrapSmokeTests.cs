using System.Reflection;
using App.WinUI;
using App.WinUI.Infrastructure.Serial;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Services.Panels;
using App.WinUI.Services.Visualizer;
using App.WinUI.ViewModels;
using App.WinUI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;
using MicaAudio.Core.Config;

namespace Integration.Smoke;

public sealed class WinUiBootstrapSmokeTests
{
    [Fact]
    public void BuildServiceProvider_ShouldResolveCoreAppServices()
    {
        var provider = App.WinUI.App.BuildServiceProvider();

        Assert.NotNull(provider.GetService<DeviceIntegrationService>());
        Assert.NotNull(provider.GetService<DeviceOperationsCoordinator>());
        Assert.NotNull(provider.GetService<IAppCatalogService>());
        Assert.NotNull(provider.GetService<IAppModifierStateStore>());
        Assert.NotNull(provider.GetService<ISerialMonitorService>());
        Assert.NotNull(provider.GetService<MainPageViewModel>());
        Assert.NotNull(provider.GetService<DevicesPageViewModel>());
        Assert.NotNull(provider.GetService<PanelsPageViewModel>());
        Assert.NotNull(provider.GetService<ShellPageViewModel>());
        Assert.NotNull(provider.GetService<ShellPageContentFactory>());
        Assert.NotNull(provider.GetService<PanelsStore>());
        Assert.NotNull(provider.GetService<PanelsFrameComposer>());
        Assert.NotNull(provider.GetService<PanelsPlaybackService>());
        Assert.NotNull(provider.GetService<PanelsDeviceSessionService>());
        Assert.NotNull(provider.GetService<VisualizerEditorNavigationCoordinator>());
        Assert.NotNull(provider.GetService<BuiltInPresetNameOverrideStore>());
    }

    [Fact]
    public void BuildServiceProvider_ShouldEnableMatrixTransportForPanelsPlaybackService()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var service = provider.GetRequiredService<PanelsPlaybackService>();

        var field = typeof(PanelsPlaybackService).GetField(
            "enableMatrixTransport",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        Assert.True(Assert.IsType<bool>(field!.GetValue(service)));
    }

    [Fact]
    public void BuildServiceProvider_ShouldRegisterShellAndPages()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var isService = provider.GetRequiredService<IServiceProviderIsService>();

        Assert.True(isService.IsService(typeof(MainPage)));
        Assert.True(isService.IsService(typeof(DevicesPage)));
        Assert.True(isService.IsService(typeof(PanelsPage)));
        Assert.True(isService.IsService(typeof(SettingsPage)));
        Assert.True(isService.IsService(typeof(VisualizerStudioPage)));
        Assert.True(isService.IsService(typeof(ShellPage)));
        Assert.True(isService.IsService(typeof(ShellPageContentFactory)));
    }

    [Fact]
    public void BuildServiceProvider_ShouldRegisterDependenciesRequiredByStartupPages()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var isService = provider.GetRequiredService<IServiceProviderIsService>();

        Assert.True(isService.IsService(typeof(MainPageViewModel)));
        Assert.True(isService.IsService(typeof(DevicesPageViewModel)));
        Assert.True(isService.IsService(typeof(PanelsPageViewModel)));
        Assert.True(isService.IsService(typeof(ShellPageViewModel)));
        Assert.True(isService.IsService(typeof(PresetRepository)));
        Assert.True(isService.IsService(typeof(SettingsRepository)));
        Assert.True(isService.IsService(typeof(AppSettingsDomainService)));
        Assert.True(isService.IsService(typeof(VisualizerEditorNavigationCoordinator)));
        Assert.True(isService.IsService(typeof(BuiltInPresetNameOverrideStore)));
    }

    [Fact]
    public void VisualizerStudioPage_ShouldAllowConstruction_BeforeSessionIsLoaded()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var exception = Record.Exception(() => provider.GetRequiredService<VisualizerStudioPage>());
        Assert.Null(exception);
    }

    [Fact]
    public void VisualizerStudioPage_ShouldOwnVerticalScrollAtPageLevel()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var page = provider.GetRequiredService<VisualizerStudioPage>();

        var root = Assert.IsType<Grid>(page.Content);
        Assert.Equal(2, root.Children.Count);
        Assert.IsType<Border>(root.Children[0]);

        var contentScrollViewer = Assert.IsType<ScrollViewer>(root.Children[1]);
        Assert.NotNull(contentScrollViewer.Content);
    }

    [Fact]
    public void StartupPages_ShouldNotExposeServiceLocatorConstructors()
    {
        AssertNoServiceProviderConstructor(typeof(ShellPage));
        AssertNoServiceProviderConstructor(typeof(MainPage));
        AssertNoServiceProviderConstructor(typeof(DevicesPage));
        AssertNoServiceProviderConstructor(typeof(PanelsPage));
    }

    [Fact]
    public void BuildServiceProvider_ShouldPopulateMicaAudioOptions()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<MicaAudioOptions>>().Value;

        Assert.False(string.IsNullOrWhiteSpace(options.AppDataRoot));
        Assert.False(string.IsNullOrWhiteSpace(options.DevicesFilePath));
        Assert.False(string.IsNullOrWhiteSpace(options.SettingsFilePath));
        Assert.False(string.IsNullOrWhiteSpace(options.PresetsDirectory));
        Assert.False(string.IsNullOrWhiteSpace(options.AppsCatalogPath));
        Assert.False(string.IsNullOrWhiteSpace(options.AppsModifierStatePath));
        Assert.False(string.IsNullOrWhiteSpace(options.PanelsFilePath));
        Assert.False(string.IsNullOrWhiteSpace(options.CrashLogPath));
    }

    private static void AssertNoServiceProviderConstructor(Type pageType)
    {
        var constructors = pageType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var hasServiceProviderCtor = constructors.Any(ctor =>
        {
            var parameters = ctor.GetParameters();
            return parameters.Length == 1 && parameters[0].ParameterType == typeof(IServiceProvider);
        });

        Assert.False(hasServiceProviderCtor, $"Service locator constructor not allowed: {pageType.FullName}");
    }
}
