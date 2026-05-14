package com.micaaudio.android.data.audio

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class VisualFrameRateLimiterTest {
    @Test
    fun shouldEmit_limitsFramesToSixtyFps() {
        val limiter = VisualFrameRateLimiter(targetFramesPerSecond = 60)

        assertTrue(limiter.shouldEmit(0L))
        assertFalse(limiter.shouldEmit(10_000_000L))
        assertTrue(limiter.shouldEmit(16_666_666L))
        assertFalse(limiter.shouldEmit(20_000_000L))
        assertTrue(limiter.shouldEmit(33_333_332L))
    }

    @Test
    fun reset_allowsImmediateNextFrame() {
        val limiter = VisualFrameRateLimiter(targetFramesPerSecond = 60)

        assertTrue(limiter.shouldEmit(100L))
        assertFalse(limiter.shouldEmit(101L))

        limiter.reset()

        assertTrue(limiter.shouldEmit(101L))
    }
}
