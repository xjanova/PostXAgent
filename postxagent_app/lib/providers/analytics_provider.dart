import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/models/analytics.dart';
import '../data/repositories/analytics_repository.dart';
import 'auth_provider.dart';

/// Analytics Repository Provider
final analyticsRepositoryProvider = Provider<AnalyticsRepository>((ref) {
  final apiService = ref.watch(apiServiceProvider);
  return AnalyticsRepository(apiService);
});

/// Dashboard State
class DashboardState {
  final DashboardSummary? summary;
  final bool isLoading;
  final String? error;

  const DashboardState({
    this.summary,
    this.isLoading = false,
    this.error,
  });

  DashboardState copyWith({
    DashboardSummary? summary,
    bool? isLoading,
    String? error,
  }) {
    return DashboardState(
      summary: summary ?? this.summary,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }
}

/// Dashboard Notifier
class DashboardNotifier extends StateNotifier<DashboardState> {
  final AnalyticsRepository _repository;

  DashboardNotifier(this._repository) : super(const DashboardState());

  /// Load dashboard summary
  Future<void> loadSummary() async {
    state = state.copyWith(isLoading: true, error: null);

    final result = await _repository.getDashboardSummary();

    if (result.isSuccess) {
      state = state.copyWith(
        summary: result.data,
        isLoading: false,
      );
    } else {
      state = state.copyWith(
        isLoading: false,
        error: result.error,
      );
    }
  }

  /// Refresh summary
  Future<void> refresh() async {
    await loadSummary();
  }
}

/// Dashboard Notifier Provider
final dashboardNotifierProvider =
    StateNotifierProvider<DashboardNotifier, DashboardState>((ref) {
  final repository = ref.watch(analyticsRepositoryProvider);
  return DashboardNotifier(repository);
});

/// Analytics Overview State
class AnalyticsOverviewState {
  final AnalyticsOverview? overview;
  final List<PlatformAnalytics> platformAnalytics;
  final List<PostPerformance> topPosts;
  final List<EngagementTrend> trends;
  final List<ViralPost> viralPosts;
  final bool isLoading;
  final String? error;
  final String period;
  final int? brandId;

  const AnalyticsOverviewState({
    this.overview,
    this.platformAnalytics = const [],
    this.topPosts = const [],
    this.trends = const [],
    this.viralPosts = const [],
    this.isLoading = false,
    this.error,
    this.period = '7d',
    this.brandId,
  });

  AnalyticsOverviewState copyWith({
    AnalyticsOverview? overview,
    List<PlatformAnalytics>? platformAnalytics,
    List<PostPerformance>? topPosts,
    List<EngagementTrend>? trends,
    List<ViralPost>? viralPosts,
    bool? isLoading,
    String? error,
    String? period,
    int? brandId,
  }) {
    return AnalyticsOverviewState(
      overview: overview ?? this.overview,
      platformAnalytics: platformAnalytics ?? this.platformAnalytics,
      topPosts: topPosts ?? this.topPosts,
      trends: trends ?? this.trends,
      viralPosts: viralPosts ?? this.viralPosts,
      isLoading: isLoading ?? this.isLoading,
      error: error,
      period: period ?? this.period,
      brandId: brandId ?? this.brandId,
    );
  }
}

/// Analytics Overview Notifier
class AnalyticsOverviewNotifier extends StateNotifier<AnalyticsOverviewState> {
  final AnalyticsRepository _repository;

  AnalyticsOverviewNotifier(this._repository)
      : super(const AnalyticsOverviewState());

  /// Load all analytics data
  Future<void> loadAnalytics() async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      // Load all data in parallel
      final results = await Future.wait([
        _repository.getOverview(
          period: state.period,
          brandId: state.brandId,
        ),
        _repository.getPlatformAnalytics(
          period: state.period,
          brandId: state.brandId,
        ),
        _repository.getPostPerformance(
          period: state.period,
          brandId: state.brandId,
        ),
        _repository.getEngagementTrends(
          period: state.period,
          brandId: state.brandId,
        ),
        _repository.getViralPosts(
          period: state.period,
          brandId: state.brandId,
        ),
      ]);

      state = state.copyWith(
        overview: (results[0] as dynamic).isSuccess
            ? (results[0] as dynamic).data
            : null,
        platformAnalytics: (results[1] as dynamic).isSuccess
            ? (results[1] as dynamic).data
            : [],
        topPosts: (results[2] as dynamic).isSuccess
            ? (results[2] as dynamic).data
            : [],
        trends: (results[3] as dynamic).isSuccess
            ? (results[3] as dynamic).data
            : [],
        viralPosts: (results[4] as dynamic).isSuccess
            ? (results[4] as dynamic).data
            : [],
        isLoading: false,
      );
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  /// Set period filter
  Future<void> setPeriod(String period) async {
    state = state.copyWith(period: period);
    await loadAnalytics();
  }

  /// Set brand filter
  Future<void> setBrand(int? brandId) async {
    state = state.copyWith(brandId: brandId);
    await loadAnalytics();
  }

  /// Export report
  Future<String?> exportReport({String format = 'pdf'}) async {
    final result = await _repository.exportReport(
      period: state.period,
      brandId: state.brandId,
      format: format,
    );

    if (result.isSuccess) {
      return result.data;
    }

    state = state.copyWith(error: result.error);
    return null;
  }
}

/// Analytics Overview Provider
final analyticsOverviewNotifierProvider =
    StateNotifierProvider<AnalyticsOverviewNotifier, AnalyticsOverviewState>(
        (ref) {
  final repository = ref.watch(analyticsRepositoryProvider);
  return AnalyticsOverviewNotifier(repository);
});

/// Quick Stats Provider (for widgets)
final quickStatsProvider = FutureProvider<Map<String, dynamic>>((ref) async {
  final repository = ref.watch(analyticsRepositoryProvider);
  final result = await repository.getDashboardSummary();

  if (result.isSuccess) {
    return {
      'total_posts': result.data!.totalPosts,
      'published_posts': result.data!.publishedPosts,
      'scheduled_posts': result.data!.scheduledPosts,
      'viral_posts': result.data!.viralPosts,
      'engagement_rate': result.data!.engagementRate,
    };
  }

  return {
    'total_posts': 0,
    'published_posts': 0,
    'scheduled_posts': 0,
    'viral_posts': 0,
    'engagement_rate': 0.0,
  };
});
