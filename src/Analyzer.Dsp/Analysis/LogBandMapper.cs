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
        ArgumentOutOfRangeException.ThrowIfLessThan(fftSize, 2, nameof(fftSize));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0, nameof(sampleRate));
        ValidateFrequencyRange(minHz, maxHz);

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
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(bandCount, 0, nameof(bandCount));
        ArgumentOutOfRangeException.ThrowIfLessThan(fftSize, 2, nameof(fftSize));
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(sampleRate, 0, nameof(sampleRate));
        ValidateFrequencyRange(minHz, maxHz);

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
        AggregateBandsRms(powerSpectrum, CreateAggregationRanges(ranges), dbFloor, dbCeiling, useDb, useLinearAmplitude, linearBoost, output);
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
        AggregateBandsPeak(powerSpectrum, CreateAggregationRanges(ranges), dbFloor, dbCeiling, useDb, useLinearAmplitude, linearBoost, output);
        return output;
    }

    internal static BandAggregationRange[] CreateAggregationRanges(ReadOnlySpan<BandRange> ranges)
    {
        var output = new BandAggregationRange[ranges.Length];
        for (var i = 0; i < ranges.Length; i++)
        {
            var range = ranges[i];
            var startBin = range.StartBin;
            var endBinExclusive = range.EndBin;
            var firstWeight = MathF.Max(0f, MathF.Min(startBin + 1f, range.EndBinExact) - range.StartBinExact);

            if (endBinExclusive == (startBin + 1))
            {
                output[i] = new BandAggregationRange(startBin, endBinExclusive, firstWeight, firstWeight, firstWeight);
                continue;
            }

            var lastBin = endBinExclusive - 1;
            var lastWeight = MathF.Max(0f, range.EndBinExact - global::System.Math.Max(lastBin, range.StartBinExact));
            var interiorCount = global::System.Math.Max(0, endBinExclusive - startBin - 2);
            var totalWeight = firstWeight + interiorCount + lastWeight;
            output[i] = new BandAggregationRange(startBin, endBinExclusive, firstWeight, lastWeight, totalWeight);
        }

        return output;
    }

    internal static void AggregateBandsRms(
        ReadOnlySpan<float> powerSpectrum,
        ReadOnlySpan<BandAggregationRange> ranges,
        float dbFloor,
        float dbCeiling,
        bool useDb,
        bool useLinearAmplitude,
        float linearBoost,
        Span<float> destination)
    {
        AggregateBands(powerSpectrum, ranges, dbFloor, dbCeiling, useDb, useLinearAmplitude, linearBoost, Span<float>.Empty, destination);
    }

    internal static void AggregateBandsPeak(
        ReadOnlySpan<float> powerSpectrum,
        ReadOnlySpan<BandAggregationRange> ranges,
        float dbFloor,
        float dbCeiling,
        bool useDb,
        bool useLinearAmplitude,
        float linearBoost,
        Span<float> destination)
    {
        AggregateBands(powerSpectrum, ranges, dbFloor, dbCeiling, useDb, useLinearAmplitude, linearBoost, destination, Span<float>.Empty);
    }

    internal static void AggregateBands(
        ReadOnlySpan<float> powerSpectrum,
        ReadOnlySpan<BandAggregationRange> ranges,
        float dbFloor,
        float dbCeiling,
        bool useDb,
        bool useLinearAmplitude,
        float linearBoost,
        Span<float> peakDestination,
        Span<float> rmsDestination)
    {
        var writePeak = !peakDestination.IsEmpty;
        var writeRms = !rmsDestination.IsEmpty;

        if (!writePeak && !writeRms)
        {
            throw new ArgumentException("At least one aggregation destination must be provided.");
        }

        if (writePeak && peakDestination.Length != ranges.Length)
        {
            throw new ArgumentException("Peak destination length must match the number of ranges.", nameof(peakDestination));
        }

        if (writeRms && rmsDestination.Length != ranges.Length)
        {
            throw new ArgumentException("Rms destination length must match the number of ranges.", nameof(rmsDestination));
        }

        for (var index = 0; index < ranges.Length; index++)
        {
            var range = ranges[index];
            var endExclusive = global::System.Math.Min(range.EndBinExclusive, powerSpectrum.Length);
            if (range.TotalWeight <= 0f || range.StartBin >= endExclusive)
            {
                if (writePeak)
                {
                    peakDestination[index] = 0f;
                }

                if (writeRms)
                {
                    rmsDestination[index] = 0f;
                }

                continue;
            }

            var peak = 0f;
            var weightedSum = 0f;

            if (range.IsSingleBin)
            {
                var value = powerSpectrum[range.StartBin];
                peak = value;
                weightedSum = value * range.TotalWeight;
            }
            else
            {
                for (var bin = range.StartBin; bin < endExclusive; bin++)
                {
                    var weight = 1f;
                    if (bin == range.StartBin)
                    {
                        weight = range.FirstBinWeight;
                    }
                    else if (bin == (range.EndBinExclusive - 1))
                    {
                        weight = range.LastBinWeight;
                    }

                    if (weight <= 0f)
                    {
                        continue;
                    }

                    var value = powerSpectrum[bin];
                    peak = MathF.Max(peak, value);
                    weightedSum += value * weight;
                }
            }

            if (writePeak)
            {
                peakDestination[index] = NormalizePower(peak, dbFloor, dbCeiling, useDb, useLinearAmplitude, linearBoost);
            }

            if (writeRms)
            {
                var averagePower = weightedSum / range.TotalWeight;
                rmsDestination[index] = NormalizePower(averagePower, dbFloor, dbCeiling, useDb, useLinearAmplitude, linearBoost);
            }
        }
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

    private static void ValidateFrequencyRange(float minHz, float maxHz)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minHz, 0f, nameof(minHz));
        if (maxHz <= minHz)
        {
            throw new ArgumentOutOfRangeException(nameof(maxHz), "Frequency range must satisfy 0 < minHz < maxHz.");
        }
    }
}
