import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/models/campaign.dart';
import '../data/repositories/campaign_repository.dart';
import 'auth_provider.dart';

/// Campaign Repository Provider
final campaignRepositoryProvider = Provider<CampaignRepository>((ref) {
  final apiService = ref.watch(apiServiceProvider);
  return CampaignRepository(apiService);
});

/// Campaigns State
class CampaignsState {
  final List<Campaign> campaigns;
  final bool isLoading;
  final bool isLoadingMore;
  final String? error;
  final int currentPage;
  final bool hasMore;
  final String? statusFilter;
  final int? brandFilter;
  final String? searchQuery;

  const CampaignsState({
    this.campaigns = const [],
    this.isLoading = false,
    this.isLoadingMore = false,
    this.error,
    this.currentPage = 1,
    this.hasMore = true,
    this.statusFilter,
    this.brandFilter,
    this.searchQuery,
  });

  CampaignsState copyWith({
    List<Campaign>? campaigns,
    bool? isLoading,
    bool? isLoadingMore,
    String? error,
    int? currentPage,
    bool? hasMore,
    String? statusFilter,
    int? brandFilter,
    String? searchQuery,
  }) {
    return CampaignsState(
      campaigns: campaigns ?? this.campaigns,
      isLoading: isLoading ?? this.isLoading,
      isLoadingMore: isLoadingMore ?? this.isLoadingMore,
      error: error,
      currentPage: currentPage ?? this.currentPage,
      hasMore: hasMore ?? this.hasMore,
      statusFilter: statusFilter ?? this.statusFilter,
      brandFilter: brandFilter ?? this.brandFilter,
      searchQuery: searchQuery ?? this.searchQuery,
    );
  }

  int get activeCount => campaigns.where((c) => c.isActive).length;
  int get draftCount => campaigns.where((c) => c.status == 'draft').length;
  int get completedCount => campaigns.where((c) => c.isCompleted).length;
}

/// Campaigns Notifier
class CampaignsNotifier extends StateNotifier<CampaignsState> {
  final CampaignRepository _repository;

  CampaignsNotifier(this._repository) : super(const CampaignsState());

  /// Load campaigns
  Future<void> loadCampaigns({bool refresh = false}) async {
    if (state.isLoading) return;

    state = state.copyWith(
      isLoading: true,
      error: null,
      currentPage: refresh ? 1 : state.currentPage,
      campaigns: refresh ? [] : state.campaigns,
    );

    final result = await _repository.getCampaigns(
      page: 1,
      status: state.statusFilter,
      brandId: state.brandFilter,
      search: state.searchQuery,
    );

    if (result.isSuccess) {
      state = state.copyWith(
        campaigns: result.data!.items,
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

  /// Load more campaigns
  Future<void> loadMore() async {
    if (state.isLoadingMore || !state.hasMore) return;

    state = state.copyWith(isLoadingMore: true);

    final result = await _repository.getCampaigns(
      page: state.currentPage + 1,
      status: state.statusFilter,
      brandId: state.brandFilter,
      search: state.searchQuery,
    );

    if (result.isSuccess) {
      state = state.copyWith(
        campaigns: [...state.campaigns, ...result.data!.items],
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

  /// Set status filter
  Future<void> setStatusFilter(String? status) async {
    state = state.copyWith(statusFilter: status);
    await loadCampaigns(refresh: true);
  }

  /// Set brand filter
  Future<void> setBrandFilter(int? brandId) async {
    state = state.copyWith(brandFilter: brandId);
    await loadCampaigns(refresh: true);
  }

  /// Search campaigns
  Future<void> search(String query) async {
    state = state.copyWith(searchQuery: query.isEmpty ? null : query);
    await loadCampaigns(refresh: true);
  }

  /// Clear filters
  Future<void> clearFilters() async {
    state = state.copyWith(
      statusFilter: null,
      brandFilter: null,
      searchQuery: null,
    );
    await loadCampaigns(refresh: true);
  }

  /// Delete campaign
  Future<bool> deleteCampaign(int id) async {
    final result = await _repository.deleteCampaign(id);
    if (result.isSuccess) {
      state = state.copyWith(
        campaigns: state.campaigns.where((c) => c.id != id).toList(),
      );
      return true;
    }
    return false;
  }

  /// Start campaign
  Future<bool> startCampaign(int id) async {
    final result = await _repository.startCampaign(id);
    if (result.isSuccess) {
      _updateCampaignInList(result.data!);
      return true;
    }
    return false;
  }

  /// Pause campaign
  Future<bool> pauseCampaign(int id) async {
    final result = await _repository.pauseCampaign(id);
    if (result.isSuccess) {
      _updateCampaignInList(result.data!);
      return true;
    }
    return false;
  }

  /// Resume campaign
  Future<bool> resumeCampaign(int id) async {
    final result = await _repository.resumeCampaign(id);
    if (result.isSuccess) {
      _updateCampaignInList(result.data!);
      return true;
    }
    return false;
  }

  /// Complete campaign
  Future<bool> completeCampaign(int id) async {
    final result = await _repository.completeCampaign(id);
    if (result.isSuccess) {
      _updateCampaignInList(result.data!);
      return true;
    }
    return false;
  }

  void _updateCampaignInList(Campaign campaign) {
    state = state.copyWith(
      campaigns: state.campaigns.map((c) {
        if (c.id == campaign.id) return campaign;
        return c;
      }).toList(),
    );
  }
}

/// Campaigns Notifier Provider
final campaignsNotifierProvider =
    StateNotifierProvider<CampaignsNotifier, CampaignsState>((ref) {
  final repository = ref.watch(campaignRepositoryProvider);
  return CampaignsNotifier(repository);
});

/// Campaign Detail State
class CampaignDetailState {
  final Campaign? campaign;
  final Map<String, dynamic>? analytics;
  final bool isLoading;
  final bool isSaving;
  final String? error;

  const CampaignDetailState({
    this.campaign,
    this.analytics,
    this.isLoading = false,
    this.isSaving = false,
    this.error,
  });

  CampaignDetailState copyWith({
    Campaign? campaign,
    Map<String, dynamic>? analytics,
    bool? isLoading,
    bool? isSaving,
    String? error,
  }) {
    return CampaignDetailState(
      campaign: campaign ?? this.campaign,
      analytics: analytics ?? this.analytics,
      isLoading: isLoading ?? this.isLoading,
      isSaving: isSaving ?? this.isSaving,
      error: error,
    );
  }
}

/// Campaign Detail Notifier
class CampaignDetailNotifier extends StateNotifier<CampaignDetailState> {
  final CampaignRepository _repository;

  CampaignDetailNotifier(this._repository)
      : super(const CampaignDetailState());

  /// Load campaign
  Future<void> loadCampaign(int id) async {
    state = state.copyWith(isLoading: true, error: null);

    final result = await _repository.getCampaign(id);

    if (result.isSuccess) {
      state = state.copyWith(campaign: result.data, isLoading: false);
      // Also load analytics
      await loadAnalytics(id);
    } else {
      state = state.copyWith(isLoading: false, error: result.error);
    }
  }

  /// Load analytics
  Future<void> loadAnalytics(int id) async {
    final result = await _repository.getCampaignAnalytics(id);
    if (result.isSuccess) {
      state = state.copyWith(analytics: result.data);
    }
  }

  /// Create campaign
  Future<Campaign?> createCampaign(CreateCampaignRequest request) async {
    state = state.copyWith(isSaving: true, error: null);

    final result = await _repository.createCampaign(request);

    if (result.isSuccess) {
      state = state.copyWith(campaign: result.data, isSaving: false);
      return result.data;
    } else {
      state = state.copyWith(isSaving: false, error: result.error);
      return null;
    }
  }

  /// Update campaign
  Future<Campaign?> updateCampaign(int id, CreateCampaignRequest request) async {
    state = state.copyWith(isSaving: true, error: null);

    final result = await _repository.updateCampaign(id, request);

    if (result.isSuccess) {
      state = state.copyWith(campaign: result.data, isSaving: false);
      return result.data;
    } else {
      state = state.copyWith(isSaving: false, error: result.error);
      return null;
    }
  }

  void clear() {
    state = const CampaignDetailState();
  }
}

/// Campaign Detail Provider
final campaignDetailNotifierProvider =
    StateNotifierProvider<CampaignDetailNotifier, CampaignDetailState>((ref) {
  final repository = ref.watch(campaignRepositoryProvider);
  return CampaignDetailNotifier(repository);
});
