package com.micaaudio.android.data.audio

// DOCS: docs/wiki/modules/visual-win2d.md#audiomotion-clone
class VisualFrameRateLimiter(
    targetFramesPerSecond: Int = TargetFramesPerSecond,
) {
    private val minimumFrameIntervalNanos = (1_000_000_000L / targetFramesPerSecond.coerceAtLeast(1))
    private var lastFrameNanos: Long? = null

    fun shouldEmit(nowNanos: Long): Boolean {
        val previous = lastFrameNanos
        if (previous != null && nowNanos - previous < minimumFrameIntervalNanos) {
            return false
        }

        lastFrameNanos = nowNanos
        return true
    }

    fun reset() {
        lastFrameNanos = null
    }

    companion object {
        const val TargetFramesPerSecond = 60
    }
}
