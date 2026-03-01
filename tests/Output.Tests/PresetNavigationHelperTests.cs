using App.WinUI.Services.Visualizer;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace Output.Tests;

public sealed class PresetNavigationHelperTests
{
    [Fact]
    public void BuildOrder_ShouldSortByName()
    {
        var presets = new[]
        {
            CreatePreset("preset-2", "Zulu"),
            CreatePreset("preset-3", "alpha"),
            CreatePreset("preset-1", "Bravo"),
        };

        var ordered = PresetNavigationHelper.BuildOrder(presets);

        Assert.Equal(new[] { "alpha", "Bravo", "Zulu" }, ordered.Select(static preset => preset.Name));
    }

    [Fact]
    public void ResolveIndex_ShouldReturnZeroWhenMissing()
    {
        var presets = PresetNavigationHelper.BuildOrder(new[]
        {
            CreatePreset("preset-1", "Alpha"),
            CreatePreset("preset-2", "Bravo"),
        });

        var index = PresetNavigationHelper.ResolveIndex(presets, "missing");

        Assert.Equal(0, index);
    }

    [Fact]
    public void WrapIndex_ShouldWrapFromLastToFirst()
    {
        var index = PresetNavigationHelper.WrapIndex(currentIndex: 2, direction: 1, count: 3);

        Assert.Equal(0, index);
    }

    [Fact]
    public void WrapIndex_ShouldWrapFromFirstToLast()
    {
        var index = PresetNavigationHelper.WrapIndex(currentIndex: 0, direction: -1, count: 3);

        Assert.Equal(2, index);
    }

    private static PresetDefinition CreatePreset(string presetId, string name)
    {
        return new PresetDefinition
        {
            PresetId = presetId,
            Name = name,
            RendererId = RendererIds.Bars,
        };
    }
}
