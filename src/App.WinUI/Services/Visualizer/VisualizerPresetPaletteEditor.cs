using MicaAudio.Core.Presets;

namespace App.WinUI.Services.Visualizer;

// DOCS: docs/wiki/modules/settings-presets-persistence.md#studio-de-presets
internal static class VisualizerPresetPaletteEditor
{
    internal const int MinimumStopCount = 2;
    internal const string EditedSuffix = " (editado)";
    internal const string CopySuffix = " (copia)";

    private static readonly IReadOnlyList<PresetDefinition> BuiltInPresets = DefaultPresets.Create();
    private static readonly Dictionary<string, string> BuiltInPresetNamesById =
        BuiltInPresets.ToDictionary(static preset => preset.PresetId, static preset => preset.Name, StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> BuiltInPresetIds =
        BuiltInPresets
            .Select(static preset => preset.PresetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<GradientPaletteTemplate> QuickGradientPresets = BuildQuickGradientPresets();

    internal static IReadOnlyList<GradientPaletteTemplate> GetQuickGradientPresets() => QuickGradientPresets;

    internal static bool IsBuiltInPreset(PresetDefinition? preset)
        => preset is not null
            && !string.IsNullOrWhiteSpace(preset.PresetId)
            && BuiltInPresetIds.Contains(preset.PresetId);

    internal static bool IsBuiltInPresetId(string? presetId)
        => !string.IsNullOrWhiteSpace(presetId) && BuiltInPresetIds.Contains(presetId);

    internal static bool TryGetEditableSourcePresetId(string? presetId, out string sourcePresetId)
    {
        sourcePresetId = string.Empty;
        if (string.IsNullOrWhiteSpace(presetId) || !presetId.StartsWith("user-", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var candidate = presetId["user-".Length..];
        if (!IsBuiltInPresetId(candidate))
        {
            return false;
        }

        sourcePresetId = candidate;
        return true;
    }

    internal static string ResolveDefaultBuiltInName(string presetId)
    {
        if (BuiltInPresetNamesById.TryGetValue(presetId, out var resolved))
        {
            return resolved;
        }

        return presetId;
    }

    internal static string ResolveDisplayName(
        PresetDefinition preset,
        IReadOnlyDictionary<string, string>? builtInNameOverrides)
    {
        ArgumentNullException.ThrowIfNull(preset);

        if (IsBuiltInPreset(preset))
        {
            if (builtInNameOverrides is not null
                && builtInNameOverrides.TryGetValue(preset.PresetId, out var overrideName)
                && !string.IsNullOrWhiteSpace(overrideName))
            {
                return overrideName.Trim();
            }

            return preset.Name;
        }

        if (TryGetEditableSourcePresetId(preset.PresetId, out var sourcePresetId)
            && LooksLikeGeneratedEditedName(preset.Name))
        {
            var baseName = builtInNameOverrides is not null
                && builtInNameOverrides.TryGetValue(sourcePresetId, out var overrideName)
                && !string.IsNullOrWhiteSpace(overrideName)
                ? overrideName.Trim()
                : ResolveDefaultBuiltInName(sourcePresetId);
            return $"{baseName}{EditedSuffix}";
        }

        return preset.Name;
    }

    internal static PresetDefinition ApplyDisplayName(PresetDefinition preset, string displayName)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return new PresetDefinition
        {
            SchemaVersion = preset.SchemaVersion,
            PresetId = preset.PresetId,
            Name = string.IsNullOrWhiteSpace(displayName) ? preset.Name : displayName.Trim(),
            RendererId = preset.RendererId,
            DisplayBandCount = preset.DisplayBandCount,
            ScaleMode = preset.ScaleMode,
            FpsTarget = preset.FpsTarget,
            Layout = preset.Layout,
            EnableGlow = preset.EnableGlow,
            RendererParameters = CloneRendererParameters(preset.RendererParameters),
            Palette = CreatePalette(preset.Palette?.Name, preset.Palette?.Stops),
        };
    }

    internal static IReadOnlyList<PresetDefinition> ApplyDisplayNames(
        IEnumerable<PresetDefinition> presets,
        IReadOnlyDictionary<string, string>? builtInNameOverrides)
    {
        ArgumentNullException.ThrowIfNull(presets);

        return presets
            .Select(preset => ApplyDisplayName(preset, ResolveDisplayName(preset, builtInNameOverrides)))
            .ToArray();
    }

    internal static string ResolveEditablePresetId(PresetDefinition preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return IsBuiltInPreset(preset)
            ? $"user-{preset.PresetId}"
            : preset.PresetId;
    }

    internal static string ResolveEditablePresetName(PresetDefinition preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return IsBuiltInPreset(preset)
            ? $"{preset.Name}{EditedSuffix}"
            : preset.Name;
    }

    internal static string ResolveEditablePresetNameFromDisplayName(string displayName)
        => string.IsNullOrWhiteSpace(displayName)
            ? $"Visual{EditedSuffix}"
            : $"{displayName.Trim()}{EditedSuffix}";

    internal static PresetDefinition ResolveEditablePreset(
        PresetDefinition selectedPreset,
        IReadOnlyDictionary<string, PresetDefinition> presetsById)
    {
        ArgumentNullException.ThrowIfNull(selectedPreset);
        ArgumentNullException.ThrowIfNull(presetsById);

        if (!IsBuiltInPreset(selectedPreset))
        {
            return ClonePreset(selectedPreset);
        }

        var editablePresetId = ResolveEditablePresetId(selectedPreset);
        if (presetsById.TryGetValue(editablePresetId, out var existingPreset))
        {
            return ClonePreset(existingPreset);
        }

        return CreateEditableCopy(selectedPreset);
    }

    internal static PresetDefinition CreateEditableCopy(PresetDefinition sourcePreset)
    {
        ArgumentNullException.ThrowIfNull(sourcePreset);

        return new PresetDefinition
        {
            SchemaVersion = sourcePreset.SchemaVersion,
            PresetId = ResolveEditablePresetId(sourcePreset),
            Name = ResolveEditablePresetName(sourcePreset),
            RendererId = sourcePreset.RendererId,
            DisplayBandCount = sourcePreset.DisplayBandCount,
            ScaleMode = sourcePreset.ScaleMode,
            FpsTarget = sourcePreset.FpsTarget,
            Layout = sourcePreset.Layout,
            EnableGlow = sourcePreset.EnableGlow,
            RendererParameters = CloneRendererParameters(sourcePreset.RendererParameters),
            Palette = CreatePalette(
                string.IsNullOrWhiteSpace(sourcePreset.Palette?.Name) ? sourcePreset.Name : sourcePreset.Palette.Name,
                sourcePreset.Palette?.Stops),
        };
    }

    internal static PresetDefinition ApplyPalette(
        PresetDefinition targetPreset,
        string? paletteName,
        IReadOnlyList<PaletteStop> stops)
    {
        ArgumentNullException.ThrowIfNull(targetPreset);
        ArgumentNullException.ThrowIfNull(stops);

        return new PresetDefinition
        {
            SchemaVersion = targetPreset.SchemaVersion,
            PresetId = targetPreset.PresetId,
            Name = targetPreset.Name,
            RendererId = targetPreset.RendererId,
            DisplayBandCount = targetPreset.DisplayBandCount,
            ScaleMode = targetPreset.ScaleMode,
            FpsTarget = targetPreset.FpsTarget,
            Layout = targetPreset.Layout,
            EnableGlow = targetPreset.EnableGlow,
            RendererParameters = CloneRendererParameters(targetPreset.RendererParameters),
            Palette = CreatePalette(
                string.IsNullOrWhiteSpace(paletteName) ? targetPreset.Palette?.Name : paletteName,
                stops),
        };
    }

    internal static PresetDefinition CreateClonedPreset(
        PresetDefinition sourcePreset,
        IEnumerable<PresetDefinition> existingPresets,
        IReadOnlyDictionary<string, string>? builtInNameOverrides)
    {
        ArgumentNullException.ThrowIfNull(sourcePreset);
        ArgumentNullException.ThrowIfNull(existingPresets);

        var existing = existingPresets.ToArray();
        var existingIds = existing
            .Where(static preset => !string.IsNullOrWhiteSpace(preset.PresetId))
            .Select(static preset => preset.PresetId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existingNames = existing
            .Select(preset => ResolveDisplayName(preset, builtInNameOverrides))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sourceDisplayName = ResolveDisplayName(sourcePreset, builtInNameOverrides);
        var cloneName = BuildUniqueCloneName(sourceDisplayName, existingNames);
        var cloneId = BuildUniqueClonePresetId(sourcePreset.PresetId, existingIds);

        var clonedPreset = ClonePreset(sourcePreset);
        return new PresetDefinition
        {
            SchemaVersion = clonedPreset.SchemaVersion,
            PresetId = cloneId,
            Name = cloneName,
            RendererId = clonedPreset.RendererId,
            DisplayBandCount = clonedPreset.DisplayBandCount,
            ScaleMode = clonedPreset.ScaleMode,
            FpsTarget = clonedPreset.FpsTarget,
            Layout = clonedPreset.Layout,
            EnableGlow = clonedPreset.EnableGlow,
            RendererParameters = CloneRendererParameters(clonedPreset.RendererParameters),
            Palette = CreatePalette(clonedPreset.Palette?.Name, clonedPreset.Palette?.Stops),
        };
    }

    internal static IReadOnlyList<PaletteStop> ReplaceStops(
        IReadOnlyList<PaletteStop> existingStops,
        IReadOnlyList<PaletteStop> replacementStops)
    {
        ArgumentNullException.ThrowIfNull(existingStops);
        ArgumentNullException.ThrowIfNull(replacementStops);

        return NormalizeStops(replacementStops);
    }

    internal static (IReadOnlyList<PaletteStop> Stops, int SelectedIndex) AddStop(
        IReadOnlyList<PaletteStop> existingStops,
        float offset)
    {
        ArgumentNullException.ThrowIfNull(existingStops);

        var normalizedExisting = NormalizeStops(existingStops);
        var clampedOffset = Clamp01(offset);
        var addedStop = new PaletteStop
        {
            Offset = clampedOffset,
            Color = SampleColorAt(normalizedExisting, clampedOffset),
        };

        var mergedStops = NormalizeStops(normalizedExisting.Concat([addedStop]).ToArray());
        var selectedIndex = ResolveStopIndex(mergedStops, addedStop);
        return (mergedStops, selectedIndex);
    }

    internal static IReadOnlyList<PaletteStop> UpdateStopColor(
        IReadOnlyList<PaletteStop> existingStops,
        int selectedIndex,
        RgbaColor color)
    {
        ArgumentNullException.ThrowIfNull(existingStops);

        var normalized = NormalizeStops(existingStops).ToArray();
        if (selectedIndex < 0 || selectedIndex >= normalized.Length)
        {
            return normalized;
        }

        normalized[selectedIndex] = new PaletteStop
        {
            Offset = normalized[selectedIndex].Offset,
            Color = color,
        };
        return normalized;
    }

    internal static (IReadOnlyList<PaletteStop> Stops, int SelectedIndex) UpdateStopOffset(
        IReadOnlyList<PaletteStop> existingStops,
        int selectedIndex,
        float offset)
    {
        ArgumentNullException.ThrowIfNull(existingStops);

        var normalized = NormalizeStops(existingStops).ToArray();
        if (selectedIndex < 0 || selectedIndex >= normalized.Length)
        {
            return (normalized, Math.Clamp(selectedIndex, 0, Math.Max(0, normalized.Length - 1)));
        }

        var movedStop = new PaletteStop
        {
            Offset = Clamp01(offset),
            Color = normalized[selectedIndex].Color,
        };
        normalized[selectedIndex] = movedStop;

        var reorderedStops = NormalizeStops(normalized);
        var resolvedIndex = ResolveStopIndex(reorderedStops, movedStop);
        return (reorderedStops, resolvedIndex);
    }

    internal static (IReadOnlyList<PaletteStop> Stops, int SelectedIndex) RemoveStop(
        IReadOnlyList<PaletteStop> existingStops,
        int selectedIndex)
    {
        ArgumentNullException.ThrowIfNull(existingStops);

        var normalized = NormalizeStops(existingStops).ToArray();
        if (normalized.Length <= MinimumStopCount || selectedIndex < 0 || selectedIndex >= normalized.Length)
        {
            return (normalized, Math.Clamp(selectedIndex, 0, Math.Max(0, normalized.Length - 1)));
        }

        var reducedStops = normalized.Where((_, index) => index != selectedIndex).ToArray();
        return (reducedStops, Math.Clamp(selectedIndex - 1, 0, reducedStops.Length - 1));
    }

    internal static IReadOnlyList<PaletteStop> NormalizeStops(IEnumerable<PaletteStop> stops)
    {
        ArgumentNullException.ThrowIfNull(stops);

        var orderedStops = stops
            .Select(static stop => new PaletteStop
            {
                Offset = Clamp01(stop.Offset),
                Color = stop.Color,
            })
            .OrderBy(static stop => stop.Offset)
            .ToList();

        if (orderedStops.Count == 0)
        {
            orderedStops.Add(new PaletteStop { Offset = 0f, Color = new RgbaColor(255, 255, 255) });
            orderedStops.Add(new PaletteStop { Offset = 1f, Color = new RgbaColor(255, 255, 255) });
            return orderedStops;
        }

        if (orderedStops.Count == 1)
        {
            var single = orderedStops[0];
            var mirroredOffset = single.Offset <= 0.5f ? 1f : 0f;
            orderedStops.Add(new PaletteStop
            {
                Offset = mirroredOffset,
                Color = single.Color,
            });
            orderedStops = orderedStops
                .OrderBy(static stop => stop.Offset)
                .ToList();
        }

        orderedStops[0] = new PaletteStop
        {
            Offset = 0f,
            Color = orderedStops[0].Color,
        };
        orderedStops[^1] = new PaletteStop
        {
            Offset = 1f,
            Color = orderedStops[^1].Color,
        };

        return orderedStops;
    }

    private static GradientPalette CreatePalette(string? paletteName, IReadOnlyList<PaletteStop>? stops)
    {
        var normalizedStops = NormalizeStops(stops ?? Array.Empty<PaletteStop>());
        return new GradientPalette
        {
            Name = string.IsNullOrWhiteSpace(paletteName) ? "Gradiente personalizado" : paletteName,
            Stops = normalizedStops,
        };
    }

    private static Dictionary<string, float> CloneRendererParameters(IReadOnlyDictionary<string, float>? rendererParameters)
    {
        return rendererParameters is { Count: > 0 }
            ? new Dictionary<string, float>(rendererParameters, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
    }

    private static PresetDefinition ClonePreset(PresetDefinition preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        return new PresetDefinition
        {
            SchemaVersion = preset.SchemaVersion,
            PresetId = preset.PresetId,
            Name = preset.Name,
            RendererId = preset.RendererId,
            DisplayBandCount = preset.DisplayBandCount,
            ScaleMode = preset.ScaleMode,
            FpsTarget = preset.FpsTarget,
            Layout = preset.Layout,
            EnableGlow = preset.EnableGlow,
            RendererParameters = CloneRendererParameters(preset.RendererParameters),
            Palette = CreatePalette(preset.Palette?.Name, preset.Palette?.Stops),
        };
    }

    private static string BuildUniqueCloneName(string sourceName, HashSet<string> existingNames)
    {
        var baseName = string.IsNullOrWhiteSpace(sourceName) ? "Visual" : sourceName.Trim();
        var candidate = $"{baseName}{CopySuffix}";
        if (!existingNames.Contains(candidate))
        {
            return candidate;
        }

        for (var index = 2; index < 1000; index++)
        {
            candidate = $"{baseName}{CopySuffix} {index}";
            if (!existingNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{baseName}{CopySuffix} {Guid.NewGuid():N}";
    }

    private static string BuildUniqueClonePresetId(string sourcePresetId, HashSet<string> existingIds)
    {
        var baseId = string.IsNullOrWhiteSpace(sourcePresetId)
            ? "custom-visual"
            : SanitizePresetId($"custom-{sourcePresetId}");
        if (!existingIds.Contains(baseId))
        {
            return baseId;
        }

        for (var index = 2; index < 1000; index++)
        {
            var candidate = $"{baseId}-{index}";
            if (!existingIds.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{baseId}-{Guid.NewGuid():N}";
    }

    private static string SanitizePresetId(string presetId)
    {
        var chars = presetId
            .Trim()
            .ToLowerInvariant()
            .Select(static character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var sanitized = new string(chars);
        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        return sanitized.Trim('-');
    }

    private static bool LooksLikeGeneratedEditedName(string? presetName)
        => string.IsNullOrWhiteSpace(presetName)
            || presetName.Trim().EndsWith(EditedSuffix, StringComparison.OrdinalIgnoreCase);

    private static RgbaColor SampleColorAt(IReadOnlyList<PaletteStop> normalizedStops, float offset)
    {
        if (normalizedStops.Count == 0)
        {
            return new RgbaColor(255, 255, 255);
        }

        if (offset <= normalizedStops[0].Offset)
        {
            return normalizedStops[0].Color;
        }

        for (var index = 1; index < normalizedStops.Count; index++)
        {
            var previous = normalizedStops[index - 1];
            var next = normalizedStops[index];
            if (offset > next.Offset)
            {
                continue;
            }

            var range = Math.Max(0.0001f, next.Offset - previous.Offset);
            var t = Clamp01((offset - previous.Offset) / range);
            return Lerp(previous.Color, next.Color, t);
        }

        return normalizedStops[^1].Color;
    }

    private static int ResolveStopIndex(IReadOnlyList<PaletteStop> stops, PaletteStop targetStop)
    {
        for (var index = 0; index < stops.Count; index++)
        {
            if (stops[index].Equals(targetStop))
            {
                return index;
            }
        }

        var bestIndex = 0;
        var bestDistance = float.MaxValue;

        for (var index = 0; index < stops.Count; index++)
        {
            var candidate = stops[index];
            var distance = Math.Abs(candidate.Offset - targetStop.Offset);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    private static RgbaColor Lerp(RgbaColor left, RgbaColor right, float t)
    {
        static byte LerpChannel(byte a, byte b, float blend)
            => (byte)Math.Clamp((int)Math.Round(a + ((b - a) * blend)), 0, 255);

        return new RgbaColor(
            LerpChannel(left.R, right.R, t),
            LerpChannel(left.G, right.G, t),
            LerpChannel(left.B, right.B, t),
            LerpChannel(left.A, right.A, t));
    }

    private static IReadOnlyList<GradientPaletteTemplate> BuildQuickGradientPresets()
    {
        var defaults = DefaultPresets
            .Create()
            .ToDictionary(static preset => preset.PresetId, StringComparer.OrdinalIgnoreCase);

        return
        [
            CreateQuickPreset(defaults, "audiomotion-clone", "rainbow", "Arco-íris"),
            CreateQuickPreset(defaults, "audiomotion-sunset", "sunset", "Sunset"),
            CreateQuickPreset(defaults, "audiomotion-arctic", "arctic", "Arctic"),
            CreateQuickPreset(defaults, "audiomotion-neon", "neon", "Neon"),
            new GradientPaletteTemplate(
                "mono",
                "Mono",
                NormalizeStops(
                [
                    new PaletteStop { Offset = 0f, Color = new RgbaColor(230, 236, 255) },
                    new PaletteStop { Offset = 1f, Color = new RgbaColor(255, 255, 255) },
                ])),
        ];
    }

    private static GradientPaletteTemplate CreateQuickPreset(
        Dictionary<string, PresetDefinition> defaults,
        string presetId,
        string id,
        string label)
    {
        if (!defaults.TryGetValue(presetId, out var preset))
        {
            throw new InvalidOperationException($"Preset padrão '{presetId}' não encontrado para o Studio.");
        }

        return new GradientPaletteTemplate(
            id,
            label,
            NormalizeStops(preset.Palette.Stops));
    }
}

internal sealed record GradientPaletteTemplate(
    string Id,
    string Label,
    IReadOnlyList<PaletteStop> Stops);
