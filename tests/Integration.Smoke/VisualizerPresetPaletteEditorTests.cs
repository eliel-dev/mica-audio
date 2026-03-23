using App.WinUI.Services;
using App.WinUI.Services.Visualizer;
using Microsoft.Extensions.Options;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace Integration.Smoke;

public sealed class VisualizerPresetPaletteEditorTests
{
    [Fact]
    public void ResolveEditablePreset_ShouldCreateStableUserCopy_ForBuiltInPreset()
    {
        var presets = DefaultPresets.Create()
            .ToDictionary(static preset => preset.PresetId, StringComparer.OrdinalIgnoreCase);
        var sourcePreset = presets["audiomotion-sunset"];

        var editablePreset = VisualizerPresetPaletteEditor.ResolveEditablePreset(sourcePreset, presets);

        Assert.Equal("user-audiomotion-sunset", editablePreset.PresetId);
        Assert.Equal("AudioMotion Sunset (editado)", editablePreset.Name);
        Assert.Equal(sourcePreset.RendererId, editablePreset.RendererId);
        Assert.NotSame(sourcePreset.Palette, editablePreset.Palette);
        Assert.Equal(sourcePreset.Palette.Stops.Count, editablePreset.Palette.Stops.Count);
    }

    [Fact]
    public void ResolveEditablePreset_ShouldReuseExistingEditableCopy_WhenBuiltInWasAlreadyCustomized()
    {
        var presets = DefaultPresets.Create()
            .ToDictionary(static preset => preset.PresetId, StringComparer.OrdinalIgnoreCase);
        var sourcePreset = presets["audiomotion-arctic"];
        var existingCopy = new PresetDefinition
        {
            SchemaVersion = sourcePreset.SchemaVersion,
            PresetId = "user-audiomotion-arctic",
            Name = "AudioMotion Arctic (editado)",
            RendererId = sourcePreset.RendererId,
            DisplayBandCount = sourcePreset.DisplayBandCount,
            ScaleMode = sourcePreset.ScaleMode,
            FpsTarget = sourcePreset.FpsTarget,
            Layout = sourcePreset.Layout,
            EnableGlow = sourcePreset.EnableGlow,
            RendererParameters = new Dictionary<string, float>(sourcePreset.RendererParameters, StringComparer.OrdinalIgnoreCase),
            Palette = new GradientPalette
            {
                Name = "Meu Arctic",
                Stops =
                [
                    new PaletteStop { Offset = 0f, Color = new RgbaColor(255, 255, 255) },
                    new PaletteStop { Offset = 1f, Color = new RgbaColor(0, 128, 255) },
                ],
            },
        };
        presets[existingCopy.PresetId] = existingCopy;

        var editablePreset = VisualizerPresetPaletteEditor.ResolveEditablePreset(sourcePreset, presets);

        Assert.Equal(existingCopy.PresetId, editablePreset.PresetId);
        Assert.Equal(existingCopy.Name, editablePreset.Name);
        Assert.Equal("Meu Arctic", editablePreset.Palette.Name);
        Assert.Equal(existingCopy.Palette.Stops[1].Color, editablePreset.Palette.Stops[1].Color);
    }

    [Fact]
    public void ApplyPalette_ShouldKeepCustomPresetIdentity_AndNormalizeStops()
    {
        var customPreset = new PresetDefinition
        {
            PresetId = "custom-bars",
            Name = "Meu Bars",
            RendererId = RendererIds.Bars,
            Palette = new GradientPalette
            {
                Name = "Base",
                Stops =
                [
                    new PaletteStop { Offset = 0.2f, Color = new RgbaColor(255, 0, 0) },
                    new PaletteStop { Offset = 0.8f, Color = new RgbaColor(0, 0, 255) },
                ],
            },
        };

        var savedPreset = VisualizerPresetPaletteEditor.ApplyPalette(
            customPreset,
            "Novo gradiente",
            [
                new PaletteStop { Offset = 0.85f, Color = new RgbaColor(0, 0, 255) },
                new PaletteStop { Offset = -0.10f, Color = new RgbaColor(255, 0, 0) },
                new PaletteStop { Offset = 0.40f, Color = new RgbaColor(0, 255, 0) },
            ]);

        Assert.Equal("custom-bars", savedPreset.PresetId);
        Assert.Equal("Meu Bars", savedPreset.Name);
        Assert.Equal(RendererIds.Bars, savedPreset.RendererId);
        Assert.Equal("Novo gradiente", savedPreset.Palette.Name);
        Assert.Equal(0f, savedPreset.Palette.Stops[0].Offset);
        Assert.Equal(1f, savedPreset.Palette.Stops[^1].Offset);
        Assert.True(savedPreset.Palette.Stops.SequenceEqual(savedPreset.Palette.Stops.OrderBy(static stop => stop.Offset)));
    }

    [Fact]
    public void StopMutations_ShouldRespectSorting_Interpolation_AndMinimumCount()
    {
        IReadOnlyList<PaletteStop> stops =
        [
            new PaletteStop { Offset = 0f, Color = new RgbaColor(255, 0, 0) },
            new PaletteStop { Offset = 1f, Color = new RgbaColor(0, 0, 255) },
        ];

        var addResult = VisualizerPresetPaletteEditor.AddStop(stops, 0.5f);
        Assert.Equal(3, addResult.Stops.Count);
        Assert.Equal(0.5f, addResult.Stops[addResult.SelectedIndex].Offset, 3);
        Assert.Equal(new RgbaColor(128, 0, 128), addResult.Stops[addResult.SelectedIndex].Color);

        var moveResult = VisualizerPresetPaletteEditor.UpdateStopOffset(addResult.Stops, addResult.SelectedIndex, 0.75f);
        Assert.Equal(3, moveResult.Stops.Count);
        Assert.Equal(0.75f, moveResult.Stops[moveResult.SelectedIndex].Offset, 3);
        Assert.True(moveResult.Stops.SequenceEqual(moveResult.Stops.OrderBy(static stop => stop.Offset)));

        var removeResult = VisualizerPresetPaletteEditor.RemoveStop(moveResult.Stops, moveResult.SelectedIndex);
        Assert.Equal(2, removeResult.Stops.Count);

        var protectedResult = VisualizerPresetPaletteEditor.RemoveStop(removeResult.Stops, removeResult.SelectedIndex);
        Assert.Equal(2, protectedResult.Stops.Count);
    }

    [Fact]
    public async Task PresetRepository_ShouldPersistEditedBuiltInCopy()
    {
        var appDataRoot = Path.Combine(Path.GetTempPath(), "mica-audio-smoke", Guid.NewGuid().ToString("N"));
        var repository = new PresetRepository(Options.Create(new MicaAudioOptions
        {
            AppDataRoot = appDataRoot,
            PresetsDirectory = Path.Combine(appDataRoot, "presets"),
        }));

        try
        {
            var defaults = DefaultPresets.Create();
            var sourcePreset = defaults.Single(static preset =>
                string.Equals(preset.PresetId, "audiomotion-clone", StringComparison.OrdinalIgnoreCase));
            var editablePreset = VisualizerPresetPaletteEditor.ResolveEditablePreset(
                sourcePreset,
                defaults.ToDictionary(static preset => preset.PresetId, StringComparer.OrdinalIgnoreCase));
            var savedPreset = VisualizerPresetPaletteEditor.ApplyPalette(
                editablePreset,
                "Neon local",
                [
                    new PaletteStop { Offset = 0f, Color = new RgbaColor(57, 255, 20) },
                    new PaletteStop { Offset = 1f, Color = new RgbaColor(255, 46, 178) },
                ]);

            await repository.SaveAllAsync(defaults.Append(savedPreset));
            var reloaded = await repository.LoadOrSeedAsync();

            var persisted = reloaded.Single(static preset =>
                string.Equals(preset.PresetId, "user-audiomotion-clone", StringComparison.OrdinalIgnoreCase));

            Assert.Equal("AudioMotion Clone (editado)", persisted.Name);
            Assert.Equal("Neon local", persisted.Palette.Name);
            Assert.Equal(new RgbaColor(255, 46, 178), persisted.Palette.Stops[^1].Color);
            Assert.Contains(reloaded, static preset =>
                string.Equals(preset.PresetId, "audiomotion-clone", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(appDataRoot))
            {
                Directory.Delete(appDataRoot, recursive: true);
            }
        }
    }

    [Fact]
    public void ResolveDisplayName_ShouldApplyBuiltInOverride_AndEditableSuffixForUserCopy()
    {
        var builtInPreset = DefaultPresets.Create().Single(static preset =>
            string.Equals(preset.PresetId, "audiomotion-sunset", StringComparison.OrdinalIgnoreCase));
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [builtInPreset.PresetId] = "Meu Sunset",
        };

        var builtInDisplayName = VisualizerPresetPaletteEditor.ResolveDisplayName(builtInPreset, overrides);
        var editedCopyDisplayName = VisualizerPresetPaletteEditor.ResolveDisplayName(
            new PresetDefinition
            {
                PresetId = "user-audiomotion-sunset",
                Name = "AudioMotion Sunset (editado)",
                RendererId = builtInPreset.RendererId,
                Palette = builtInPreset.Palette,
            },
            overrides);

        Assert.Equal("Meu Sunset", builtInDisplayName);
        Assert.Equal("Meu Sunset (editado)", editedCopyDisplayName);
    }

    [Fact]
    public void CreateClonedPreset_ShouldCreateUniqueLocalIdAndDeduplicatedName()
    {
        var defaults = DefaultPresets.Create();
        var sourcePreset = defaults.Single(static preset =>
            string.Equals(preset.PresetId, "audiomotion-clone", StringComparison.OrdinalIgnoreCase));
        var existing = defaults.Append(new PresetDefinition
        {
            PresetId = "custom-audiomotion-clone",
            Name = "AudioMotion Clone (copia)",
            RendererId = sourcePreset.RendererId,
            Palette = sourcePreset.Palette,
        }).ToArray();

        var clonePreset = VisualizerPresetPaletteEditor.CreateClonedPreset(
            sourcePreset,
            existing,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        Assert.NotEqual(sourcePreset.PresetId, clonePreset.PresetId);
        Assert.StartsWith("custom-audiomotion-clone", clonePreset.PresetId, StringComparison.Ordinal);
        Assert.Equal("AudioMotion Clone (copia) 2", clonePreset.Name);
        Assert.Equal(sourcePreset.RendererId, clonePreset.RendererId);
    }

    [Fact]
    public async Task BuiltInPresetNameOverrideStore_ShouldPersistAndReloadOverrides()
    {
        var appDataRoot = Path.Combine(Path.GetTempPath(), "mica-audio-name-overrides", Guid.NewGuid().ToString("N"));
        var store = new BuiltInPresetNameOverrideStore(Options.Create(new MicaAudioOptions
        {
            AppDataRoot = appDataRoot,
            PresetsDirectory = Path.Combine(appDataRoot, "presets"),
        }));

        try
        {
            await store.SaveAsync(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["audiomotion-clone"] = "Meu Clone",
                ["spectrum-wave-mirror"] = "Wave Local",
            });

            var reloaded = await store.LoadAsync();

            Assert.Equal("Meu Clone", reloaded["audiomotion-clone"]);
            Assert.Equal("Wave Local", reloaded["spectrum-wave-mirror"]);
        }
        finally
        {
            if (Directory.Exists(appDataRoot))
            {
                Directory.Delete(appDataRoot, recursive: true);
            }
        }
    }
}
