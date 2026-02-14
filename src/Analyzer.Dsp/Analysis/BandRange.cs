namespace Analyzer.Dsp.Analysis;

public readonly record struct BandRange(int StartBin, int EndBin, float StartBinExact, float EndBinExact);
