using System.Globalization;
using System.Text.Json;
using App.WinUI.Models.Apps;

namespace App.WinUI.Services.Apps.UseCases;

// DOCS: docs/wiki/modules/apps-catalog-deployment.md#pontos-de-alteracao-frequente
internal sealed class AppConfigValidationUseCase
{
    public bool TryBuildPayload(AppCatalogItem item, IReadOnlyDictionary<string, string> rawValues, out string configJson, out string error)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var modifier in item.Modifiers.Where(static m => m.IsValid()))
        {
            rawValues.TryGetValue(modifier.Key, out var rawValue);
            if (!TryParseRawValue(modifier, rawValue, out var typedValue, out error))
            {
                configJson = string.Empty;
                return false;
            }

            data[modifier.Key] = typedValue;
        }

        configJson = JsonSerializer.Serialize(data);
        error = string.Empty;
        return true;
    }

    private static bool TryParseRawValue(AppModifierDefinition modifier, string? rawValue, out object? typedValue, out string error)
    {
        var value = rawValue?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            typedValue = modifier.Type == AppModifierFieldType.Toggle ? false : string.Empty;
            error = string.Empty;
            return !modifier.Required;
        }

        switch (modifier.Type)
        {
            case AppModifierFieldType.Toggle:
                if (!bool.TryParse(value, out var boolValue))
                {
                    typedValue = null;
                    error = $"Valor inválido para '{modifier.Label}'.";
                    return false;
                }

                typedValue = boolValue;
                error = string.Empty;
                return true;
            case AppModifierFieldType.Number:
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberValue))
                {
                    typedValue = null;
                    error = $"Valor numérico inválido para '{modifier.Label}'.";
                    return false;
                }

                typedValue = numberValue;
                error = string.Empty;
                return true;
            default:
                typedValue = value;
                error = string.Empty;
                return true;
        }
    }
}
