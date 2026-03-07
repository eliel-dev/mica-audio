using App.WinUI.Infrastructure;

namespace Integration.Smoke;

public sealed class AppStartupDiagnosticsTests
{
    [Fact]
    public void WriteCrashLog_ShouldCreateFileEvenWhenLoggerDelegateExists()
    {
        AppStartupDiagnostics.ResetBreadcrumbs();
        AppStartupDiagnostics.RecordBreadcrumb("Resolve MainPage");

        var tempDirectory = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        var crashLogPath = Path.Combine(tempDirectory, "crash.log");
        var loggerCalled = false;

        try
        {
            AppStartupDiagnostics.WriteCrashLog(
                crashLogPath,
                "Startup failed",
                new InvalidOperationException("boom"),
                (_, _) => loggerCalled = true);

            Assert.True(loggerCalled);
            Assert.True(File.Exists(crashLogPath));

            var content = File.ReadAllText(crashLogPath);
            Assert.Contains("Startup failed", content);
            Assert.Contains("Resolve MainPage", content);
            Assert.Contains("InvalidOperationException", content);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
