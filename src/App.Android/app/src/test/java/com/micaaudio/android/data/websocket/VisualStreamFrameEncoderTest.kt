package com.micaaudio.android.data.websocket

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Test
import java.nio.ByteBuffer
import java.nio.ByteOrder

class VisualStreamFrameEncoderTest {
    @Test
    fun createTargetedBins128Envelope_usesStreamFrameV2CloneFlags() {
        val bins = ByteArray(128) { index -> index.toByte() }

        val envelope = VisualStreamFrameEncoder.createTargetedBins128Envelope(
            deviceId = "Esp32-Mica",
            bins = bins,
            level = 77,
            brightness = 160,
            sequence = 0x01020304,
            timestampMillis = 0x0102030405060708L,
        )

        val buffer = ByteBuffer.wrap(envelope).order(ByteOrder.LITTLE_ENDIAN)
        assertEquals(1, buffer.get().toInt() and 0xFF)

        val deviceIdBytes = ByteArray(buffer.short.toInt() and 0xFFFF)
        buffer.get(deviceIdBytes)
        assertEquals("Esp32-Mica", deviceIdBytes.decodeToString())

        val payload = ByteArray(buffer.remaining())
        buffer.get(payload)

        assertEquals(145, payload.size)
        assertEquals(2, payload[0].toInt() and 0xFF)
        assertEquals(1, payload[1].toInt() and 0xFF)
        assertEquals(0x01020304, ByteBuffer.wrap(payload, 2, 4).order(ByteOrder.LITTLE_ENDIAN).int)
        assertEquals(0x0102030405060708L, ByteBuffer.wrap(payload, 6, 8).order(ByteOrder.LITTLE_ENDIAN).long)
        assertEquals(77, payload[14].toInt() and 0xFF)
        assertArrayEquals(bins, payload.copyOfRange(15, 143))
        assertEquals(160, payload[143].toInt() and 0xFF)
        assertEquals(0x11, payload[144].toInt() and 0xFF)
    }
}
