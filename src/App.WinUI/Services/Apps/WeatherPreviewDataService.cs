using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
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
    private readonly TimeSpan failureRetryInterval = TimeSpan.FromSeconds(30);

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
                entry.LastRefreshUtc = DateTimeOffset.UtcNow - refreshInterval + failureRetryInterval;
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
            entry.LastRefreshUtc = DateTimeOffset.UtcNow - refreshInterval + failureRetryInterval;
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

        foreach (var query in BuildGeocodingQueries(location))
        {
            var encoded = Uri.EscapeDataString(query);
            var url = $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=8&language=pt&format=json";
            var response = await Client.GetFromJsonAsync<OpenMeteoGeocodingResponse>(url).ConfigureAwait(false);
            var best = SelectBestGeocodingResult(response?.Results, location);
            if (best is null)
            {
                continue;
            }

            var cityName = BuildDisplayName(best.Name, best.Admin1, best.Country);
            return new WeatherLocation
            {
                DisplayName = cityName,
                NameHint = location.NameHint,
                RegionHint = location.RegionHint,
                CountryHint = location.CountryHint,
                Latitude = best.Latitude,
                Longitude = best.Longitude,
            };
        }

        return location;
    }

    private static WeatherLocation ParseLocation(string? cityConfig)
    {
        var fallback = new WeatherLocation
        {
            DisplayName = "São Paulo",
            NameHint = "São Paulo",
            RegionHint = "São Paulo",
            CountryHint = "Brasil",
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

        var labels = display.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var location = new WeatherLocation
        {
            DisplayName = display,
            NameHint = labels.Length > 0 ? labels[0] : display,
            RegionHint = labels.Length > 1 ? labels[1] : string.Empty,
            CountryHint = labels.Length > 2 ? labels[2] : string.Empty,
        };

        if (parts.Length >= 3
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon))
        {
            location.Latitude = lat;
            location.Longitude = lon;
        }

        return location;
    }

    private static IReadOnlyList<string> BuildGeocodingQueries(WeatherLocation location)
    {
        var ordered = new List<string>(capacity: 8);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddQuery(location.DisplayName);
        AddQuery(location.NameHint);

        if (!string.IsNullOrWhiteSpace(location.NameHint) && !string.IsNullOrWhiteSpace(location.RegionHint))
        {
            AddQuery($"{location.NameHint} {location.RegionHint}");
        }

        if (!string.IsNullOrWhiteSpace(location.NameHint) && !string.IsNullOrWhiteSpace(location.CountryHint))
        {
            AddQuery($"{location.NameHint} {location.CountryHint}");
        }

        return ordered;

        void AddQuery(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var normalized = NormalizeQuery(candidate);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (seen.Add(normalized))
            {
                ordered.Add(normalized);
            }

            var ascii = RemoveDiacritics(normalized);
            if (!string.Equals(ascii, normalized, StringComparison.OrdinalIgnoreCase) && seen.Add(ascii))
            {
                ordered.Add(ascii);
            }
        }
    }

    private static OpenMeteoGeocodingResult? SelectBestGeocodingResult(IReadOnlyList<OpenMeteoGeocodingResult>? results, WeatherLocation location)
    {
        return results?
            .Where(static result => !string.IsNullOrWhiteSpace(result.Name) && result.Latitude is not null && result.Longitude is not null)
            .Select(result => new
            {
                Result = result,
                Score = ScoreGeocodingResult(result, location),
            })
            .OrderByDescending(static item => item.Score)
            .ThenBy(static item => item.Result.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static item => item.Result)
            .FirstOrDefault();
    }

    private static int ScoreGeocodingResult(OpenMeteoGeocodingResult result, WeatherLocation location)
    {
        var score = 0;

        if (TokenEquals(result.Name, location.NameHint))
        {
            score += 40;
        }
        else if (TokenContains(result.Name, location.NameHint))
        {
            score += 15;
        }

        if (TokenEquals(result.Admin1, location.RegionHint))
        {
            score += 20;
        }
        else if (TokenContains(result.Admin1, location.RegionHint))
        {
            score += 8;
        }

        if (!string.IsNullOrWhiteSpace(location.CountryHint))
        {
            if (TokenEquals(result.Country, location.CountryHint))
            {
                score += 12;
            }
        }
        else if (IsBrazil(result.Country))
        {
            score += 6;
        }

        if (LocationHintsIndicateBrazil(location) && !IsBrazil(result.Country))
        {
            score -= 12;
        }

        return score;
    }

    private static bool LocationHintsIndicateBrazil(WeatherLocation location)
    {
        if (string.IsNullOrWhiteSpace(location.CountryHint))
        {
            return true;
        }

        return IsBrazil(location.CountryHint);
    }

    private static bool IsBrazil(string? value)
    {
        var normalized = NormalizeToken(value);
        return normalized is "brasil" or "brazil" or "br";
    }

    private static bool TokenEquals(string? left, string? right)
    {
        var leftToken = NormalizeToken(left);
        var rightToken = NormalizeToken(right);
        return !string.IsNullOrWhiteSpace(leftToken)
            && !string.IsNullOrWhiteSpace(rightToken)
            && string.Equals(leftToken, rightToken, StringComparison.Ordinal);
    }

    private static bool TokenContains(string? source, string? target)
    {
        var sourceToken = NormalizeToken(source);
        var targetToken = NormalizeToken(target);
        return !string.IsNullOrWhiteSpace(sourceToken)
            && !string.IsNullOrWhiteSpace(targetToken)
            && sourceToken.Contains(targetToken, StringComparison.Ordinal);
    }

    private static string NormalizeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var ascii = RemoveDiacritics(value).ToLowerInvariant();
        var builder = new StringBuilder(ascii.Length);
        var lastWasSpace = false;
        foreach (var ch in ascii)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasSpace = false;
                continue;
            }

            if (lastWasSpace)
            {
                continue;
            }

            builder.Append(' ');
            lastWasSpace = true;
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeQuery(string value)
    {
        var collapsed = value.Replace(',', ' ').Replace(';', ' ');
        return string.Join(' ', collapsed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string RemoveDiacritics(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
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

        public string NameHint { get; init; } = string.Empty;

        public string RegionHint { get; init; } = string.Empty;

        public string CountryHint { get; init; } = string.Empty;

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
