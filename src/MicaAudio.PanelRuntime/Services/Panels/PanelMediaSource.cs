using App.WinUI.Models.Panels;
using Device.Protocol.Models;

namespace App.WinUI.Services.Panels;

// DOCS: docs/wiki/modules/paineis.md#runtime-autonomo-server-owned
// DOCS: docs/handoffs/2026-04-30-server-owned-panels-runtime.md
internal sealed record PanelMediaSource(string Key, string FileName, byte[] Payload);

internal interface IPanelMediaSourceResolver
{
    Task<IReadOnlyList<PanelMediaSource>> ResolveAsync(PanelWidgetDefinition widget, CancellationToken cancellationToken);
}

internal sealed class LocalPanelMediaSourceResolver : IPanelMediaSourceResolver
{
    private static readonly HashSet<string> SupportedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gif", ".png", ".jpg", ".jpeg", ".bmp", ".webp" };

    public async Task<IReadOnlyList<PanelMediaSource>> ResolveAsync(
        PanelWidgetDefinition widget,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(widget);

        if (!widget.RuntimeState.TryGetValue(PanelWidgetRuntimeStateKeys.SourcePath, out var rawPath)
            || string.IsNullOrWhiteSpace(rawPath))
        {
            return Array.Empty<PanelMediaSource>();
        }

        var sourcePath = rawPath.Trim();
        if (Directory.Exists(sourcePath))
        {
            var files = Directory
                .EnumerateFiles(sourcePath)
                .Where(static path => SupportedImageExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return await LoadFilesAsync(files, cancellationToken).ConfigureAwait(false);
        }

        if (!File.Exists(sourcePath)
            || !SupportedImageExtensions.Contains(Path.GetExtension(sourcePath)))
        {
            return Array.Empty<PanelMediaSource>();
        }

        return await LoadFilesAsync([sourcePath], cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<PanelMediaSource>> LoadFilesAsync(
        string[] files,
        CancellationToken cancellationToken)
    {
        if (files.Length == 0)
        {
            return Array.Empty<PanelMediaSource>();
        }

        var sources = new List<PanelMediaSource>(files.Length);
        foreach (var path in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            sources.Add(new PanelMediaSource(
                path,
                Path.GetFileName(path),
                await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)));
        }

        return sources;
    }
}
