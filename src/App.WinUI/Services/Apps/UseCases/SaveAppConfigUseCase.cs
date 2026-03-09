using System.Diagnostics;
using App.WinUI.Infrastructure.Observability;
using App.WinUI.Models.Apps;
using Microsoft.Extensions.Logging;

namespace App.WinUI.Services.Apps.UseCases;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#fluxo-de-execucao
internal sealed partial class SaveAppConfigUseCase
{
    private readonly IAppModifierStateStore? modifierStore;
    private readonly ILogger<SaveAppConfigUseCase>? logger;

    public SaveAppConfigUseCase(IAppModifierStateStore? modifierStore, ILogger<SaveAppConfigUseCase>? logger = null)
    {
        this.modifierStore = modifierStore;
        this.logger = logger;
    }

    public async Task<SaveAppConfigResult> ExecuteAsync(string scope, AppCatalogItem item, IReadOnlyDictionary<string, string> rawValues, CancellationToken cancellationToken = default)
    {
        using var activity = AppObservability.StartActivity("app-config.save-draft", AppObservability.AppComponent);
        activity?.SetTag(AppObservability.AppIdKey, item.Id);
        using var scopeState = AppObservability.BeginScope(
            logger,
            activity,
            (AppObservability.ComponentKey, AppObservability.AppComponent),
            (AppObservability.MicaComponentKey, AppObservability.AppComponent),
            (AppObservability.OperationKey, "app-config.save-draft"),
            (AppObservability.AppIdKey, item.Id));

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (modifierStore is null)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "modifier-store-unavailable");
                if (logger is not null)
                {
                    LogModifierStoreUnavailable(logger);
                }

                return SaveAppConfigResult.Failure("Repositorio de modificadores indisponivel.");
            }

            var normalizedValues = global::App.WinUI.Services.Apps.WeatherAppFixedLocation.NormalizeRawValues(item, rawValues);
            await modifierStore.SetDraftAsync(
                scope,
                item.Id,
                new AppConfigDraft
                {
                    Values = new Dictionary<string, string>(normalizedValues, StringComparer.OrdinalIgnoreCase),
                },
                cancellationToken).ConfigureAwait(false);
            if (logger is not null)
            {
                LogDraftSaved(logger);
            }

            return SaveAppConfigResult.FromSuccess(normalizedValues);
        }
        catch (Exception ex)
        {
            AppObservability.SetException(activity, ex);
            if (logger is not null)
            {
                LogDraftSaveFailed(logger, ex);
            }

            throw;
        }
    }

    [LoggerMessage(EventId = 1530, Level = LogLevel.Warning, Message = "Save draft falhou porque o repositorio de modificadores esta indisponivel.")]
    private static partial void LogModifierStoreUnavailable(ILogger logger);

    [LoggerMessage(EventId = 1531, Level = LogLevel.Information, Message = "Draft de configuracao salvo com sucesso.")]
    private static partial void LogDraftSaved(ILogger logger);

    [LoggerMessage(EventId = 1532, Level = LogLevel.Warning, Message = "Save draft falhou com excecao.")]
    private static partial void LogDraftSaveFailed(ILogger logger, Exception exception);
}

internal sealed record SaveAppConfigResult(bool Success, string Message, IReadOnlyDictionary<string, string>? RawValues)
{
    public static SaveAppConfigResult Failure(string message) => new(false, message, null);

    public static SaveAppConfigResult FromSuccess(IReadOnlyDictionary<string, string> rawValues) => new(true, string.Empty, rawValues);
}
