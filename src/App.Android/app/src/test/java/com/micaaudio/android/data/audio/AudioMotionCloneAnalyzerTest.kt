package com.micaaudio.android.data.audio

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.math.PI
import kotlin.math.roundToInt
import kotlin.math.sin

class AudioMotionCloneAnalyzerTest {
    @Test
    fun process_doesNotEmitUntilAnalysisWindowIsFull() {
        val analyzer = AudioMotionCloneAnalyzer()

        assertNull(analyzer.process(ShortArray(AudioMotionCloneAnalyzer.FftSize / 2), AudioMotionCloneAnalyzer.FftSize / 2))

        val frame = analyzer.process(ShortArray(AudioMotionCloneAnalyzer.FftSize / 2), AudioMotionCloneAnalyzer.FftSize / 2)

        assertNotNull(frame)
        assertEquals(128, frame!!.bins128.size)
    }

    @Test
    fun process_silenceProducesZeroBinsAndLevel() {
        val analyzer = AudioMotionCloneAnalyzer()

        val frame = analyzer.process(ShortArray(AudioMotionCloneAnalyzer.FftSize), AudioMotionCloneAnalyzer.FftSize)

        assertNotNull(frame)
        assertEquals(128, frame!!.bins128.size)
        assertTrue(frame.bins128.all { it == 0f })
        assertEquals(0f, frame.level, 0.000001f)
    }

    @Test
    fun process_lowFrequencySineConcentratesEnergyInLowBins() {
        val analyzer = AudioMotionCloneAnalyzer()
        val samples = ShortArray(AudioMotionCloneAnalyzer.FftSize) { index ->
            (sin(2.0 * PI * 80.0 * index / AudioMotionCloneAnalyzer.SampleRate) * 12000.0)
                .roundToInt()
                .toShort()
        }

        val frame = analyzer.process(samples, samples.size)

        assertNotNull(frame)
        val bins = frame!!.bins128
        val lowAverage = bins.take(24).average()
        val highAverage = bins.takeLast(24).average()
        val saturatedBins = bins.count { it >= 0.99f }

        assertEquals(128, bins.size)
        assertTrue("lowAverage=$lowAverage highAverage=$highAverage", lowAverage > (highAverage * 1.5) + 0.02)
        assertTrue("saturatedBins=$saturatedBins", saturatedBins < 96)
    }
}
