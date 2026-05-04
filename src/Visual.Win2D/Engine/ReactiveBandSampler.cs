namespace Visual.Win2D.Engine;

// DOCS: docs/wiki/modules/visual-win2d.md#reatividade-compartilhada
public static class ReactiveBandSampler
{
    private const float GlobalLevelContribution = 0.10f;

    public static ReactiveBandSnapshot Sample(
        ReadOnlySpan<float> sourceBands,
        float level,
        float deltaSeconds,
        int targetCount,
        RendererBarCountMode mode,
        ref float[] previousBands)
    {
        var effectiveCount = ResolveEffectiveCount(sourceBands.Length, targetCount, mode);
        if (effectiveCount <= 0)
        {
            previousBands = Array.Empty<float>();
            return new ReactiveBandSnapshot(ReadOnlySpan<float>.Empty, 0f, 0f, 0f, 0f);
        }

        if (previousBands.Length != effectiveCount)
        {
            previousBands = new float[effectiveCount];
        }

        var globalLevel = sourceBands.Length == 0
            ? 0f
            : Math.Clamp(level, 0f, 1f);

        for (var i = 0; i < effectiveCount; i++)
        {
            var sample = ResolveBand(sourceBands, i, effectiveCount, mode);
            sample = Math.Clamp(
                (sample * (1f - GlobalLevelContribution)) + (globalLevel * GlobalLevelContribution),
                0f,
                1f);

            previousBands[i] = Math.Clamp(
                ReactiveEnvelopeState.Blend(previousBands[i], sample, deltaSeconds),
                0f,
                1f);
        }

        var lowEnd = Math.Clamp((int)MathF.Ceiling(effectiveCount * 0.20f), 1, effectiveCount);
        var midEnd = Math.Clamp((int)MathF.Ceiling(effectiveCount * 0.65f), lowEnd, effectiveCount);

        var low = AverageRange(previousBands, 0, lowEnd);
        var mid = AverageRange(previousBands, lowEnd, midEnd);
        var high = AverageRange(previousBands, midEnd, effectiveCount);

        return new ReactiveBandSnapshot(previousBands, low, mid, high, globalLevel);
    }

    private static int ResolveEffectiveCount(int sourceCount, int targetCount, RendererBarCountMode mode)
    {
        return mode switch
        {
            RendererBarCountMode.Native => Math.Max(0, sourceCount),
            RendererBarCountMode.Resampled => Math.Max(0, targetCount),
            RendererBarCountMode.Fixed => Math.Max(0, targetCount),
            _ => Math.Max(0, targetCount),
        };
    }

    private static float ResolveBand(ReadOnlySpan<float> sourceBands, int index, int effectiveCount, RendererBarCountMode mode)
    {
        if (sourceBands.Length == 0 || effectiveCount <= 0)
        {
            return 0f;
        }

        if (mode == RendererBarCountMode.Native || sourceBands.Length == effectiveCount)
        {
            return Math.Clamp(sourceBands[Math.Clamp(index, 0, sourceBands.Length - 1)], 0f, 1f);
        }

        var start = (index * sourceBands.Length) / (float)effectiveCount;
        var end = ((index + 1) * sourceBands.Length) / (float)effectiveCount;
        var startIndex = Math.Clamp((int)MathF.Floor(start), 0, sourceBands.Length - 1);
        var endIndexExclusive = Math.Clamp((int)MathF.Ceiling(end), startIndex + 1, sourceBands.Length);

        var sum = 0f;
        for (var i = startIndex; i < endIndexExclusive; i++)
        {
            sum += Math.Clamp(sourceBands[i], 0f, 1f);
        }

        return sum / Math.Max(1, endIndexExclusive - startIndex);
    }

    private static float AverageRange(float[] values, int startInclusive, int endExclusive)
    {
        if (values.Length == 0)
        {
            return 0f;
        }

        var start = Math.Clamp(startInclusive, 0, values.Length - 1);
        var end = Math.Clamp(Math.Max(start + 1, endExclusive), start + 1, values.Length);

        var sum = 0f;
        for (var i = start; i < end; i++)
        {
            sum += values[i];
        }

        return sum / Math.Max(1, end - start);
    }
}
