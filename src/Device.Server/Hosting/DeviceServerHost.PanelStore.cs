using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Panels.Composition.Models;
using Panels.Composition.ServerSide;

namespace Device.Server.Hosting;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md
//
// Admin panel-storage endpoints. The WinUI client uploads the active panel
// (PUT /api/v1/admin/devices/{deviceId}/panel) so that, after the desktop
// process exits, MicaAudio.Server's PanelCompositorHostedService can keep
// the autonomous widgets (Clock today, more later) alive on the device.
//
// The DELETE endpoint clears the stored panel (e.g. when the user picks a
// panel that requires the client). The GET endpoint helps debugging and is
// also useful for the dashboard to reflect which panel will resume rendering
// once the client disconnects.
public sealed partial class DeviceServerHost
{
    private IServerPanelStore? panelStore;

    /// <summary>
    /// Allows MicaAudio.Server to register its panel store after construction.
    /// We expose this via a setter (instead of a ctor parameter) because the
    /// store is optional: legacy callers and embedded scenarios may not need
    /// the panel-upload endpoints at all.
    /// </summary>
    public void AttachPanelStore(IServerPanelStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        panelStore = store;
    }

    /// <summary>
    /// Read-only access to the store so the hosted compositor can enumerate
    /// devices with stored panels without holding a separate reference.
    /// </summary>
    public IServerPanelStore? PanelStore => panelStore;

    private async Task<IResult> HandleAdminUploadPanelAsync(HttpContext ctx, string deviceId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (panelStore is null)
        {
            return Results.Json(
                new { error = "panel_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest(new { error = "missing_device_id" });
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

        panel.Normalize();
        var capability = PanelServerCapabilityClassifier.Classify(panel);

        panelStore.Save(deviceId, panel);
        Log($"Panel uploaded for device {deviceId} ({panel.Widgets.Count} widget(s), capability={capability}).");
        return Results.Ok(new
        {
            deviceId,
            panelId = panel.PanelId,
            widgetCount = panel.Widgets.Count,
            capability = capability.ToString(),
        });
    }

    private IResult HandleAdminGetPanel(HttpContext ctx, string deviceId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (panelStore is null)
        {
            return Results.Json(
                new { error = "panel_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest(new { error = "missing_device_id" });
        }

        var panel = panelStore.TryGet(deviceId);
        if (panel is null)
        {
            return Results.NotFound(new { error = "panel_not_found", deviceId });
        }

        var capability = PanelServerCapabilityClassifier.Classify(panel);
        return Results.Ok(new
        {
            deviceId,
            panel,
            capability = capability.ToString(),
        });
    }

    private IResult HandleAdminDeletePanel(HttpContext ctx, string deviceId)
    {
        if (TryRejectAdminRequest(ctx, out var rejected))
        {
            return rejected;
        }

        if (panelStore is null)
        {
            return Results.Json(
                new { error = "panel_store_not_configured" },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Results.BadRequest(new { error = "missing_device_id" });
        }

        var removed = panelStore.Remove(deviceId);
        Log($"Panel removal for device {deviceId} (existed={removed}).");
        return Results.Ok(new { deviceId, removed });
    }
}
