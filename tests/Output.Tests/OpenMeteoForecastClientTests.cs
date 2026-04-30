using System.Net;
using System.Net.Http;
using System.Text;
using App.WinUI.Infrastructure.Observability;
using App.WinUI.Services.Apps;

namespace Output.Tests;

public sealed class OpenMeteoForecastClientTests
{
    [Fact]
    public async Task GetCurrentAsync_ShouldMapCurrentWeatherFromApi()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateJsonContent(
                    """
                    {
                      "current": {
                        "temperature_2m": 21.5,
                        "weathercode": 3
                      }
                    }
                    """),
            });
        });
        using var client = CreateClient(handler);
        var forecastClient = new OpenMeteoForecastClient(client);

        var result = await forecastClient.GetCurrentAsync(-26.82f, -49.27f, "America/Sao_Paulo");

        Assert.Equal(21.5, result.Temperature);
        Assert.Equal(3, result.WeatherCode);
        Assert.NotNull(handler.LastRequestUri);
        Assert.Equal("api.open-meteo.com", handler.LastRequestUri!.Host);
        Assert.Equal("/v1/forecast", handler.LastRequestUri.AbsolutePath);
        Assert.Contains("current=temperature_2m,weathercode", handler.LastRequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("temperature_unit=celsius", handler.LastRequestUri.Query, StringComparison.Ordinal);
        Assert.Contains("timezone=America%2FSao_Paulo", handler.LastRequestUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetCurrentAsync_ShouldReturnEmptySnapshotData_WhenPayloadHasNoCurrentBlock()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateJsonContent("""{ }"""),
            });
        });
        using var client = CreateClient(handler);
        var forecastClient = new OpenMeteoForecastClient(client);

        var result = await forecastClient.GetCurrentAsync(-26.82f, -49.27f, "America/Sao_Paulo");

        Assert.Null(result.Temperature);
        Assert.Null(result.WeatherCode);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.BadGateway)]
    public async Task GetCurrentAsync_ShouldThrowHttpRequestException_WhenApiReturnsTransientFailure(HttpStatusCode statusCode)
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = CreateJsonContent("""{ "error": true }"""),
            });
        });
        using var client = CreateClient(handler);
        var forecastClient = new OpenMeteoForecastClient(client);

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => forecastClient.GetCurrentAsync(-26.82f, -49.27f, "America/Sao_Paulo"));

        Assert.Equal(statusCode, ex.StatusCode);
    }

    [Fact]
    public async Task GetCurrentAsync_ShouldPropagateTimeoutFailures()
    {
        using var handler = new StubHttpMessageHandler((_, _) => throw new TaskCanceledException("timeout"));
        using var client = CreateClient(handler);
        var forecastClient = new OpenMeteoForecastClient(client);

        await Assert.ThrowsAsync<TaskCanceledException>(() => forecastClient.GetCurrentAsync(-26.82f, -49.27f, "America/Sao_Paulo"));
    }

    [Fact]
    public async Task GetCurrentAsync_ShouldEmitWeatherPreviewActivity()
    {
        using var handler = new StubHttpMessageHandler((_, _) =>
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = CreateJsonContent("""{ "current": { "temperature_2m": 20.1, "weathercode": 2 } }"""),
            });
        });
        using var capture = new ActivityCapture(AppObservability.ActivitySourceName);
        using var client = CreateClient(handler);
        var forecastClient = new OpenMeteoForecastClient(client);

        _ = await forecastClient.GetCurrentAsync(-26.82f, -49.27f, "America/Sao_Paulo");

        var activity = Assert.Single(capture.CompletedActivities, item => item.OperationName == "weather-preview.fetch-current");
        Assert.Equal("weather-preview", activity.GetTagItem(AppObservability.FeatureKey));
        Assert.Equal("custom-http-client", activity.GetTagItem(AppObservability.HttpClientNameKey));
        Assert.Equal(20.1, activity.GetTagItem("weather.temperature"));
    }

    private static HttpClient CreateClient(StubHttpMessageHandler handler)
    {
        return new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.open-meteo.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
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
