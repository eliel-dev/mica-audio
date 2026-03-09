using App.WinUI.Services.Logging;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;

namespace Output.Tests;

public sealed class AppLogStoreTests
{
    [Fact]
    public void Append_ShouldPersistOnlyErrorEntries_ToCrashLog()
    {
        var root = CreateTempRoot();
        var crashLogPath = Path.Combine(root, "crash.log");

        try
        {
            var options = CreateOptions(root, crashLogPath);
            var now = new DateTimeOffset(2026, 3, 9, 12, 0, 0, TimeSpan.Zero);
            var store = new AppLogStore(options, () => now);

            store.Append(LogCategory.App, LogSeverity.Info, "info");
            store.Append(LogCategory.System, LogSeverity.Warning, "warning");
            store.Append(LogCategory.Devices, LogSeverity.Error, "falha no flash", "device-1");

            var content = File.ReadAllText(crashLogPath);

            Assert.DoesNotContain("info", content);
            Assert.DoesNotContain("warning", content);
            Assert.Contains("Devices", content);
            Assert.Contains("falha no flash", content);
            Assert.Contains("app=device-1", content);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Append_ShouldKeepInfoAndWarningOnlyInMemory()
    {
        var root = CreateTempRoot();
        var crashLogPath = Path.Combine(root, "crash.log");

        try
        {
            var store = new AppLogStore(CreateOptions(root, crashLogPath), () => DateTimeOffset.UtcNow);

            store.Append(LogCategory.System, LogSeverity.Info, "startup");
            store.Append(LogCategory.System, LogSeverity.Warning, "fallback");

            var all = store.GetAll();

            Assert.Equal(2, all.Count);
            Assert.DoesNotContain(all, static entry => entry.Severity == LogSeverity.Error);
            Assert.False(File.Exists(crashLogPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldIgnoreLegacyAppLogsJson()
    {
        var root = CreateTempRoot();
        var legacyDirectory = Path.Combine(root, "logs");
        var legacyPath = Path.Combine(legacyDirectory, "app-logs.json");
        var crashLogPath = Path.Combine(root, "crash.log");

        try
        {
            Directory.CreateDirectory(legacyDirectory);
            File.WriteAllText(legacyPath, "{\"entries\":[{\"message\":\"legacy\"}]}");

            var store = new AppLogStore(CreateOptions(root, crashLogPath), () => DateTimeOffset.UtcNow);

            Assert.Empty(store.GetAll());
            Assert.False(File.Exists(crashLogPath));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void BuildPersistedErrorLine_ShouldIncludeTimestampCategoryMessageAndOptionalAppId()
    {
        var entry = new LogEntry(
            new DateTimeOffset(2026, 3, 9, 12, 30, 0, TimeSpan.Zero),
            LogCategory.App,
            LogSeverity.Error,
            "deploy falhou",
            "clock");

        var line = AppLogStore.BuildPersistedErrorLine(entry);

        Assert.Contains("2026-03-09T12:30:00.0000000+00:00", line);
        Assert.Contains("App", line);
        Assert.Contains("deploy falhou", line);
        Assert.Contains("app=clock", line);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mica-audio-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static IOptions<MicaAudioOptions> CreateOptions(string root, string crashLogPath)
    {
        return Options.Create(new MicaAudioOptions
        {
            AppDataRoot = root,
            CrashLogPath = crashLogPath,
        });
    }
}
