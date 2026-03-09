using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Apps.UseCases;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#fluxo-de-execucao
internal sealed class SaveAppConfigUseCase
{
    private readonly IAppModifierStateStore? modifierStore;

    public SaveAppConfigUseCase(IAppModifierStateStore? modifierStore)
    {
        this.modifierStore = modifierStore;
    }

    public async Task<SaveAppConfigResult> ExecuteAsync(string scope, AppCatalogItem item, IReadOnlyDictionary<string, string> rawValues, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (modifierStore is null)
        {
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
        return SaveAppConfigResult.FromSuccess(normalizedValues);
    }
}

internal sealed record SaveAppConfigResult(bool Success, string Message, IReadOnlyDictionary<string, string>? RawValues)
{
    public static SaveAppConfigResult Failure(string message) => new(false, message, null);

    public static SaveAppConfigResult FromSuccess(IReadOnlyDictionary<string, string> rawValues) => new(true, string.Empty, rawValues);
}



