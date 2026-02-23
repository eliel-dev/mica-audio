using App.WinUI.Models.Apps;
using App.WinUI.Services.Apps;

namespace Output.Tests;

public sealed class AppRuntimeProviderRegistryTests
{
    [Fact]
    public void Resolve_ReturnsProviderByRuntimeKind()
    {
        using var registry = new AppRuntimeProviderRegistry([
            new FakeRuntimeProvider("alpha"),
            new FakeRuntimeProvider("beta")
        ]);

        var item = new AppCatalogItem { Runtime = new AppRuntimeDefinition { Kind = "beta" } };
        var provider = registry.Resolve(item);

        Assert.NotNull(provider);
        Assert.Equal("beta", ((FakeRuntimeProvider)provider!).Kind);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenKindNotMapped()
    {
        using var registry = new AppRuntimeProviderRegistry([new FakeRuntimeProvider("alpha")]);
        var item = new AppCatalogItem { Runtime = new AppRuntimeDefinition { Kind = "unknown" } };

        Assert.Null(registry.Resolve(item));
    }

    private sealed class FakeRuntimeProvider : IAppRuntimeProvider
    {
        public FakeRuntimeProvider(string kind)
        {
            Kind = kind;
            SupportedKinds = [kind];
        }

        public string Kind { get; }

        public IReadOnlyList<string> SupportedKinds { get; }

        public bool CanHandle(AppCatalogItem item)
            => string.Equals(item.Runtime?.Kind, Kind, StringComparison.OrdinalIgnoreCase);

        public void Attach(AppRuntimeHost host) { }

        public void OnSelected(AppCatalogItem item) { }

        public Task OnConfigSavedAsync(AppCatalogItem item, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
            => Task.CompletedTask;

        public void OnDeselected(AppCatalogItem item) { }

        public void Dispose() { }
    }
}

namespace App.WinUI.Services.Apps;

using App.WinUI.Models.Apps;

internal interface IAppRuntimeProvider : IDisposable
{
    IReadOnlyList<string> SupportedKinds { get; }
    bool CanHandle(AppCatalogItem item);
    void Attach(AppRuntimeHost host);
    void OnSelected(AppCatalogItem item);
    Task OnConfigSavedAsync(AppCatalogItem item, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
    void OnDeselected(AppCatalogItem item);
}

internal sealed class AppRuntimeHost;
