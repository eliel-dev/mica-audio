using App.WinUI.Services.Logging;
using OpenMeteo;
using OpenMeteo.Weather.Forecast.Options;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/guides/configure-app-modifiers.md#apps-clima
internal sealed class WeatherPreviewDataService
{
    private readonly IWeatherForecastClient forecastClient;
    private readonly AppLogStore? appLogStore;
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

    internal WeatherPreviewDataService()
        : this(new OpenMeteoSdkWeatherForecastClient(), appLogStore: null)
    {
    }

    public WeatherPreviewDataService(AppLogStore appLogStore)
        : this(new OpenMeteoSdkWeatherForecastClient(), appLogStore)
    {
    }

    internal WeatherPreviewDataService(
        IWeatherForecastClient forecastClient,
        AppLogStore? appLogStore,
        Func<DateTimeOffset>? nowProvider = null,
        TimeSpan? refreshInterval = null,
        TimeSpan? failureRetryInterval = null)
    {
        this.forecastClient = forecastClient;
        this.appLogStore = appLogStore;
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
        try
        {
            var currentWeather = await forecastClient.GetCurrentAsync(
                (float)WeatherAppFixedLocation.FixedLatitude,
                (float)WeatherAppFixedLocation.FixedLongitude,
                "America/Sao_Paulo").ConfigureAwait(false);

            if (currentWeather.Temperature is null || currentWeather.WeatherCode is null)
            {
                UpdateFailure(entry, "Open-Meteo nao retornou temperatura atual para Timbó.");
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
        }
        catch (Exception ex)
        {
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
            OpenMeteoClientException openMeteoException when !string.IsNullOrWhiteSpace(openMeteoException.Message)
                => openMeteoException.Message.Trim().TrimEnd('.').ToLowerInvariant() + ".",
            HttpRequestException httpException when httpException.StatusCode is not null
                => $"falha HTTP {(int)httpException.StatusCode.Value} ao consultar o Open-Meteo.",
            HttpRequestException => "falha de rede ao consultar o Open-Meteo.",
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

    private sealed class OpenMeteoSdkWeatherForecastClient : IWeatherForecastClient
    {
        private readonly OpenMeteoClient client;

        public OpenMeteoSdkWeatherForecastClient()
            : this(CreateConfiguredClient())
        {
        }

        internal OpenMeteoSdkWeatherForecastClient(OpenMeteoClient client)
        {
            this.client = client;
            this.client.RethrowExceptions = true;
        }

        public async Task<CurrentWeatherData> GetCurrentAsync(float latitude, float longitude, string timezone)
        {
            var forecast = await client.QueryWeatherApiAsync(new WeatherForecastOptions(latitude, longitude)
            {
                Temperature_Unit = TemperatureUnitType.celsius,
                Timezone = timezone,
                Current = new CurrentOptions(new[] { CurrentOptionsParameter.temperature_2m, CurrentOptionsParameter.weathercode }),
            }).ConfigureAwait(false);

            var current = forecast?.Current;
            return new CurrentWeatherData(current?.Temperature_2m, current?.Weathercode);
        }

        private static OpenMeteoClient CreateConfiguredClient()
        {
            return new OpenMeteoClient
            {
                RethrowExceptions = true,
            };
        }
    }
}
