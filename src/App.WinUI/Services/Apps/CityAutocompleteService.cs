using App.WinUI.Infrastructure.Http;
using App.WinUI.Infrastructure.Observability;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace App.WinUI.Services.Apps;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#integracoes-http-externas
// DOCS: docs/wiki/guides/troubleshoot-city-autocomplete.md#passos
internal sealed partial class CityAutocompleteService
{
    internal const int MinQueryLength = 2;
    internal static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    private readonly HttpClient? client;
    private readonly IHttpClientFactory? clientFactory;
    private readonly string? clientName;
    private readonly ILogger<CityAutocompleteService>? logger;

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

    public CityAutocompleteService(IHttpClientFactory clientFactory, ILogger<CityAutocompleteService> logger)
        : this(clientFactory, ExternalHttpClients.OpenMeteoGeocodingClientName, logger)
    {
    }

    internal CityAutocompleteService(IHttpClientFactory clientFactory, string clientName, ILogger<CityAutocompleteService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        this.clientFactory = clientFactory;
        this.clientName = clientName;
        this.logger = logger;
    }

    internal CityAutocompleteService(HttpClient client, ILogger<CityAutocompleteService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        this.client = client;
        this.logger = logger;
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
        var url = $"v1/search?name={encoded}&count=20&language=pt&format=json&countryCode=BR";
        using var activity = AppObservability.StartActivity("city-autocomplete.search", AppObservability.AppComponent);
        activity?.SetTag(AppObservability.FeatureKey, "autocomplete");
        activity?.SetTag(AppObservability.HttpClientNameKey, ResolveClientNameForTelemetry());
        activity?.SetTag("query.length", trimmedQuery.Length);
        using var scope = AppObservability.BeginScope(
            logger,
            activity,
            (AppObservability.ComponentKey, AppObservability.AppComponent),
            (AppObservability.MicaComponentKey, AppObservability.AppComponent),
            (AppObservability.OperationKey, "city-autocomplete.search"),
            (AppObservability.FeatureKey, "autocomplete"),
            (AppObservability.HttpClientNameKey, ResolveClientNameForTelemetry()));

        try
        {
            using var response = await ResolveClient().GetAsync(url, cancellationToken).ConfigureAwait(false);
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

            var result = new CitySearchResult(
                suggestions,
                rawResults.Length,
                suggestions.Length,
                CitySearchFailureKind.None,
                string.Empty);
            activity?.SetTag("results.raw", rawResults.Length);
            activity?.SetTag("results.filtered", suggestions.Length);
            if (logger is not null)
            {
                LogAutocompleteSuccess(logger, suggestions.Length);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            activity?.SetTag("cancelled", true);
            return new CitySearchResult(Array.Empty<CitySuggestion>(), 0, 0, CitySearchFailureKind.Cancelled, string.Empty);
        }
        catch (TimeoutRejectedException)
        {
            var result = new CitySearchResult(
                Array.Empty<CitySuggestion>(),
                0,
                0,
                CitySearchFailureKind.Timeout,
                $"A busca demorou mais que {RequestTimeout.TotalSeconds:0} segundos.");
            activity?.SetStatus(ActivityStatusCode.Error, result.FailureMessage);
            if (logger is not null)
            {
                LogAutocompleteTimeout(logger);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            var result = new CitySearchResult(
                Array.Empty<CitySuggestion>(),
                0,
                0,
                CitySearchFailureKind.Timeout,
                $"A busca demorou mais que {RequestTimeout.TotalSeconds:0} segundos.");
            activity?.SetStatus(ActivityStatusCode.Error, result.FailureMessage);
            if (logger is not null)
            {
                LogAutocompleteTimeout(logger);
            }

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            var result = new CitySearchResult(
                Array.Empty<CitySuggestion>(),
                0,
                0,
                CitySearchFailureKind.Http,
                $"Falha HTTP ao consultar o Open-Meteo: {ex.Message}");
            AppObservability.SetException(activity, ex);
            if (logger is not null)
            {
                LogAutocompleteCircuitOpen(logger, ex);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            var result = new CitySearchResult(
                Array.Empty<CitySuggestion>(),
                0,
                0,
                CitySearchFailureKind.Http,
                $"Falha HTTP ao consultar o Open-Meteo: {ex.Message}");
            AppObservability.SetException(activity, ex);
            if (logger is not null)
            {
                LogAutocompleteHttpFailure(logger, ex);
            }

            return result;
        }
        catch (JsonException ex)
        {
            var result = new CitySearchResult(
                Array.Empty<CitySuggestion>(),
                0,
                0,
                CitySearchFailureKind.InvalidResponse,
                $"Resposta invalida do Open-Meteo: {ex.Message}");
            AppObservability.SetException(activity, ex);
            if (logger is not null)
            {
                LogAutocompleteInvalidPayload(logger, ex);
            }

            return result;
        }
    }

    private HttpClient ResolveClient()
    {
        return client ?? clientFactory!.CreateClient(clientName!);
    }

    private string ResolveClientNameForTelemetry() => clientName ?? "custom-http-client";

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

    [LoggerMessage(EventId = 1500, Level = LogLevel.Information, Message = "Autocomplete retornou {SuggestionCount} sugestoes.")]
    private static partial void LogAutocompleteSuccess(ILogger logger, int suggestionCount);

    [LoggerMessage(EventId = 1501, Level = LogLevel.Warning, Message = "Autocomplete falhou por timeout.")]
    private static partial void LogAutocompleteTimeout(ILogger logger);

    [LoggerMessage(EventId = 1502, Level = LogLevel.Warning, Message = "Autocomplete falhou com circuit breaker aberto.")]
    private static partial void LogAutocompleteCircuitOpen(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1503, Level = LogLevel.Warning, Message = "Autocomplete falhou com erro HTTP.")]
    private static partial void LogAutocompleteHttpFailure(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1504, Level = LogLevel.Warning, Message = "Autocomplete recebeu payload invalido.")]
    private static partial void LogAutocompleteInvalidPayload(ILogger logger, Exception exception);
}
