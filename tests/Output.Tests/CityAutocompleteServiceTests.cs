using System.Net;
using System.Net.Http;
using System.Text;
using App.WinUI.Services.Apps;

namespace Output.Tests;

public sealed class CityAutocompleteServiceTests
{
    [Fact]
    public async Task SearchWithDiagnosticsAsync_ShouldRequestBrazilResultsAndMapSuggestions()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateJsonContent(
                    """
                    {
                      "results": [
                        {
                          "name": "Sao Paulo",
                          "admin1": "Sao Paulo",
                          "country": "Brazil",
                          "country_code": "BR",
                          "latitude": -23.5505,
                          "longitude": -46.6333
                        }
                      ]
                    }
                    """),
            });
        });
        using var client = CreateClient(handler);
        var service = new CityAutocompleteService(client);

        var result = await service.SearchWithDiagnosticsAsync("Sao Paulo");

        Assert.False(result.HasFailure);
        Assert.Equal(1, result.RawResultCount);
        Assert.Equal(1, result.FilteredResultCount);
        var suggestion = Assert.Single(result.Suggestions);
        Assert.Equal("Sao Paulo", suggestion.Name);
        Assert.Equal("Sao Paulo", suggestion.Region);
        Assert.Equal("Brasil", suggestion.Country);
        Assert.Equal(-23.5505, suggestion.Latitude);
        Assert.Equal(-46.6333, suggestion.Longitude);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Contains("countryCode=BR", handler.LastRequestUri!.Query, StringComparison.Ordinal);
        Assert.Contains("language=pt", handler.LastRequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("name=Sao%20Paulo", handler.LastRequestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchWithDiagnosticsAsync_ShouldReturnEmptyWhenApiReturnsNoResults()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateJsonContent("""{ "results": [] }"""),
            });
        });
        using var client = CreateClient(handler);
        var service = new CityAutocompleteService(client);

        var result = await service.SearchWithDiagnosticsAsync("Ri");

        Assert.False(result.HasFailure);
        Assert.Empty(result.Suggestions);
        Assert.Equal(0, result.RawResultCount);
        Assert.Equal(0, result.FilteredResultCount);
    }

    [Fact]
    public async Task SearchWithDiagnosticsAsync_ShouldReportTimeoutFailures()
    {
        using var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException("timeout"));
        using var client = CreateClient(handler);
        var service = new CityAutocompleteService(client);

        var result = await service.SearchWithDiagnosticsAsync("Santos");

        Assert.True(result.HasFailure);
        Assert.Equal(CityAutocompleteService.CitySearchFailureKind.Timeout, result.FailureKind);
        Assert.Contains("demorou mais", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task SearchWithDiagnosticsAsync_ShouldReportHttpFailures()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = CreateJsonContent("""{ "error": true, "reason": "upstream failed" }"""),
            });
        });
        using var client = CreateClient(handler);
        var service = new CityAutocompleteService(client);

        var result = await service.SearchWithDiagnosticsAsync("Curitiba");

        Assert.True(result.HasFailure);
        Assert.Equal(CityAutocompleteService.CitySearchFailureKind.Http, result.FailureKind);
        Assert.Contains("Open-Meteo", result.FailureMessage, StringComparison.Ordinal);
        Assert.Empty(result.Suggestions);
    }

    private static HttpClient CreateClient(StubHttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            Timeout = CityAutocompleteService.RequestTimeout,
        };
    }

    private static StringContent CreateJsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            this.responder = responder;
        }

        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            return responder(request, cancellationToken);
        }
    }
}
