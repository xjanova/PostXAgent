import 'dart:io';

import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/models/brand.dart';
import '../data/repositories/brand_repository.dart';
import 'auth_provider.dart';

/// Brand Repository Provider
final brandRepositoryProvider = Provider<BrandRepository>((ref) {
  final apiService = ref.watch(apiServiceProvider);
  return BrandRepository(apiService);
});

/// Brands State
class BrandsState {
  final List<Brand> brands;
  final bool isLoading;
  final bool isLoadingMore;
  final String? error;
  final int currentPage;
  final bool hasMore;
  final String? searchQuery;
  final bool? activeFilter;

  const BrandsState({
    this.brands = const [],
    this.isLoading = false,
    this.isLoadingMore = false,
    this.error,
    this.currentPage = 1,
    this.hasMore = true,
    this.searchQuery,
    this.activeFilter,
  });

  BrandsState copyWith({
    List<Brand>? brands,
    bool? isLoading,
    bool? isLoadingMore,
    String? error,
    int? currentPage,
    bool? hasMore,
    String? searchQuery,
    bool? activeFilter,
  }) {
    return BrandsState(
      brands: brands ?? this.brands,
      isLoading: isLoading ?? this.isLoading,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      error: error,
      currentPage: currentPage ?? this.currentPage,
      hasMore: hasMore ?? this.hasMore,
      searchQuery: searchQuery ?? this.searchQuery,
      activeFilter: activeFilter ?? this.activeFilter,
    );
  }

  int get activeCount => brands.where((b) => b.isActive).length;
  int get inactiveCount => brands.where((b) => !b.isActive).length;
}

/// Brands Notifier
class BrandsNotifier extends StateNotifier<BrandsState> {
  final BrandRepository _repository;

  BrandsNotifier(this._repository) : super(const BrandsState());

  /// Load brands
  Future<void> loadBrands({bool refresh = false}) async {
    if (state.isLoading) return;

    state = state.copyWith(
      isLoading: true,
      error: null,
      currentPage: refresh ? 1 : state.currentPage,
      brands: refresh ? [] : state.brands,
    );

    final result = await _repository.getBrands(
      page: 1,
      search: state.searchQuery,
      isActive: state.activeFilter,
    );

    if (result.isSuccess) {
      state = state.copyWith(
        brands: result.data!.items,
        isLoading: false,
        currentPage: 1,
        hasMore: result.data!.hasMore,
      );
    } else {
      state = state.copyWith(
        isLoading: false,
        error: result.error,
      );
    }
  }

  /// Load more brands
  Future<void> loadMore() async {
    if (state.isLoadingMore || !state.hasMore) return;

    state = state.copyWith(isLoadingMore: true);

    final result = await _repository.getBrands(
      page: state.currentPage + 1,
      search: state.searchQuery,
      isActive: state.activeFilter,
    );

    if (result.isSuccess) {
      state = state.copyWith(
        brands: [...state.brands, ...result.data!.items],
        isLoadingMore: false,
        currentPage: state.currentPage + 1,
        hasMore: result.data!.hasMore,
      );
    } else {
      state = state.copyWith(
        isLoadingMore: false,
        error: result.error,
      );
    }
  }

  /// Search brands
  Future<void> search(String query) async {
    state = state.copyWith(searchQuery: query.isEmpty ? null : query);
    await loadBrands(refresh: true);
  }

  /// Filter by active status
  Future<void> setActiveFilter(bool? isActive) async {
    state = state.copyWith(activeFilter: isActive);
    await loadBrands(refresh: true);
  }

  /// Delete brand
  Future<bool> deleteBrand(int id) async {
    final result = await _repository.deleteBrand(id);
    if (result.isSuccess) {
      state = state.copyWith(
        brands: state.brands.where((b) => b.id != id).toList(),
      );
      return true;
    }
    return false;
  }

  /// Toggle brand status
  Future<bool> toggleBrandStatus(int id) async {
    final result = await _repository.toggleBrandStatus(id);
    if (result.isSuccess) {
      state = state.copyWith(
        brands: state.brands.map((b) {
          if (b.id == id) return result.data!;
          return b;
        }).toList(),
      );
      return true;
    }
    return false;
  }
}

/// Brands Notifier Provider
final brandsNotifierProvider =
    StateNotifierProvider<BrandsNotifier, BrandsState>((ref) {
  final repository = ref.watch(brandRepositoryProvider);
  return BrandsNotifier(repository);
});

/// All Brands Provider (for dropdowns)
final allBrandsProvider = FutureProvider<List<Brand>>((ref) async {
  final repository = ref.watch(brandRepositoryProvider);
  final result = await repository.getAllBrands();
  return result.isSuccess ? result.data! : [];
});

/// Brand Detail State
class BrandDetailState {
  final Brand? brand;
  final bool isLoading;
  final bool isSaving;
  final String? error;

  const BrandDetailState({
    this.brand,
    this.isLoading = false,
    this.isSaving = false,
    this.error,
  });

  BrandDetailState copyWith({
    Brand? brand,
    bool? isLoading,
    bool? isSaving,
    String? error,
  }) {
    return BrandDetailState(
      brand: brand ?? this.brand,
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      error: error,
    );
  }
}

/// Brand Detail Notifier
class BrandDetailNotifier extends StateNotifier<BrandDetailState> {
  final BrandRepository _repository;

  BrandDetailNotifier(this._repository) : super(const BrandDetailState());

  /// Load brand
  Future<void> loadBrand(int id) async {
    state = state.copyWith(isLoading: true, error: null);

    final result = await _repository.getBrand(id);

    if (result.isSuccess) {
      state = state.copyWith(brand: result.data, isLoading: false);
    } else {
      state = state.copyWith(isLoading: false, error: result.error);
    }
  }

  /// Create brand
  Future<Brand?> createBrand(BrandRequest request) async {
    state = state.copyWith(isSaving: true, error: null);

    final result = await _repository.createBrand(request);

    if (result.isSuccess) {
      state = state.copyWith(brand: result.data, isSaving: false);
      return result.data;
    } else {
      state = state.copyWith(isSaving: false, error: result.error);
      return null;
    }
  }

  /// Update brand
  Future<Brand?> updateBrand(int id, BrandRequest request) async {
    state = state.copyWith(isSaving: true, error: null);

    final result = await _repository.updateBrand(id, request);

    if (result.isSuccess) {
      state = state.copyWith(brand: result.data, isSaving: false);
      return result.data;
    } else {
      state = state.copyWith(isSaving: false, error: result.error);
      return null;
    }
  }

  /// Upload logo
  Future<String?> uploadLogo(int brandId, File file) async {
    final result = await _repository.uploadLogo(brandId, file);
    if (result.isSuccess) {
      // Reload brand to get updated logo URL
      await loadBrand(brandId);
      return result.data;
    }
    return null;
  }

  void clear() {
    state = const BrandDetailState();
  }
}

/// Brand Detail Provider
final brandDetailNotifierProvider =
    StateNotifierProvider<BrandDetailNotifier, BrandDetailState>((ref) {
  final repository = ref.watch(brandRepositoryProvider);
  return BrandDetailNotifier(repository);
});
