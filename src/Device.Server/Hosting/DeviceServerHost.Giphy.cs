using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#giphy-search-proxy
//
// Admin GIPHY search proxy. The server holds the API key; clients call
// GET /api/v1/admin/giphy/search?q=... and receive a simplified response.
// Clients then download GIF bytes directly from the GIPHY CDN (no auth needed)
// and upload via the existing PUT /media/{mediaId} endpoint.
//
// Route registered in DeviceServerHost.Routes.cs.
public sealed partial class DeviceServerHost
{
    // Shared HttpClient for GIPHY API requests — reused across calls.
    private static readonly HttpClient GiphyHttp = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
    };

    private string giphyApiKey = string.Empty;

    /// <summary>
    /// Supplies the GIPHY API key after construction.
    /// Mirrors the <see cref="AttachMediaStore"/> post-construction injection pattern.
    /// </summary>
    public void AttachGiphyApiKey(string apiKey)
    {
        ArgumentNullException.ThrowIfNull(apiKey);
        giphyApiKey = apiKey.Trim();
    }

    /// <summary>
    /// Sets the GIPHY API key at runtime without restarting the server.
    /// Body: <c>{"apiKey":"…"}</c>. Pass an empty string to clear the key.
    /// </summary>
    private async Task<IResult> HandleAdminSetGiphyApiKeyAsync(HttpContext ctx)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        SetGiphyApiKeyRequest req;
        try
        {
            req = await JsonSerializer.DeserializeAsync<SetGiphyApiKeyRequest>(
                ctx.Request.Body, JsonOptions, ctx.RequestAborted).ConfigureAwait(false)
                ?? new SetGiphyApiKeyRequest();
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        giphyApiKey = req.ApiKey?.Trim() ?? string.Empty;
        return Results.Ok(new { configured = !string.IsNullOrEmpty(giphyApiKey) });
    }

    private sealed record SetGiphyApiKeyRequest(string? ApiKey = null);

    private async Task<IResult> HandleAdminGiphySearchAsync(
        HttpContext ctx,
        string q,
        int limit = 20,
        int offset = 0)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (string.IsNullOrEmpty(giphyApiKey))
        {
            return Results.Json(
                new { error = "giphy_not_configured", detail = "Set MICA_SERVER__GiphyApiKey to enable GIF search." },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(q))
        {
            return Results.BadRequest(new { error = "missing_query" });
        }

        limit = Math.Clamp(limit, 1, 50);
        offset = Math.Max(0, offset);

        var giphyUrl = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            "https://api.giphy.com/v1/gifs/search?api_key={0}&q={1}&limit={2}&offset={3}&rating=g",
            Uri.EscapeDataString(giphyApiKey),
            Uri.EscapeDataString(q),
            limit,
            offset);

        try
        {
            using var response = await GiphyHttp.GetAsync(giphyUrl, ctx.RequestAborted).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return Results.Json(
                    new { error = "giphy_api_error", status = (int)response.StatusCode },
                    statusCode: StatusCodes.Status502BadGateway);
            }

            var rawJson = await response.Content.ReadAsStringAsync(ctx.RequestAborted).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
            var root = doc.RootElement;

            var items = new List<object>();
            if (root.TryGetProperty("data", out var data)
                && data.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var gif in data.EnumerateArray())
                {
                    var id    = gif.TryGetProperty("id",    out var p) ? p.GetString() ?? "" : "";
                    var title = gif.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";

                    var previewUrl = string.Empty;
                    var gifUrl     = string.Empty;

                    if (gif.TryGetProperty("images", out var images))
                    {
                        // Prefer small fixed-width for thumbnails; fall back to fixed_width
                        if (images.TryGetProperty("fixed_width_small", out var small)
                            && small.TryGetProperty("url", out var smallUrl))
                        {
                            previewUrl = smallUrl.GetString() ?? "";
                        }
                        else if (images.TryGetProperty("fixed_width", out var fw)
                                 && fw.TryGetProperty("url", out var fwUrl))
                        {
                            previewUrl = fwUrl.GetString() ?? "";
                        }

                        if (images.TryGetProperty("original", out var original)
                            && original.TryGetProperty("url", out var origUrl))
                        {
                            gifUrl = origUrl.GetString() ?? "";
                        }
                    }

                    if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(gifUrl))
                    {
                        items.Add(new { id, title, previewUrl, gifUrl });
                    }
                }
            }

            var total = 0;
            if (root.TryGetProperty("pagination", out var pagination)
                && pagination.TryGetProperty("total_count", out var totalProp))
            {
                total = totalProp.TryGetInt32(out var t) ? t : 0;
            }

            return Results.Ok(new { items, total, offset });
        }
        catch (OperationCanceledException)
        {
            return Results.StatusCode(StatusCodes.Status499ClientClosedRequest);
        }
        catch (HttpRequestException ex)
        {
            return Results.Json(
                new { error = "giphy_unreachable", detail = ex.Message },
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
