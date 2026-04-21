using System.Globalization;
using App.WinUI.Services;
using App.WinUI.Services.Monitoring;
using App.WinUI.ViewModels;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
public sealed partial class MonitoringPage : Page, IDisposable
{
    private readonly MonitoringPageViewModel viewModel;
    private readonly HwinfoSharedMemorySource source;
    private readonly SemaphoreSlim refreshGate = new(1, 1);

    private CancellationTokenSource? refreshLoopCts;
    private Task? refreshLoopTask;
    private int currentKpiColumns = -1;

    internal MonitoringPage(
        MonitoringPageViewModel viewModel,
        HwinfoSharedMemorySource source)
    {
        this.viewModel = viewModel;
        this.source = source;

        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnPageSizeChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (refreshLoopCts is not null)
        {
            return;
        }

        var cts = new CancellationTokenSource();
        refreshLoopCts = cts;

        try
        {
            UpdateAdaptiveLayout(ActualWidth);
            ApplySnapshot(MonitoringSnapshot.Empty);
            await RefreshSnapshotAsync().ConfigureAwait(false);
            if (!ShouldStartRefreshLoop(refreshLoopCts, cts))
            {
                return;
            }

            refreshLoopTask = RunRefreshLoopAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(refreshLoopCts, cts))
            {
                CancelRefreshLoop();
            }

            App.ReportError("MonitoringPage.OnLoaded failed", ex);
            await DispatcherQueue.EnqueueAsync(() =>
            {
                viewModel.SourceStatus = "Falha ao carregar monitoramento";
                viewModel.SourceHint = "A sessao foi mantida aberta, mas a leitura inicial falhou.";
                SourceStatusText.Text = viewModel.SourceStatus;
                SourceHintText.Text = viewModel.SourceHint;
            }).ConfigureAwait(false);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelRefreshLoop();
    }

    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateAdaptiveLayout(e.NewSize.Width);
    }

    private async Task RunRefreshLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await RefreshSnapshotAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefreshSnapshotAsync()
    {
        await RefreshSnapshotAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private async Task RefreshSnapshotAsync(CancellationToken cancellationToken)
    {
        if (!await refreshGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            var snapshot = await Task.Run(source.ReadSnapshot, cancellationToken).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await DispatcherQueue.EnqueueAsync(() => ApplySnapshot(snapshot)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            refreshGate.Release();
        }
    }

    private void ApplySnapshot(MonitoringSnapshot snapshot)
    {
        viewModel.SourceStatus = snapshot.StateText;
        viewModel.SourceHint = snapshot.HintText;
        viewModel.LastRefreshText = BuildLastRefreshText(snapshot.CapturedAtUtc);
        viewModel.SensorCountText = BuildSensorCountText(snapshot.SensorCount, snapshot.ReadingCount);

        SourceStatusText.Text = viewModel.SourceStatus;
        SourceHintText.Text = viewModel.SourceHint;
        LastRefreshText.Text = viewModel.LastRefreshText;
        SensorCountText.Text = viewModel.SensorCountText;
        ApplyKpis(snapshot.Kpis);
    }

    private void ApplyKpis(IReadOnlyList<MonitoringKpi> kpis)
    {
        for (var index = 0; index < KpiTiles.Length; index++)
        {
            var tile = KpiTiles[index];
            var kpi = index < kpis.Count
                ? kpis[index]
                : BuildFallbackKpi(index);

            tile.Title.Text = kpi.Title;
            tile.Context.Text = BuildKpiContextText(kpi);

            var capacity = kpi.Capacity;
            var usesCapacity = capacity is not null;
            tile.MetricsHost.Visibility = usesCapacity ? Visibility.Collapsed : Visibility.Visible;
            tile.Capacity.Root.Visibility = usesCapacity ? Visibility.Visible : Visibility.Collapsed;

            if (usesCapacity)
            {
                ApplyCapacity(tile, kpi, capacity!);
                tile.Root.Opacity = kpi.IsAvailable ? 1d : 0.72d;
                continue;
            }

            for (var metricIndex = 0; metricIndex < tile.Metrics.Length; metricIndex++)
            {
                var metric = metricIndex < kpi.Metrics.Count
                    ? kpi.Metrics[metricIndex]
                    : MonitoringKpiMetric.Unavailable("Sem dado", "-");
                var metricView = tile.Metrics[metricIndex];
                metricView.Label.Text = metric.Label;
                metricView.Hardware.Text = BuildMetricHardwareText(metric);
                metricView.Value.Text = metric.ValueText;
                metricView.Progress.Visibility = metric.Progress.HasValue ? Visibility.Visible : Visibility.Collapsed;
                metricView.Progress.Value = metric.Progress ?? 0d;
                metricView.Root.Opacity = metric.IsAvailable ? 1d : 0.64d;
            }

            tile.Root.Opacity = kpi.IsAvailable ? 1d : 0.72d;
        }
    }

    private static void ApplyCapacity(KpiTileView tile, MonitoringKpi kpi, MonitoringKpiCapacity capacity)
    {
        var usedMetric = kpi.Metrics.Count > 0 ? kpi.Metrics[0] : MonitoringKpiMetric.Unavailable("Usada", "-");
        tile.Capacity.UsedLabel.Text = usedMetric.Label;
        tile.Capacity.UsedValue.Text = capacity.UsedText;
        tile.Capacity.BarText.Text = capacity.BarText;
        tile.Capacity.Progress.Value = capacity.Progress ?? 0d;
        tile.Capacity.Progress.Visibility = capacity.Progress.HasValue ? Visibility.Visible : Visibility.Collapsed;
        tile.Capacity.BarText.Visibility = capacity.IsAvailable ? Visibility.Visible : Visibility.Collapsed;
        tile.Capacity.AvailableValue.Text = "Disponivel: " + capacity.AvailableText;
        tile.Capacity.TotalValue.Text = "Total: " + capacity.TotalText;
        tile.Capacity.Root.Opacity = capacity.IsAvailable ? 1d : 0.64d;
    }

    private void UpdateAdaptiveLayout(double width)
    {
        var columns = ResolveKpiColumns(width);
        if (currentKpiColumns == columns)
        {
            return;
        }

        currentKpiColumns = columns;
        ReflowKpiTiles(columns);
    }

    internal static string BuildLastRefreshText(DateTimeOffset? capturedAtUtc)
        => capturedAtUtc.HasValue
            ? "Ultima leitura: " + capturedAtUtc.Value.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture)
            : "Ultima leitura: -";

    internal static string BuildSensorCountText(int sensorCount, int readingCount)
        => $"Sensores: {Math.Max(sensorCount, 0)} | Leituras: {Math.Max(readingCount, 0)}";

    internal static int ResolveKpiColumns(double width)
        => width < 720d ? 1 : width < 1140d ? 2 : 3;

    internal static string BuildMetricHardwareText(MonitoringKpiMetric metric)
        => metric.DataOrigin == MonitoringDataOrigin.WindowsFallback
            ? metric.HardwareName + " | Windows"
            : metric.HardwareName;

    internal static string BuildKpiContextText(MonitoringKpi kpi)
        => kpi.Capacity?.DataOrigin == MonitoringDataOrigin.WindowsFallback
            ? kpi.ContextText + " | Windows"
            : kpi.ContextText;

    internal static bool ShouldStartRefreshLoop(CancellationTokenSource? activeLoopCts, CancellationTokenSource candidate)
        => ReferenceEquals(activeLoopCts, candidate) && !candidate.IsCancellationRequested;

    public void Dispose()
    {
        CancelRefreshLoop();
        refreshGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private void CancelRefreshLoop()
    {
        var cts = refreshLoopCts;
        refreshLoopCts = null;
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
        refreshLoopTask = null;
    }

    private static MonitoringKpi BuildFallbackKpi(int index)
        => new(
            "empty-" + index,
            "Sem dado",
            "Aguardando sensores",
            [
                MonitoringKpiMetric.Unavailable("Metrica 1", "-"),
                MonitoringKpiMetric.Unavailable("Metrica 2", "-"),
            ],
            capacity: null);
}
