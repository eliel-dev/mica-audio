using System.Reflection;
using App.WinUI;
using App.WinUI.Services;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Apps.UseCases;
using App.WinUI.Services.Devices;
using App.WinUI.ViewModels;
using App.WinUI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
        Assert.NotNull(provider.GetService<IAppDeploymentService>());
        Assert.NotNull(provider.GetService<IAppModifierStateStore>());
        Assert.NotNull(provider.GetService<AppConfigValidationUseCase>());
        Assert.NotNull(provider.GetService<SaveAppConfigUseCase>());
        Assert.NotNull(provider.GetService<DeployAppUseCase>());
        Assert.NotNull(provider.GetService<MainPageViewModel>());
        Assert.NotNull(provider.GetService<DevicesPageViewModel>());
        Assert.NotNull(provider.GetService<AppsPageViewModel>());
        Assert.NotNull(provider.GetService<ShellPageViewModel>());
        Assert.NotNull(provider.GetService<ShellPageContentFactory>());
    }

    [Fact]
    public void BuildServiceProvider_ShouldRegisterShellAndPages()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var isService = provider.GetRequiredService<IServiceProviderIsService>();

        Assert.True(isService.IsService(typeof(MainPage)));
        Assert.True(isService.IsService(typeof(DevicesPage)));
        Assert.True(isService.IsService(typeof(AppsPage)));
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
        Assert.True(isService.IsService(typeof(AppsPageViewModel)));
        Assert.True(isService.IsService(typeof(ShellPageViewModel)));
        Assert.True(isService.IsService(typeof(PresetRepository)));
        Assert.True(isService.IsService(typeof(SettingsRepository)));
        Assert.True(isService.IsService(typeof(AppSettingsDomainService)));
    }

    [Fact]
    public void StartupPages_ShouldNotExposeServiceLocatorConstructors()
    {
        AssertNoServiceProviderConstructor(typeof(ShellPage));
        AssertNoServiceProviderConstructor(typeof(MainPage));
        AssertNoServiceProviderConstructor(typeof(DevicesPage));
        AssertNoServiceProviderConstructor(typeof(AppsPage));
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
