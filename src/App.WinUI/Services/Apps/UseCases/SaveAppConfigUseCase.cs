using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Apps.UseCases;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#fluxo-de-execucao
internal sealed class SaveAppConfigUseCase
{
    private readonly AppModifierStateStore? modifierStore;

    public SaveAppConfigUseCase(AppModifierStateStore? modifierStore)
    {
        this.modifierStore = modifierStore;
    }

    public async Task<SaveAppConfigResult> ExecuteAsync(string scope, AppCatalogItem item, IReadOnlyDictionary<string, string> rawValues, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (modifierStore is null)
        {
            return SaveAppConfigResult.Failure("Repositório de modificadores indisponível.");
        }

        await modifierStore.SetDraftAsync(scope, item.Id, new AppConfigDraft { Values = new Dictionary<string, string>(rawValues, StringComparer.OrdinalIgnoreCase) }).ConfigureAwait(false);
        return SaveAppConfigResult.Success(rawValues);
    }
}

internal sealed record SaveAppConfigResult(bool Success, string Message, IReadOnlyDictionary<string, string>? RawValues)
{
    public static SaveAppConfigResult Failure(string message) => new(false, message, null);

    public static SaveAppConfigResult Success(IReadOnlyDictionary<string, string> rawValues) => new(true, string.Empty, rawValues);
}
