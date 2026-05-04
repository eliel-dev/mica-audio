namespace Visual.Win2D.Engine;

public readonly ref struct ReactiveBandSnapshot
{
    public ReactiveBandSnapshot(
        ReadOnlySpan<float> bands,
        float low,
        float mid,
        float high,
        float globalLevel)
    {
        Bands = bands;
        Low = low;
        Mid = mid;
        High = high;
        GlobalLevel = globalLevel;
    }

    public ReadOnlySpan<float> Bands { get; }

    public float Low { get; }

    public float Mid { get; }

    public float High { get; }

    public float GlobalLevel { get; }
}
