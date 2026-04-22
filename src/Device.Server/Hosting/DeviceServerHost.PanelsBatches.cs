using Device.Client;
using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#transporte-de-lotes-webp-para-paineis
// DOCS: docs/handoffs/2026-04-22-device-server-panels-batch-storage.md
// DOCS: docs/handoffs/2026-04-22-device-server-session-state-store.md
public sealed partial class DeviceServerHost
{
    public PanelsBatchRegistration RegisterPanelsBatch(
        string deviceId,
        string panelsSessionId,
        ulong batchSequence,
        byte[] payload,
        int frameCount,
        int durationMs,
        string contentType = "image/webp")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(panelsSessionId);
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var batch = panelsBatchStore.Save(new PanelsBatchWrite(
            deviceId,
            panelsSessionId,
            batchSequence,
            payload,
            frameCount,
            durationMs,
            contentType));

        return new PanelsBatchRegistration(
            batch.PanelsSessionId,
            batch.BatchSequence,
            batch.FileSizeBytes,
            batch.Sha256,
            batch.ContentType,
            batch.FrameCount,
            batch.DurationMs,
            $"{GetPublicHttpBaseAddress()}/api/v1/device/panels/batches/{batch.BatchSequence}.webp?panelsSessionId={Uri.EscapeDataString(batch.PanelsSessionId)}");
    }

    public void ClearPanelsBatches(string deviceId, string? panelsSessionId = null)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        panelsBatchStore.Clear(deviceId, panelsSessionId);
    }

    private IResult HandlePanelsBatchDownload(HttpContext ctx, ulong batchSequence)
    {
        if (!TryAuthenticate(ctx, AuthContext.HttpApi, out var state))
        {
            return Results.Unauthorized();
        }

        var panelsSessionId = ctx.Request.Query["panelsSessionId"].ToString().Trim();
        if (string.IsNullOrWhiteSpace(panelsSessionId))
        {
            return Results.BadRequest(new { error = "panels_session_id_missing" });
        }

        if (!panelsBatchStore.TryGet(state.Record.DeviceId, panelsSessionId, batchSequence, out var batch)
            || batch is null)
        {
            return Results.NotFound(new { error = "batch_not_found" });
        }

        var now = timeProvider.GetUtcNow();
        state.Touch(now);
        state.MarkSeen(
            now,
            ctx.Connection.RemoteIpAddress?.ToString(),
            state.Record.LastKnownRssi,
            state.Record.FirmwareVersion,
            state.Record.ActiveAppId,
            state.Record.ActiveAppName,
            state.Record.BoardModel,
            state.Record.PanelType);

        return Results.File(batch.Payload, batch.ContentType, enableRangeProcessing: false);
    }
}
