using System.Reflection;
using App.WinUI.Infrastructure.Serial;
using App.WinUI.Views;

namespace Integration.Smoke;

public sealed class SettingsPageSmokeTests
{
    [Fact]
    public void SettingsPageShouldDeclareSerialMonitorFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(SettingsPage).GetField("serialMonitorPortComboBox", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("serialMonitorConnectButton", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("serialMonitorClearButton", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("serialMonitorAutoFollowToggle", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("serialMonitorStatusText", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("serialMonitorListView", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("serialMonitorPlaceholderText", flags));
    }

    [Fact]
    public void SettingsPageShouldKeepSerialMonitorBuilders()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(SettingsPage).GetMethod("BuildDeviceObservabilityCard", flags));
        Assert.NotNull(typeof(SettingsPage).GetMethod("BuildSerialMonitorContent", flags));
    }

    [Theory]
    [InlineData(false, false, "Mica desativado. O app usa uma superficie solida.")]
    [InlineData(true, true, "Mica ativo na janela principal.")]
    [InlineData(true, false, "Mica configurado, mas este ambiente esta usando o fallback de superficie solida.")]
    public void BuildMicaBackdropStatusText_ShouldDescribeCurrentBackdropState(
        bool useMicaRequested,
        bool micaApplied,
        string expected)
    {
        var text = SettingsPage.BuildMicaBackdropStatusText(useMicaRequested, micaApplied);

        Assert.Equal(expected, text);
    }

    [Fact]
    public void ResolveLogsDirectoryPath_ShouldReturnCrashLogDirectory()
    {
        var directory = SettingsPage.ResolveLogsDirectoryPath(@"C:\Users\eliels\AppData\Local\MicaAudio\crash.log");

        Assert.Equal(@"C:\Users\eliels\AppData\Local\MicaAudio", directory);
    }

    [Fact]
    public void BuildOpenLogsDirectoryStartInfo_ShouldTargetExplorerWithFolderPath()
    {
        var startInfo = SettingsPage.BuildOpenLogsDirectoryStartInfo(@"C:\Users\eliels\AppData\Local\MicaAudio");

        Assert.Equal("explorer.exe", startInfo.FileName);
        Assert.Equal("\"C:\\Users\\eliels\\AppData\\Local\\MicaAudio\"", startInfo.Arguments);
        Assert.True(startInfo.UseShellExecute);
    }

    [Fact]
    public void ResolveSerialMonitorPlaceholder_ShouldDescribeDisconnectedAndConnectedStates()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

        var method = typeof(SettingsPage).GetMethod("ResolveSerialMonitorPlaceholder", flags);
        Assert.NotNull(method);

        var disconnected = (string?)method!.Invoke(null, new object?[]
        {
            new SerialMonitorSessionState
            {
                ConnectionState = SerialMonitorConnectionState.Disconnected,
                AvailablePorts = [new SerialPortDescriptor { PortName = "COM7", DisplayName = "ESP32-S3", IsPreferredDevice = true, PriorityRank = 0 }],
                SelectedPortName = "COM7",
            },
        });
        var connected = (string?)method.Invoke(null, new object?[]
        {
            new SerialMonitorSessionState
            {
                ConnectionState = SerialMonitorConnectionState.Connected,
            },
        });

        Assert.Equal("Pronto para monitorar COM7.", disconnected);
        Assert.Equal("Conectado. Aguardando saida serial do firmware...", connected);
    }
}
