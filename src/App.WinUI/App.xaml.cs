using App.WinUI.Views;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Text;

namespace App.WinUI;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App()
    {
        UnhandledException += OnUnhandledException;
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            WriteCrashLog("InitializeComponent(App) failed", ex);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow ??= new Window();

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            rootFrame.NavigationFailed += OnNavigationFailed;
            MainWindow.Content = rootFrame;
        }

        try
        {
            if (rootFrame.Content is null)
            {
                rootFrame.Navigate(typeof(MainPage), args.Arguments);
            }
        }
        catch (Exception ex)
        {
            WriteCrashLog("OnLaunched failed", ex);
            MainWindow.Content = BuildStartupFallbackView(ex);
        }

        MainWindow.Activate();
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to navigate to {e.SourcePageType.FullName}.");
    }

    private static void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        try
        {
            WriteCrashLog("UnhandledException", e.Exception);
        }
        catch
        {
            // Ignore secondary failures while logging crashes.
        }
    }

    private static void WriteCrashLog(string header, Exception ex)
    {
        var path = GetCrashLogPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var log = new StringBuilder()
            .AppendLine("=== " + header + " ===")
            .AppendLine(DateTimeOffset.Now.ToString("O"))
            .AppendLine(ex.ToString())
            .AppendLine();

        File.AppendAllText(path, log.ToString());
    }

    private static string GetCrashLogPath()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MicaAudio");
        return Path.Combine(root, "crash.log");
    }

    private static UIElement BuildStartupFallbackView(Exception ex)
    {
        var panel = new StackPanel
        {
            Padding = new Thickness(20),
            Spacing = 10,
        };

        panel.Children.Add(new TextBlock
        {
            Text = "Falha ao iniciar a interface principal.",
            FontSize = 20,
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"Log de erro: {GetCrashLogPath()}",
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"{ex.GetType().Name}: {ex.Message}",
            TextWrapping = TextWrapping.WrapWholeWords,
        });

        return new ScrollViewer
        {
            Content = panel,
        };
    }
}
