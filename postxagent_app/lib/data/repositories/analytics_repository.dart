import '../models/analytics.dart';
import '../services/api_service.dart';
import 'base_repository.dart';

class AnalyticsRepository extends BaseRepository {
  AnalyticsRepository(super.apiService);

  /// Get dashboard summary
  Future<Result<DashboardSummary>> getDashboardSummary() async {
    try {
      final response = await apiService.get(ApiEndpoints.analyticsSummary);

      if (response.data['success'] == true) {
        return Result.success(DashboardSummary.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดข้อมูลได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get overview analytics
  Future<Result<AnalyticsOverview>> getOverview({
    String period = '7d',
    int? brandId,
  }) async {
    try {
      final response = await apiService.get(
        ApiEndpoints.analyticsOverview,
        queryParameters: {
          'period': period,
          if (brandId != null) 'brand_id': brandId,
        },
      );

      if (response.data['success'] == true) {
        return Result.success(AnalyticsOverview.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดสถิติได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get platform-specific analytics
  Future<Result<List<PlatformAnalytics>>> getPlatformAnalytics({
    String period = '7d',
    int? brandId,
  }) async {
    try {
      final response = await apiService.get(
        ApiEndpoints.analyticsByPlatform,
        queryParameters: {
          'period': period,
          if (brandId != null) 'brand_id': brandId,
        },
      );

      if (response.data['success'] == true) {
        final items = (response.data['data'] as List?)
                ?.map((item) =>
                    PlatformAnalytics.fromJson(item as Map<String, dynamic>))
                .toList() ??
            [];
        return Result.success(items);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดสถิติได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get post performance analytics
  Future<Result<List<PostPerformance>>> getPostPerformance({
    String period = '7d',
    int? brandId,
    int limit = 10,
  }) async {
    try {
      final response = await apiService.get(
        '${ApiEndpoints.analytics}/posts',
        queryParameters: {
          'period': period,
          if (brandId != null) 'brand_id': brandId,
          'limit': limit,
        },
      );

      if (response.data['success'] == true) {
        final items = (response.data['data'] as List?)
                ?.map((item) =>
                    PostPerformance.fromJson(item as Map<String, dynamic>))
                .toList() ??
            [];
        return Result.success(items);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดสถิติได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get engagement trends
  Future<Result<List<EngagementTrend>>> getEngagementTrends({
    String period = '7d',
    int? brandId,
    String? platform,
  }) async {
    try {
      final response = await apiService.get(
        '${ApiEndpoints.analytics}/trends',
        queryParameters: {
          'period': period,
          if (brandId != null) 'brand_id': brandId,
          if (platform != null) 'platform': platform,
        },
      );

      if (response.data['success'] == true) {
        final items = (response.data['data'] as List?)
                ?.map((item) =>
                    EngagementTrend.fromJson(item as Map<String, dynamic>))
                .toList() ??
            [];
        return Result.success(items);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดสถิติได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get viral posts
  Future<Result<List<ViralPost>>> getViralPosts({
    String period = '7d',
    int? brandId,
    int limit = 5,
  }) async {
    try {
      final response = await apiService.get(
        '${ApiEndpoints.analytics}/viral',
        queryParameters: {
          'period': period,
          if (brandId != null) 'brand_id': brandId,
          'limit': limit,
        },
      );

      if (response.data['success'] == true) {
        final items = (response.data['data'] as List?)
                ?.map(
                    (item) => ViralPost.fromJson(item as Map<String, dynamic>))
                .toList() ??
            [];
        return Result.success(items);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดสถิติได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Export analytics report
  Future<Result<String>> exportReport({
    String period = '7d',
    int? brandId,
    String format = 'pdf',
  }) async {
    try {
      final response = await apiService.post(
        ApiEndpoints.analyticsExport,
        data: {
          'period': period,
          if (brandId != null) 'brand_id': brandId,
          'format': format,
        },
      );

      if (response.data['success'] == true) {
        return Result.success(response.data['data']['download_url']);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถส่งออกรายงานได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }
}
