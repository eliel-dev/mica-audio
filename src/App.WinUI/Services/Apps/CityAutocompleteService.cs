using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/guides/troubleshoot-city-autocomplete.md#passos
internal sealed class CityAutocompleteService
{
    internal const int MinQueryLength = 2;
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    private static readonly HttpClient DefaultClient = CreateDefaultClient();
    private readonly HttpClient client;

    internal enum CitySearchFailureKind
    {
        None,
        Cancelled,
        Timeout,
        Http,
        InvalidResponse,
    }

    internal sealed class CitySearchResult
    {
        public static CitySearchResult Empty { get; } = new(Array.Empty<CitySuggestion>(), 0, 0, CitySearchFailureKind.None, string.Empty);

        public CitySearchResult(
            IReadOnlyList<CitySuggestion> suggestions,
            int rawResultCount,
            int filteredResultCount,
            CitySearchFailureKind failureKind,
            string failureMessage)
        {
            Suggestions = suggestions;
            RawResultCount = rawResultCount;
            FilteredResultCount = filteredResultCount;
            FailureKind = failureKind;
            FailureMessage = failureMessage;
        }

        public IReadOnlyList<CitySuggestion> Suggestions { get; }

        public int RawResultCount { get; }

        public int FilteredResultCount { get; }

        public CitySearchFailureKind FailureKind { get; }

        public string FailureMessage { get; }

        public bool IsCancelled => FailureKind == CitySearchFailureKind.Cancelled;

        public bool HasFailure => FailureKind is not CitySearchFailureKind.None and not CitySearchFailureKind.Cancelled;
    }

    public CityAutocompleteService()
        : this(DefaultClient)
    {
    }

    public CityAutocompleteService(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Mantido como service por instancia para preservar o registro em DI e permitir evolucao futura sem churn de contrato.")]
    public async Task<IReadOnlyList<CitySuggestion>> SearchAsync(string query, CancellationToken cancellationToken = default)
        => (await SearchWithDiagnosticsAsync(query, cancellationToken).ConfigureAwait(false)).Suggestions;

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Mantido como service por instancia para preservar o registro em DI e permitir evolucao futura sem churn de contrato.")]
    public async Task<CitySearchResult> SearchWithDiagnosticsAsync(string query, CancellationToken cancellationToken = default)
    {
        var trimmedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedQuery) || trimmedQuery.Length < MinQueryLength)
        {
            return CitySearchResult.Empty;
        }

        var encoded = Uri.EscapeDataString(trimmedQuery);
        var url = $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=20&language=pt&format=json&countryCode=BR";

        try
        {
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<OpenMeteoResponse>(cancellationToken: cancellationToken).ConfigureAwait(false);
            var rawResults = payload?.Results?
                .Where(static item => !string.IsNullOrWhiteSpace(item.Name))
                .ToArray() ?? Array.Empty<OpenMeteoResult>();
            var suggestions = rawResults
                .Select(item => new CitySuggestion
                {
                    Name = item.Name ?? string.Empty,
                    Region = item.Admin1 ?? item.Admin2 ?? string.Empty,
                    Country = NormalizeCountry(item.Country),
                    Latitude = item.Latitude,
                    Longitude = item.Longitude,
                })
                .ToArray();

            return new CitySearchResult(
                suggestions,
                rawResults.Length,
                suggestions.Length,
                CitySearchFailureKind.None,
                string.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new CitySearchResult(Array.Empty<CitySuggestion>(), 0, 0, CitySearchFailureKind.Cancelled, string.Empty);
        }
        catch (OperationCanceledException)
        {
            return new CitySearchResult(
                Array.Empty<CitySuggestion>(),
                0,
                0,
                CitySearchFailureKind.Timeout,
                $"A busca demorou mais que {RequestTimeout.TotalSeconds:0} segundos.");
        }
        catch (HttpRequestException ex)
        {
            return new CitySearchResult(
                Array.Empty<CitySuggestion>(),
                0,
                0,
                CitySearchFailureKind.Http,
                $"Falha HTTP ao consultar o Open-Meteo: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return new CitySearchResult(
                Array.Empty<CitySuggestion>(),
                0,
                0,
                CitySearchFailureKind.InvalidResponse,
                $"Resposta invalida do Open-Meteo: {ex.Message}");
        }
    }

    private static HttpClient CreateDefaultClient()
    {
        return new HttpClient
        {
            Timeout = RequestTimeout,
        };
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

        [JsonPropertyName("country_code")]
        public string? CountryCode { get; init; }

        [JsonPropertyName("latitude")]
        public double? Latitude { get; init; }

        [JsonPropertyName("longitude")]
        public double? Longitude { get; init; }
    }
}
