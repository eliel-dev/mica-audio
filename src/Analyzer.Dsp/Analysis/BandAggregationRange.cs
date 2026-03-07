namespace Analyzer.Dsp.Analysis;

internal readonly record struct BandAggregationRange(
    int StartBin,
    int EndBinExclusive,
    float FirstBinWeight,
    float LastBinWeight,
    float TotalWeight)
{
    public bool IsSingleBin => EndBinExclusive == (StartBin + 1);
}
