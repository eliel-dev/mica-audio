using System.Text.Json;
using App.WinUI.Infrastructure.Observability;
using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;
using App.WinUI.Services.Apps.UseCases;
using Device.Protocol.Models;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace Output.Tests;

public sealed class AppConfigUseCasesTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldSaveDraftAndDispatchInstall()
    {
        var root = CreateTempRoot();
        try
        {
            using var store = new AppModifierStateStore(CreateOptions(root));
            var saveUseCase = new SaveAppConfigUseCase(store);
            var validationUseCase = new AppConfigValidationUseCase();
            var fakeDeployment = new FakeAppDeploymentService();
            var deployUseCase = new DeployAppUseCase(fakeDeployment, saveUseCase, validationUseCase);

            var app = CreateWeatherApp();
            var rawValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["city"] = "São Paulo",
                ["units"] = "imperial",
                ["refreshMinutes"] = "5",
                ["showIcon"] = "true",
            };

            var result = await deployUseCase.ExecuteAsync("device-a", "device-a", app, rawValues);

            Assert.True(result.Success);
            Assert.NotNull(result.CommandResult);
            Assert.Equal("device-a", result.CommandResult!.DeviceId);
            Assert.Equal("install_app", fakeDeployment.LastAction);

            var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(fakeDeployment.LastConfigJson ?? "{}")!;
            Assert.Equal(WeatherAppFixedLocation.FixedCityConfigValue, payload["city"].GetString());
            Assert.Equal(WeatherAppFixedLocation.FixedUnitsValue, payload["units"].GetString());
            Assert.Equal(5d, payload["refreshMinutes"].GetDouble());
            Assert.True(payload["showIcon"].GetBoolean());

            var persisted = await store.GetDraftAsync("device-a", "accuweather");
            Assert.NotNull(persisted);
            Assert.Equal(WeatherAppFixedLocation.FixedCityConfigValue, persisted!.Values["city"]);
            Assert.Equal(WeatherAppFixedLocation.FixedUnitsValue, persisted.Values["units"]);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldEmitDeployAndSaveActivities()
    {
        var root = CreateTempRoot();
        try
        {
            using var capture = new ActivityCapture(AppObservability.ActivitySourceName);
            using var store = new AppModifierStateStore(CreateOptions(root));
            var saveUseCase = new SaveAppConfigUseCase(store);
            var validationUseCase = new AppConfigValidationUseCase();
            var fakeDeployment = new FakeAppDeploymentService();
            var deployUseCase = new DeployAppUseCase(fakeDeployment, saveUseCase, validationUseCase);
            var app = CreateWeatherApp();

            var result = await deployUseCase.ExecuteAsync(
                "device-a",
                "device-a",
                app,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["refreshMinutes"] = "5",
                    ["showIcon"] = "true",
                });

            Assert.True(result.Success);

            var deployActivity = Assert.Single(capture.CompletedActivities, item => item.OperationName == "app.deploy.install");
            Assert.Equal("device-a", deployActivity.GetTagItem(AppObservability.DeviceIdKey));
            Assert.Equal("accuweather", deployActivity.GetTagItem(AppObservability.AppIdKey));
            Assert.Equal(result.CommandResult!.CommandId, deployActivity.GetTagItem(AppObservability.CommandIdKey));

            var saveActivity = Assert.Single(capture.CompletedActivities, item => item.OperationName == "app-config.save-draft");
            Assert.Equal("accuweather", saveActivity.GetTagItem(AppObservability.AppIdKey));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void TryBuildPayload_ShouldInjectFixedWeatherConfig_WhenModifiersAreMissing()
    {
        var validation = new AppConfigValidationUseCase();
        var app = CreateWeatherApp();

        var rawValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["refreshMinutes"] = "5",
        };

        var success = validation.TryBuildPayload(app, rawValues, out var configJson, out var error);

        Assert.True(success);
        Assert.True(string.IsNullOrWhiteSpace(error));

        var payload = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(configJson)!;
        Assert.Equal(WeatherAppFixedLocation.FixedCityConfigValue, payload["city"].GetString());
        Assert.Equal(WeatherAppFixedLocation.FixedUnitsValue, payload["units"].GetString());
    }

    [Fact]
    public void TryBuildPayload_ShouldRejectInvalidNumericValues()
    {
        var validation = new AppConfigValidationUseCase();
        var app = CreateWeatherApp();

        var rawValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["refreshMinutes"] = "abc",
        };

        var success = validation.TryBuildPayload(app, rawValues, out _, out var error);

        Assert.False(success);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    private static AppCatalogItem CreateWeatherApp()
    {
        return new AppCatalogItem
        {
            Id = "accuweather",
            Name = "Clima",
            Category = "clima",
            Modifiers =
            [
                new AppModifierDefinition { Key = "refreshMinutes", Label = "Atualização", Type = AppModifierFieldType.Number, Required = false, DefaultValue = "5" },
                new AppModifierDefinition { Key = "showIcon", Label = "Mostrar ícone", Type = AppModifierFieldType.Toggle, Required = false, DefaultValue = "true" },
            ],
        };
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static IOptions<MicaAudioOptions> CreateOptions(string root)
    {
        return Options.Create(new MicaAudioOptions
        {
            AppDataRoot = root,
            AppsModifierStatePath = Path.Combine(root, "apps", "modifiers.json"),
        });
    }

    private sealed class FakeAppDeploymentService : IAppDeploymentService
    {
        public string? LastAction { get; private set; }

        public string? LastConfigJson { get; private set; }

        public Task<CommandDispatchResult> ActivateAsync(string deviceId, AppCatalogItem item, CancellationToken cancellationToken = default)
        {
            LastAction = "activate_app";
            return Task.FromResult(CreateResult(deviceId));
        }

        public Task<CommandDispatchResult> InstallAsync(string deviceId, AppCatalogItem item, string? configJson = null, CancellationToken cancellationToken = default)
        {
            LastAction = "install_app";
            LastConfigJson = configJson;
            return Task.FromResult(CreateResult(deviceId));
        }

        public Task<CommandDispatchResult> SetConfigAsync(string deviceId, AppCatalogItem item, string configJson, CancellationToken cancellationToken = default)
        {
            LastAction = "set_app_config";
            LastConfigJson = configJson;
            return Task.FromResult(CreateResult(deviceId));
        }

        private static CommandDispatchResult CreateResult(string deviceId)
        {
            return new CommandDispatchResult
            {
                DeviceId = deviceId,
                CommandId = Guid.NewGuid().ToString("N"),
                Accepted = true,
                Completed = true,
                Success = true,
                ProgressPercent = 100,
                Stage = "done",
                Message = "ok",
            };
        }
    }
}


