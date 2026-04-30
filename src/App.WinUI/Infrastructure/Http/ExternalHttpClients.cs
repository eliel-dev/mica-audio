using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace App.WinUI.Infrastructure.Http;

// DOCS: docs/wiki/modules/app-winui.md#integracoes-http-externas
// DOCS: docs/wiki/modules/apps-catalog-deployment.md#integracoes-http-externas
internal enum ExternalHttpTimeoutProfile
{
    Short,
    Medium,
    Long,
}

// DOCS: docs/wiki/modules/app-winui.md#integracoes-http-externas
// DOCS: docs/wiki/modules/apps-catalog-deployment.md#integracoes-http-externas
internal static class ExternalHttpClients
{
    public const string OpenMeteoGeocodingClientName = "open-meteo-geocoding";
    public const string OpenMeteoForecastClientName = "open-meteo-forecast";

    public static IServiceCollection AddExternalHttpClients(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = AddExternalClient(
            services,
            OpenMeteoGeocodingClientName,
            "https://geocoding-api.open-meteo.com/",
            ExternalHttpTimeoutProfile.Short);

        _ = AddExternalClient(
            services,
            OpenMeteoForecastClientName,
            "https://api.open-meteo.com/",
            ExternalHttpTimeoutProfile.Medium);

        return services;
    }

    internal static (TimeSpan TotalRequestTimeout, TimeSpan AttemptTimeout) GetTimeouts(ExternalHttpTimeoutProfile profile)
    {
        return profile switch
        {
            ExternalHttpTimeoutProfile.Short => (TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(3)),
            ExternalHttpTimeoutProfile.Medium => (TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(5)),
            ExternalHttpTimeoutProfile.Long => (TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(10)),
            _ => throw new ArgumentOutOfRangeException(nameof(profile), profile, "Perfil de timeout HTTP externo invalido."),
        };
    }

    private static IHttpClientBuilder AddExternalClient(
        IServiceCollection services,
        string name,
        string baseAddress,
        ExternalHttpTimeoutProfile timeoutProfile)
    {
        var (totalTimeout, attemptTimeout) = GetTimeouts(timeoutProfile);
        var builder = services.AddHttpClient(name, client =>
        {
            client.BaseAddress = new Uri(baseAddress, UriKind.Absolute);
            client.Timeout = Timeout.InfiniteTimeSpan;
        });

        builder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = totalTimeout;
            options.AttemptTimeout.Timeout = attemptTimeout;
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.FailureRatio = 0.5d;
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            options.Retry.DisableForUnsafeHttpMethods();
        });

        return builder;
    }
}
