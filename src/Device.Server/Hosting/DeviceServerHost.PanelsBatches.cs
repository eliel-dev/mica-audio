using System.Security.Cryptography;
using Device.Client;
using Device.Protocol.Models;
using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#transporte-de-lotes-webp-para-paineis
public sealed partial class DeviceServerHost
{
    private const int MaxPanelsBatchesPerDevice = 4;
    private readonly Dictionary<string, SortedDictionary<ulong, PanelsBatchEnvelope>> panelsBatchStore =
        new(StringComparer.OrdinalIgnoreCase);

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

        var normalizedDeviceId = deviceId.Trim();
        var normalizedSessionId = panelsSessionId.Trim();
        var normalizedContentType = contentType.Trim();
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(payload));
        var createdAtUtc = timeProvider.GetUtcNow();

        lock (gate)
        {
            if (!panelsBatchStore.TryGetValue(normalizedDeviceId, out var batches))
            {
                batches = new SortedDictionary<ulong, PanelsBatchEnvelope>();
                panelsBatchStore[normalizedDeviceId] = batches;
            }

            foreach (var sequence in batches
                         .Where(pair => !string.Equals(pair.Value.PanelsSessionId, normalizedSessionId, StringComparison.Ordinal))
                         .Select(static pair => pair.Key)
                         .ToArray())
            {
                batches.Remove(sequence);
            }

            batches[batchSequence] = new PanelsBatchEnvelope(
                normalizedDeviceId,
                normalizedSessionId,
                batchSequence,
                payload,
                sha256,
                normalizedContentType,
                frameCount,
                durationMs,
                createdAtUtc);

            while (batches.Count > MaxPanelsBatchesPerDevice)
            {
                batches.Remove(batches.First().Key);
            }
        }

        return new PanelsBatchRegistration(
            normalizedSessionId,
            batchSequence,
            payload.LongLength,
            sha256,
            normalizedContentType,
            frameCount,
            durationMs,
            $"{GetPublicHttpBaseAddress()}/api/v1/device/panels/batches/{batchSequence}.webp?panelsSessionId={Uri.EscapeDataString(normalizedSessionId)}");
    }

    public void ClearPanelsBatches(string deviceId, string? panelsSessionId = null)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return;
        }

        var normalizedDeviceId = deviceId.Trim();
        var normalizedSessionId = string.IsNullOrWhiteSpace(panelsSessionId) ? null : panelsSessionId.Trim();

        lock (gate)
        {
            if (!panelsBatchStore.TryGetValue(normalizedDeviceId, out var batches))
            {
                return;
            }

            if (normalizedSessionId is null)
            {
                panelsBatchStore.Remove(normalizedDeviceId);
                return;
            }

            foreach (var sequence in batches
                         .Where(pair => string.Equals(pair.Value.PanelsSessionId, normalizedSessionId, StringComparison.Ordinal))
                         .Select(static pair => pair.Key)
                         .ToArray())
            {
                batches.Remove(sequence);
            }

            if (batches.Count == 0)
            {
                panelsBatchStore.Remove(normalizedDeviceId);
            }
        }
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

        PanelsBatchEnvelope? batch = null;
        lock (gate)
        {
            if (panelsBatchStore.TryGetValue(state.Record.DeviceId, out var batches)
                && batches.TryGetValue(batchSequence, out var stored)
                && string.Equals(stored.PanelsSessionId, panelsSessionId, StringComparison.Ordinal))
            {
                batch = stored;
            }
        }

        if (batch is null)
        {
            return Results.NotFound(new { error = "batch_not_found" });
        }

        state.Touch();
        state.MarkSeen(
            ctx.Connection.RemoteIpAddress?.ToString(),
            state.Record.LastKnownRssi,
            state.Record.FirmwareVersion,
            state.Record.ActiveAppId,
            state.Record.ActiveAppName,
            state.Record.BoardModel,
            state.Record.PanelType);

        return Results.File(batch.Payload, batch.ContentType, enableRangeProcessing: false);
    }

    private sealed record PanelsBatchEnvelope(
        string DeviceId,
        string PanelsSessionId,
        ulong BatchSequence,
        byte[] Payload,
        string Sha256,
        string ContentType,
        int FrameCount,
        int DurationMs,
        DateTimeOffset CreatedAtUtc);
}
