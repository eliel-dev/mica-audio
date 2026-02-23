using System.Reflection;
using App.WinUI;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Devices;
using App.WinUI.Views;
using Microsoft.Extensions.DependencyInjection;

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
    public void ShellAndAppsPages_ShouldExposePublicConstructorsForDi()
    {
        AssertHasPublicConstructor(typeof(ShellPage));
        AssertHasPublicConstructor(typeof(AppsPage));
    }

    private static void AssertHasPublicConstructor(Type pageType)
    {
        var constructors = pageType.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.True(
            constructors.Length > 0,
            $"Expected at least one public constructor for DI activation: {pageType.FullName}");
    }
}
