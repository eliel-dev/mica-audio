using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Output.Tests;

public sealed class HybridCacheBootstrapTests
{
    [Fact]
    public void AddHybridCache_ShouldRegisterHybridCacheService()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<HybridCache>());
    }
}
