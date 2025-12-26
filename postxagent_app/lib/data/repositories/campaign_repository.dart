import '../models/campaign.dart';
import '../services/api_service.dart';
import 'base_repository.dart';

class CampaignRepository extends BaseRepository {
  CampaignRepository(super.apiService);

  /// Get all campaigns with pagination
  Future<Result<PaginatedResponse<Campaign>>> getCampaigns({
    int page = 1,
    int perPage = 20,
    String? status,
    int? brandId,
    String? search,
    String? sortBy,
    String? sortOrder,
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'page': page,
        'per_page': perPage,
        if (status != null) 'status': status,
        if (brandId != null) 'brand_id': brandId,
        if (search != null && search.isNotEmpty) 'search': search,
        if (sortBy != null) 'sort_by': sortBy,
        if (sortOrder != null) 'sort_order': sortOrder,
      };

      final response = await apiService.get(
        ApiEndpoints.campaigns,
        queryParameters: queryParams,
      );

      if (response.data['success'] == true) {
        final paginatedResponse = parsePaginatedResponse<Campaign>(
          response.data,
          (json) => Campaign.fromJson(json),
        );
        return Result.success(paginatedResponse);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดแคมเปญได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get single campaign by ID
  Future<Result<Campaign>> getCampaign(int id) async {
    try {
      final response = await apiService.get('${ApiEndpoints.campaigns}/$id');

      if (response.data['success'] == true) {
        return Result.success(Campaign.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่พบแคมเปญ');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Create new campaign
  Future<Result<Campaign>> createCampaign(CreateCampaignRequest request) async {
    try {
      final response = await apiService.post(
        ApiEndpoints.campaigns,
        data: request.toJson(),
      );

      if (response.data['success'] == true) {
        return Result.success(Campaign.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถสร้างแคมเปญได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Update campaign
  Future<Result<Campaign>> updateCampaign(
      int id, CreateCampaignRequest request) async {
    try {
      final response = await apiService.put(
        '${ApiEndpoints.campaigns}/$id',
        data: request.toJson(),
      );

      if (response.data['success'] == true) {
        return Result.success(Campaign.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถอัพเดทแคมเปญได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Delete campaign
  Future<Result<bool>> deleteCampaign(int id) async {
    try {
      final response = await apiService.delete('${ApiEndpoints.campaigns}/$id');

      if (response.data['success'] == true) {
        return Result.success(true);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถลบแคมเปญได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Start campaign
  Future<Result<Campaign>> startCampaign(int id) async {
    try {
      final response = await apiService.post('${ApiEndpoints.campaigns}/$id/start');

      if (response.data['success'] == true) {
        return Result.success(Campaign.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถเริ่มแคมเปญได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Pause campaign
  Future<Result<Campaign>> pauseCampaign(int id) async {
    try {
      final response = await apiService.post('${ApiEndpoints.campaigns}/$id/pause');

      if (response.data['success'] == true) {
        return Result.success(Campaign.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถหยุดแคมเปญได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Resume campaign
  Future<Result<Campaign>> resumeCampaign(int id) async {
    try {
      final response =
          await apiService.post('${ApiEndpoints.campaigns}/$id/resume');

      if (response.data['success'] == true) {
        return Result.success(Campaign.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถเริ่มแคมเปญใหม่ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Complete campaign
  Future<Result<Campaign>> completeCampaign(int id) async {
    try {
      final response =
          await apiService.post('${ApiEndpoints.campaigns}/$id/complete');

      if (response.data['success'] == true) {
        return Result.success(Campaign.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถเสร็จสิ้นแคมเปญได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get campaign analytics
  Future<Result<Map<String, dynamic>>> getCampaignAnalytics(int id) async {
    try {
      final response =
          await apiService.get('${ApiEndpoints.campaigns}/$id/analytics');

      if (response.data['success'] == true) {
        return Result.success(response.data['data'] as Map<String, dynamic>);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดสถิติได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }
}
