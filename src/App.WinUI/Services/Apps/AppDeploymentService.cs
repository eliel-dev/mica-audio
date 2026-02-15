using App.WinUI.Models.Apps;
using App.WinUI.Services.Devices;
using Device.Protocol.Models;

namespace App.WinUI.Services.Apps;

internal sealed class AppDeploymentService
{
    private readonly DeviceOperationsCoordinator deviceOps;

    public AppDeploymentService(DeviceOperationsCoordinator deviceOps)
    {
        this.deviceOps = deviceOps;
    }

    public Task<CommandDispatchResult> InstallAsync(string deviceId, AppCatalogItem item, string? configJson = null, CancellationToken cancellationToken = default)
    {
        var payload = new DeviceAppCommandPayload
        {
            AppId = item.Id,
            PackageName = item.PackageName,
            DisplayName = item.Name,
            RecommendedIntervalMinutes = item.RecommendedIntervalMinutes,
            ConfigJson = configJson,
        };

        return deviceOps.InstallAppAsync(deviceId, payload, cancellationToken);
    }

    public Task<CommandDispatchResult> ActivateAsync(string deviceId, AppCatalogItem item, CancellationToken cancellationToken = default)
    {
        return deviceOps.ActivateAppAsync(deviceId, item.Id, item.Name, cancellationToken);
    }

    public Task<CommandDispatchResult> SetConfigAsync(string deviceId, AppCatalogItem item, string configJson, CancellationToken cancellationToken = default)
    {
        return deviceOps.SetAppConfigAsync(deviceId, item.Id, configJson, cancellationToken);
    }
}
