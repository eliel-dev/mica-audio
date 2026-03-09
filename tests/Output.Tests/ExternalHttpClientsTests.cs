using App.WinUI.Infrastructure.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Output.Tests;

public sealed class ExternalHttpClientsTests
{
    [Fact]
    public void AddExternalHttpClients_ShouldRegisterConfiguredNamedClients()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddExternalHttpClients();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var geocodingClient = factory.CreateClient(ExternalHttpClients.OpenMeteoGeocodingClientName);
        using var forecastClient = factory.CreateClient(ExternalHttpClients.OpenMeteoForecastClientName);

        Assert.Equal(new Uri("https://geocoding-api.open-meteo.com/"), geocodingClient.BaseAddress);
        Assert.Equal(new Uri("https://api.open-meteo.com/"), forecastClient.BaseAddress);
        Assert.Equal(Timeout.InfiniteTimeSpan, geocodingClient.Timeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, forecastClient.Timeout);
    }

    [Fact]
    public void GetTimeouts_ShouldReturnExpectedProfiles()
    {
        Assert.Equal((TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(3)), ExternalHttpClients.GetTimeouts(ExternalHttpTimeoutProfile.Short));
        Assert.Equal((TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5)), ExternalHttpClients.GetTimeouts(ExternalHttpTimeoutProfile.Medium));
        Assert.Equal((TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10)), ExternalHttpClients.GetTimeouts(ExternalHttpTimeoutProfile.Long));
    }
}
