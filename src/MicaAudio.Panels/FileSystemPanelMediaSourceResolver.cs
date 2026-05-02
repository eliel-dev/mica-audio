using Device.Protocol.Models;

namespace MicaAudio.Panels;

// DOCS: docs/wiki/modules/paineis.md#renderizacao-e-preview-local
public sealed class FileSystemPanelMediaSourceResolver : IPanelMediaSourceResolver
{
    private static readonly HashSet<string> SupportedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".gif", ".png", ".jpg", ".jpeg", ".bmp", ".webp" };

    public async Task<IReadOnlyList<PanelMediaSource>> ResolveAsync(PanelWidgetItem widget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(widget);

        var sourcePath = widget.RuntimeState.TryGetValue("sourcePath", out var storedPath)
            ? storedPath?.Trim()
            : string.Empty;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return Array.Empty<PanelMediaSource>();
        }

        if (Directory.Exists(sourcePath))
        {
            var sources = Directory
                .EnumerateFiles(sourcePath)
                .Where(static path => SupportedImageExtensions.Contains(Path.GetExtension(path)))
                .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var resolved = new List<PanelMediaSource>(sources.Length);
            foreach (var path in sources)
            {
                resolved.Add(await ReadFileAsync(path, cancellationToken).ConfigureAwait(false));
            }

            return resolved;
        }

        return File.Exists(sourcePath) && SupportedImageExtensions.Contains(Path.GetExtension(sourcePath))
            ? [await ReadFileAsync(sourcePath, cancellationToken).ConfigureAwait(false)]
            : Array.Empty<PanelMediaSource>();
    }

    private static async Task<PanelMediaSource> ReadFileAsync(string path, CancellationToken cancellationToken)
    {
        var payload = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
        return new PanelMediaSource(
            CacheKey: Path.GetFullPath(path),
            Payload: payload,
            ContentType: ResolveContentType(path),
            FileName: Path.GetFileName(path));
    }

    private static string ResolveContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".gif" => "image/gif",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".bmp" => "image/bmp",
            _ => "application/octet-stream",
        };
    }
}
