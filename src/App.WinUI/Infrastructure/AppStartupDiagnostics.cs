using System.Text;

namespace App.WinUI.Infrastructure;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-startup-estavel-e-observabilidade-real
internal static class AppStartupDiagnostics
{
    private const int MaxBreadcrumbCount = 32;

    private static readonly object Gate = new();
    private static readonly Queue<string> Breadcrumbs = new();

    internal static void RecordBreadcrumb(string stage)
    {
        if (string.IsNullOrWhiteSpace(stage))
        {
            return;
        }

        var entry = $"{DateTimeOffset.Now:O} | {stage}";

        lock (Gate)
        {
            if (Breadcrumbs.Count >= MaxBreadcrumbCount)
            {
                Breadcrumbs.Dequeue();
            }

            Breadcrumbs.Enqueue(entry);
        }
    }

    internal static void ResetBreadcrumbs()
    {
        lock (Gate)
        {
            Breadcrumbs.Clear();
        }
    }

    internal static IReadOnlyList<string> GetBreadcrumbSnapshot()
    {
        lock (Gate)
        {
            return Breadcrumbs.ToArray();
        }
    }

    internal static string BuildCrashLogContent(
        string header,
        Exception exception,
        IReadOnlyList<string>? breadcrumbs = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(header);
        ArgumentNullException.ThrowIfNull(exception);

        var snapshot = breadcrumbs ?? GetBreadcrumbSnapshot();
        var builder = new StringBuilder()
            .AppendLine("=== " + header + " ===")
            .AppendLine(DateTimeOffset.Now.ToString("O"))
            .AppendLine();

        if (snapshot.Count > 0)
        {
            builder.AppendLine("Startup breadcrumbs:");
            foreach (var breadcrumb in snapshot)
            {
                builder.AppendLine(" - " + breadcrumb);
            }

            builder.AppendLine();
        }

        builder.AppendLine(exception.ToString());
        builder.AppendLine();
        return builder.ToString();
    }

    internal static void WriteCrashLog(
        string path,
        string header,
        Exception exception,
        Action<string, Exception>? writeToLogger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(exception);

        try
        {
            writeToLogger?.Invoke(header, exception);
        }
        catch
        {
            // Logging diagnostics must not block crash file creation.
        }

        try
        {
            var logEntry = BuildCrashLogContent(header, exception);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (Gate)
            {
                File.AppendAllText(path, logEntry, Encoding.UTF8);
            }
        }
        catch
        {
            // Ignore secondary failures while writing crash diagnostics.
        }
    }
}
