using App.WinUI.Models.Apps;
using Device.Protocol.Models;

namespace App.WinUI.Services.Apps.UseCases;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#fluxo-de-execucao
internal sealed class DeployAppUseCase
{
    private readonly IAppDeploymentService? deploymentService;
    private readonly SaveAppConfigUseCase saveAppConfigUseCase;
    private readonly AppConfigValidationUseCase validationUseCase;

    public DeployAppUseCase(IAppDeploymentService? deploymentService, SaveAppConfigUseCase saveAppConfigUseCase, AppConfigValidationUseCase validationUseCase)
    {
        this.deploymentService = deploymentService;
        this.saveAppConfigUseCase = saveAppConfigUseCase;
        this.validationUseCase = validationUseCase;
    }

    public async Task<DeployAppResult> ExecuteAsync(string scope, string deviceId, AppCatalogItem item, IReadOnlyDictionary<string, string> rawValues, CancellationToken cancellationToken = default)
    {
        if (!validationUseCase.TryBuildPayload(item, rawValues, out var configJson, out var validationError))
        {
            return DeployAppResult.Failure(validationError);
        }

        var saveResult = await saveAppConfigUseCase.ExecuteAsync(scope, item, rawValues, cancellationToken).ConfigureAwait(false);
        if (!saveResult.Success)
        {
            return DeployAppResult.Failure(saveResult.Message);
        }

        if (deploymentService is null)
        {
            return DeployAppResult.Failure("Servico de deploy indisponivel.");
        }

        var commandResult = await deploymentService.InstallAsync(deviceId, item, configJson, cancellationToken).ConfigureAwait(false);
        return DeployAppResult.FromSuccess(commandResult, saveResult.RawValues ?? rawValues);
    }
}

internal sealed record DeployAppResult(bool Success, string Message, CommandDispatchResult? CommandResult, IReadOnlyDictionary<string, string>? RawValues)
{
    public static DeployAppResult Failure(string message) => new(false, message, null, null);

    public static DeployAppResult FromSuccess(CommandDispatchResult commandResult, IReadOnlyDictionary<string, string> rawValues) => new(true, string.Empty, commandResult, rawValues);
}



