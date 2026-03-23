using App.WinUI.Views;

namespace Integration.Smoke;

public sealed class ShellPageContentFactoryTests
{
    [Fact]
    public void Resolve_ShouldCachePagesPerTag()
    {
        var visualizerPage = new object();
        var devicesPage = new object();
        var panelsPage = new object();
        var monitoringPage = new object();
        var studioPage = new object();
        var visualizerResolutions = 0;
        var panelsResolutions = 0;
        var monitoringResolutions = 0;
        var studioResolutions = 0;

        var factory = new ShellPageContentFactory(
            () =>
            {
                visualizerResolutions++;
                return visualizerPage;
            },
            () => devicesPage,
            () =>
            {
                panelsResolutions++;
                return panelsPage;
            },
            () =>
            {
                monitoringResolutions++;
                return monitoringPage;
            },
            () => new object(),
            () =>
            {
                studioResolutions++;
                return studioPage;
            });

        var first = factory.Resolve("visualizer");
        var second = factory.Resolve("visualizer");
        var panelsFirst = factory.Resolve("panels");
        var panelsSecond = factory.Resolve("panels");
        var monitoringFirst = factory.Resolve("monitoring");
        var monitoringSecond = factory.Resolve("monitoring");
        var studioFirst = factory.Resolve("visualizer-studio");
        var studioSecond = factory.Resolve("visualizer-studio");

        Assert.Same(visualizerPage, first);
        Assert.Same(first, second);
        Assert.Same(panelsPage, panelsFirst);
        Assert.Same(panelsFirst, panelsSecond);
        Assert.Same(monitoringPage, monitoringFirst);
        Assert.Same(monitoringFirst, monitoringSecond);
        Assert.Same(studioPage, studioFirst);
        Assert.Same(studioFirst, studioSecond);
        Assert.Equal(1, visualizerResolutions);
        Assert.Equal(1, panelsResolutions);
        Assert.Equal(1, monitoringResolutions);
        Assert.Equal(1, studioResolutions);
    }

    [Fact]
    public void TryResolve_ShouldCapturePageConstructionFailures()
    {
        var factory = new ShellPageContentFactory(
            () => throw new InvalidOperationException("boom"),
            () => new object(),
            () => new object(),
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
