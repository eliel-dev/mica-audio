using App.WinUI.Views;
using App.WinUI.Services.Gif;
using MicaAudio.Core.Config;
using MicaAudio.Core.Presets;
using Visual.Win2D.Engine;

namespace Integration.Smoke;

public sealed class MainPageStartupHelpersTests
{
    [Fact]
    public void BootstrapGuard_ShouldReportActiveOnlyInsideScope()
    {
        var guard = new MainPageUiBootstrapGuard();

        Assert.False(guard.IsActive);

        using (guard.Enter())
        {
            Assert.True(guard.IsActive);
        }

        Assert.False(guard.IsActive);
    }

    [Fact]
    public void BuildSafeAnalyzerPreset_ShouldFallbackLegacyNullMembers()
    {
        var legacyPreset = new PresetDefinition
        {
            PresetId = string.Empty,
            Name = string.Empty,
            RendererId = string.Empty,
            DisplayBandCount = 0,
            FpsTarget = 0,
            RendererParameters = null!,
            Palette = null!,
        };

        var normalized = MainPage.BuildSafeAnalyzerPreset(
            legacyPreset,
            selectedRendererId: null,
            displayBandCount: 0,
            availableRendererIds: [RendererIds.AudioMotionClone],
            fallbackPresetId: "audiomotion-clone");

        Assert.Equal("audiomotion-clone", normalized.PresetId);
        Assert.Equal(RendererIds.AudioMotionClone, normalized.RendererId);
        Assert.True(normalized.DisplayBandCount > 0);
        Assert.NotNull(normalized.RendererParameters);
        Assert.NotEmpty(normalized.RendererParameters);
        Assert.NotNull(normalized.Palette);
        Assert.NotEmpty(normalized.Palette.Stops);
    }

    [Fact]
    public void BuildSafeAnalyzerPreset_ShouldFallbackToSupportedRenderer()
    {
        var runtimePreset = MainPage.BuildSafeAnalyzerPreset(
            new PresetDefinition
            {
                PresetId = "legacy",
                Name = "Legacy",
                RendererId = "unsupported-renderer",
                DisplayBandCount = 0,
                RendererParameters = null!,
                Palette = null!,
            },
            selectedRendererId: "missing-renderer",
            displayBandCount: 64,
            availableRendererIds: [RendererIds.Bars],
            fallbackPresetId: "audiomotion-clone");

        Assert.Equal(RendererIds.Bars, runtimePreset.RendererId);
        Assert.Equal(64, runtimePreset.DisplayBandCount);
        Assert.NotEmpty(runtimePreset.RendererParameters);
        Assert.NotEmpty(runtimePreset.Palette.Stops);
    }

    [Fact]
    public void VisualizerRuntimeApplyState_ShouldSkipEquivalentPendingSettings()
    {
        var state = new VisualizerRuntimeApplyState();
        var runtimeSettings = VisualizerRuntimeSettings.From(new AppSettings());

        state.MarkApplied(runtimeSettings);
        state.SetPending(VisualizerRuntimeSettings.From(new AppSettings()));

        Assert.False(state.ShouldApply());
        Assert.True(VisualizerRuntimeApplyState.AreEquivalent(state.Pending, state.Applied));
    }

    [Fact]
    public void VisualizerRuntimeApplyState_ShouldCollapseRapidPendingChangesIntoSingleApply()
    {
        var state = new VisualizerRuntimeApplyState();
        state.MarkApplied(VisualizerRuntimeSettings.From(new AppSettings()));

        state.SetPending(VisualizerRuntimeSettings.From(new AppSettings
        {
            FftSmoothing = 0.82f,
        }));

        state.SetPending(VisualizerRuntimeSettings.From(new AppSettings
        {
            FftSmoothing = 0.65f,
            FftSize = 4096,
            FrequencyMinHz = 40f,
            FrequencyMaxHz = 4000f,
        }));

        Assert.True(state.ShouldApply());
        Assert.Equal(1.3f, state.Pending.LinearBoost);
        Assert.Equal(0.65f, state.Pending.FftSmoothing);
        Assert.Equal(2048, state.Pending.FftSize);
        Assert.Equal(40f, state.Pending.FrequencyMinHz);
        Assert.Equal(4000f, state.Pending.FrequencyMaxHz);

        state.MarkApplied(state.Pending);

        Assert.False(state.ShouldApply());
        Assert.True(VisualizerRuntimeApplyState.AreEquivalent(state.Pending, state.Applied));
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 3, true)]
    [InlineData(1, 0, true)]
    public void ShouldShowContentModeSetting_ShouldReflectGifContext(
        int modeValue,
        int loadedGifFrameCount,
        bool expected)
    {
        var mode = (GifContentSourceMode)modeValue;
        var result = MainPage.ShouldShowContentModeSetting(mode, loadedGifFrameCount);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true, "O preset AudioMotion Clone usa o renderizador nativo e mantém esta opção fixa.")]
    [InlineData(false, "Escolhe como o visualizador é desenhado no canvas principal.")]
    public void ResolveRendererSelectionHint_ShouldExposeSettingsSpecificMessage(
        bool usesFixedRenderer,
        string expected)
    {
        var hint = MainPage.ResolveRendererSelectionHint(usesFixedRenderer);

        Assert.Equal(expected, hint);
    }
}
