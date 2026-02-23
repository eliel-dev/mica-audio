using System.Reflection;
using App.WinUI;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Apps.UseCases;
using App.WinUI.Services.Devices;
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
    }

    [Fact]
    public void BuildServiceProvider_ShouldRegisterShellAndPages()
    {
        var provider = App.WinUI.App.BuildServiceProvider();
        var isService = provider.GetRequiredService<IServiceProviderIsService>();

        Assert.True(isService.IsService(typeof(MainPage)));
        Assert.True(isService.IsService(typeof(DevicesPage)));
        Assert.True(isService.IsService(typeof(AppsPage)));
        Assert.True(isService.IsService(typeof(ServerPage)));
        Assert.True(isService.IsService(typeof(ShellPage)));
    }

    [Fact]
    public void StartupPages_ShouldExposePublicConstructorsForDi()
    {
        AssertHasPublicConstructor(typeof(ShellPage));
        AssertHasPublicConstructor(typeof(MainPage));
        AssertHasPublicConstructor(typeof(DevicesPage));
        AssertHasPublicConstructor(typeof(AppsPage));
        AssertHasPublicConstructor(typeof(ServerPage));
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

    private static void AssertHasPublicConstructor(Type pageType)
    {
        var constructors = pageType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.True(
            constructors.Length > 0,
            $"Expected at least one public constructor for DI activation: {pageType.FullName}");
    }
}

