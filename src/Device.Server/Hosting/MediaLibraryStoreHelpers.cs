namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/paineis.md#server-first-library
// DOCS: docs/handoffs/2026-04-28-zero-code-lan-onboarding.md
internal static class MediaLibraryStoreHelpers
{
    public static string NormalizeFileName(string? fileName, string fallback)
    {
        var leaf = Path.GetFileName(fileName ?? string.Empty);
        return string.IsNullOrWhiteSpace(leaf) ? fallback : leaf.Trim();
    }

    public static string NormalizeExtension(string? fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.Trim().ToLowerInvariant();
        }

        return NormalizeContentType(contentType, string.Empty) switch
        {
            "image/gif" => ".gif",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".bin",
        };
    }

    public static string NormalizeContentType(string? contentType, string extension)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            return contentType.Trim().ToLowerInvariant();
        }

        return extension.ToLowerInvariant() switch
        {
            ".gif" => "image/gif",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "application/octet-stream",
        };
    }
}
