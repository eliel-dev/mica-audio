using Device.Protocol.Stream;
using MicaAudio.Core.Led;
using MicaAudio.Core.Presets;
using Output.Led;

namespace Output.Tests;

public sealed class Bins128PreviewRendererTests
{
    [Fact]
    public void Render_ShouldProduceDifferentFrames_ForDifferentStyles()
    {
        var renderer = new Bins128PreviewRenderer();
        var bins = Enumerable.Repeat(0.65f, LedDefaults.MatrixWidth).ToArray();
        var wave = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        var radial = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];

        renderer.Render(
            bins,
            0.45f,
            Bins128VisualFlags.Create(Bins128VisualStyle.WaveMirror, Bins128PaletteFamily.Canonical),
            LedDefaults.MatrixWidth,
            LedDefaults.MatrixHeight,
            1f,
            wave);

        renderer.Render(
            bins,
            0.45f,
            Bins128VisualFlags.Create(Bins128VisualStyle.RadialOrbit, Bins128PaletteFamily.Canonical),
            LedDefaults.MatrixWidth,
            LedDefaults.MatrixHeight,
            1f,
            radial);

        Assert.NotEqual(wave, radial);
    }

    [Fact]
    public void Render_ClassicBars_ShouldResetPeakState_WhenStyleChanges()
    {
        var renderer = new Bins128PreviewRenderer();
        var heldBars = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        var resetBars = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        var strongBins = Enumerable.Repeat(1f, LedDefaults.MatrixWidth).ToArray();
        var silentBins = new float[LedDefaults.MatrixWidth];
        var classicFlags = Bins128VisualFlags.Create(Bins128VisualStyle.ClassicBars, Bins128PaletteFamily.Rainbow);
        var waveFlags = Bins128VisualFlags.Create(Bins128VisualStyle.WaveMirror, Bins128PaletteFamily.Canonical);

        renderer.Render(strongBins, 1f, classicFlags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight]);
        renderer.Render(silentBins, 0f, classicFlags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, heldBars);
        renderer.Render(silentBins, 0f, waveFlags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight]);
        renderer.Render(silentBins, 0f, classicFlags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, resetBars);

        Assert.Contains(heldBars, static px => px.R > 0 || px.G > 0 || px.B > 0);
        Assert.DoesNotContain(resetBars, static px => px.R > 0 || px.G > 0 || px.B > 0);
    }

    [Fact]
    public void Render_HistoryScan_ShouldKeepHistoryUntilReset()
    {
        var renderer = new Bins128PreviewRenderer();
        var carriedFrame = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        var resetFrame = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        var activeBins = new float[LedDefaults.MatrixWidth];
        for (var i = 0; i < 16; i++)
        {
            activeBins[i] = 1f;
        }

        var flags = Bins128VisualFlags.Create(Bins128VisualStyle.HistoryScan, Bins128PaletteFamily.Rainbow);

        renderer.Render(activeBins, 0.8f, flags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight]);
        renderer.Render(new float[LedDefaults.MatrixWidth], 0f, flags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, carriedFrame);
        renderer.Reset();
        renderer.Render(new float[LedDefaults.MatrixWidth], 0f, flags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, resetFrame);

        Assert.Contains(carriedFrame, static px => px.R > 0 || px.G > 0 || px.B > 0);
        Assert.DoesNotContain(resetFrame, static px => px.R > 0 || px.G > 0 || px.B > 0);
    }

    [Fact]
    public void Render_LaunchpadGrid_ShouldKeepHoldUntilReset()
    {
        var renderer = new Bins128PreviewRenderer();
        var freshRenderer = new Bins128PreviewRenderer();
        var heldFrame = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        var resetFrame = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        var freshFrame = new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight];
        var activeBins = Enumerable.Repeat(1f, LedDefaults.MatrixWidth).ToArray();
        var flags = Bins128VisualFlags.Create(Bins128VisualStyle.LaunchpadGrid, Bins128PaletteFamily.Canonical);

        renderer.Render(activeBins, 1f, flags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, new RgbaColor[LedDefaults.MatrixWidth * LedDefaults.MatrixHeight]);
        renderer.Render(new float[LedDefaults.MatrixWidth], 0f, flags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, heldFrame);
        renderer.Reset();
        renderer.Render(new float[LedDefaults.MatrixWidth], 0f, flags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, resetFrame);
        freshRenderer.Render(new float[LedDefaults.MatrixWidth], 0f, flags, LedDefaults.MatrixWidth, LedDefaults.MatrixHeight, 1f, freshFrame);

        Assert.Contains(heldFrame, static px => px.R > 0 || px.G > 0 || px.B > 0);
        Assert.Equal(freshFrame, resetFrame);
        Assert.NotEqual(heldFrame, resetFrame);
    }
}
