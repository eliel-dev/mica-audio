using App.WinUI.Models.Apps;
using App.WinUI.Services.Devices;

namespace Output.Tests;

public sealed class DevicePreviewResolverTests
{
    [Fact]
    public void Resolve_ShouldUseCatalogItem_WhenActiveAppIdMatches()
    {
        var catalogItem = new AppCatalogItem
        {
            Id = "weather.accu",
            Name = "AccuWeather",
            Category = "clima",
            Preview = new AppPreviewDefinition
            {
                Kind = "weather",
                Speed = 1f,
            },
        };

        var catalog = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase)
        {
            [catalogItem.Id] = catalogItem,
        };

        var result = DevicePreviewResolver.Resolve("weather.accu", "Clima", catalog);

        Assert.Same(catalogItem, result);
    }

    [Fact]
    public void Resolve_ShouldFallbackToHeuristic_WhenCatalogDoesNotMatch()
    {
        var result = DevicePreviewResolver.Resolve(
            "analogclock",
            "Relogio",
            new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase));

        Assert.NotNull(result);
        Assert.Equal("analogclock", result.Id);
        Assert.Equal("Relogio", result.Name);
        Assert.NotNull(result.Preview);
        Assert.Equal("clock", result.Preview!.Kind);
    }

    [Fact]
    public void Resolve_ShouldTreatEmptyAppAsNoActiveApp()
    {
        var result = DevicePreviewResolver.Resolve(
            string.Empty,
            string.Empty,
            new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase));

        Assert.Null(result);
    }
}
