using System.Net;
using Device.Protocol.Models;
using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

// DOCS: docs/wiki/modules/device-server-protocol.md#modulo-deviceserver-deviceprotocol
// DOCS: docs/wiki/modules/server-build-and-artifacts.md#modulo-server-build-and-artifacts
public sealed partial class DeviceServerHost
{
    private IResult HandleDeviceFirmwareLatest(HttpContext ctx)
    {
        if (!TryAuthenticate(ctx, AuthContext.HttpApi, out var state))
        {
            return Results.Unauthorized();
        }

        MarkDeviceHttpHeartbeat(state, ctx.Connection.RemoteIpAddress?.ToString());

        if (!TryResolveOfficialFirmware(state.Record, out var package, out _))
        {
            return Results.NotFound(new { error = "firmware_not_found" });
        }

        return Results.Ok(BuildFirmwareReleaseInfo(package));
    }

    private IResult HandleDeviceFirmwareDownload(HttpContext ctx)
    {
        if (!TryAuthenticate(ctx, AuthContext.HttpApi, out var state))
        {
            return Results.Unauthorized();
        }

        MarkDeviceHttpHeartbeat(state, ctx.Connection.RemoteIpAddress?.ToString());

        if (!TryResolveOfficialFirmware(state.Record, out var package, out _))
        {
            return Results.NotFound(new { error = "firmware_not_found" });
        }

        var requestedVersion = NormalizeOptional(ctx.Request.Query["version"].ToString());
        if (!string.IsNullOrWhiteSpace(requestedVersion)
            && !string.Equals(requestedVersion, package.FirmwareVersion, StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound(new { error = "firmware_version_not_found" });
        }

        ctx.Response.Headers["X-Firmware-Version"] = package.FirmwareVersion;
        ctx.Response.Headers["X-Firmware-Sha256"] = package.Sha256;
        ctx.Response.Headers["X-Firmware-Size"] = package.FileSizeBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.File(
            package.FilePath,
            contentType: "application/octet-stream",
            fileDownloadName: Path.GetFileName(package.FilePath),
            enableRangeProcessing: false);
    }

    private bool TryResolveOfficialFirmware(DeviceSnapshot snapshot, out DeviceOfficialFirmwarePackage package, out string error)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return TryResolveOfficialFirmware(snapshot.BoardModel, snapshot.PanelType, snapshot.Profile, out package, out error);
    }

    private bool TryResolveOfficialFirmware(DeviceRecord record, out DeviceOfficialFirmwarePackage package, out string error)
    {
        ArgumentNullException.ThrowIfNull(record);
        return TryResolveOfficialFirmware(record.BoardModel, record.PanelType, record.Profile, out package, out error);
    }

    private bool TryResolveOfficialFirmware(
        string? boardModel,
        string? panelType,
        string? profile,
        out DeviceOfficialFirmwarePackage package,
        out string error)
    {
        package = default!;
        error = string.Empty;

        if (firmwareCatalog is null)
        {
            error = "firmware_catalog_unavailable";
            return false;
        }

        return firmwareCatalog.TryResolveLatest(boardModel, panelType, profile, out package, out error);
    }

    private static DeviceFirmwareReleaseInfo BuildFirmwareReleaseInfo(DeviceOfficialFirmwarePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        var encodedVersion = WebUtility.UrlEncode(package.FirmwareVersion);
        return new DeviceFirmwareReleaseInfo
        {
            FirmwareVersion = package.FirmwareVersion,
            BoardModel = package.BoardModel,
            PanelType = package.PanelType,
            Profile = package.Profile,
            ControlPlane = package.ControlPlane,
            Sha256 = package.Sha256,
            FileSizeBytes = package.FileSizeBytes,
            DownloadPath = $"/api/v1/device/firmware/download?version={encodedVersion}",
        };
    }

    private void MarkDeviceHttpHeartbeat(DeviceSession state, string? remoteIp)
    {
        ArgumentNullException.ThrowIfNull(state);

        state.MarkSeen(
            remoteIp,
            state.Record.LastKnownRssi,
            state.Record.FirmwareVersion,
            state.Record.ActiveAppId,
            state.Record.ActiveAppName,
            state.Record.BoardModel,
            state.Record.PanelType);
        NotifyDevicesChanged();
    }
}
