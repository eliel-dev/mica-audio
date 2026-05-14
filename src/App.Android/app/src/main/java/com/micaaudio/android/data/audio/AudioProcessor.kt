package com.micaaudio.android.data.audio

import kotlin.math.*

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
 * Port of WinUI's LogBandMapper logic for logarithmic bin aggregation.
 */
object LogBandMapper {

    fun toScale(hz: Float): Float = log2(max(1f, hz))

    fun fromScale(value: Float): Float = 2.0f.pow(value)

    fun calculateBins(
        magnitudes: FloatArray,
        sampleRate: Int,
        fftSize: Int,
        minHz: Float = 20f,
        maxHz: Float = 20000f,
        bandCount: Int = 128,
        gain: Float = 1.0f,
        linearBoost: Float = 1.0f
    ): FloatArray {
        val bins = FloatArray(bandCount)
        val nyquist = sampleRate / 2f
        val nyquistBin = fftSize / 2

        val minScale = toScale(minHz)
        val maxScale = toScale(maxHz)
        val scaleRange = maxScale - minScale

        var previousEndBin = 1

        for (i in 0 until bandCount) {
            val t0 = i / bandCount.toFloat()
            val t1 = (i + 1) / bandCount.toFloat()

            val f0 = fromScale(minScale + scaleRange * t0)
            val f1 = fromScale(minScale + scaleRange * t1)

            val startBin = max(previousEndBin, (f0 * fftSize / sampleRate).toInt().coerceIn(1, nyquistBin - 1))
            val endBin = (f1 * fftSize / sampleRate).toInt().coerceIn(startBin + 1, nyquistBin)

            var sum = 0f
            var count = 0
            for (bin in startBin until endBin) {
                sum += magnitudes[bin]
                count++
            }

            val avg = if (count > 0) sum / count else 0f

            // Normalize with Gain and Linear Boost (simplified version of WinUI's NormalizePower)
            val boosted = avg * gain * linearBoost
            bins[i] = boosted.coerceIn(0f, 1f)

            previousEndBin = endBin
        }

        return bins
    }
}
