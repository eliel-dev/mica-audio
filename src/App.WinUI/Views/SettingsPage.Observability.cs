using System.Globalization;
using App.WinUI.Services.Devices;
using Device.Protocol.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

public sealed partial class SettingsPage
{
    private bool deviceObservabilityActive;
    private bool suppressObservedDeviceSelectionChanged;
    private DeviceOperationsState currentDeviceOperationsState = new();
    private string? selectedObservedDeviceId;
    private string? currentObservedDeviceLogsDeviceId;
    private IReadOnlyList<DeviceLogEntry> currentObservedDeviceLogEntries = Array.Empty<DeviceLogEntry>();

    private ComboBox observedDeviceComboBox = null!;
    private TextBlock observedDeviceSummaryText = null!;
    private TextBlock observedStatisticsStatusText = null!;
    private StackPanel observedStatisticsContentPanel = null!;
    private TextBlock observedStatsIdentityFirmwareText = null!;
    private TextBlock observedStatsIdentityChipText = null!;
    private TextBlock observedStatsIdentitySdkText = null!;
    private TextBlock observedStatsIdentityBoardText = null!;
    private TextBlock observedStatsCapacityHeapText = null!;
    private TextBlock observedStatsCapacityPsramText = null!;
    private TextBlock observedStatsCapacityFlashText = null!;
    private TextBlock observedStatsCapacitySketchText = null!;
    private TextBlock observedStatsStreamSummaryText = null!;
    private TextBlock observedStatsStreamFramesText = null!;
    private TextBlock observedStatsStreamIntegrityText = null!;
    private TextBlock observedStatsStreamHeartbeatText = null!;
    private TextBlock observedRssiHistorySummaryText = null!;
    private TextBlock observedLoopHistorySummaryText = null!;
    private TextBlock observedHeapHistorySummaryText = null!;
    private TextBlock observedPsramHistorySummaryText = null!;
    private StackPanel observedRssiHistoryBarsPanel = null!;
    private StackPanel observedLoopHistoryBarsPanel = null!;
    private StackPanel observedHeapHistoryBarsPanel = null!;
    private StackPanel observedPsramHistoryBarsPanel = null!;
    private TextBlock observedDeviceLogsHeaderText = null!;
    private TextBlock observedDeviceLogsHintText = null!;
    private ComboBox observedDeviceLogsSeverityComboBox = null!;
    private TextBox observedDeviceLogsSearchBox = null!;
    private ToggleSwitch observedDeviceLogsAutoFollowToggle = null!;
    private ListView observedDeviceLogsListView = null!;
    private TextBlock observedDeviceLogsPlaceholderText = null!;

    private void ActivateDeviceObservability()
    {
        if (deviceObservabilityActive)
        {
            return;
        }

        deviceOps.StateChanged += OnDeviceOperationsStateChanged;
        deviceObservabilityActive = true;
        ApplyDeviceObservabilityState(deviceOps.GetStateSnapshot());
    }

    private void DeactivateDeviceObservability()
    {
        if (!deviceObservabilityActive)
        {
            return;
        }

        deviceOps.StateChanged -= OnDeviceOperationsStateChanged;
        deviceObservabilityActive = false;
    }

    private void OnDeviceOperationsStateChanged(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(() => ApplyDeviceObservabilityState(deviceOps.GetStateSnapshot()));
    }

    private Border BuildDeviceObservabilityCard()
    {
        var content = new StackPanel
        {
            Spacing = 12,
        };

        content.Children.Add(new TextBlock
        {
            Text = "Device alvo",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        });

        observedDeviceComboBox = new ComboBox
        {
            PlaceholderText = "Nenhum device disponivel",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        observedDeviceComboBox.SelectionChanged += OnObservedDeviceSelectionChanged;
        content.Children.Add(observedDeviceComboBox);

        observedDeviceSummaryText = new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            Text = "Carregando devices conhecidos...",
        };
        content.Children.Add(observedDeviceSummaryText);

        var logsExpander = new Expander
        {
            Header = "Logs",
            IsExpanded = false,
            Content = BuildObservedLogsContent(),
        };
        content.Children.Add(logsExpander);

        return CreateCard(
            "Observabilidade do device",
            "Escolha um device para ver os logs estruturados desta sessao, sem depender da selecao atual da DevicesPage. As estatisticas ficaram ocultas temporariamente para curadoria do dashboard principal.",
            content);
    }

    // DOCS: docs/wiki/reference/device-observability-dashboard.md#estatisticas-fora-da-ui-por-enquanto
    private StackPanel BuildObservedStatisticsContent()
    {
        var host = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(0, 12, 0, 0),
        };

        observedStatisticsStatusText = new TextBlock
        {
            Text = "Selecione um device acima para ver estatisticas e historico de telemetria.",
            Opacity = 0.82,
            TextWrapping = TextWrapping.Wrap,
        };
        host.Children.Add(observedStatisticsStatusText);

        observedStatisticsContentPanel = new StackPanel
        {
            Spacing = 12,
            Visibility = Visibility.Collapsed,
        };

        observedStatisticsContentPanel.Children.Add(CreateSurfaceCard(BuildObservedStatisticsIdentityCard()));
        observedStatisticsContentPanel.Children.Add(CreateSurfaceCard(BuildObservedStatisticsCapacityCard()));
        observedStatisticsContentPanel.Children.Add(CreateSurfaceCard(BuildObservedStatisticsStreamCard()));
        observedStatisticsContentPanel.Children.Add(CreateSurfaceCard(BuildObservedHistoryCard("RSSI Wi-Fi", Color.FromArgb(255, 86, 156, 214), out observedRssiHistorySummaryText, out observedRssiHistoryBarsPanel)));
        observedStatisticsContentPanel.Children.Add(CreateSurfaceCard(BuildObservedHistoryCard("Carga do loop", Color.FromArgb(255, 78, 201, 176), out observedLoopHistorySummaryText, out observedLoopHistoryBarsPanel)));
        observedStatisticsContentPanel.Children.Add(CreateSurfaceCard(BuildObservedHistoryCard("Heap livre", Color.FromArgb(255, 108, 203, 95), out observedHeapHistorySummaryText, out observedHeapHistoryBarsPanel)));
        observedStatisticsContentPanel.Children.Add(CreateSurfaceCard(BuildObservedHistoryCard("PSRAM livre", Color.FromArgb(255, 155, 89, 182), out observedPsramHistorySummaryText, out observedPsramHistoryBarsPanel)));

        host.Children.Add(observedStatisticsContentPanel);
        return host;
    }

    // DOCS: docs/wiki/reference/device-observability-dashboard.md#logs-em-configuracoes
    private StackPanel BuildObservedLogsContent()
    {
        var host = new StackPanel
        {
            Spacing = 12,
            Margin = new Thickness(0, 12, 0, 0),
        };

        observedDeviceLogsHeaderText = new TextBlock
        {
            Text = "Logs do device",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        host.Children.Add(observedDeviceLogsHeaderText);

        observedDeviceLogsHintText = new TextBlock
        {
            Text = "Eventos do app e do firmware para o device selecionado no combo acima.",
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
        };
        host.Children.Add(observedDeviceLogsHintText);

        var filters = new Grid
        {
            ColumnSpacing = 12,
        };
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        filters.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        observedDeviceLogsSeverityComboBox = new ComboBox
        {
            MinWidth = 150,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        observedDeviceLogsSeverityComboBox.Items.Add(new ComboBoxItem { Content = "Todos", Tag = "all" });
        observedDeviceLogsSeverityComboBox.Items.Add(new ComboBoxItem { Content = "Info", Tag = "info" });
        observedDeviceLogsSeverityComboBox.Items.Add(new ComboBoxItem { Content = "Avisos", Tag = "warning" });
        observedDeviceLogsSeverityComboBox.Items.Add(new ComboBoxItem { Content = "Erros", Tag = "error" });
        observedDeviceLogsSeverityComboBox.SelectedIndex = 0;
        observedDeviceLogsSeverityComboBox.SelectionChanged += OnObservedDeviceLogsFilterChanged;
        filters.Children.Add(observedDeviceLogsSeverityComboBox);

        observedDeviceLogsSearchBox = new TextBox
        {
            PlaceholderText = "Buscar por mensagem, categoria ou codigo",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        observedDeviceLogsSearchBox.TextChanged += OnObservedDeviceLogsSearchTextChanged;
        Grid.SetColumn(observedDeviceLogsSearchBox, 1);
        filters.Children.Add(observedDeviceLogsSearchBox);

        observedDeviceLogsAutoFollowToggle = new ToggleSwitch
        {
            Header = "Auto-follow",
            IsOn = true,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(observedDeviceLogsAutoFollowToggle, 2);
        filters.Children.Add(observedDeviceLogsAutoFollowToggle);

        host.Children.Add(filters);
        host.Children.Add(CreateSurfaceCard(BuildObservedDeviceLogsSurface(), padding: 0));
        return host;
    }

    private Grid BuildObservedDeviceLogsSurface()
    {
        var surface = new Grid
        {
            MinHeight = 260,
        };

        observedDeviceLogsListView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = false,
            Margin = new Thickness(0),
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            MinHeight = 260,
            MaxHeight = 360,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(observedDeviceLogsListView, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(observedDeviceLogsListView, ScrollBarVisibility.Auto);
        surface.Children.Add(observedDeviceLogsListView);

        observedDeviceLogsPlaceholderText = new TextBlock
        {
            Text = "Selecione um device acima para ver logs.",
            Opacity = 0.72,
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            Margin = new Thickness(20),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
        };
        surface.Children.Add(observedDeviceLogsPlaceholderText);

        return surface;
    }

    private StackPanel BuildObservedStatisticsIdentityCard()
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Identidade do firmware",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        observedStatsIdentityFirmwareText = BuildObservedStatisticsValueText("Firmware: -");
        observedStatsIdentityChipText = BuildObservedStatisticsValueText("Chip: -");
        observedStatsIdentitySdkText = BuildObservedStatisticsValueText("SDK: -");
        observedStatsIdentityBoardText = BuildObservedStatisticsValueText("Board: -");

        stack.Children.Add(observedStatsIdentityFirmwareText);
        stack.Children.Add(observedStatsIdentityChipText);
        stack.Children.Add(observedStatsIdentitySdkText);
        stack.Children.Add(observedStatsIdentityBoardText);
        return stack;
    }

    private StackPanel BuildObservedStatisticsCapacityCard()
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Capacidade do device",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        observedStatsCapacityHeapText = BuildObservedStatisticsValueText("Heap total: -");
        observedStatsCapacityPsramText = BuildObservedStatisticsValueText("PSRAM total: -");
        observedStatsCapacityFlashText = BuildObservedStatisticsValueText("Flash total: -");
        observedStatsCapacitySketchText = BuildObservedStatisticsValueText("Sketch: -");

        stack.Children.Add(observedStatsCapacityHeapText);
        stack.Children.Add(observedStatsCapacityPsramText);
        stack.Children.Add(observedStatsCapacityFlashText);
        stack.Children.Add(observedStatsCapacitySketchText);
        return stack;
    }

    private StackPanel BuildObservedStatisticsStreamCard()
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = "Stream e heartbeat",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        observedStatsStreamSummaryText = BuildObservedStatisticsValueText("Controle: -");
        observedStatsStreamFramesText = BuildObservedStatisticsValueText("Frames: -");
        observedStatsStreamIntegrityText = BuildObservedStatisticsValueText("Integridade: -");
        observedStatsStreamHeartbeatText = BuildObservedStatisticsValueText("Heartbeat: -");

        stack.Children.Add(observedStatsStreamSummaryText);
        stack.Children.Add(observedStatsStreamFramesText);
        stack.Children.Add(observedStatsStreamIntegrityText);
        stack.Children.Add(observedStatsStreamHeartbeatText);
        return stack;
    }

    private static StackPanel BuildObservedHistoryCard(string title, Color accentColor, out TextBlock summaryText, out StackPanel barsPanel)
    {
        var stack = new StackPanel { Spacing = 10 };
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });

        summaryText = BuildObservedStatisticsValueText("Sem amostras.");
        stack.Children.Add(summaryText);

        var chartHost = new Border
        {
            Height = 88,
            Padding = new Thickness(10, 8, 10, 8),
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = CreateSettingsStrokeBrush(),
            Background = CreateSettingsBaseBrush(),
        };

        var chartGrid = new Grid();
        chartGrid.Children.Add(new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)),
        });

        barsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 3,
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        chartGrid.Children.Add(barsPanel);

        chartHost.Child = chartGrid;
        stack.Children.Add(chartHost);

        RenderObservedHistoryBars(barsPanel, Array.Empty<double?>(), accentColor);
        return stack;
    }

    private static TextBlock BuildObservedStatisticsValueText(string text)
    {
        return new TextBlock
        {
            Text = text,
            Opacity = 0.84,
            TextWrapping = TextWrapping.Wrap,
        };
    }

    private void ApplyDeviceObservabilityState(DeviceOperationsState state)
    {
        currentDeviceOperationsState = state ?? new DeviceOperationsState();
        RefreshObservedDeviceSelector();
        RefreshObservedDevicePanels();
    }

    private void RefreshObservedDeviceSelector()
    {
        if (observedDeviceComboBox is null || observedDeviceSummaryText is null)
        {
            return;
        }

        var devices = currentDeviceOperationsState.DeviceListSnapshot;
        var options = devices
            .Select(static snapshot => new ObservedDeviceOption(snapshot.DeviceId, BuildObservedDeviceOptionLabel(snapshot)))
            .ToArray();

        selectedObservedDeviceId = ResolvePreferredObservedDeviceId(selectedObservedDeviceId, devices);

        suppressObservedDeviceSelectionChanged = true;
        observedDeviceComboBox.ItemsSource = options;
        observedDeviceComboBox.DisplayMemberPath = nameof(ObservedDeviceOption.Label);
        observedDeviceComboBox.IsEnabled = options.Length > 0;
        observedDeviceComboBox.SelectedItem = options.FirstOrDefault(option =>
            string.Equals(option.DeviceId, selectedObservedDeviceId, StringComparison.OrdinalIgnoreCase));
        suppressObservedDeviceSelectionChanged = false;

        var selectedSnapshot = FindObservedDeviceSnapshot(selectedObservedDeviceId);
        observedDeviceSummaryText.Text = selectedSnapshot is null
            ? "Nenhum device registrado ainda."
            : $"Exibindo {selectedSnapshot.Name} | {selectedSnapshot.DeviceId} | {selectedSnapshot.Status}.";
    }

    private void OnObservedDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressObservedDeviceSelectionChanged)
        {
            return;
        }

        selectedObservedDeviceId = (observedDeviceComboBox.SelectedItem as ObservedDeviceOption)?.DeviceId;
        RefreshObservedDevicePanels();
    }

    private void RefreshObservedDevicePanels()
    {
        var snapshot = FindObservedDeviceSnapshot(selectedObservedDeviceId);
        var deviceLogs = snapshot is null
            ? Array.Empty<DeviceLogEntry>()
            : deviceOps.GetDeviceLogs(snapshot.DeviceId);
        UpdateObservedStructuredDeviceLogs(selectedObservedDeviceId, deviceLogs, snapshot);
    }

    private DeviceSnapshot? FindObservedDeviceSnapshot(string? deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return null;
        }

        foreach (var snapshot in currentDeviceOperationsState.DeviceListSnapshot)
        {
            if (string.Equals(snapshot.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                return snapshot;
            }
        }

        return null;
    }

    private static string? ResolvePreferredObservedDeviceId(string? currentDeviceId, IReadOnlyList<DeviceSnapshot> devices)
    {
        if (!string.IsNullOrWhiteSpace(currentDeviceId))
        {
            foreach (var snapshot in devices)
            {
                if (string.Equals(snapshot.DeviceId, currentDeviceId, StringComparison.OrdinalIgnoreCase))
                {
                    return snapshot.DeviceId;
                }
            }
        }

        foreach (var snapshot in devices)
        {
            if (snapshot.Status == DeviceStatus.Online)
            {
                return snapshot.DeviceId;
            }
        }

        return devices.Count > 0 ? devices[0].DeviceId : null;
    }

    private static string BuildObservedDeviceOptionLabel(DeviceSnapshot snapshot)
    {
        var name = string.IsNullOrWhiteSpace(snapshot.Name) ? snapshot.DeviceId : snapshot.Name;
        return $"{name} | {snapshot.DeviceId} | {snapshot.Status}";
    }

    private void ApplyObservedStatisticsPanel(string? deviceId, DeviceSnapshot? snapshot, DeviceTelemetryHistorySnapshot history)
    {
        if (observedStatisticsStatusText is null || observedStatisticsContentPanel is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(deviceId) || snapshot is null)
        {
            observedStatisticsStatusText.Text = "Selecione um device acima para ver estatisticas e historico de telemetria.";
            observedStatisticsContentPanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (snapshot.ControlPlaneState == DeviceControlPlaneState.LegacyOnly)
        {
            observedStatisticsStatusText.Text = "Firmware legado detectado: estatisticas e historico estruturados exigem control plane MQTT.";
            observedStatisticsContentPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var hasStructuredStats = HasObservedStructuredStats(snapshot);
        var hasHistory = HasObservedTelemetryHistory(history);
        var hasLiveTelemetry = snapshot.LastTelemetryUtc.HasValue;

        if (!hasStructuredStats && !hasHistory && !hasLiveTelemetry)
        {
            observedStatisticsStatusText.Text = "Aguardando primeiro snapshot de estatisticas ou historico deste dispositivo.";
            observedStatisticsContentPanel.Visibility = Visibility.Collapsed;
            return;
        }

        observedStatisticsStatusText.Text = snapshot.Status == DeviceStatus.Online
            ? $"Atualizado para {deviceId} com a ultima telemetria conhecida."
            : $"Offline: exibindo ultimo snapshot conhecido de {deviceId}.";
        observedStatisticsContentPanel.Visibility = Visibility.Visible;

        observedStatsIdentityFirmwareText.Text = $"Firmware: {snapshot.FirmwareVersion ?? "-"}";
        observedStatsIdentityChipText.Text = $"Chip: {BuildObservedChipIdentityLabel(snapshot)}";
        observedStatsIdentitySdkText.Text = $"SDK: {snapshot.SdkVersion ?? "-"}";
        observedStatsIdentityBoardText.Text = $"Board: {snapshot.BoardModel ?? "-"} | Painel: {snapshot.PanelType ?? "-"}";

        observedStatsCapacityHeapText.Text = $"Heap total: {FormatObservedBytes(snapshot.HeapTotalBytes)}";
        observedStatsCapacityPsramText.Text = $"PSRAM total: {FormatObservedBytes(snapshot.PsramTotalBytes)}";
        observedStatsCapacityFlashText.Text = $"Flash total: {FormatObservedBytes(snapshot.FlashTotalBytes)}";
        observedStatsCapacitySketchText.Text = $"Sketch: {FormatObservedBytes(snapshot.SketchSizeBytes)} usados | {FormatObservedBytes(snapshot.FreeSketchBytes)} livres";

        observedStatsStreamSummaryText.Text = $"Controle: {snapshot.ControlPlaneState} | Wi-Fi: {snapshot.WifiState ?? "-"}";
        observedStatsStreamFramesText.Text = $"Frames: RX {snapshot.StreamFramesReceived?.ToString(CultureInfo.InvariantCulture) ?? "-"} | APL {snapshot.StreamFramesApplied?.ToString(CultureInfo.InvariantCulture) ?? "-"}";
        observedStatsStreamIntegrityText.Text = $"Integridade: sucesso {BuildObservedStreamSuccessRateLabel(snapshot)} | gaps {snapshot.StreamSequenceGapCount?.ToString(CultureInfo.InvariantCulture) ?? "-"} | invalidos {snapshot.StreamInvalidFrameCount?.ToString(CultureInfo.InvariantCulture) ?? "-"}";
        observedStatsStreamHeartbeatText.Text = $"Heartbeat: telemetria #{snapshot.TelemetrySequence?.ToString(CultureInfo.InvariantCulture) ?? "-"} | ultimo frame #{snapshot.StreamLastSequence?.ToString(CultureInfo.InvariantCulture) ?? "-"}";

        observedRssiHistorySummaryText.Text = BuildObservedRssiHistorySummary(history.RssiSamples);
        observedLoopHistorySummaryText.Text = BuildObservedLoopHistorySummary(history.LoopLoadSamples);
        observedHeapHistorySummaryText.Text = BuildObservedMemoryHistorySummary("Heap", history.FreeHeapSamples, snapshot.HeapTotalBytes);
        observedPsramHistorySummaryText.Text = snapshot.PsramAvailable == false
            ? "PSRAM indisponivel neste build."
            : BuildObservedMemoryHistorySummary("PSRAM", history.FreePsramSamples, snapshot.PsramTotalBytes);

        RenderObservedHistoryBars(
            observedRssiHistoryBarsPanel,
            history.RssiSamples.Select(static value => NormalizeObservedRssi(value)).ToArray(),
            Color.FromArgb(255, 86, 156, 214));
        RenderObservedHistoryBars(
            observedLoopHistoryBarsPanel,
            history.LoopLoadSamples.Select(static value => NormalizeObservedPercent(value)).ToArray(),
            Color.FromArgb(255, 78, 201, 176));
        RenderObservedHistoryBars(
            observedHeapHistoryBarsPanel,
            history.FreeHeapSamples.Select(value => NormalizeObservedMemory(value, snapshot.HeapTotalBytes)).ToArray(),
            Color.FromArgb(255, 108, 203, 95));
        RenderObservedHistoryBars(
            observedPsramHistoryBarsPanel,
            history.FreePsramSamples.Select(value => NormalizeObservedMemory(value, snapshot.PsramTotalBytes)).ToArray(),
            Color.FromArgb(255, 155, 89, 182));
    }

    private void UpdateObservedStructuredDeviceLogs(string? deviceId, IReadOnlyList<DeviceLogEntry> entries, DeviceSnapshot? snapshot)
    {
        currentObservedDeviceLogsDeviceId = deviceId;
        currentObservedDeviceLogEntries = entries ?? Array.Empty<DeviceLogEntry>();

        if (observedDeviceLogsHeaderText is null
            || observedDeviceLogsHintText is null
            || observedDeviceLogsListView is null
            || observedDeviceLogsPlaceholderText is null)
        {
            return;
        }

        observedDeviceLogsHeaderText.Text = string.IsNullOrWhiteSpace(deviceId)
            ? "Logs do device"
            : $"Logs do device | {deviceId}";

        observedDeviceLogsHintText.Text = ResolveObservedDeviceLogsHint(snapshot);
        RefreshObservedStructuredDeviceLogsView();
    }

    private void OnObservedDeviceLogsFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshObservedStructuredDeviceLogsView();
    }

    private void OnObservedDeviceLogsSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshObservedStructuredDeviceLogsView();
    }

    private void RefreshObservedStructuredDeviceLogsView()
    {
        if (observedDeviceLogsListView is null || observedDeviceLogsPlaceholderText is null)
        {
            return;
        }

        var snapshot = FindObservedDeviceSnapshot(currentObservedDeviceLogsDeviceId);
        var filteredEntries = currentObservedDeviceLogEntries
            .Where(entry => MatchesObservedSeverityFilter(entry) && MatchesObservedSearchFilter(entry))
            .OrderBy(entry => entry.Timestamp)
            .ToArray();

        observedDeviceLogsListView.Items.Clear();
        foreach (var entry in filteredEntries)
        {
            observedDeviceLogsListView.Items.Add(BuildObservedDeviceLogRow(entry));
        }

        var placeholder = ResolveObservedLogsPlaceholder(snapshot, filteredEntries.Length);
        observedDeviceLogsPlaceholderText.Text = placeholder;
        observedDeviceLogsPlaceholderText.Visibility = filteredEntries.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (filteredEntries.Length > 0 && observedDeviceLogsAutoFollowToggle?.IsOn == true)
        {
            ScrollObservedStructuredDeviceLogsToEnd();
        }
    }

    private void ScrollObservedStructuredDeviceLogsToEnd()
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (observedDeviceLogsListView is null || observedDeviceLogsListView.Items.Count == 0)
            {
                return;
            }

            observedDeviceLogsListView.UpdateLayout();
            observedDeviceLogsListView.ScrollIntoView(observedDeviceLogsListView.Items[observedDeviceLogsListView.Items.Count - 1]);
        });
    }

    private static Border BuildObservedDeviceLogRow(DeviceLogEntry entry)
    {
        var foreground = entry.Severity switch
        {
            DeviceLogSeverity.Warning => new SolidColorBrush(Color.FromArgb(255, 252, 225, 0)),
            DeviceLogSeverity.Error => new SolidColorBrush(Color.FromArgb(255, 255, 99, 132)),
            _ => new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)),
        };

        return new Border
        {
            Padding = new Thickness(16, 10, 16, 10),
            BorderBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new TextBlock
            {
                Text = DeviceLogEntryFormatter.Format(entry),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.WrapWholeWords,
                Foreground = foreground,
            },
        };
    }

    private bool MatchesObservedSeverityFilter(DeviceLogEntry entry)
    {
        var selected = (observedDeviceLogsSeverityComboBox?.SelectedItem as ComboBoxItem)?.Tag as string;
        return selected switch
        {
            "info" => entry.Severity == DeviceLogSeverity.Info,
            "warning" => entry.Severity == DeviceLogSeverity.Warning,
            "error" => entry.Severity == DeviceLogSeverity.Error,
            _ => true,
        };
    }

    private bool MatchesObservedSearchFilter(DeviceLogEntry entry)
    {
        var rawSearch = observedDeviceLogsSearchBox?.Text;
        if (string.IsNullOrWhiteSpace(rawSearch))
        {
            return true;
        }

        var search = rawSearch.Trim();
        return entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase)
            || entry.Category.Contains(search, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(entry.Code) && entry.Code.Contains(search, StringComparison.OrdinalIgnoreCase))
            || entry.Source.ToString().Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveObservedDeviceLogsHint(DeviceSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Eventos do app e do firmware para o device selecionado no combo acima.";
        }

        if (snapshot.ControlPlaneState == DeviceControlPlaneState.LegacyOnly)
        {
            return "Firmware legado: exibindo apenas eventos locais do app; logs estruturados do firmware exigem MQTT.";
        }

        return snapshot.Status == DeviceStatus.Online
            ? "Fluxo unificado do app e do firmware para o dispositivo selecionado."
            : "Dispositivo offline: exibindo o historico desta sessao no app.";
    }

    private string ResolveObservedLogsPlaceholder(DeviceSnapshot? snapshot, int filteredCount)
    {
        if (!string.IsNullOrWhiteSpace(currentObservedDeviceLogsDeviceId) && filteredCount > 0)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(currentObservedDeviceLogsDeviceId))
        {
            return "Selecione um device acima para ver logs.";
        }

        if (snapshot?.ControlPlaneState == DeviceControlPlaneState.LegacyOnly)
        {
            return "Firmware legado sem logs MQTT estruturados. Os eventos locais do app aparecerao aqui quando existirem.";
        }

        if (currentObservedDeviceLogEntries.Count == 0)
        {
            return "Sem eventos para este dispositivo ainda.";
        }

        return "Nenhum evento coincide com o filtro atual.";
    }

    private static bool HasObservedStructuredStats(DeviceSnapshot? snapshot)
    {
        return snapshot is not null
            && (!string.IsNullOrWhiteSpace(snapshot.ChipModel)
                || snapshot.ChipRevision.HasValue
                || snapshot.ChipCores.HasValue
                || snapshot.CpuFreqMHz.HasValue
                || !string.IsNullOrWhiteSpace(snapshot.SdkVersion)
                || snapshot.HeapTotalBytes.HasValue
                || snapshot.PsramTotalBytes.HasValue
                || snapshot.FlashTotalBytes.HasValue
                || snapshot.SketchSizeBytes.HasValue
                || snapshot.FreeSketchBytes.HasValue);
    }

    private static bool HasObservedTelemetryHistory(DeviceTelemetryHistorySnapshot history)
    {
        ArgumentNullException.ThrowIfNull(history);

        return history.RssiSamples.Count > 0
            || history.LoopLoadSamples.Count > 0
            || history.FreeHeapSamples.Count > 0
            || history.FreePsramSamples.Count > 0;
    }

    private static string BuildObservedChipIdentityLabel(DeviceSnapshot snapshot)
    {
        var parts = new List<string>(capacity: 4);
        if (!string.IsNullOrWhiteSpace(snapshot.ChipModel))
        {
            parts.Add(snapshot.ChipModel!);
        }

        if (snapshot.ChipRevision.HasValue)
        {
            parts.Add($"rev {snapshot.ChipRevision.Value}");
        }

        if (snapshot.ChipCores.HasValue)
        {
            parts.Add($"{snapshot.ChipCores.Value} cores");
        }

        if (snapshot.CpuFreqMHz.HasValue)
        {
            parts.Add($"{snapshot.CpuFreqMHz.Value} MHz");
        }

        return parts.Count == 0 ? "-" : string.Join(" | ", parts);
    }

    private static string BuildObservedRssiHistorySummary(IReadOnlyList<int?> samples)
    {
        var current = samples.LastOrDefault(value => value.HasValue);
        return current.HasValue
            ? $"Atual {current.Value} dBm | {samples.Count} amostras"
            : "Sem amostras de RSSI ainda.";
    }

    private static string BuildObservedLoopHistorySummary(IReadOnlyList<int?> samples)
    {
        var current = samples.LastOrDefault(value => value.HasValue);
        return current.HasValue
            ? $"Atual {current.Value}% | {samples.Count} amostras"
            : "Sem amostras de carga ainda.";
    }

    private static string BuildObservedMemoryHistorySummary(string label, IReadOnlyList<long?> samples, long? totalBytes)
    {
        var current = samples.LastOrDefault(value => value.HasValue);
        if (!current.HasValue)
        {
            return $"Sem amostras de {label} ainda.";
        }

        return totalBytes.HasValue && totalBytes.Value > 0
            ? $"{label} atual {FormatObservedBytes(current.Value)} de {FormatObservedBytes(totalBytes)}"
            : $"{label} atual {FormatObservedBytes(current.Value)}";
    }

    private static void RenderObservedHistoryBars(StackPanel panel, double?[] normalizedValues, Color accentColor)
    {
        panel.Children.Clear();

        if (normalizedValues.Length == 0)
        {
            for (var index = 0; index < 24; index++)
            {
                panel.Children.Add(BuildObservedHistoryBar(8d, Color.FromArgb(28, accentColor.R, accentColor.G, accentColor.B)));
            }

            return;
        }

        foreach (var value in normalizedValues)
        {
            var normalized = value.HasValue && double.IsFinite(value.Value)
                ? Math.Clamp(value.Value, 0d, 1d)
                : 0.08d;
            var alpha = value.HasValue ? (byte)220 : (byte)48;
            var barHeight = 6d + (normalized * 54d);
            panel.Children.Add(BuildObservedHistoryBar(barHeight, Color.FromArgb(alpha, accentColor.R, accentColor.G, accentColor.B)));
        }
    }

    private static Border BuildObservedHistoryBar(double height, Color color)
    {
        return new Border
        {
            Width = 5,
            Height = height,
            CornerRadius = new CornerRadius(3),
            Background = new SolidColorBrush(color),
            VerticalAlignment = VerticalAlignment.Bottom,
        };
    }

    private static double? NormalizeObservedRssi(int? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        const double minRssi = -100d;
        const double maxRssi = -30d;
        return Math.Clamp((value.Value - minRssi) / (maxRssi - minRssi), 0d, 1d);
    }

    private static double? NormalizeObservedPercent(int? value)
    {
        return value.HasValue
            ? Math.Clamp(value.Value / 100d, 0d, 1d)
            : null;
    }

    private static double? NormalizeObservedMemory(long? value, long? max)
    {
        if (!value.HasValue || value.Value < 0)
        {
            return null;
        }

        var maxValue = max.GetValueOrDefault();
        if (maxValue <= 0)
        {
            return 0d;
        }

        return Math.Clamp(value.Value / (double)maxValue, 0d, 1d);
    }

    private static string FormatObservedBytes(long? bytes)
    {
        if (!bytes.HasValue || bytes.Value < 0)
        {
            return "-";
        }

        const double kib = 1024d;
        const double mib = 1024d * 1024d;

        if (bytes.Value < 1024)
        {
            return $"{bytes.Value} B";
        }

        if (bytes.Value < mib)
        {
            return (bytes.Value / kib).ToString("0", CultureInfo.InvariantCulture) + " KB";
        }

        return (bytes.Value / mib).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }

    private static string BuildObservedStreamSuccessRateLabel(DeviceSnapshot? snapshot)
    {
        if (snapshot?.StreamFramesReceived is not uint framesReceived || framesReceived == 0)
        {
            return "-";
        }

        var framesApplied = snapshot.StreamFramesApplied ?? 0;
        var successRate = Math.Clamp((int)Math.Round((framesApplied / (double)framesReceived) * 100d), 0, 100);
        return $"{successRate}%";
    }

    private static Border CreateSurfaceCard(UIElement content, double padding = 16)
    {
        return new Border
        {
            Background = CreateSettingsPaneBrush(),
            BorderBrush = CreateSettingsStrokeBrush(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(padding),
            Child = content,
        };
    }

    private sealed record ObservedDeviceOption(string DeviceId, string Label);
}
