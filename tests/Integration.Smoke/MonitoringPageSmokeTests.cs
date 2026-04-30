using System.Reflection;
using App.WinUI.Services.Monitoring;
using App.WinUI.Views;

namespace Integration.Smoke;

public sealed class MonitoringPageSmokeTests
{
    [Fact]
    public void MonitoringPageShouldDeclareDashboardFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(MonitoringPage).GetField("SourceStatusText", flags));
        Assert.NotNull(typeof(MonitoringPage).GetField("SourceHintText", flags));
        Assert.NotNull(typeof(MonitoringPage).GetField("LastRefreshText", flags));
        Assert.NotNull(typeof(MonitoringPage).GetField("SensorCountText", flags));
        Assert.NotNull(typeof(MonitoringPage).GetField("KpiGrid", flags));
    }

    [Fact]
    public void MonitoringPageShouldNotDeclareLegacyReadingsOrRefreshFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.Null(typeof(MonitoringPage).GetField("RefreshButton", flags));
        Assert.Null(typeof(MonitoringPage).GetField("RefreshProgressRing", flags));
        Assert.Null(typeof(MonitoringPage).GetField("SearchBox", flags));
        Assert.Null(typeof(MonitoringPage).GetField("ReadingsGroupsHost", flags));
        Assert.Null(typeof(MonitoringPage).GetField("EmptyReadingsText", flags));
    }

    [Fact]
    public void BuildSummaryTexts_ShouldRenderExpectedPlaceholders()
    {
        Assert.Equal("Ultima leitura: -", MonitoringPage.BuildLastRefreshText(null));
        Assert.Equal("Sensores: 3 | Leituras: 12", MonitoringPage.BuildSensorCountText(3, 12));
    }

    [Theory]
    [InlineData(640, 1)]
    [InlineData(960, 2)]
    [InlineData(1280, 3)]
    public void ResolveKpiColumns_ShouldReflowAcrossBreakpoints(double width, int expectedColumns)
    {
        Assert.Equal(expectedColumns, MonitoringPage.ResolveKpiColumns(width));
    }

    [Fact]
    public void BuildMetricHardwareText_ShouldExposeWindowsFallbackOrigin()
    {
        var metric = new MonitoringKpiMetric("Usada", "Memoria do sistema", "12 GB", progress: 0.4d, isAvailable: true, MonitoringDataOrigin.WindowsFallback);

        Assert.Equal("Memoria do sistema | Windows", MonitoringPage.BuildMetricHardwareText(metric));
    }

    [Fact]
    public void BuildKpiContextText_ShouldExposeWindowsFallbackOriginForCapacityCards()
    {
        var kpi = new MonitoringKpi(
            "ram-usage",
            "Memoria RAM",
            "Memoria do sistema",
            [new MonitoringKpiMetric("Usada", "Memoria do sistema", "12 GB", progress: 0.4d, isAvailable: true, MonitoringDataOrigin.WindowsFallback)],
            new MonitoringKpiCapacity("12 GB", "20 GB", "32 GB", "37.5% (12 GB)", progress: 0.4d, isAvailable: true, MonitoringDataOrigin.WindowsFallback));

        Assert.Equal("Memoria do sistema | Windows", MonitoringPage.BuildKpiContextText(kpi));
    }

    [Fact]
    public void ShouldStartRefreshLoop_ShouldRejectReplacedOrCancelledTokenSource()
    {
        using var active = new CancellationTokenSource();
        using var replaced = new CancellationTokenSource();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.True(MonitoringPage.ShouldStartRefreshLoop(active, active));
        Assert.False(MonitoringPage.ShouldStartRefreshLoop(replaced, active));
        Assert.False(MonitoringPage.ShouldStartRefreshLoop(cancelled, cancelled));
        Assert.False(MonitoringPage.ShouldStartRefreshLoop(null, active));
    }
}
