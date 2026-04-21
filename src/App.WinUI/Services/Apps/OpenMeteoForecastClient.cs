using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using App.WinUI.Infrastructure.Http;
using App.WinUI.Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#integracoes-http-externas
// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
internal sealed partial class OpenMeteoForecastClient : WeatherPreviewDataService.IWeatherForecastClient
{
    private readonly HttpClient? client;
    private readonly IHttpClientFactory? clientFactory;
    private readonly string? clientName;
    private readonly ILogger<OpenMeteoForecastClient>? logger;

    public OpenMeteoForecastClient(IHttpClientFactory clientFactory, ILogger<OpenMeteoForecastClient> logger)
        : this(clientFactory, ExternalHttpClients.OpenMeteoForecastClientName, logger)
    {
    }

    internal OpenMeteoForecastClient(IHttpClientFactory clientFactory, string clientName, ILogger<OpenMeteoForecastClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        this.clientFactory = clientFactory;
        this.clientName = clientName;
        this.logger = logger;
    }

    internal OpenMeteoForecastClient(HttpClient client, ILogger<OpenMeteoForecastClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
        this.logger = logger;
    }

    public async Task<WeatherPreviewDataService.CurrentWeatherData> GetCurrentAsync(float latitude, float longitude, string timezone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timezone);

        var requestUri =
            $"v1/forecast?latitude={latitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&longitude={longitude.ToString(CultureInfo.InvariantCulture)}" +
            $"&current=temperature_2m,weathercode" +
            $"&temperature_unit=celsius" +
            $"&timezone={Uri.EscapeDataString(timezone)}";
        using var activity = AppObservability.StartActivity("weather-preview.fetch-current", AppObservability.AppComponent);
        activity?.SetTag(AppObservability.FeatureKey, "weather-preview");
        activity?.SetTag(AppObservability.HttpClientNameKey, ResolveClientNameForTelemetry());
        activity?.SetTag("weather.latitude", latitude);
        activity?.SetTag("weather.longitude", longitude);
        using var scope = AppObservability.BeginScope(
            logger,
            activity,
            (AppObservability.ComponentKey, AppObservability.AppComponent),
            (AppObservability.MicaComponentKey, AppObservability.AppComponent),
            (AppObservability.OperationKey, "weather-preview.fetch-current"),
            (AppObservability.FeatureKey, "weather-preview"),
            (AppObservability.HttpClientNameKey, ResolveClientNameForTelemetry()));

        try
        {
            using var response = await ResolveClient().GetAsync(requestUri).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<ForecastResponse>().ConfigureAwait(false);
            var result = new WeatherPreviewDataService.CurrentWeatherData(
                payload?.Current?.Temperature2M,
                payload?.Current?.WeatherCode);
            activity?.SetTag("weather.temperature", result.Temperature);
            activity?.SetTag("weather.code", result.WeatherCode);
            if (logger is not null)
            {
                LogWeatherFetchSuccess(logger);
            }

            return result;
        }
        catch (Exception ex)
        {
            AppObservability.SetException(activity, ex);
            if (logger is not null)
            {
                LogWeatherFetchFailure(logger, ex);
            }

            throw;
        }
    }

    private HttpClient ResolveClient()
    {
        return client ?? clientFactory!.CreateClient(clientName!);
    }

    private string ResolveClientNameForTelemetry() => clientName ?? "custom-http-client";

    private sealed class ForecastResponse
    {
        [JsonPropertyName("current")]
        public CurrentForecastPayload? Current { get; init; }
    }

    private sealed class CurrentForecastPayload
    {
        [JsonPropertyName("temperature_2m")]
        public double? Temperature2M { get; init; }

        [JsonPropertyName("weathercode")]
        public int? WeatherCode { get; init; }
    }

    [LoggerMessage(EventId = 1510, Level = LogLevel.Information, Message = "Weather preview retornou dados atuais do Open-Meteo.")]
    private static partial void LogWeatherFetchSuccess(ILogger logger);

    [LoggerMessage(EventId = 1511, Level = LogLevel.Warning, Message = "Weather preview falhou ao consultar o Open-Meteo.")]
    private static partial void LogWeatherFetchFailure(ILogger logger, Exception exception);
}
