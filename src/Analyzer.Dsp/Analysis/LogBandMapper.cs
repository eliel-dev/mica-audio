using MicaAudio.Core.Audio;

namespace Analyzer.Dsp.Analysis;

public static class LogBandMapper
{
    public static Mode0BandLayout CreateMode0Ranges(
        int fftSize,
        int sampleRate,
        float minHz,
        float maxHz,
        FrequencyScale frequencyScale,
        float viewportWidthPx,
        float barSpace)
    {
        if (fftSize < 2 || sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException("FFT size and sample rate must be positive.");
        }

        if (minHz <= 0f || maxHz <= minHz)
        {
            throw new ArgumentOutOfRangeException("Frequency range must satisfy 0 < minHz < maxHz.");
        }

        // `barSpace` is intentionally ignored for mode0 because audioMotion's mode 0
        // uses one-pixel bars positioned directly by bin->x mapping.
        _ = barSpace;

        var width = MathF.Max(1f, viewportWidthPx);
        var widthInt = global::System.Math.Max(1, (int)MathF.Round(width));
        var maxAnalyserBin = (fftSize / 2) - 1;
        if (maxAnalyserBin < 1)
        {
            return new Mode0BandLayout(Array.Empty<BandRange>(), Array.Empty<float>(), Array.Empty<float>());
        }

        var edgeInsetPx = widthInt > 2 ? 1 : 0;
        var xMin = edgeInsetPx;
        var xMax = global::System.Math.Max(xMin, widthInt - 1 - edgeInsetPx);
        var minScale = ToScale(minHz, frequencyScale);
        var maxScale = ToScale(maxHz, frequencyScale);
        var unitWidth = width / MathF.Max(1e-6f, maxScale - minScale);
        // Ignore DC bin to keep mode0 behavior aligned with audioMotion.
        var minBin = global::System.Math.Clamp((int)MathF.Floor(minHz * fftSize / sampleRate), 1, maxAnalyserBin);
        var maxBinExclusive = global::System.Math.Clamp(
            (int)MathF.Ceiling(maxHz * fftSize / sampleRate),
            minBin + 1,
            maxAnalyserBin + 1);

        var ranges = new List<BandRange>();
        var normalizedX = new List<float>();
        var normalizedW = new List<float>();
        var barWidthNorm = 1f / width;
        var previousX = int.MinValue;

        for (var bin = minBin; bin < maxBinExclusive; bin++)
        {
            var freq = MathF.Max(1f, (bin + 0.5f) * sampleRate / (float)fftSize);
            var scaled = ToScale(freq, frequencyScale);
            var x = (int)MathF.Round((scaled - minScale) * unitWidth);
            x = global::System.Math.Clamp(x, xMin, xMax);

            if (x > previousX || ranges.Count == 0)
            {
                var endBin = global::System.Math.Min(maxAnalyserBin + 1, bin + 1);
                ranges.Add(new BandRange(bin, endBin, bin, bin + 1f));
                normalizedX.Add(x / width);
                normalizedW.Add(barWidthNorm);
                previousX = x;
                continue;
            }

            var lastIndex = ranges.Count - 1;
            var last = ranges[lastIndex];
            var endExclusive = global::System.Math.Min(maxAnalyserBin + 1, bin + 1);
            ranges[lastIndex] = new BandRange(last.StartBin, endExclusive, last.StartBinExact, bin + 1f);
        }

        return new Mode0BandLayout(ranges.ToArray(), normalizedX.ToArray(), normalizedW.ToArray());
    }

    public static BandRange[] CreateRanges(
        int bandCount,
        int fftSize,
        int sampleRate,
        float minHz,
        float maxHz,
        FrequencyScale frequencyScale = FrequencyScale.Logarithmic)
    {
        if (bandCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bandCount));
        }

        if (fftSize < 2 || sampleRate <= 0)
        {
            throw new ArgumentOutOfRangeException("FFT size and sample rate must be positive.");
        }

        if (minHz <= 0f || maxHz <= minHz)
        {
            throw new ArgumentOutOfRangeException("Frequency range must satisfy 0 < minHz < maxHz.");
        }

        var ranges = new BandRange[bandCount];
        var nyquistBin = fftSize / 2;
        var minScale = ToScale(minHz, frequencyScale);
        var maxScale = ToScale(maxHz, frequencyScale);
        var scaleRange = maxScale - minScale;
        var previousEndExact = 1f;
        var previousEndBin = 1;

        for (var i = 0; i < bandCount; i++)
        {
            var t0 = i / (float)bandCount;
            var t1 = (i + 1) / (float)bandCount;

            var f0 = FromScale(minScale + (scaleRange * t0), frequencyScale);
            var f1 = FromScale(minScale + (scaleRange * t1), frequencyScale);

            var startExact = global::System.Math.Clamp(f0 * fftSize / sampleRate, 1f, nyquistBin - 1f);
            var endExact = global::System.Math.Clamp(f1 * fftSize / sampleRate, startExact + 1e-3f, nyquistBin);

            // Keep contiguous, monotonic ranges with at least one FFT bin of width.
            startExact = MathF.Max(startExact, previousEndExact);
            endExact = MathF.Max(endExact, startExact + 1f);
            endExact = MathF.Min(endExact, nyquistBin);
            startExact = MathF.Min(startExact, endExact - 1f);

            var start = global::System.Math.Clamp((int)MathF.Floor(startExact), 1, nyquistBin - 1);
            start = global::System.Math.Max(start, previousEndBin);
            startExact = MathF.Max(startExact, start);
            var end = global::System.Math.Clamp((int)MathF.Ceiling(endExact), start + 1, nyquistBin);

            ranges[i] = new BandRange(start, end, startExact, endExact);
            previousEndExact = endExact;
            previousEndBin = end;
        }

        return ranges;
    }

    private static float ToScale(float hz, FrequencyScale scale)
    {
        return scale switch
        {
            FrequencyScale.Mel => MathF.Log2(1f + (hz / 700f)),
            FrequencyScale.Bark => ((26.81f * hz) / (1960f + hz)) - 0.53f,
            _ => MathF.Log2(hz),
        };
    }

    private static float FromScale(float value, FrequencyScale scale)
    {
        return scale switch
        {
            FrequencyScale.Mel => 700f * (MathF.Pow(2f, value) - 1f),
            FrequencyScale.Bark => 1960f / global::System.Math.Max(1e-6f, (26.81f / (value + 0.53f)) - 1f),
            _ => MathF.Pow(2f, value),
        };
    }

    public static float[] AggregateBands(
        float[] powerSpectrum,
        BandRange[] ranges,
        float dbFloor,
        float dbCeiling,
        bool useDb,
        bool useLinearAmplitude = true,
        float linearBoost = 1f)
        => AggregateBandsRms(powerSpectrum, ranges, dbFloor, dbCeiling, useDb, useLinearAmplitude, linearBoost);

    public static float[] AggregateBandsRms(
        float[] powerSpectrum,
        BandRange[] ranges,
        float dbFloor,
        float dbCeiling,
        bool useDb,
        bool useLinearAmplitude = true,
        float linearBoost = 1f)
    {
        var output = new float[ranges.Length];

        for (var i = 0; i < ranges.Length; i++)
        {
            var range = ranges[i];
            var start = range.StartBin;
            var end = global::System.Math.Min(range.EndBin, powerSpectrum.Length);

            var weightedSum = 0f;
            var totalWeight = 0f;

            for (var bin = start; bin < end; bin++)
            {
                var binStart = MathF.Max(bin, range.StartBinExact);
                var binEnd = MathF.Min(bin + 1f, range.EndBinExact);
                var weight = MathF.Max(0f, binEnd - binStart);
                if (weight <= 0f)
                {
                    continue;
                }

                weightedSum += powerSpectrum[bin] * weight;
                totalWeight += weight;
            }

            var avgPower = totalWeight > 0f ? weightedSum / totalWeight : 0f;
            output[i] = NormalizePower(avgPower, dbFloor, dbCeiling, useDb, useLinearAmplitude, linearBoost);
        }

        return output;
    }

    public static float[] AggregateBandsPeak(
        float[] powerSpectrum,
        BandRange[] ranges,
        float dbFloor,
        float dbCeiling,
        bool useDb,
        bool useLinearAmplitude = true,
        float linearBoost = 1f)
    {
        var output = new float[ranges.Length];

        for (var i = 0; i < ranges.Length; i++)
        {
            var range = ranges[i];
            var start = range.StartBin;
            var end = global::System.Math.Min(range.EndBin, powerSpectrum.Length);

            var peakPower = 0f;
            for (var bin = start; bin < end; bin++)
            {
                var binStart = MathF.Max(bin, range.StartBinExact);
                var binEnd = MathF.Min(bin + 1f, range.EndBinExact);
                var weight = MathF.Max(0f, binEnd - binStart);
                if (weight <= 0f)
                {
                    continue;
                }

                peakPower = MathF.Max(peakPower, powerSpectrum[bin]);
            }

            output[i] = NormalizePower(peakPower, dbFloor, dbCeiling, useDb, useLinearAmplitude, linearBoost);
        }

        return output;
    }

    private static float NormalizePower(
        float power,
        float dbFloor,
        float dbCeiling,
        bool useDb,
        bool useLinearAmplitude,
        float linearBoost)
    {
        return useDb
            ? NormalizeDb(power, dbFloor, dbCeiling)
            : useLinearAmplitude
                ? NormalizeLinearFromPower(power, dbFloor, dbCeiling, linearBoost)
                : global::System.Math.Clamp(MathF.Sqrt(MathF.Max(power, 0f)) * 4f, 0f, 1f);
    }

    private static float NormalizeDb(float power, float dbFloor, float dbCeiling)
    {
        var db = 10f * MathF.Log10(MathF.Max(power, 1e-12f));
        return global::System.Math.Clamp((db - dbFloor) / (dbCeiling - dbFloor), 0f, 1f);
    }

    private static float NormalizeLinearFromPower(float power, float dbFloor, float dbCeiling, float linearBoost)
    {
        var amplitude = MathF.Sqrt(MathF.Max(power, 0f));
        var minAmplitude = MathF.Pow(10f, dbFloor / 20f);
        var maxAmplitude = MathF.Pow(10f, dbCeiling / 20f);
        if (maxAmplitude <= minAmplitude + 1e-12f)
        {
            return 0f;
        }

        var normalized = (amplitude - minAmplitude) / (maxAmplitude - minAmplitude);
        var boost = global::System.Math.Max(0f, linearBoost);
        return global::System.Math.Clamp(normalized * boost, 0f, 1f);
    }
}
