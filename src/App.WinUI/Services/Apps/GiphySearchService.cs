using System.Net.Http.Headers;
using System.Text.Json;

namespace App.WinUI.Services.Apps;

/// <summary>
/// Calls the MicaAudio.Server GIPHY search proxy endpoint and downloads GIF bytes
/// directly from the GIPHY CDN (no auth required for CDN URLs).
/// </summary>
internal sealed class GiphySearchService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    private readonly string searchBaseUrl;
    private readonly string adminToken;

    /// <param name="serverBaseUrl">MicaAudio.Server HTTP base (e.g. "http://localhost:5272").</param>
    /// <param name="adminToken">Value for the Authorization: Bearer header.</param>
    public GiphySearchService(string serverBaseUrl, string adminToken)
    {
        ArgumentNullException.ThrowIfNull(serverBaseUrl);
        this.searchBaseUrl = serverBaseUrl.TrimEnd('/') + "/api/v1/admin/giphy/search";
        this.adminToken = adminToken ?? string.Empty;
    }

    /// <summary>Searches GIPHY via the server proxy.</summary>
    public async Task<GiphySearchResponse?> SearchAsync(
        string q,
        int limit = 20,
        int offset = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return null;
        }

        var url = $"{searchBaseUrl}?q={Uri.EscapeDataString(q)}&limit={limit}&offset={offset}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrEmpty(adminToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        }

        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<GiphySearchResponse>(json, JsonOpts);
    }

    /// <summary>Downloads GIF bytes directly from the GIPHY CDN (no auth needed).</summary>
    public static async Task<byte[]?> DownloadGifBytesAsync(string gifUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(gifUrl))
        {
            return null;
        }

        try
        {
            return await Http.GetByteArrayAsync(gifUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

// ── Response models ───────────────────────────────────────────────────────────

internal sealed record GiphySearchResponse(
    List<GiphyItem> Items,
    int Total,
    int Offset);

internal sealed record GiphyItem(
    string Id,
    string Title,
    string PreviewUrl,
    string GifUrl);
