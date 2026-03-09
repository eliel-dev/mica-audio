using App.WinUI.Views;

namespace Integration.Smoke;

public sealed class ShellPageContentFactoryTests
{
    [Fact]
    public void Resolve_ShouldCachePagesPerTag()
    {
        var visualizerPage = new object();
        var devicesPage = new object();
        var appsPage = new object();
        var visualizerResolutions = 0;

        var factory = new ShellPageContentFactory(
            () =>
            {
                visualizerResolutions++;
                return visualizerPage;
            },
            () => devicesPage,
            () => appsPage,
            () => new object());

        var first = factory.Resolve("visualizer");
        var second = factory.Resolve("visualizer");

        Assert.Same(visualizerPage, first);
        Assert.Same(first, second);
        Assert.Equal(1, visualizerResolutions);
    }

    [Fact]
    public void TryResolve_ShouldCapturePageConstructionFailures()
    {
        var factory = new ShellPageContentFactory(
            () => throw new InvalidOperationException("boom"),
            () => new object(),
            () => new object(),
            () => new object());

        var resolved = factory.TryResolve("visualizer", out var page, out var exception);

        Assert.False(resolved);
        Assert.Null(page);
        Assert.NotNull(exception);
        Assert.Equal("boom", exception.Message);
    }
}
