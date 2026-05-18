package com.micaaudio.android.data.audio

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Singleton that holds the current frequency bins for UI preview.
 */
@Singleton
class SpectrumState @Inject constructor() {
    // Empty initial state — actual size is determined by the audio capture service
    // (currentBarCount from AppSettings). This prevents the canvas from rendering
    // 128 empty slots while waiting for the first FFT frame.
    private val _bins = MutableStateFlow(FloatArray(0))
    val bins: StateFlow<FloatArray> = _bins.asStateFlow()

    fun updateBins(newBins: FloatArray) {
        _bins.value = newBins
    }
}
