using System.Text;

namespace App.WinUI.Services.Devices;

// DOCS: docs/wiki/reference/code-index.md#referencia-code-index
internal static class DeviceListRenderDiff
{
    public static string BuildSignature(IEnumerable<string> orderedTokens)
    {
        ArgumentNullException.ThrowIfNull(orderedTokens);

        var builder = new StringBuilder();
        foreach (var token in orderedTokens)
        {
            builder.Append(token);
            builder.Append('\u001f');
        }

        return builder.ToString();
    }

    public static bool HasSameOrder(IReadOnlyList<string> currentIds, IReadOnlyList<string> nextIds)
    {
        ArgumentNullException.ThrowIfNull(currentIds);
        ArgumentNullException.ThrowIfNull(nextIds);

        if (currentIds.Count != nextIds.Count)
        {
            return false;
        }

        for (var i = 0; i < currentIds.Count; i++)
        {
            if (!string.Equals(currentIds[i], nextIds[i], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
