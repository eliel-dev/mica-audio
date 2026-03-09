using System.Globalization;
using System.Text;

namespace App.WinUI.Services.Monitoring;

// DOCS: docs/wiki/modules/app-winui.md#atualizacao-2026-03-monitoramento-local-com-hwinfo64
internal static class MonitoringTextNormalization
{
    public static string NormalizeLookup(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static bool ContainsAny(string? text, params string[] terms)
    {
        var normalized = NormalizeLookup(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        foreach (var term in terms)
        {
            if (normalized.Contains(NormalizeLookup(term), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
