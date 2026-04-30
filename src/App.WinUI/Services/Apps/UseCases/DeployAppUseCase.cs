using System.Diagnostics;
using App.WinUI.Infrastructure.Observability;
using App.WinUI.Models.Apps;
using Device.Protocol.Models;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Services.Apps.UseCases;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#fluxo-de-execucao
internal sealed partial class DeployAppUseCase
{
    private readonly IAppDeploymentService? deploymentService;
    private readonly SaveAppConfigUseCase saveAppConfigUseCase;
    private readonly AppConfigValidationUseCase validationUseCase;
    private readonly ILogger<DeployAppUseCase>? logger;

    public DeployAppUseCase(
        IAppDeploymentService? deploymentService,
        SaveAppConfigUseCase saveAppConfigUseCase,
        AppConfigValidationUseCase validationUseCase,
        ILogger<DeployAppUseCase>? logger = null)
    {
        this.deploymentService = deploymentService;
        this.saveAppConfigUseCase = saveAppConfigUseCase;
        this.validationUseCase = validationUseCase;
        this.logger = logger;
    }

    public async Task<DeployAppResult> ExecuteAsync(
        string scope,
        string deviceId,
        AppCatalogItem item,
        IReadOnlyDictionary<string, string> rawValues,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using var activity = AppObservability.StartActivity("app.deploy.install", AppObservability.AppComponent);
        activity?.SetTag(AppObservability.DeviceIdKey, deviceId);
        activity?.SetTag(AppObservability.AppIdKey, item.Id);
        using var scopeState = AppObservability.BeginScope(
            logger,
            activity,
            (AppObservability.ComponentKey, AppObservability.AppComponent),
            (AppObservability.MicaComponentKey, AppObservability.AppComponent),
            (AppObservability.OperationKey, "app.deploy.install"),
            (AppObservability.DeviceIdKey, deviceId),
            (AppObservability.AppIdKey, item.Id));

        try
        {
            if (!validationUseCase.TryBuildPayload(item, rawValues, out var configJson, out var validationError))
            {
                activity?.SetStatus(ActivityStatusCode.Error, validationError);
                if (logger is not null)
                {
                    LogDeployValidationFailed(logger);
                }

                AppObservability.RecordDeployDuration(stopwatch.Elapsed, deviceId, item.Id, success: false);
                return DeployAppResult.Failure(validationError);
            }

            var saveResult = await saveAppConfigUseCase.ExecuteAsync(scope, item, rawValues, cancellationToken).ConfigureAwait(false);
            if (!saveResult.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, saveResult.Message);
                if (logger is not null)
                {
                    LogDeploySaveFailed(logger);
                }

                AppObservability.RecordDeployDuration(stopwatch.Elapsed, deviceId, item.Id, success: false);
                return DeployAppResult.Failure(saveResult.Message);
            }

            if (deploymentService is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "deployment-service-unavailable");
                if (logger is not null)
                {
                    LogDeployServiceUnavailable(logger);
                }

                AppObservability.RecordDeployDuration(stopwatch.Elapsed, deviceId, item.Id, success: false);
                return DeployAppResult.Failure("Servico de deploy indisponivel.");
            }

            var commandResult = await deploymentService.InstallAsync(deviceId, item, configJson, cancellationToken).ConfigureAwait(false);
            activity?.SetTag(AppObservability.CommandIdKey, commandResult.CommandId);
            if (!commandResult.Success)
            {
                activity?.SetStatus(ActivityStatusCode.Error, commandResult.ErrorCode ?? commandResult.Stage);
            }

            AppObservability.RecordDeployDuration(stopwatch.Elapsed, deviceId, item.Id, commandResult.Success);
            if (logger is not null)
            {
                LogDeployCompleted(logger, commandResult.Stage ?? "unknown");
            }

            return DeployAppResult.FromSuccess(commandResult, saveResult.RawValues ?? rawValues);
        }
        catch (Exception ex)
        {
            AppObservability.SetException(activity, ex);
            AppObservability.RecordDeployDuration(stopwatch.Elapsed, deviceId, item.Id, success: false);
            if (logger is not null)
            {
                LogDeployFailed(logger, ex);
            }

            throw;
        }
    }

    [LoggerMessage(EventId = 1540, Level = LogLevel.Warning, Message = "Deploy do app falhou na validacao de payload.")]
    private static partial void LogDeployValidationFailed(ILogger logger);

    [LoggerMessage(EventId = 1541, Level = LogLevel.Warning, Message = "Deploy do app falhou ao salvar configuracao local.")]
    private static partial void LogDeploySaveFailed(ILogger logger);

    [LoggerMessage(EventId = 1542, Level = LogLevel.Warning, Message = "Deploy do app falhou porque o servico de deploy esta indisponivel.")]
    private static partial void LogDeployServiceUnavailable(ILogger logger);

    [LoggerMessage(EventId = 1543, Level = LogLevel.Information, Message = "Deploy do app concluiu com status {Stage}.")]
    private static partial void LogDeployCompleted(ILogger logger, string stage);

    [LoggerMessage(EventId = 1544, Level = LogLevel.Warning, Message = "Deploy do app falhou com excecao.")]
    private static partial void LogDeployFailed(ILogger logger, Exception exception);
}

internal sealed record DeployAppResult(bool Success, string Message, CommandDispatchResult? CommandResult, IReadOnlyDictionary<string, string>? RawValues)
{
    public static DeployAppResult Failure(string message) => new(false, message, null, null);

    public static DeployAppResult FromSuccess(CommandDispatchResult commandResult, IReadOnlyDictionary<string, string> rawValues) => new(true, string.Empty, commandResult, rawValues);
}
