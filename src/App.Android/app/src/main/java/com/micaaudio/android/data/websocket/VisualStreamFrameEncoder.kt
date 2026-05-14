package com.micaaudio.android.data.websocket

import java.nio.ByteBuffer
import java.nio.ByteOrder

// DOCS: docs/wiki/reference/ws-protocol-v2.md#mensagem-tipo-1---bins128
object VisualStreamFrameEncoder {
    private const val STREAM_VERSION = 2
    private const val MESSAGE_TYPE_BINS128 = 1
    private const val BINS128_PAYLOAD_SIZE = 145
    private const val BINS128_COUNT = 128

    const val AudioMotionCloneFlags: Int = 0x11

    fun createTargetedBins128Envelope(
        deviceId: String,
        bins: ByteArray,
        level: Int,
        brightness: Int,
        sequence: Int,
        timestampMillis: Long = System.nanoTime(),
    ): ByteArray {
        require(bins.size == BINS128_COUNT) { "bins must contain exactly 128 values." }

        val deviceIdBytes = deviceId.toByteArray(Charsets.UTF_8)
        require(deviceIdBytes.size <= 0xFFFF) { "deviceId is too long for the admin frame envelope." }

        val payload = createBins128Payload(
            bins = bins,
            level = level,
            brightness = brightness,
            sequence = sequence,
            timestampMillis = timestampMillis,
        )

        return ByteBuffer
            .allocate(1 + 2 + deviceIdBytes.size + payload.size)
            .order(ByteOrder.LITTLE_ENDIAN)
            .apply {
                put(1.toByte())
                putShort(deviceIdBytes.size.toShort())
                put(deviceIdBytes)
                put(payload)
            }
            .array()
    }

    fun createBins128Payload(
        bins: ByteArray,
        level: Int,
        brightness: Int,
        sequence: Int,
        timestampMillis: Long = System.nanoTime(),
    ): ByteArray {
        require(bins.size == BINS128_COUNT) { "bins must contain exactly 128 values." }

        return ByteBuffer
            .allocate(BINS128_PAYLOAD_SIZE)
            .order(ByteOrder.LITTLE_ENDIAN)
            .apply {
                put(STREAM_VERSION.toByte())
                put(MESSAGE_TYPE_BINS128.toByte())
                putInt(sequence)
                putLong(timestampMillis)
                put(level.coerceIn(0, 255).toByte())
                put(bins)
                put(brightness.coerceIn(0, 255).toByte())
                put(AudioMotionCloneFlags.toByte())
            }
            .array()
    }
}
