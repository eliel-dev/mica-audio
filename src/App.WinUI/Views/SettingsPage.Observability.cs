using App.WinUI.Infrastructure.Serial;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace App.WinUI.Views;

public sealed partial class SettingsPage
{
    private bool serialMonitorActive;
    private bool suppressSerialMonitorPortSelectionChanged;
    private IReadOnlyList<SerialMonitorLine> currentSerialMonitorLines = Array.Empty<SerialMonitorLine>();
    private ComboBox serialMonitorPortComboBox = null!;
    private Button serialMonitorConnectButton = null!;
    private Button serialMonitorClearButton = null!;
    private Button serialMonitorCopyAllButton = null!;
    private ToggleSwitch serialMonitorAutoFollowToggle = null!;
    private TextBlock serialMonitorStatusText = null!;
    private ListView serialMonitorListView = null!;
    private TextBlock serialMonitorPlaceholderText = null!;

    private async Task ActivateSerialMonitorAsync()
    {
        if (serialMonitorActive)
        {
            return;
        }

        serialMonitorService.StateChanged += OnSerialMonitorStateChanged;
        serialMonitorActive = true;
        await serialMonitorService.RefreshPortsAsync().ConfigureAwait(true);
        RefreshSerialMonitorView(serialMonitorService.GetStateSnapshot());
    }

    private async Task DeactivateSerialMonitorAsync()
    {
        if (!serialMonitorActive)
        {
            return;
        }

        serialMonitorService.StateChanged -= OnSerialMonitorStateChanged;
        serialMonitorActive = false;
        await serialMonitorService.DisconnectAsync().ConfigureAwait(true);
    }

    private void OnSerialMonitorStateChanged(object? sender, EventArgs e)
    {
        _ = DispatcherQueue.TryEnqueue(() => RefreshSerialMonitorView(serialMonitorService.GetStateSnapshot()));
    }

    private Border BuildDeviceObservabilityCard()
    {
        return CreateCard(
            "Monitor serial",
            "Acompanhe a saida crua do firmware via COM manual, em um monitor no estilo Arduino IDE, sem depender da selecao de devices MQTT.",
            BuildSerialMonitorContent());
    }

    // DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitor-serial-em-configuracoes
    private StackPanel BuildSerialMonitorContent()
    {
        var host = new StackPanel
        {
            Spacing = 12,
        };

        var actionsGrid = new Grid
        {
            ColumnSpacing = 12,
        };
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        serialMonitorPortComboBox = new ComboBox
        {
            PlaceholderText = "Nenhuma porta COM detectada",
            MinWidth = 280,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            DisplayMemberPath = nameof(SerialPortDescriptor.Label),
        };
        serialMonitorPortComboBox.SelectionChanged += OnSerialMonitorPortSelectionChanged;
        serialMonitorPortComboBox.DropDownOpened += OnSerialMonitorPortDropDownOpened;
        actionsGrid.Children.Add(serialMonitorPortComboBox);

        serialMonitorConnectButton = new Button
        {
            Content = "Conectar",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = Application.Current.Resources["AppChromeButtonStyle"] as Style,
        };
        serialMonitorConnectButton.Click += OnSerialMonitorConnectClicked;
        Grid.SetColumn(serialMonitorConnectButton, 1);
        actionsGrid.Children.Add(serialMonitorConnectButton);

        serialMonitorCopyAllButton = new Button
        {
            Content = "Copiar tudo",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = Application.Current.Resources["AppChromeButtonStyle"] as Style,
            IsEnabled = false,
        };
        serialMonitorCopyAllButton.Click += OnSerialMonitorCopyAllClicked;
        Grid.SetColumn(serialMonitorCopyAllButton, 2);
        actionsGrid.Children.Add(serialMonitorCopyAllButton);

        serialMonitorClearButton = new Button
        {
            Content = "Limpar",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = Application.Current.Resources["AppChromeButtonStyle"] as Style,
            IsEnabled = false,
        };
        serialMonitorClearButton.Click += OnSerialMonitorClearClicked;
        Grid.SetColumn(serialMonitorClearButton, 3);
        actionsGrid.Children.Add(serialMonitorClearButton);

        host.Children.Add(actionsGrid);

        var statusGrid = new Grid
        {
            ColumnSpacing = 12,
        };
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        serialMonitorStatusText = new TextBlock
        {
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
            Text = "Carregando portas COM disponiveis...",
        };
        statusGrid.Children.Add(serialMonitorStatusText);

        serialMonitorAutoFollowToggle = new ToggleSwitch
        {
            Header = "Auto-scroll",
            IsOn = true,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        Grid.SetColumn(serialMonitorAutoFollowToggle, 1);
        statusGrid.Children.Add(serialMonitorAutoFollowToggle);

        host.Children.Add(statusGrid);
        host.Children.Add(BuildSerialMonitorSurface());
        return host;
    }

    private Border BuildSerialMonitorSurface()
    {
        var surface = new Grid
        {
            MinHeight = 320,
        };

        serialMonitorListView = new ListView
        {
            SelectionMode = ListViewSelectionMode.None,
            IsItemClickEnabled = false,
            Margin = new Thickness(0),
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
            MinHeight = 320,
            MaxHeight = 460,
            Background = new SolidColorBrush(Color.FromArgb(255, 3, 3, 3)),
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(serialMonitorListView, ScrollBarVisibility.Disabled);
        ScrollViewer.SetVerticalScrollBarVisibility(serialMonitorListView, ScrollBarVisibility.Auto);
        surface.Children.Add(serialMonitorListView);

        serialMonitorPlaceholderText = new TextBlock
        {
            Text = "Selecione uma porta COM para iniciar o monitor serial.",
            Opacity = 0.68,
            FontStyle = Windows.UI.Text.FontStyle.Italic,
            Margin = new Thickness(20),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Top,
            Foreground = new SolidColorBrush(Color.FromArgb(255, 170, 170, 170)),
        };
        surface.Children.Add(serialMonitorPlaceholderText);

        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(255, 3, 3, 3)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 26, 26, 26)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(0),
            Child = surface,
        };
    }

    private async void OnSerialMonitorPortDropDownOpened(object? sender, object e)
    {
        try
        {
            await serialMonitorService.RefreshPortsAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            App.ReportError("SettingsPage.RefreshSerialPorts failed", ex);
        }
    }

    private void OnSerialMonitorPortSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressSerialMonitorPortSelectionChanged)
        {
            return;
        }

        serialMonitorService.SetSelectedPort((serialMonitorPortComboBox.SelectedItem as SerialPortDescriptor)?.PortName);
        RefreshSerialMonitorView(serialMonitorService.GetStateSnapshot());
    }

    private async void OnSerialMonitorConnectClicked(object sender, RoutedEventArgs e)
    {
        var snapshot = serialMonitorService.GetStateSnapshot();

        try
        {
            if (snapshot.ConnectionState == SerialMonitorConnectionState.Connected)
            {
                await serialMonitorService.DisconnectAsync().ConfigureAwait(true);
                return;
            }

            await serialMonitorService.ConnectAsync(snapshot.SelectedPortName ?? string.Empty).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            App.ReportError("SettingsPage.ToggleSerialMonitor failed", ex);
        }
    }

    private void OnSerialMonitorClearClicked(object sender, RoutedEventArgs e)
    {
        serialMonitorService.Clear();
        RefreshSerialMonitorView(serialMonitorService.GetStateSnapshot());
    }

    private void OnSerialMonitorCopyAllClicked(object sender, RoutedEventArgs e)
    {
        var text = serialMonitorService.ExportAllText();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var data = new DataPackage();
        data.SetText(text);
        Clipboard.SetContent(data);
    }

    private void RefreshSerialMonitorView(SerialMonitorSessionState state)
    {
        if (serialMonitorPortComboBox is null
            || serialMonitorConnectButton is null
            || serialMonitorClearButton is null
            || serialMonitorCopyAllButton is null
            || serialMonitorStatusText is null
            || serialMonitorListView is null
            || serialMonitorPlaceholderText is null)
        {
            return;
        }

        suppressSerialMonitorPortSelectionChanged = true;
        serialMonitorPortComboBox.ItemsSource = state.AvailablePorts;
        serialMonitorPortComboBox.SelectedItem = state.AvailablePorts.FirstOrDefault(item =>
            string.Equals(item.PortName, state.SelectedPortName, StringComparison.OrdinalIgnoreCase));
        serialMonitorPortComboBox.IsEnabled = state.ConnectionState is not SerialMonitorConnectionState.Connecting
            and not SerialMonitorConnectionState.Connected;
        suppressSerialMonitorPortSelectionChanged = false;

        serialMonitorConnectButton.Content = state.ConnectionState switch
        {
            SerialMonitorConnectionState.Connected => "Desconectar",
            SerialMonitorConnectionState.Connecting => "Conectando...",
            _ => "Conectar",
        };
        serialMonitorConnectButton.IsEnabled = state.ConnectionState != SerialMonitorConnectionState.Connecting
            && (state.ConnectionState == SerialMonitorConnectionState.Connected
                || !string.IsNullOrWhiteSpace(state.SelectedPortName));

        serialMonitorClearButton.IsEnabled = state.LineCount > 0;
        serialMonitorCopyAllButton.IsEnabled = state.LineCount > 0;
        serialMonitorStatusText.Text = state.StatusText;

        UpdateSerialMonitorList(state.VisibleLines);

        serialMonitorPlaceholderText.Text = ResolveSerialMonitorPlaceholder(state);
        serialMonitorPlaceholderText.Visibility = state.LineCount == 0 ? Visibility.Visible : Visibility.Collapsed;

        if (state.LineCount > 0 && serialMonitorAutoFollowToggle?.IsOn == true)
        {
            ScrollSerialMonitorToEnd();
        }
    }

    private void UpdateSerialMonitorList(IReadOnlyList<SerialMonitorLine> nextLines)
    {
        if (serialMonitorListView is null)
        {
            return;
        }

        var shouldRebuild = nextLines.Count < currentSerialMonitorLines.Count;
        if (!shouldRebuild)
        {
            for (var index = 0; index < currentSerialMonitorLines.Count; index++)
            {
                if (!currentSerialMonitorLines[index].Equals(nextLines[index]))
                {
                    shouldRebuild = true;
                    break;
                }
            }
        }

        if (shouldRebuild)
        {
            serialMonitorListView.Items.Clear();
            foreach (var line in nextLines)
            {
                serialMonitorListView.Items.Add(BuildSerialMonitorRow(line));
            }
        }
        else
        {
            for (var index = currentSerialMonitorLines.Count; index < nextLines.Count; index++)
            {
                serialMonitorListView.Items.Add(BuildSerialMonitorRow(nextLines[index]));
            }
        }

        currentSerialMonitorLines = nextLines;
    }

    private void ScrollSerialMonitorToEnd()
    {
        _ = DispatcherQueue.TryEnqueue(() =>
        {
            if (serialMonitorListView is null || serialMonitorListView.Items.Count == 0)
            {
                return;
            }

            serialMonitorListView.UpdateLayout();
            serialMonitorListView.ScrollIntoView(serialMonitorListView.Items[serialMonitorListView.Items.Count - 1]);
        });
    }

    private static Border BuildSerialMonitorRow(SerialMonitorLine line)
    {
        return new Border
        {
            Padding = new Thickness(16, 6, 16, 6),
            BorderBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new TextBlock
            {
                Text = line.Text,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11.5,
                TextWrapping = TextWrapping.WrapWholeWords,
                IsTextSelectionEnabled = true,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 231, 231, 231)),
            },
        };
    }

    private static string ResolveSerialMonitorPlaceholder(SerialMonitorSessionState state)
    {
        if (state.ConnectionState == SerialMonitorConnectionState.Connected)
        {
            return "Conectado. Aguardando saida serial do firmware...";
        }

        if (state.ConnectionState == SerialMonitorConnectionState.Connecting)
        {
            return "Abrindo a porta serial...";
        }

        if (state.ConnectionState == SerialMonitorConnectionState.Error)
        {
            return state.ErrorText ?? "Falha ao abrir ou ler a porta serial.";
        }

        if (state.AvailablePorts.Count == 0)
        {
            return "Nenhuma porta COM detectada.";
        }

        return string.IsNullOrWhiteSpace(state.SelectedPortName)
            ? "Selecione uma porta COM para iniciar o monitor serial."
            : $"Pronto para monitorar {state.SelectedPortName}.";
    }
}
