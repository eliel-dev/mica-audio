using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/guides/troubleshoot-city-autocomplete.md#passos
internal sealed class WeatherPreviewDataService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    private readonly ConcurrentDictionary<string, CacheEntry> cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan refreshInterval = TimeSpan.FromMinutes(5);

    public WeatherPreviewSnapshot GetSnapshot(string? cityConfig, string? units)
    {
        var unit = string.Equals(units, "imperial", StringComparison.OrdinalIgnoreCase) ? "imperial" : "metric";
        var unitSymbol = unit == "imperial" ? "F" : "C";
        var location = ParseLocation(cityConfig);

        var cacheKey = $"{location.DisplayName}|{location.Latitude?.ToString("F4", CultureInfo.InvariantCulture)}|{location.Longitude?.ToString("F4", CultureInfo.InvariantCulture)}|{unit}";
        var entry = cache.GetOrAdd(cacheKey, _ => new CacheEntry
        {
            Snapshot = new WeatherPreviewSnapshot
            {
                CityDisplay = location.DisplayName,
                UnitSymbol = unitSymbol,
            },
        });

        var now = DateTimeOffset.UtcNow;
        var stale = (now - entry.LastRefreshUtc) >= refreshInterval;
        if (stale && Interlocked.CompareExchange(ref entry.RefreshInFlight, 1, 0) == 0)
        {
            _ = RefreshAsync(entry, location, unit, unitSymbol);
        }

        return entry.Snapshot;
    }

    private async Task RefreshAsync(CacheEntry entry, WeatherLocation location, string units, string unitSymbol)
    {
        try
        {
            var resolved = await ResolveLocationAsync(location).ConfigureAwait(false);
            if (resolved.Latitude is null || resolved.Longitude is null)
            {
                entry.Snapshot = new WeatherPreviewSnapshot
                {
                    CityDisplay = resolved.DisplayName,
                    UnitSymbol = unitSymbol,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
                entry.LastRefreshUtc = DateTimeOffset.UtcNow;
                return;
            }

            var lat = resolved.Latitude.Value.ToString("F4", CultureInfo.InvariantCulture);
            var lon = resolved.Longitude.Value.ToString("F4", CultureInfo.InvariantCulture);
            var tempUnitParam = units == "imperial" ? "fahrenheit" : "celsius";
            var forecastUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current=temperature_2m,weather_code&temperature_unit={tempUnitParam}&timezone=America%2FSao_Paulo";

            var forecast = await Client.GetFromJsonAsync<OpenMeteoForecastResponse>(forecastUrl).ConfigureAwait(false);
            var temp = forecast?.Current?.Temperature;
            var code = forecast?.Current?.WeatherCode;

            entry.Snapshot = new WeatherPreviewSnapshot
            {
                CityDisplay = resolved.DisplayName,
                Temperature = temp,
                WeatherCode = code,
                UnitSymbol = unitSymbol,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            };
            entry.LastRefreshUtc = DateTimeOffset.UtcNow;
        }
        catch
        {
            entry.LastRefreshUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            Interlocked.Exchange(ref entry.RefreshInFlight, 0);
        }
    }

    private static async Task<WeatherLocation> ResolveLocationAsync(WeatherLocation location)
    {
        if (location.Latitude is not null && location.Longitude is not null)
        {
            return location;
        }

        var encoded = Uri.EscapeDataString(location.DisplayName);
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=1&language=pt&countryCode=BR&format=json";
        var response = await Client.GetFromJsonAsync<OpenMeteoGeocodingResponse>(url).ConfigureAwait(false);
        var best = response?.Results?.FirstOrDefault();
        if (best is null || string.IsNullOrWhiteSpace(best.Name))
        {
            return location;
        }

        var cityName = BuildDisplayName(best.Name, best.Admin1, best.Country);
        return new WeatherLocation
        {
            DisplayName = cityName,
            Latitude = best.Latitude,
            Longitude = best.Longitude,
        };
    }

    private static WeatherLocation ParseLocation(string? cityConfig)
    {
        var fallback = new WeatherLocation
        {
            DisplayName = "São Paulo",
            Latitude = -23.5505,
            Longitude = -46.6333,
        };

        if (string.IsNullOrWhiteSpace(cityConfig))
        {
            return fallback;
        }

        var parts = cityConfig.Split('|', StringSplitOptions.TrimEntries);
        var display = parts[0].Trim();
        if (string.IsNullOrWhiteSpace(display))
        {
            display = fallback.DisplayName;
        }

        var location = new WeatherLocation { DisplayName = display };

        if (parts.Length >= 3
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            location.Latitude = lat;
            location.Longitude = lon;
        }

        return location;
    }

    private static string BuildDisplayName(string? name, string? admin1, string? country)
    {
        var normalizedCountry = string.IsNullOrWhiteSpace(country)
            ? "Brasil"
            : country.Trim().Equals("Brazil", StringComparison.OrdinalIgnoreCase)
                ? "Brasil"
                : country.Trim();

        return string.Join(", ",
            new[] { name, admin1, normalizedCountry }
                .Where(static part => !string.IsNullOrWhiteSpace(part))
                .Select(static part => part!.Trim()));
    }

    private sealed class CacheEntry
    {
        public WeatherPreviewSnapshot Snapshot { get; set; } = new();

        public DateTimeOffset LastRefreshUtc { get; set; } = DateTimeOffset.MinValue;

        public int RefreshInFlight;
    }

    private sealed class WeatherLocation
    {
        public string DisplayName { get; init; } = string.Empty;

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }
    }

    private sealed class OpenMeteoGeocodingResponse
    {
        [JsonPropertyName("results")]
        public IReadOnlyList<OpenMeteoGeocodingResult>? Results { get; init; }
    }

    private sealed class OpenMeteoGeocodingResult
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("admin1")]
        public string? Admin1 { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; init; }
    }

    private sealed class OpenMeteoForecastResponse
    {
        [JsonPropertyName("current")]
        public OpenMeteoCurrent? Current { get; init; }
    }

    private sealed class OpenMeteoCurrent
    {
        [JsonPropertyName("temperature_2m")]
        public double? Temperature { get; init; }

        [JsonPropertyName("weather_code")]
        public int? WeatherCode { get; init; }
    }
}
