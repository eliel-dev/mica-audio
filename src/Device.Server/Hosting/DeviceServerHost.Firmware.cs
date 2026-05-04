using System.IO;
using Device.Protocol.Models;
using Microsoft.AspNetCore.Http;

namespace Device.Server.Hosting;

public sealed partial class DeviceServerHost
{
    private static readonly string[] SupportedFirmwareExtensions = [".bin", ".zip", ".uf2"];

    private static bool IsSupportedFirmwareExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName);
        return SupportedFirmwareExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryResolveOfficialFirmware(DeviceSnapshot snapshot, out DeviceOfficialFirmwarePackage package, out string error)
    {
        return TryResolveOfficialFirmware(snapshot.BoardModel, snapshot.PanelType, snapshot.Profile, out package, out error);
    }

    private static bool TryResolveOfficialFirmware(DeviceRecord record, out DeviceOfficialFirmwarePackage package, out string error)
    {
        return TryResolveOfficialFirmware(record.BoardModel, record.PanelType, record.Profile, out package, out error);
    }

    private static bool TryResolveOfficialFirmware(
        string? boardModel,
        string? panelType,
        string? profile,
        out DeviceOfficialFirmwarePackage package,
        out string failureReason)
    {
        package = null!;
        failureReason = "No official firmware catalog is configured.";
        return false;
    }

    private static Task<IResult> HandleFirmwareLatestAsync(HttpContext ctx)
    {
        return Task.FromResult(Results.StatusCode(StatusCodes.Status501NotImplemented));
    }

    private static Task<IResult> HandleFirmwareDownloadAsync(HttpContext ctx)
    {
        return Task.FromResult(Results.StatusCode(StatusCodes.Status501NotImplemented));
    }

    private static Task HandleFirmwareStreamAsync(HttpContext ctx)
    {
        ctx.Response.StatusCode = StatusCodes.Status501NotImplemented;
        return Task.CompletedTask;
    }
}
