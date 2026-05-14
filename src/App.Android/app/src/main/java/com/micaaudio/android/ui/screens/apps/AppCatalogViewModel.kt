package com.micaaudio.android.ui.screens.apps

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.micaaudio.android.data.api.AppCatalogItem
import com.micaaudio.android.data.repository.AppCatalogRepository
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class AppCatalogUiState(
    val items: List<AppCatalogItem> = emptyList(),
    val filteredItems: List<AppCatalogItem> = emptyList(),
    val isLoading: Boolean = true,
    val searchQuery: String = "",
    val selectedCategory: String = "Todos",
    val error: String? = null,
)

@HiltViewModel
class AppCatalogViewModel @Inject constructor(
    private val catalogRepository: AppCatalogRepository,
) : ViewModel() {

    private val _uiState = MutableStateFlow(AppCatalogUiState())
    val uiState: StateFlow<AppCatalogUiState> = _uiState.asStateFlow()

    init {
        loadCatalog()
    }

    fun refresh() {
        catalogRepository.invalidateCache()
        loadCatalog()
    }

    fun search(query: String) {
        _uiState.update { it.copy(searchQuery = query) }
        applyFilters()
    }

    fun selectCategory(category: String) {
        _uiState.update { it.copy(selectedCategory = category) }
        applyFilters()
    }

    private fun loadCatalog() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, error = null) }
            catalogRepository.getCatalog()
                .onSuccess { items ->
                    _uiState.update { it.copy(items = items, isLoading = false) }
                    applyFilters()
                }
                .onFailure { e ->
                    _uiState.update { it.copy(isLoading = false, error = e.message) }
                }
        }
    }

    private fun applyFilters() {
        val state = _uiState.value
        val query = state.searchQuery.trim().lowercase()
        val category = state.selectedCategory
        val filtered = state.items.filter { app ->
            val matchesCategory = category == "Todos" || app.category.equals(category, ignoreCase = true)
            val matchesQuery = query.isEmpty() ||
                app.name.lowercase().contains(query) ||
                app.summary.lowercase().contains(query) ||
                app.category.lowercase().contains(query)
            matchesCategory && matchesQuery
        }
        _uiState.update { it.copy(filteredItems = filtered) }
    }
}
