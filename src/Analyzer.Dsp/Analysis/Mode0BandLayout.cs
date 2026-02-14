namespace Analyzer.Dsp.Analysis;

public sealed record Mode0BandLayout(
    BandRange[] Ranges,
    float[] BarX,
    float[] BarWidth);
