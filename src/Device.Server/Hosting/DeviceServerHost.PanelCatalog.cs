using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Panels.Composition.Models;
using Panels.Composition.ServerSide;

namespace Device.Server.Hosting;

// CRUD endpoints for the server-side panel catalog.
// Unlike the per-device panel store (DeviceServerHost.PanelStore.cs), the
// catalog holds EVERY panel ever created by any client — Windows, Android, …
// Clients call these endpoints when creating, editing, or deleting a panel so
// the server remains the authoritative source of truth.
//
// Routes (registered in DeviceServerHost.Routes.cs):
//   POST   /api/v1/admin/panels              → create in catalog
//   GET    /api/v1/admin/panels/{panelId}    → get full panel from catalog
//   PUT    /api/v1/admin/panels/{panelId}    → upsert (create or update)
//   DELETE /api/v1/admin/panels/{panelId}    → remove from catalog
public sealed partial class DeviceServerHost
{
    private IServerPanelCatalogStore? panelCatalogStore;

    /// <summary>
    /// Registers the panel catalog store. Called by PanelCompositorHostedService
    /// during startup (same pattern as AttachPanelStore).
    /// </summary>
    public void AttachPanelCatalogStore(IServerPanelCatalogStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        panelCatalogStore = store;
    }

    /// <summary>Read-only access for diagnostics.</summary>
    public IServerPanelCatalogStore? PanelCatalogStore => panelCatalogStore;

    // ── POST /panels ──────────────────────────────────────────────────────────

    private async Task<IResult> HandleAdminCreateCatalogPanelAsync(HttpContext ctx)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (panelCatalogStore is null)
        {
            return Results.Json(
                new { error = "panel_catalog_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (IsRequestBodyTooLarge(ctx))
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        PanelDefinition? panel;
        try
        {
            panel = await JsonSerializer.DeserializeAsync<PanelDefinition>(
                ctx.Request.Body, JsonOptions, ctx.RequestAborted).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        if (panel is null)
        {
            return Results.BadRequest(new { error = "missing_panel_body" });
        }

        panel.PanelId = string.IsNullOrWhiteSpace(panel.PanelId)
            ? Guid.NewGuid().ToString("N")
            : panel.PanelId.Trim();
        panel.Normalize();
        panel.UpdatedAtUtc = timeProvider.GetUtcNow();

        var stored = panelCatalogStore.Upsert(panel);
        Log($"Panel catalog: created '{stored.Name}' (panelId={stored.PanelId}).");
        return Results.Created($"/api/v1/admin/panels/{stored.PanelId}", stored);
    }

    // ── GET /panels/{panelId} ─────────────────────────────────────────────────

    private IResult HandleAdminGetCatalogPanel(HttpContext ctx, string panelId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (panelCatalogStore is null)
        {
            return Results.Json(
                new { error = "panel_catalog_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(panelId))
        {
            return Results.BadRequest(new { error = "missing_panel_id" });
        }

        var panel = panelCatalogStore.TryGet(panelId);
        if (panel is null)
        {
            return Results.NotFound(new { error = "panel_not_found", panelId });
        }

        return Results.Ok(panel);
    }

    // ── PUT /panels/{panelId} ─────────────────────────────────────────────────

    private async Task<IResult> HandleAdminUpdateCatalogPanelAsync(HttpContext ctx, string panelId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (panelCatalogStore is null)
        {
            return Results.Json(
                new { error = "panel_catalog_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(panelId))
        {
            return Results.BadRequest(new { error = "missing_panel_id" });
        }

        if (IsRequestBodyTooLarge(ctx))
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        PanelDefinition? panel;
        try
        {
            panel = await JsonSerializer.DeserializeAsync<PanelDefinition>(
                ctx.Request.Body, JsonOptions, ctx.RequestAborted).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        if (panel is null)
        {
            return Results.BadRequest(new { error = "missing_panel_body" });
        }

        // Ensure the route panelId wins over any id in the body.
        panel.PanelId = panelId.Trim();
        panel.Normalize();
        panel.UpdatedAtUtc = timeProvider.GetUtcNow();

        var stored = panelCatalogStore.Upsert(panel);
        Log($"Panel catalog: upserted '{stored.Name}' (panelId={stored.PanelId}).");
        return Results.Ok(stored);
    }

    // ── DELETE /panels/{panelId} ──────────────────────────────────────────────

    private IResult HandleAdminDeleteCatalogPanel(HttpContext ctx, string panelId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (panelCatalogStore is null)
        {
            return Results.Json(
                new { error = "panel_catalog_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(panelId))
        {
            return Results.BadRequest(new { error = "missing_panel_id" });
        }

        var removed = panelCatalogStore.Remove(panelId);
        Log($"Panel catalog: removed panelId={panelId} (existed={removed}).");
        return Results.Ok(new { panelId, removed });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds the summary list for GET /panels, annotating each catalog entry
    /// with the deviceId it is currently active on (if any).
    /// </summary>
    private object[] BuildCatalogSummary()
    {
        var catalogPanels = panelCatalogStore!.ListAll();

        // Build panelId → deviceId reverse-map from the per-device panel store.
        var panelToDevice = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (panelStore is not null)
        {
            foreach (var deviceId in panelStore.EnumerateDeviceIds())
            {
                var active = panelStore.TryGet(deviceId);
                if (active is not null && !string.IsNullOrWhiteSpace(active.PanelId))
                {
                    panelToDevice[active.PanelId] = deviceId;
                }
            }
        }

        return catalogPanels.Select(panel =>
        {
            panelToDevice.TryGetValue(panel.PanelId, out var activeOnDeviceId);
            var capability = PanelServerCapabilityClassifier.Classify(panel);
            return (object)new
            {
                panel,
                activeOnDeviceId,
                capability = capability.ToString(),
            };
        }).ToArray();
    }
}
