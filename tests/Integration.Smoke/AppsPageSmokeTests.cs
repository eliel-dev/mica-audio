using System.Reflection;
using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using App.WinUI.Views;
using Device.Protocol.Models;

namespace Integration.Smoke;

public sealed class AppsPageSmokeTests
{
    [Fact]
    public void AppsPageShouldDeclareCatalogAndOperationFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(AppsPage).GetField("SearchBox", flags));
        Assert.NotNull(typeof(AppsPage).GetField("CatalogGrid", flags));
        Assert.NotNull(typeof(AppsPage).GetField("TargetDeviceCombo", flags));
        Assert.NotNull(typeof(AppsPage).GetField("OperationInfoBar", flags));
        Assert.NotNull(typeof(AppsPage).GetField("GifOpenFileButton", flags));
        Assert.NotNull(typeof(AppsPage).GetField("ModifiersPanel", flags));
    }

    [Fact]
    public void AppsPageShouldKeepCatalogModifierAndDeploymentMethods()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(AppsPage).GetMethod("LoadCatalogAsync", flags));
        Assert.NotNull(typeof(AppsPage).GetMethod("LoadModifierEditorAsync", flags));
        Assert.NotNull(typeof(AppsPage).GetMethod("EnsureGifRuntimeWhileAppsPageIsVisibleAsync", flags));
        Assert.NotNull(typeof(AppsPage).GetMethod("OnInstallClicked", flags));
        Assert.NotNull(typeof(AppsPage).GetMethod("OnSaveModifiersClicked", flags));
    }

    [Fact]
    public void AppsPageShouldParseCityConfigWithCoordinates()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var method = typeof(AppsPage).GetMethod("ParseCityConfig", flags);
        Assert.NotNull(method);

        var args = new object?[] { "Sao Paulo, SP, Brasil|-23.55|-46.63", null, null };
        _ = method!.Invoke(null, args);

        Assert.Equal("Sao Paulo, SP, Brasil", args[1]);
        Assert.NotNull(args[2]);
    }

    [Fact]
    public void AppsPageShouldBuildConfigJsonFromDraft()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var method = typeof(AppsPage).GetMethod("TryBuildConfigJsonFromDraft", flags);
        Assert.NotNull(method);

        var item = new AppCatalogItem
        {
            Id = "weather",
            Name = "Weather",
            Modifiers =
            [
                new AppModifierDefinition
                {
                    Key = "enabled",
                    Label = "Enabled",
                    Type = AppModifierFieldType.Toggle,
                },
                new AppModifierDefinition
                {
                    Key = "interval",
                    Label = "Interval",
                    Type = AppModifierFieldType.Number,
                },
            ],
        };

        var rawValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["enabled"] = "true",
            ["interval"] = "15",
        };

        var args = new object?[] { item, rawValues, null, null };
        var result = method!.Invoke(null, args);

        Assert.True((bool)result!);
        Assert.Contains("\"enabled\":true", args[2]!.ToString(), StringComparison.Ordinal);
        Assert.Contains("\"interval\":15", args[2]!.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, args[3]);
    }

    [Fact]
    public void AppsPageShouldRenderDeploymentResultMessages()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var method = typeof(AppsPage).GetMethod("RenderResult", flags);
        Assert.NotNull(method);

        var success = new CommandDispatchResult
        {
            DeviceId = "device-01",
            Success = true,
        };

        var failure = new CommandDispatchResult
        {
            DeviceId = "device-01",
            Success = false,
            Message = "falhou",
        };

        var successText = method!.Invoke(null, ["instalar", "Weather", success])!.ToString();
        var failureText = method.Invoke(null, ["instalar", "Weather", failure])!.ToString();

        Assert.Contains("com sucesso", successText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Falha ao instalar", failureText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppsPageShouldBuildCityAutocompleteFailureMessage()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
        var method = typeof(AppsPage).GetMethod("BuildCityAutocompleteFailureMessage", flags);
        Assert.NotNull(method);

        var result = new CityAutocompleteService.CitySearchResult(
            Array.Empty<CitySuggestion>(),
            0,
            0,
            CityAutocompleteService.CitySearchFailureKind.Timeout,
            "A busca demorou demais.");

        var message = method!.Invoke(null, ["sa", result])?.ToString();

        Assert.NotNull(message);
        Assert.Contains("Autocomplete de cidade indisponivel", message, StringComparison.Ordinal);
        Assert.Contains("sa", message, StringComparison.Ordinal);
        Assert.Contains("A busca demorou demais.", message, StringComparison.Ordinal);
    }
}
