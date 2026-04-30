using System.Diagnostics;
using System.Text.Json;
using App.WinUI.Infrastructure.Observability;
using App.WinUI.Services.Logging;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#integracoes-http-externas
// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
internal sealed partial class WeatherPreviewDataService
{
    private readonly IWeatherForecastClient forecastClient;
    private readonly AppLogStore? appLogStore;
    private readonly ILogger<WeatherPreviewDataService>? logger;
    private readonly Func<DateTimeOffset> nowProvider;
    private readonly TimeSpan refreshInterval;
    private readonly TimeSpan failureRetryInterval;
    private readonly CacheEntry cacheEntry = new()
    {
        Snapshot = new WeatherPreviewSnapshot
        {
            CityDisplay = WeatherAppFixedLocation.FixedCityDisplayName,
            State = WeatherPreviewLoadState.Loading,
        },
    };

    public WeatherPreviewDataService(IWeatherForecastClient forecastClient, AppLogStore appLogStore, ILogger<WeatherPreviewDataService> logger)
        : this(forecastClient, appLogStore, logger, nowProvider: null, refreshInterval: null, failureRetryInterval: null)
    {
    }

    internal WeatherPreviewDataService(
        IWeatherForecastClient forecastClient,
        AppLogStore? appLogStore,
        ILogger<WeatherPreviewDataService>? logger = null,
        Func<DateTimeOffset>? nowProvider = null,
        TimeSpan? refreshInterval = null,
        TimeSpan? failureRetryInterval = null)
    {
        this.forecastClient = forecastClient;
        this.appLogStore = appLogStore;
        this.logger = logger;
        this.nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow);
        this.refreshInterval = refreshInterval ?? TimeSpan.FromMinutes(5);
        this.failureRetryInterval = failureRetryInterval ?? TimeSpan.FromSeconds(30);
    }

    public WeatherPreviewSnapshot GetSnapshot(string? cityConfig = null)
    {
        _ = cityConfig;

        var entry = cacheEntry;
        var now = nowProvider();
        var stale = (now - entry.LastRefreshUtc) >= refreshInterval;
        if (stale && Interlocked.CompareExchange(ref entry.RefreshInFlight, 1, 0) == 0)
        {
            _ = RefreshAsync(entry);
        }

        return entry.Snapshot;
    }

    private async Task RefreshAsync(CacheEntry entry)
    {
        using var activity = AppObservability.StartActivity("weather-preview.refresh", AppObservability.AppComponent);
        activity?.SetTag(AppObservability.FeatureKey, "weather-preview");
        activity?.SetTag(AppObservability.AppIdKey, WeatherAppFixedLocation.AppId);
        using var scope = AppObservability.BeginScope(
            logger,
            activity,
            (AppObservability.ComponentKey, AppObservability.AppComponent),
            (AppObservability.MicaComponentKey, AppObservability.AppComponent),
            (AppObservability.OperationKey, "weather-preview.refresh"),
            (AppObservability.FeatureKey, "weather-preview"),
            (AppObservability.AppIdKey, WeatherAppFixedLocation.AppId));

        try
        {
            var currentWeather = await forecastClient.GetCurrentAsync(
                (float)WeatherAppFixedLocation.FixedLatitude,
                (float)WeatherAppFixedLocation.FixedLongitude,
                "America/Sao_Paulo").ConfigureAwait(false);

            if (currentWeather.Temperature is null || currentWeather.WeatherCode is null)
            {
                UpdateFailure(entry, "Open-Meteo nao retornou temperatura atual para Timbo.");
                activity?.SetStatus(ActivityStatusCode.Error, "missing-current-weather");
                if (logger is not null)
                {
                    LogWeatherPreviewMissingCurrent(logger);
                }

                return;
            }

            var refreshedAt = nowProvider();
            entry.Snapshot = new WeatherPreviewSnapshot
            {
                CityDisplay = WeatherAppFixedLocation.FixedCityDisplayName,
                Temperature = currentWeather.Temperature.Value,
                WeatherCode = currentWeather.WeatherCode.Value,
                State = WeatherPreviewLoadState.Live,
                UpdatedAtUtc = refreshedAt,
            };
            entry.LastRefreshUtc = refreshedAt;
            entry.LastLoggedFailureMessage = string.Empty;
            entry.LastLoggedFailureAtUtc = DateTimeOffset.MinValue;
            activity?.SetTag("weather.temperature", currentWeather.Temperature.Value);
            activity?.SetTag("weather.code", currentWeather.WeatherCode.Value);
            if (logger is not null)
            {
                LogWeatherPreviewUpdated(logger);
            }
        }
        catch (Exception ex)
        {
            AppObservability.SetException(activity, ex);
            if (logger is not null)
            {
                LogWeatherPreviewRefreshFailure(logger, ex);
            }

            UpdateFailure(entry, BuildFailureMessage(ex));
        }
        finally
        {
            Interlocked.Exchange(ref entry.RefreshInFlight, 0);
        }
    }

    private void UpdateFailure(CacheEntry entry, string message)
    {
        var now = nowProvider();
        entry.Snapshot = new WeatherPreviewSnapshot
        {
            CityDisplay = WeatherAppFixedLocation.FixedCityDisplayName,
            State = WeatherPreviewLoadState.Error,
            FailureMessage = message,
            UpdatedAtUtc = now,
        };
        entry.LastRefreshUtc = now - refreshInterval + failureRetryInterval;

        if (!ShouldLogFailure(entry, message, now))
        {
            return;
        }

        appLogStore?.Append(
            LogCategory.App,
            LogSeverity.Error,
            $"Preview do clima indisponivel: {message}",
            WeatherAppFixedLocation.AppId);

        entry.LastLoggedFailureMessage = message;
        entry.LastLoggedFailureAtUtc = now;
    }

    private bool ShouldLogFailure(CacheEntry entry, string message, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(entry.LastLoggedFailureMessage))
        {
            return true;
        }

        var sameMessage = string.Equals(entry.LastLoggedFailureMessage, message, StringComparison.Ordinal);
        var withinRetryWindow = (now - entry.LastLoggedFailureAtUtc) < failureRetryInterval;
        return !sameMessage || !withinRetryWindow;
    }

    private static string BuildFailureMessage(Exception exception)
    {
        return exception switch
        {
            TaskCanceledException => "tempo limite ao consultar o Open-Meteo.",
            TimeoutRejectedException => "tempo limite ao consultar o Open-Meteo.",
            BrokenCircuitException => "servico Open-Meteo temporariamente indisponivel apos falhas consecutivas.",
            HttpRequestException httpException when httpException.StatusCode is not null
                => $"falha HTTP {(int)httpException.StatusCode.Value} ao consultar o Open-Meteo.",
            HttpRequestException => "falha de rede ao consultar o Open-Meteo.",
            JsonException => "resposta invalida do Open-Meteo.",
            _ => "falha inesperada ao consultar o Open-Meteo.",
        };
    }

    private sealed class CacheEntry
    {
        public WeatherPreviewSnapshot Snapshot { get; set; } = new();

        public DateTimeOffset LastRefreshUtc { get; set; } = DateTimeOffset.MinValue;

        public string LastLoggedFailureMessage { get; set; } = string.Empty;

        public DateTimeOffset LastLoggedFailureAtUtc { get; set; } = DateTimeOffset.MinValue;

        public int RefreshInFlight;
    }

    internal interface IWeatherForecastClient
    {
        Task<CurrentWeatherData> GetCurrentAsync(float latitude, float longitude, string timezone);
    }

    internal readonly record struct CurrentWeatherData(double? Temperature, int? WeatherCode);

    [LoggerMessage(EventId = 1520, Level = LogLevel.Warning, Message = "Weather preview nao recebeu temperatura atual do Open-Meteo.")]
    private static partial void LogWeatherPreviewMissingCurrent(ILogger logger);

    [LoggerMessage(EventId = 1521, Level = LogLevel.Information, Message = "Weather preview atualizado com sucesso.")]
    private static partial void LogWeatherPreviewUpdated(ILogger logger);

    [LoggerMessage(EventId = 1522, Level = LogLevel.Warning, Message = "Weather preview falhou ao atualizar snapshot.")]
    private static partial void LogWeatherPreviewRefreshFailure(ILogger logger, Exception exception);
}
