using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using App.WinUI.Services;
using App.WinUI.Services.Devices;
using MicaAudio.Core.Presets;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace App.WinUI.Views;

// DOCS: docs/wiki/modules/app-winui.md#modulo-appwinui
// DOCS: docs/handoffs/2026-04-21-remove-settings-serial-monitor.md
// DOCS: docs/handoffs/2026-04-22-winui-remote-full-visual-client.md
public sealed partial class SettingsPage : Page
{
    private readonly SettingsRepository settingsRepository;
    private readonly AppSettingsDomainService settingsDomainService;
    private readonly RemoteDeviceServerSecretStore remoteSecretStore;
    private AppSettings currentSettings = new();
    private bool suppressMicaBackdropChanged;
    private ToggleSwitch micaBackdropToggle = null!;
    private TextBox remoteServerBaseAddressBox = null!;
    private PasswordBox remoteAdminTokenBox = null!;
    private TextBlock micaBackdropStatusText = null!;
    private TextBlock deviceServerStatusText = null!;
    private PasswordBox giphyApiKeyBox = null!;
    private TextBlock giphyApiKeyStatusText = null!;
    private TextBlock errorLogsPathText = null!;
    private TextBlock errorLogsStatusText = null!;

    internal SettingsPage(
        SettingsRepository settingsRepository,
        AppSettingsDomainService settingsDomainService,
        RemoteDeviceServerSecretStore remoteSecretStore)
    {
        this.settingsRepository = settingsRepository;
        this.settingsDomainService = settingsDomainService;
        this.remoteSecretStore = remoteSecretStore;

        InitializeComponent();
        Content = BuildLayout();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateErrorLogsInfo();
        await LoadGeneralSettingsAsync();
    }

    private async Task LoadGeneralSettingsAsync()
    {
        try
        {
            currentSettings = settingsDomainService.Migrate(await settingsRepository.LoadAsync());
            var adminToken = await remoteSecretStore.LoadAdminTokenAsync();
            var giphyKey = await remoteSecretStore.LoadGiphyApiKeyAsync();
            suppressMicaBackdropChanged = true;
            micaBackdropToggle.IsOn = currentSettings.UseMicaBackdrop;
            remoteServerBaseAddressBox.Text = currentSettings.RemoteServerBaseAddress;
            remoteAdminTokenBox.Password = adminToken;
            giphyApiKeyBox.Password = giphyKey;
            suppressMicaBackdropChanged = false;
            micaBackdropStatusText.Text = BuildMicaBackdropStatusText(
                currentSettings.UseMicaBackdrop,
                App.MainWindow?.SystemBackdrop is MicaBackdrop);
            deviceServerStatusText.Text = BuildDeviceServerStatusText(currentSettings.RemoteServerBaseAddress);
            giphyApiKeyStatusText.Text = string.IsNullOrWhiteSpace(giphyKey)
                ? "Nenhuma chave GIPHY configurada."
                : "Chave GIPHY carregada.";
        }
        catch (Exception ex)
        {
            suppressMicaBackdropChanged = true;
            micaBackdropToggle.IsOn = true;
            remoteServerBaseAddressBox.Text = "http://127.0.0.1:5272";
            remoteAdminTokenBox.Password = string.Empty;
            giphyApiKeyBox.Password = string.Empty;
            suppressMicaBackdropChanged = false;
            micaBackdropStatusText.Text = "Nao foi possivel carregar esta preferencia. O app manteve a aparencia atual.";
            deviceServerStatusText.Text = "Nao foi possivel carregar as preferencias do servidor.";
            giphyApiKeyStatusText.Text = "Nao foi possivel carregar a chave GIPHY.";
            App.ReportError("SettingsPage.LoadGeneralSettingsAsync failed", ex);
        }
    }

    private async void OnUseMicaBackdropToggled(object sender, RoutedEventArgs e)
    {
        if (suppressMicaBackdropChanged)
        {
            return;
        }

        var previousSettings = currentSettings;
        var updatedSettings = settingsDomainService.Copy(currentSettings, builder =>
        {
            builder.SetUseMicaBackdrop(micaBackdropToggle.IsOn);
        });

        try
        {
            await settingsRepository.SaveAsync(updatedSettings);
            currentSettings = updatedSettings;
            var result = App.ApplyBackdropPreference(updatedSettings.UseMicaBackdrop);
            micaBackdropStatusText.Text = result.StatusMessage;
        }
        catch (Exception ex)
        {
            currentSettings = previousSettings;
            suppressMicaBackdropChanged = true;
            micaBackdropToggle.IsOn = previousSettings.UseMicaBackdrop;
            suppressMicaBackdropChanged = false;
            micaBackdropStatusText.Text = "Nao foi possivel salvar a preferencia. O valor anterior foi restaurado.";
            App.ReportError("SettingsPage.OnUseMicaBackdropToggled failed", ex);
        }
    }

    private void OnOpenLogsDirectoryClicked(object sender, RoutedEventArgs e)
    {
        var directoryPath = ResolveLogsDirectoryPath(App.CurrentCrashLogPath);

        try
        {
            Directory.CreateDirectory(directoryPath);
            _ = Process.Start(BuildOpenLogsDirectoryStartInfo(directoryPath));
            errorLogsStatusText.Text = "Pasta dos logs aberta.";
        }
        catch (Exception ex)
        {
            errorLogsStatusText.Text = "Nao foi possivel abrir a pasta dos logs.";
            App.ReportError("SettingsPage.OpenLogsDirectory failed", ex);
        }
    }

    private async void OnSaveDeviceServerSettingsClicked(object sender, RoutedEventArgs e)
    {
        var previousSettings = currentSettings;
        var updatedSettings = settingsDomainService.Copy(currentSettings, builder =>
        {
            builder.SetRemoteServerBaseAddress(remoteServerBaseAddressBox.Text);
        });

        try
        {
            await settingsRepository.SaveAsync(updatedSettings);
            await remoteSecretStore.SaveAdminTokenAsync(remoteAdminTokenBox.Password);
            currentSettings = updatedSettings;
            remoteServerBaseAddressBox.Text = updatedSettings.RemoteServerBaseAddress;
            deviceServerStatusText.Text = BuildDeviceServerStatusText(updatedSettings.RemoteServerBaseAddress);
        }
        catch (Exception ex)
        {
            currentSettings = previousSettings;
            remoteServerBaseAddressBox.Text = previousSettings.RemoteServerBaseAddress;
            deviceServerStatusText.Text = "Nao foi possivel salvar as preferencias do servidor.";
            App.ReportError("SettingsPage.SaveDeviceServerSettings failed", ex);
        }
    }

    private void UpdateErrorLogsInfo()
    {
        errorLogsPathText.Text = $"Arquivo: {App.CurrentCrashLogPath}";
        errorLogsStatusText.Text = $"Pasta: {ResolveLogsDirectoryPath(App.CurrentCrashLogPath)}";
    }

    private Grid BuildLayout()
    {
        var root = new Grid
        {
            Background = CreateSettingsBaseBrush(),
            Padding = new Thickness(16),
            RowSpacing = 12,
        };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var headerBorder = new Border
        {
            Background = CreateSettingsSurfaceBrush(),
            BorderBrush = CreateSettingsStrokeBrush(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12, 16, 12),
            Child = new TextBlock
            {
                Text = "Configuracoes",
                VerticalAlignment = VerticalAlignment.Center,
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
            },
        };
        Grid.SetRow(headerBorder, 0);

        var contentBorder = new Border
        {
            Background = CreateSettingsSurfaceBrush(),
            BorderBrush = CreateSettingsStrokeBrush(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
        };
        Grid.SetRow(contentBorder, 1);

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        };

        var contentStack = new StackPanel
        {
            Spacing = 16,
        };

        contentStack.Children.Add(new TextBlock
        {
            Text = "Geral",
            Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
        });
        contentStack.Children.Add(new TextBlock
        {
            Opacity = 0.78,
            TextWrapping = TextWrapping.Wrap,
            Text = "Preferencias globais da janela e acesso rapido ao arquivo de erros do app.",
        });
        contentStack.Children.Add(BuildWindowAppearanceCard());
        contentStack.Children.Add(BuildDeviceServerCard());
        contentStack.Children.Add(BuildGiphyCard());
        contentStack.Children.Add(BuildErrorLogsCard());

        scrollViewer.Content = contentStack;
        contentBorder.Child = scrollViewer;

        root.Children.Add(headerBorder);
        root.Children.Add(contentBorder);
        return root;
    }

    private Border BuildWindowAppearanceCard()
    {
        micaBackdropToggle = new ToggleSwitch
        {
            Header = "Usar efeito Mica",
            OnContent = "Ligado",
            OffContent = "Desligado",
        };
        micaBackdropToggle.Toggled += OnUseMicaBackdropToggled;

        micaBackdropStatusText = new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            Text = "Carregando preferencia de aparencia da janela...",
        };

        return CreateCard(
            "Aparencia da janela",
            "Quando desativado, o app usa uma superficie solida em vez do material Mica.",
            micaBackdropToggle,
            micaBackdropStatusText);
    }

    private Border BuildGiphyCard()
    {
        giphyApiKeyBox = new PasswordBox
        {
            Header = "Chave de API GIPHY",
            PlaceholderText = "Cole sua chave de API aqui",
            MinWidth = 320,
        };

        var saveButton = new Button
        {
            Content = "Salvar chave GIPHY",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = Application.Current.Resources["AppChromeButtonStyle"] as Style,
        };
        saveButton.Click += OnSaveGiphySettingsClicked;

        giphyApiKeyStatusText = new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            Text = "Carregando...",
        };

        return CreateCard(
            "GIPHY",
            "Chave de API do GIPHY para busca de GIFs animados. Salva localmente com criptografia DPAPI. Ao salvar, o app tenta configurar o servidor automaticamente — assim a busca funciona sem reiniciar o servidor.",
            giphyApiKeyBox,
            saveButton,
            giphyApiKeyStatusText);
    }

    private async void OnSaveGiphySettingsClicked(object sender, RoutedEventArgs e)
    {
        var key = giphyApiKeyBox.Password;
        try
        {
            await remoteSecretStore.SaveGiphyApiKeyAsync(key);
            giphyApiKeyStatusText.Text = string.IsNullOrWhiteSpace(key)
                ? "Chave GIPHY removida."
                : "Chave GIPHY salva localmente.";

            await TryPushGiphyApiKeyToServerAsync(key);
        }
        catch (Exception ex)
        {
            giphyApiKeyStatusText.Text = "Nao foi possivel salvar a chave GIPHY.";
            App.ReportError("SettingsPage.OnSaveGiphySettingsClicked failed", ex);
        }
    }

    private async Task TryPushGiphyApiKeyToServerAsync(string apiKey)
    {
        var baseAddress = remoteServerBaseAddressBox.Text.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            return;
        }

        try
        {
            var adminToken = await remoteSecretStore.LoadAdminTokenAsync();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(HttpMethod.Put, $"{baseAddress}/api/v1/admin/giphy/apikey");
            if (!string.IsNullOrEmpty(adminToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
            }

            var body = JsonSerializer.Serialize(new { apiKey });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                giphyApiKeyStatusText.Text += " Servidor configurado.";
            }
            else
            {
                giphyApiKeyStatusText.Text += $" Servidor retornou {(int)response.StatusCode} — configure MICA_SERVER__GiphyApiKey manualmente se necessario.";
            }
        }
        catch
        {
            giphyApiKeyStatusText.Text += " Servidor nao acessivel — configure MICA_SERVER__GiphyApiKey manualmente se necessario.";
        }
    }

    private Border BuildErrorLogsCard()
    {
        errorLogsPathText = new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Consolas"),
            Text = "Arquivo: carregando...",
        };

        errorLogsStatusText = new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            Text = "Pasta: carregando...",
        };

        var openButton = new Button
        {
            Content = "Abrir pasta dos logs",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = Application.Current.Resources["AppChromeButtonStyle"] as Style,
        };
        openButton.Click += OnOpenLogsDirectoryClicked;

        return CreateCard(
            "Logs de erro",
            "Apenas erros do app sao gravados em crash.log. Use o atalho abaixo para abrir a pasta do arquivo.",
            errorLogsPathText,
            errorLogsStatusText,
            openButton);
    }

    private Border BuildDeviceServerCard()
    {
        remoteServerBaseAddressBox = new TextBox
        {
            Header = "Servidor remoto",
            PlaceholderText = "http://127.0.0.1:5272",
            MinWidth = 320,
        };

        remoteAdminTokenBox = new PasswordBox
        {
            Header = "Admin token",
            MinWidth = 320,
        };

        var saveButton = new Button
        {
            Content = "Salvar servidor",
            HorizontalAlignment = HorizontalAlignment.Left,
            Style = Application.Current.Resources["AppChromeButtonStyle"] as Style,
        };
        saveButton.Click += OnSaveDeviceServerSettingsClicked;

        deviceServerStatusText = new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            Text = "Carregando preferencias do servidor...",
        };

        return CreateCard(
            "Servidor de dispositivos",
            "Endereco do MicaAudio.Server externo usado pela aplicacao no proximo restart.",
            remoteServerBaseAddressBox,
            remoteAdminTokenBox,
            saveButton,
            deviceServerStatusText);
    }

    private static Border CreateCard(string title, string description, params UIElement[] content)
    {
        var stack = new StackPanel
        {
            Spacing = 8,
        };

        stack.Children.Add(new TextBlock
        {
            Text = title,
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        });
        stack.Children.Add(new TextBlock
        {
            Opacity = 0.72,
            TextWrapping = TextWrapping.Wrap,
            Text = description,
        });

        foreach (var element in content)
        {
            stack.Children.Add(element);
        }

        return new Border
        {
            Background = CreateSettingsPaneBrush(),
            BorderBrush = CreateSettingsStrokeBrush(),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Child = stack,
        };
    }

    internal static string BuildMicaBackdropStatusText(bool useMicaRequested, bool micaApplied)
    {
        if (!useMicaRequested)
        {
            return "Mica desativado. O app usa uma superficie solida.";
        }

        return micaApplied
            ? "Mica ativo na janela principal."
            : "Mica configurado, mas este ambiente esta usando o fallback de superficie solida.";
    }

    internal static string BuildDeviceServerStatusText(string remoteBaseAddress)
    {
        return $"Remote salvo para o proximo restart: {remoteBaseAddress}";
    }

    internal static string ResolveLogsDirectoryPath(string crashLogPath)
    {
        if (!string.IsNullOrWhiteSpace(crashLogPath))
        {
            var directory = Path.GetDirectoryName(crashLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                return directory;
            }
        }

        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MicaAudio");
    }

    internal static ProcessStartInfo BuildOpenLogsDirectoryStartInfo(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        return new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{directoryPath}\"",
            UseShellExecute = true,
        };
    }

    private static SolidColorBrush CreateSettingsBaseBrush() => CreateBrush(0x00, 0x00, 0x00);

    private static SolidColorBrush CreateSettingsSurfaceBrush() => CreateBrush(0x05, 0x05, 0x05);

    private static SolidColorBrush CreateSettingsPaneBrush() => CreateBrush(0x0B, 0x0B, 0x0B);

    private static SolidColorBrush CreateSettingsStrokeBrush() => CreateBrush(0x1D, 0x1D, 0x1D);

    private static SolidColorBrush CreateBrush(byte red, byte green, byte blue)
        => new(Color.FromArgb(255, red, green, blue));
}
