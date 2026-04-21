using System.Globalization;
using MicaAudio.Core.Config;
using Microsoft.Extensions.Options;

namespace App.WinUI.Services.Logging;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-settingspage-simplificada-e-arquivo-unico-de-erros
internal enum LogCategory
{
    System,
    Devices,
    Visualizer,
    App,
}

internal enum LogSeverity
{
    Info,
    Warning,
    Error,
}

internal sealed class LogEntry
{
    public LogEntry(DateTimeOffset timestamp, LogCategory category, LogSeverity severity, string message, string? appId = null)
    {
        Timestamp = timestamp;
        Category = category;
        Severity = severity;
        Message = message;
        AppId = appId;
    }

    public DateTimeOffset Timestamp { get; }

    public LogCategory Category { get; }

    public LogSeverity Severity { get; }

    public string Message { get; }

    public string? AppId { get; }

    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

internal sealed class AppLogStore
{
    internal static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);

    private static readonly object FileGate = new();
    private readonly object gate = new();
    private readonly List<LogEntry> entries = new();
    private readonly string crashLogPath;
    private readonly Func<DateTimeOffset> nowProvider;

    public AppLogStore(IOptions<MicaAudioOptions> options)
        : this(options, static () => DateTimeOffset.Now)
    {
    }

    internal AppLogStore(IOptions<MicaAudioOptions> options, Func<DateTimeOffset> nowProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(nowProvider);

        crashLogPath = ResolveCrashLogPath(options.Value);
        this.nowProvider = nowProvider;
    }

    public event EventHandler? LogAdded;

    public void Append(LogCategory category, LogSeverity severity, string message, string? appId = null)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var entry = new LogEntry(nowProvider(), category, severity, message, appId);

        lock (gate)
        {
            PruneExpiredEntriesLocked();
            entries.Add(entry);

            if (severity == LogSeverity.Error)
            {
                PersistError(entry);
            }
        }

        LogAdded?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<LogEntry> GetAll()
    {
        lock (gate)
        {
            PruneExpiredEntriesLocked();
            return entries.ToArray();
        }
    }

    public IReadOnlyList<LogEntry> GetErrors()
    {
        lock (gate)
        {
            PruneExpiredEntriesLocked();
            return entries.Where(static entry => entry.Severity == LogSeverity.Error).ToArray();
        }
    }

    public IReadOnlyList<LogEntry> GetByAppId(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return Array.Empty<LogEntry>();
        }

        lock (gate)
        {
            PruneExpiredEntriesLocked();
            return entries
                .Where(entry => string.Equals(entry.AppId, appId, StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }

    public IReadOnlyList<LogEntry> GetByCategory(LogCategory category)
    {
        lock (gate)
        {
            PruneExpiredEntriesLocked();
            return entries.Where(entry => entry.Category == category).ToArray();
        }
    }

    public void Clear()
    {
        lock (gate)
        {
            entries.Clear();
        }

        LogAdded?.Invoke(this, EventArgs.Empty);
    }

    internal static string BuildPersistedErrorLine(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return string.IsNullOrWhiteSpace(entry.AppId)
            ? $"{entry.Timestamp:O} | {entry.Category} | {entry.Message}"
            : $"{entry.Timestamp:O} | {entry.Category} | app={entry.AppId} | {entry.Message}";
    }

    private void PruneExpiredEntriesLocked()
    {
        var threshold = nowProvider().Subtract(RetentionPeriod);
        _ = entries.RemoveAll(entry => entry.Timestamp < threshold);
    }

    private void PersistError(LogEntry entry)
    {
        try
        {
            var directory = Path.GetDirectoryName(crashLogPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (FileGate)
            {
                File.AppendAllText(crashLogPath, BuildPersistedErrorLine(entry) + Environment.NewLine);
            }
        }
        catch
        {
            // Logging must not fail the app flow.
        }
    }

    private static string ResolveCrashLogPath(MicaAudioOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.CrashLogPath))
        {
            return options.CrashLogPath;
        }

        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MicaAudio");
        return Path.Combine(root, "crash.log");
    }
}
