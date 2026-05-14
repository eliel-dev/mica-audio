package com.micaaudio.android.data.audio

import kotlin.math.PI
import kotlin.math.ceil
import kotlin.math.cos
import kotlin.math.floor
import kotlin.math.log10
import kotlin.math.max
import kotlin.math.min
import kotlin.math.pow
import kotlin.math.roundToInt
import kotlin.math.sin
import kotlin.math.sqrt

// DOCS: docs/wiki/modules/visual-win2d.md#audiomotion-clone
// DOCS: docs/wiki/reference/ws-protocol-v2.md#mensagem-tipo-1---bins128
data class AudioMotionCloneFrame(
    val bins128: FloatArray,
    val level: Float,
)

class AudioMotionCloneAnalyzer {
    private val hannWindow = FloatArray(FftSize) { index ->
        (0.5f * (1f - cos((2f * PI.toFloat() * index) / (FftSize - 1)))).toFloat()
    }
    private val sampleWindow = FloatArray(FftSize)
    private val realBuffer = FloatArray(FftSize)
    private val imaginaryBuffer = FloatArray(FftSize)
    private val powerSpectrum = FloatArray((FftSize / 2) + 1)
    private val smoothedSpectrum = FloatArray((FftSize / 2) + 1)
    private val weightingPowerMultipliers = buildBWeightingPowerMultipliers()
    private val bitReversal = buildBitReversal(FftSize)
    private val twiddles = buildTwiddles(FftSize)
    private val displayRanges = createMode0Ranges()
    private val displayAggregationRanges = createAggregationRanges(displayRanges)
    private val displayRaw = FloatArray(displayAggregationRanges.size)
    private val displaySmooth = FloatArray(displayAggregationRanges.size)
    private val bins128Scratch = FloatArray(OutputBinCount)
    private val outputFrameScratch = FloatArray(OutputBinCount)
    private val envelopeSmoother = CloneEnvelopeSmoother(displayAggregationRanges.size)

    private var sampleCount = 0
    private var hasSmoothedSpectrum = false

    fun process(samples: ShortArray, length: Int): AudioMotionCloneFrame? {
        val safeLength = length.coerceIn(0, samples.size)
        var offset = 0
        var latestFrame: AudioMotionCloneFrame? = null

        while (offset < safeLength) {
            val writable = min(FftSize - sampleCount, safeLength - offset)
            for (index in 0 until writable) {
                sampleWindow[sampleCount + index] = samples[offset + index] / 32768f
            }

            sampleCount += writable
            offset += writable

            if (sampleCount == FftSize) {
                latestFrame = analyzeCurrentWindow()
                advanceWindow()
            }
        }

        return latestFrame
    }

    private fun analyzeCurrentWindow(): AudioMotionCloneFrame {
        for (index in 0 until FftSize) {
            realBuffer[index] = sampleWindow[index] * hannWindow[index]
            imaginaryBuffer[index] = 0f
        }

        transformRealToPowerSpectrum()
        applyFftSmoothing()
        applyBWeighting()
        aggregateBandsPeak(displayAggregationRanges, displayRaw)
        envelopeSmoother.process(displayRaw, displaySmooth)
        resampleTo128(displaySmooth, bins128Scratch)
        val level = computeLevel()

        bins128Scratch.copyInto(outputFrameScratch)
        return AudioMotionCloneFrame(outputFrameScratch.copyOf(), level)
    }

    private fun advanceWindow() {
        val retained = FftSize - HopSize
        sampleWindow.copyInto(sampleWindow, destinationOffset = 0, startIndex = HopSize, endIndex = FftSize)
        sampleWindow.fill(0f, retained, FftSize)
        sampleCount = retained
    }

    private fun transformRealToPowerSpectrum() {
        for (index in 0 until FftSize) {
            val reversed = bitReversal[index]
            if (reversed > index) {
                val real = realBuffer[index]
                realBuffer[index] = realBuffer[reversed]
                realBuffer[reversed] = real

                val imaginary = imaginaryBuffer[index]
                imaginaryBuffer[index] = imaginaryBuffer[reversed]
                imaginaryBuffer[reversed] = imaginary
            }
        }

        for (stage in twiddles.indices) {
            val len = 1 shl (stage + 1)
            val half = len shr 1
            val cosStage = twiddles[stage].cos
            val sinStage = twiddles[stage].sin

            var offset = 0
            while (offset < FftSize) {
                for (index in 0 until half) {
                    val even = offset + index
                    val odd = even + half
                    val twiddleReal = cosStage[index]
                    val twiddleImaginary = sinStage[index]
                    val oddReal = realBuffer[odd]
                    val oddImaginary = imaginaryBuffer[odd]
                    val vReal = (oddReal * twiddleReal) - (oddImaginary * twiddleImaginary)
                    val vImaginary = (oddReal * twiddleImaginary) + (oddImaginary * twiddleReal)
                    val uReal = realBuffer[even]
                    val uImaginary = imaginaryBuffer[even]

                    realBuffer[even] = uReal + vReal
                    imaginaryBuffer[even] = uImaginary + vImaginary
                    realBuffer[odd] = uReal - vReal
                    imaginaryBuffer[odd] = uImaginary - vImaginary
                }

                offset += len
            }
        }

        val normalization = 1f / (FftSize * FftSize).toFloat()
        for (index in powerSpectrum.indices) {
            val real = realBuffer[index]
            val imaginary = imaginaryBuffer[index]
            powerSpectrum[index] = ((real * real) + (imaginary * imaginary)) * normalization
        }
    }

    private fun applyFftSmoothing() {
        if (!hasSmoothedSpectrum) {
            powerSpectrum.copyInto(smoothedSpectrum)
            hasSmoothedSpectrum = true
            return
        }

        val previousWeight = FftSmoothing
        val currentWeight = 1f - previousWeight
        for (index in powerSpectrum.indices) {
            val smoothed = (smoothedSpectrum[index] * previousWeight) + (powerSpectrum[index] * currentWeight)
            smoothedSpectrum[index] = smoothed
            powerSpectrum[index] = smoothed
        }
    }

    private fun applyBWeighting() {
        for (index in 1 until min(powerSpectrum.size, weightingPowerMultipliers.size)) {
            powerSpectrum[index] *= weightingPowerMultipliers[index]
        }
    }

    private fun aggregateBandsPeak(ranges: Array<BandAggregationRange>, destination: FloatArray) {
        for (rangeIndex in ranges.indices) {
            val range = ranges[rangeIndex]
            val endExclusive = min(range.endBinExclusive, powerSpectrum.size)
            if (range.totalWeight <= 0f || range.startBin >= endExclusive) {
                destination[rangeIndex] = 0f
                continue
            }

            var peak = 0f
            if (range.isSingleBin) {
                peak = powerSpectrum[range.startBin]
            } else {
                for (bin in range.startBin until endExclusive) {
                    val weight = when (bin) {
                        range.startBin -> range.firstBinWeight
                        range.endBinExclusive - 1 -> range.lastBinWeight
                        else -> 1f
                    }

                    if (weight > 0f) {
                        peak = max(peak, powerSpectrum[bin])
                    }
                }
            }

            destination[rangeIndex] = normalizeLinearFromPower(peak)
        }
    }

    private fun computeLevel(): Float {
        var sum = 0f
        for (index in 1 until powerSpectrum.size) {
            sum += powerSpectrum[index]
        }

        val rms = sqrt(sum / max(1, powerSpectrum.size - 1))
        return (rms * LevelCompression * 6f).pow(0.5f).coerceIn(0f, 1f)
    }

    private fun normalizeLinearFromPower(power: Float): Float {
        val amplitude = sqrt(max(power, 0f))
        val minAmplitude = 10f.pow(MinDecibels / 20f)
        val maxAmplitude = 10f.pow(MaxDecibels / 20f)
        if (maxAmplitude <= minAmplitude + 1e-12f) {
            return 0f
        }

        val normalized = (amplitude - minAmplitude) / (maxAmplitude - minAmplitude)
        return (normalized * LinearBoost).coerceIn(0f, 1f)
    }

    private fun resampleTo128(source: FloatArray, destination: FloatArray) {
        if (source.isEmpty()) {
            destination.fill(0f)
            return
        }

        if (source.size == destination.size) {
            source.copyInto(destination)
            return
        }

        for (index in destination.indices) {
            val t = if (destination.size == 1) 0f else index / (destination.size - 1).toFloat()
            val scaled = t * (source.size - 1)
            val left = floor(scaled).toInt()
            val right = min(source.size - 1, left + 1)
            val blend = scaled - left
            destination[index] = (source[left] * (1f - blend)) + (source[right] * blend)
        }
    }

    private data class BandRange(
        val startBin: Int,
        val endBin: Int,
        val startBinExact: Float,
        val endBinExact: Float,
    )

    private data class BandAggregationRange(
        val startBin: Int,
        val endBinExclusive: Int,
        val firstBinWeight: Float,
        val lastBinWeight: Float,
        val totalWeight: Float,
    ) {
        val isSingleBin: Boolean get() = endBinExclusive == startBin + 1
    }

    private data class TwiddleStage(
        val cos: FloatArray,
        val sin: FloatArray,
    )

    private class CloneEnvelopeSmoother(size: Int) {
        private val targetState = FloatArray(size)
        private val smoothedState = FloatArray(size)

        fun process(input: FloatArray, destination: FloatArray) {
            for (index in input.indices) {
                val inputValue = input[index]
                var target = targetState[index]
                val speed = if (inputValue > target) DisplaySmoothingRise else DisplaySmoothingFall
                target += (inputValue - target) * speed
                targetState[index] = target

                var smooth = smoothedState[index]
                smooth += (target - smooth) * DisplayMotionDamping
                smoothedState[index] = smooth
                destination[index] = smooth
            }
        }
    }

    companion object {
        const val SampleRate = 48_000
        const val FftSize = 2048
        const val HopSize = 256
        private const val OutputBinCount = 128
        private const val MinHz = 20f
        private const val MaxHz = 1000f
        private const val FftSmoothing = 0.75f
        private const val LinearBoost = 1.3f
        private const val MinDecibels = -85f
        private const val MaxDecibels = -25f
        private const val DisplaySmoothingRise = 0.82f
        private const val DisplaySmoothingFall = 0.06f
        private const val DisplayMotionDamping = 0.30f
        private const val LevelCompression = 1.2f

        private fun createMode0Ranges(): Array<BandRange> {
            val width = OutputBinCount.toFloat()
            val widthInt = max(1, width.roundToInt())
            val maxAnalyserBin = (FftSize / 2) - 1
            if (maxAnalyserBin < 1) {
                return emptyArray()
            }

            val edgeInsetPx = if (widthInt > 2) 1 else 0
            val xMin = edgeInsetPx
            val xMax = max(xMin, widthInt - 1 - edgeInsetPx)
            val minScale = toBarkScale(MinHz)
            val maxScale = toBarkScale(MaxHz)
            val unitWidth = width / max(1e-6f, maxScale - minScale)
            val minBin = floor(MinHz * FftSize / SampleRate).toInt().coerceIn(1, maxAnalyserBin)
            val maxBinExclusive = ceil(MaxHz * FftSize / SampleRate)
                .toInt()
                .coerceIn(minBin + 1, maxAnalyserBin + 1)

            val ranges = mutableListOf<BandRange>()
            var previousX = Int.MIN_VALUE

            for (bin in minBin until maxBinExclusive) {
                val frequency = max(1f, (bin + 0.5f) * SampleRate / FftSize)
                val scaled = toBarkScale(frequency)
                val x = ((scaled - minScale) * unitWidth)
                    .roundToInt()
                    .coerceIn(xMin, xMax)

                if (x > previousX || ranges.isEmpty()) {
                    val endBin = min(maxAnalyserBin + 1, bin + 1)
                    ranges.add(BandRange(bin, endBin, bin.toFloat(), bin + 1f))
                    previousX = x
                    continue
                }

                val lastIndex = ranges.lastIndex
                val last = ranges[lastIndex]
                val endExclusive = min(maxAnalyserBin + 1, bin + 1)
                ranges[lastIndex] = last.copy(endBin = endExclusive, endBinExact = bin + 1f)
            }

            return ranges.toTypedArray()
        }

        private fun createAggregationRanges(ranges: Array<BandRange>): Array<BandAggregationRange> {
            return Array(ranges.size) { index ->
                val range = ranges[index]
                val startBin = range.startBin
                val endBinExclusive = range.endBin
                val firstWeight = max(0f, min(startBin + 1f, range.endBinExact) - range.startBinExact)

                if (endBinExclusive == startBin + 1) {
                    BandAggregationRange(startBin, endBinExclusive, firstWeight, firstWeight, firstWeight)
                } else {
                    val lastBin = endBinExclusive - 1
                    val lastWeight = max(0f, range.endBinExact - max(lastBin.toFloat(), range.startBinExact))
                    val interiorCount = max(0, endBinExclusive - startBin - 2)
                    val totalWeight = firstWeight + interiorCount + lastWeight
                    BandAggregationRange(startBin, endBinExclusive, firstWeight, lastWeight, totalWeight)
                }
            }
        }

        private fun toBarkScale(hz: Float): Float = ((26.81f * hz) / (1960f + hz)) - 0.53f

        private fun buildBWeightingPowerMultipliers(): FloatArray {
            val multipliers = FloatArray((FftSize / 2) + 1) { 1f }
            for (bin in 1 until multipliers.size) {
                val frequency = bin.toDouble() * SampleRate / FftSize
                val weightingDb = computeBWeightingDb(frequency)
                val amplitudeMultiplier = 10.0.pow(weightingDb / 20.0)
                multipliers[bin] = (amplitudeMultiplier * amplitudeMultiplier)
                    .coerceIn(1e-12, 1e12)
                    .toFloat()
            }

            return multipliers
        }

        private fun computeBWeightingDb(frequency: Double): Double {
            if (frequency <= 0.0) {
                return 0.0
            }

            val f2 = frequency * frequency
            val c1 = 424.36
            val c2 = 148_693_636.0
            val value = c2 * f2 * frequency /
                ((f2 + c1) * sqrt(f2 + 25_122.25) * (f2 + c2))
            return 0.17 + (20.0 * log10(max(value, 1e-30)))
        }

        private fun buildBitReversal(size: Int): IntArray {
            val bits = 31 - Integer.numberOfLeadingZeros(size)
            return IntArray(size) { value -> reverseBits(value, bits) }
        }

        private fun reverseBits(value: Int, bits: Int): Int {
            var input = value
            var output = 0
            repeat(bits) {
                output = (output shl 1) or (input and 1)
                input = input shr 1
            }

            return output
        }

        private fun buildTwiddles(size: Int): Array<TwiddleStage> {
            val stages = 31 - Integer.numberOfLeadingZeros(size)
            return Array(stages) { stage ->
                val len = 1 shl (stage + 1)
                val half = len shr 1
                val angle = -2f * PI.toFloat() / len
                TwiddleStage(
                    cos = FloatArray(half) { index -> cos(angle * index) },
                    sin = FloatArray(half) { index -> sin(angle * index) },
                )
            }
        }
    }
}
