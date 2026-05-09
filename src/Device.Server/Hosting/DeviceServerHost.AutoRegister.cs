using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Device.Protocol.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;

namespace Device.Server.Hosting;

// DOCS: docs/handoffs/2026-05-08-remote-only-autonomous-widgets-firmware-sta.md
// DOCS: docs/adr/0010-remote-only-and-server-side-autonomous-widgets.md
//
// Auto-registration endpoint for firmware booting in STA-hardcoded mode.
//
// Why: After removing the WiFiManager AP portal, the firmware no longer collects
// a pair code. Instead the device contacts the server on first boot with its MAC
// address; the server responds with a deterministic deviceId and a token. The
// pairing is idempotent per MAC: the same firmware reflashed many times keeps the
// same identity, and the same physical device returns to the same record on a
// factory reset.
//
// Security: the endpoint is rejected when the request does not come from a
// private LAN address (or the configured allow-list), exactly the same gate used
// by the dashboard and admin endpoints. The token is generated once per MAC and
// then persisted; re-registration with the same MAC returns the original token.
public sealed partial class DeviceServerHost
{
    private const string AutoRegisterDeviceIdPrefix = "mp-auto-";

    private async Task<IResult> HandleAutoRegisterAsync(HttpContext ctx)
    {
        var remoteIpKey = BuildRateLimitPartitionKey(ctx.Connection.RemoteIpAddress);
        if (!TryRegisterPairingAttempt(remoteIpKey, out var retryAfterSeconds))
        {
            return Results.Json(
                new { error = "auto_register_rate_limited", retryAfterSeconds },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        if (!IsRequestAllowed(runtimeConfig, ctx.Connection.RemoteIpAddress))
        {
            return Results.Json(
                new { error = "auto_register_remote_address_not_allowed" },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (IsRequestBodyTooLarge(ctx))
        {
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        AutoRegisterDeviceRequest req;
        try
        {
            req = await JsonSerializer.DeserializeAsync<AutoRegisterDeviceRequest>(
                ctx.Request.Body, JsonOptions, ctx.RequestAborted).ConfigureAwait(false)
                ?? new AutoRegisterDeviceRequest();
        }
        catch (JsonException)
        {
            return Results.BadRequest(new { error = "invalid_json" });
        }

        var normalizedMac = NormalizeMacAddress(req.MacAddress);
        if (string.IsNullOrEmpty(normalizedMac))
        {
            return Results.BadRequest(new { error = "missing_or_invalid_mac" });
        }

        var deviceId = ComputeDeterministicDeviceId(normalizedMac);

        DeviceSessionState state;
        bool reusedExisting;
        lock (gate)
        {
            if (sessionStateStore.TryGetValue(deviceId, out var existing) && existing is not null)
            {
                // Idempotent: same MAC → same deviceId/token. Just refresh metadata.
                existing.MarkSeen(
                    timeProvider.GetUtcNow(),
                    ip: ctx.Connection.RemoteIpAddress?.ToString(),
                    rssi: existing.Record.LastKnownRssi,
                    firmwareVersion: req.FirmwareVersion,
                    boardModel: NormalizeOptional(req.BoardModel),
                    panelType: NormalizeOptional(req.PanelType));
                state = existing;
                reusedExisting = true;
            }
            else
            {
                if (sessionStateStore.Count >= runtimeConfig.MaxDevices)
                {
                    return Results.BadRequest(new { error = "max_devices_reached" });
                }

                var now = timeProvider.GetUtcNow();
                var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
                var record = DeviceRecordMutations.CreatePairedRecord(
                    deviceId: deviceId,
                    token: token,
                    name: string.IsNullOrWhiteSpace(req.DeviceName) ? ResolveDefaultDeviceName(req.BoardModel) : req.DeviceName.Trim(),
                    profile: NormalizeFirmwareProfile(req.Profile),
                    firmwareVersion: req.FirmwareVersion,
                    ip: ctx.Connection.RemoteIpAddress?.ToString(),
                    boardModel: NormalizeOptional(req.BoardModel),
                    panelType: NormalizeOptional(req.PanelType),
                    now: now);

                state = new DeviceSessionState(record, SocketDetachGracePeriod);
                var replaced = sessionStateStore.Upsert(state);
                if (replaced is not null)
                {
                    frameConnections.RemoveAndDispose(record.DeviceId);
                }

                reusedExisting = false;
            }

            pairingStore.ResetAttempts(remoteIpKey);
        }

        NotifyDevicesChanged();
        Log(reusedExisting
            ? $"Device auto-registered (existing): {state.Record.DeviceId}"
            : $"Device auto-registered (new): {state.Record.DeviceId}");

        return Results.Ok(new AutoRegisterDeviceResponse
        {
            DeviceId = state.Record.DeviceId,
            Token = state.Record.Token,
            WsPath = "/ws/v1/stream",
            HttpBase = ResolveAdvertisedHttpBaseAddress(ctx),
            MqttHost = ResolveAdvertisedMqttHost(ctx),
            MqttPort = runtimeConfig.MqttPort,
            MqttRootTopic = runtimeConfig.MqttRootTopic,
            MdnsService = runtimeConfig.MdnsServiceName,
            ReusedExisting = reusedExisting,
        });
    }

    /// <summary>
    /// Lowercases and strips separators so "AA:BB:CC:DD:EE:FF" and "aabbccddeeff"
    /// produce the same normalized form. Returns empty when the input has fewer
    /// than 12 hex characters or any non-hex character.
    /// </summary>
    internal static string NormalizeMacAddress(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var buffer = new StringBuilder(12);
        foreach (var ch in raw)
        {
            if (ch is ':' or '-' or '.' or ' ')
            {
                continue;
            }

            if (!Uri.IsHexDigit(ch))
            {
                return string.Empty;
            }

            buffer.Append(char.ToLowerInvariant(ch));
            if (buffer.Length > 12)
            {
                return string.Empty;
            }
        }

        return buffer.Length == 12 ? buffer.ToString() : string.Empty;
    }

    /// <summary>
    /// SHA-256 of the lowercased MAC, base32hex first 12 chars. Stable per MAC,
    /// not reversible on its own (enough entropy that scanning the device list
    /// does not leak MACs).
    /// </summary>
    internal static string ComputeDeterministicDeviceId(string normalizedMac)
    {
        ArgumentException.ThrowIfNullOrEmpty(normalizedMac);

        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(normalizedMac));
        // Take first 8 bytes → 16 hex chars. Combined with prefix yields 24 chars.
        var sb = new StringBuilder(AutoRegisterDeviceIdPrefix.Length + 16);
        sb.Append(AutoRegisterDeviceIdPrefix);
        for (var i = 0; i < 8; i++)
        {
            sb.Append(hash[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }
}
