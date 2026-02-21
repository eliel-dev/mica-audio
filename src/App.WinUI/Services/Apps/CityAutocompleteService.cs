using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/guides/troubleshoot-city-autocomplete.md#passos
internal sealed class CityAutocompleteService
{
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    public async Task<IReadOnlyList<CitySuggestion>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return Array.Empty<CitySuggestion>();
        }

        var encoded = Uri.EscapeDataString(query.Trim());
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=8&language=pt&countryCode=BR&format=json";

        try
        {
            var response = await Client.GetFromJsonAsync<OpenMeteoResponse>(url, cancellationToken).ConfigureAwait(false);
            return response?.Results?
                .Where(static item => !string.IsNullOrWhiteSpace(item.Name))
                .Select(item => new CitySuggestion
                {
                    Name = item.Name ?? string.Empty,
                    Region = item.Admin1 ?? item.Admin2 ?? string.Empty,
                    Country = NormalizeCountry(item.Country),
                    Latitude = item.Latitude,
                    Longitude = item.Longitude,
                })
                .ToArray() ?? Array.Empty<CitySuggestion>();
        }
        catch
        {
            return Array.Empty<CitySuggestion>();
        }
    }

    private static string NormalizeCountry(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Brasil";
        }

        return value.Trim().Equals("Brazil", StringComparison.OrdinalIgnoreCase)
            ? "Brasil"
            : value.Trim();
    }

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("results")]
        public IReadOnlyList<OpenMeteoResult>? Results { get; init; }
    }

    private sealed class OpenMeteoResult
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("admin1")]
        public string? Admin1 { get; init; }

        [JsonPropertyName("admin2")]
        public string? Admin2 { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; init; }
    }
}
