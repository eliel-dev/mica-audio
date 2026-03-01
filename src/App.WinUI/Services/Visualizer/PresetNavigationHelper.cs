using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/visual-win2d.md#navegacao-de-presets-por-teclado
internal static class PresetNavigationHelper
{
    public static IReadOnlyList<PresetDefinition> BuildOrder(IEnumerable<PresetDefinition> presets)
    {
        return presets
            .OrderBy(static preset => preset.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static preset => preset.PresetId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static int ResolveIndex(IReadOnlyList<PresetDefinition> order, string? presetId)
    {
        if (order.Count == 0)
        {
            return -1;
        }

        if (!string.IsNullOrWhiteSpace(presetId))
        {
            for (var i = 0; i < order.Count; i++)
            {
                if (string.Equals(order[i].PresetId, presetId, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return 0;
    }

    public static int WrapIndex(int currentIndex, int direction, int count)
    {
        if (count <= 0)
        {
            return -1;
        }

        if (count == 1)
        {
            return 0;
        }

        var normalizedIndex = currentIndex;
        if (normalizedIndex < 0 || normalizedIndex >= count)
        {
            normalizedIndex = 0;
        }

        var candidate = normalizedIndex + Math.Sign(direction);
        if (candidate < 0)
        {
            return count - 1;
        }

        if (candidate >= count)
        {
            return 0;
        }

        return candidate;
    }
}
