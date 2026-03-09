using App.WinUI.Views;

namespace Integration.Smoke;

public sealed class SettingsPageSmokeTests
{
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
}
