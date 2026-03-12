using System.Reflection;
using App.WinUI.Views;
using Device.Protocol.Models;

namespace Integration.Smoke;

public sealed class SettingsPageSmokeTests
{
    [Fact]
    public void SettingsPageShouldDeclareObservedDeviceLogsFields()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(SettingsPage).GetField("observedDeviceComboBox", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("observedDeviceLogsHeaderText", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("observedDeviceLogsHintText", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("observedDeviceLogsSeverityComboBox", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("observedDeviceLogsSearchBox", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("observedDeviceLogsAutoFollowToggle", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("observedDeviceLogsListView", flags));
        Assert.NotNull(typeof(SettingsPage).GetField("observedDeviceLogsPlaceholderText", flags));
    }

    [Fact]
    public void SettingsPageShouldKeepObservedDeviceLogsBuilders()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        Assert.NotNull(typeof(SettingsPage).GetMethod("BuildDeviceObservabilityCard", flags));
        Assert.NotNull(typeof(SettingsPage).GetMethod("BuildObservedLogsContent", flags));
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
    public void ResolvePreferredObservedDeviceId_ShouldPreserveCurrentThenPreferOnline()
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

        var method = typeof(SettingsPage).GetMethod("ResolvePreferredObservedDeviceId", flags);
        Assert.NotNull(method);

        var devices = new[]
        {
            new DeviceSnapshot { DeviceId = "device-offline", Status = DeviceStatus.Offline },
            new DeviceSnapshot { DeviceId = "device-online", Status = DeviceStatus.Online },
        };

        var current = method!.Invoke(null, new object?[] { "device-offline", devices });
        var preferredOnline = method.Invoke(null, new object?[] { "missing", devices });
        var empty = method.Invoke(null, new object?[] { null, Array.Empty<DeviceSnapshot>() });

        Assert.Equal("device-offline", current);
        Assert.Equal("device-online", preferredOnline);
        Assert.Null(empty);
    }
}
