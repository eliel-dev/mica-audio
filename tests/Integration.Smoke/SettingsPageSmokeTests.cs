using System.Reflection;
using App.WinUI.Views;

namespace Integration.Smoke;

public sealed class SettingsPageSmokeTests
{
    [Fact]
    public void SettingsPageShouldNotDeclareSerialMonitorFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.Null(typeof(SettingsPage).GetField("serialMonitorPortComboBox", flags));
        Assert.Null(typeof(SettingsPage).GetField("serialMonitorConnectButton", flags));
        Assert.Null(typeof(SettingsPage).GetField("serialMonitorClearButton", flags));
        Assert.Null(typeof(SettingsPage).GetField("serialMonitorAutoFollowToggle", flags));
        Assert.Null(typeof(SettingsPage).GetField("serialMonitorStatusText", flags));
        Assert.Null(typeof(SettingsPage).GetField("serialMonitorListView", flags));
        Assert.Null(typeof(SettingsPage).GetField("serialMonitorPlaceholderText", flags));
    }

    [Fact]
    public void SettingsPageShouldNotKeepSerialMonitorBuilders()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.Null(typeof(SettingsPage).GetMethod("BuildDeviceObservabilityCard", flags));
        Assert.Null(typeof(SettingsPage).GetMethod("BuildSerialMonitorContent", flags));
        Assert.Null(typeof(SettingsPage).GetMethod("ActivateSerialMonitorAsync", flags));
        Assert.Null(typeof(SettingsPage).GetMethod("DeactivateSerialMonitorAsync", flags));
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
    public void SettingsPageConstructor_ShouldNotRequireSerialMonitorService()
    {
        var parameterTypes = typeof(SettingsPage)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
            .SelectMany(static constructor => constructor.GetParameters())
            .Select(static parameter => parameter.ParameterType.FullName)
            .ToArray();

        Assert.DoesNotContain("App.WinUI.Infrastructure.Serial.ISerialMonitorService", parameterTypes);
    }
}
