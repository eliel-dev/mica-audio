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
    private val _bins = MutableStateFlow(FloatArray(128))
    val bins: StateFlow<FloatArray> = _bins.asStateFlow()

    fun updateBins(newBins: FloatArray) {
        _bins.value = newBins
    }
}
