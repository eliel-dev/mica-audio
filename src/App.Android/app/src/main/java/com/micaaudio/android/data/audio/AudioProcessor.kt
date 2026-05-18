package com.micaaudio.android.data.audio

import kotlin.math.*

// DOCS: docs/wiki/modules/visual-win2d.md#audiomotion-clone

/** Frequency scale used for logarithmic band mapping. Mirrors WinUI's FrequencyScale enum. */
enum class FrequencyScale { Logarithmic, Bark, Mel }

/** Psychoacoustic weighting filter applied to FFT bins before aggregation. Mirrors WinUI's WeightingFilter. */
enum class WeightingFilter { Off, A, B, C }

/**
 * Port of WinUI's EnvelopeSmoother logic for smooth rise/fall transitions.
 */
class EnvelopeSmoother(
    var rise: Float,
    var fall: Float,
    var motionDamping: Float = 1f
) {
    private var targetState: FloatArray? = null
    private var smoothedState: FloatArray? = null

    fun process(input: FloatArray, destination: FloatArray) {
        if (targetState == null || targetState!!.size != input.size) {
            targetState = FloatArray(input.size)
            smoothedState = FloatArray(input.size)
        }

        val targets = targetState!!
        val smooths = smoothedState!!

        for (i in input.indices) {
            val inputValue = input[i]
            var target = targets[i]

            val speed = if (inputValue > target) rise else fall
            target += (inputValue - target) * speed
            targets[i] = target

            var smooth = smooths[i]
            smooth += (target - smooth) * motionDamping
            smooths[i] = smooth
            destination[i] = smooth
        }
    }
}

/**
 * Port of WinUI's LogBandMapper + WeightingCurve.
 *
 * Supports Logarithmic, Bark, and Mel frequency scales, plus A/B/C
 * psychoacoustic weighting filters — identical to the Windows client.
 */
object LogBandMapper {

    // ── Frequency scale transforms ─────────────────────────────────────────

    fun toScale(hz: Float, scale: FrequencyScale = FrequencyScale.Logarithmic): Float = when (scale) {
        FrequencyScale.Mel  -> log2(1f + hz / 700f)
        FrequencyScale.Bark -> (26.81f * hz) / (1960f + hz) - 0.53f
        else                -> log2(max(1f, hz))
    }

    fun fromScale(value: Float, scale: FrequencyScale = FrequencyScale.Logarithmic): Float = when (scale) {
        FrequencyScale.Mel  -> 700f * (2f.pow(value) - 1f)
        FrequencyScale.Bark -> 1960f / max(1e-6f, (26.81f / (value + 0.53f)) - 1f)
        else                -> 2f.pow(value)
    }

    // Backward-compatible overloads (log2 only)
    fun toScale(hz: Float): Float = toScale(hz, FrequencyScale.Logarithmic)
    fun fromScale(value: Float): Float = fromScale(value, FrequencyScale.Logarithmic)

    // ── Weighting filters (port of WinUI's WeightingCurve) ────────────────

    /**
     * Build per-FFT-bin **amplitude** multipliers for the given weighting filter.
     *
     * The Windows implementation works in the power domain; here we pre-convert to
     * amplitude multipliers so they can be applied directly to the magnitude array.
     * Formula: amplitudeMultiplier = 10^(weightingDb / 20).
     */
    fun buildWeightingMultipliers(fftSize: Int, sampleRate: Int, filter: WeightingFilter): FloatArray {
        val half = fftSize / 2
        val multipliers = FloatArray(half + 1) { 1f }
        if (filter == WeightingFilter.Off) return multipliers

        for (bin in 1..half) {
            val freq = bin.toDouble() * sampleRate / fftSize
            val db = computeWeightingDb(freq, filter)
            multipliers[bin] = 10.0.pow(db / 20.0).toFloat().coerceIn(1e-6f, 1e6f)
        }
        return multipliers
    }

    private fun computeWeightingDb(f: Double, filter: WeightingFilter): Double {
        if (f <= 0.0) return 0.0
        val f2 = f * f
        val c1 = 424.36
        val c2 = 148_693_636.0
        return when (filter) {
            WeightingFilter.A -> 2.0 + toDb(
                c2 * f2 * f2 /
                ((f2 + c1) * sqrt((f2 + 11_599.29) * (f2 + 544_496.41)) * (f2 + c2)),
            )
            WeightingFilter.B -> 0.17 + toDb(
                c2 * f2 * f /
                ((f2 + c1) * sqrt(f2 + 25_122.25) * (f2 + c2)),
            )
            WeightingFilter.C -> 0.06 + toDb(
                c2 * f2 /
                ((f2 + c1) * (f2 + c2)),
            )
            else -> 0.0
        }
    }

    private fun toDb(v: Double): Double = 20.0 * log10(max(1e-30, v))

    // ── Band aggregation ────────────────────────────────────────────────────

    /**
     * Aggregate FFT magnitudes into [destination.size] display bands.
     *
     * Pipeline matches Windows `LogBandMapper.AggregateBandsPeak` +
     * `NormalizeLinearFromPower`:
     *   1. For each band, find the **peak** weighted magnitude across the FFT bins it covers.
     *   2. Normalize from amplitude domain to [0..1] using `dbFloor`/`dbCeiling`:
     *        normalized = (amplitude - 10^(dbFloor/20)) / (10^(dbCeiling/20) - 10^(dbFloor/20))
     *   3. Multiply by `linearBoost` and clamp to [0..1].
     *
     * The Android `Visualizer` class returns FFT bytes scaled to roughly [0, 1] amplitude,
     * so the dB thresholds are calibrated for that range (Windows uses -85..-25 dB for
     * floating-point PCM input, which would saturate everything to 1 on Android).
     */
    fun calculateBinsInto(
        magnitudes: FloatArray,
        destination: FloatArray,
        sampleRate: Int,
        fftSize: Int,
        minHz: Float = 20f,
        maxHz: Float = 20000f,
        linearBoost: Float = 1.3f,
        dbFloor: Float = -60f,
        dbCeiling: Float = -10f,
        frequencyScale: FrequencyScale = FrequencyScale.Logarithmic,
        /** Pre-computed amplitude multipliers from [buildWeightingMultipliers]. Null = no weighting. */
        weightingMultipliers: FloatArray? = null,
    ) {
        val bandCount = destination.size
        val nyquist = sampleRate / 2f
        val nyquistBin = fftSize / 2

        if (bandCount == 0 || magnitudes.isEmpty() || nyquistBin <= 1) {
            destination.fill(0f)
            return
        }

        val minScale = toScale(minHz.coerceAtLeast(1f), frequencyScale)
        val maxScale = toScale(maxHz.coerceIn(minHz + 1f, nyquist), frequencyScale)
        val scaleRange = maxScale - minScale

        // Windows-style normalization constants (amplitude domain).
        val minAmplitude = 10.0.pow(dbFloor.toDouble() / 20.0).toFloat()
        val maxAmplitude = 10.0.pow(dbCeiling.toDouble() / 20.0).toFloat()
        val amplitudeRange = (maxAmplitude - minAmplitude).coerceAtLeast(1e-12f)

        var previousEndBin = 1

        for (i in 0 until bandCount) {
            val t0 = i / bandCount.toFloat()
            val t1 = (i + 1) / bandCount.toFloat()

            val f0 = fromScale(minScale + scaleRange * t0, frequencyScale)
            val f1 = fromScale(minScale + scaleRange * t1, frequencyScale)

            val startBin = max(previousEndBin, (f0 * fftSize / sampleRate).toInt().coerceIn(1, nyquistBin - 1))
            val endBin = (f1 * fftSize / sampleRate).toInt().coerceIn(startBin + 1, nyquistBin)

            // Peak aggregation across bins in this band (matches Windows AudioMotion mode).
            var peak = 0f
            for (bin in startBin until endBin) {
                val w = weightingMultipliers?.getOrNull(bin) ?: 1f
                val v = magnitudes[bin] * w
                if (v > peak) peak = v
            }

            // Windows-style amplitude → normalized linear in [0..1].
            val normalized = (peak - minAmplitude) / amplitudeRange
            destination[i] = (normalized * linearBoost).coerceIn(0f, 1f)
            previousEndBin = endBin
        }
    }
}
