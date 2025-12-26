import '../models/social_account.dart';
import '../services/api_service.dart';
import 'base_repository.dart';

class SocialAccountRepository extends BaseRepository {
  SocialAccountRepository(super.apiService);

  /// Get all social accounts
  Future<Result<List<SocialAccount>>> getSocialAccounts({
    int? brandId,
    String? platform,
    String? status,
  }) async {
    try {
      final queryParams = <String, dynamic>{
        if (brandId != null) 'brand_id': brandId,
        if (platform != null) 'platform': platform,
        if (status != null) 'status': status,
      };

      final response = await apiService.get(
        ApiEndpoints.socialAccounts,
        queryParameters: queryParams,
      );

      if (response.data['success'] == true) {
        final items = (response.data['data'] as List?)
                ?.map((item) => SocialAccount.fromJson(item as Map<String, dynamic>))
                .toList() ??
            [];
        return Result.success(items);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดบัญชีได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get single social account
  Future<Result<SocialAccount>> getSocialAccount(int id) async {
    try {
      final response = await apiService.get('${ApiEndpoints.socialAccounts}/$id');

      if (response.data['success'] == true) {
        return Result.success(SocialAccount.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่พบบัญชี');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Connect social account (initiate OAuth flow)
  Future<Result<String>> connectAccount(String platform, int? brandId) async {
    try {
      final response = await apiService.post(
        '${ApiEndpoints.socialAccounts}/connect',
        data: {
          'platform': platform,
          if (brandId != null) 'brand_id': brandId,
        },
      );

      if (response.data['success'] == true) {
        // Returns OAuth URL to redirect user
        return Result.success(response.data['data']['auth_url']);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถเชื่อมต่อได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Disconnect social account
  Future<Result<bool>> disconnectAccount(int id) async {
    try {
      final response = await apiService.post(
        '${ApiEndpoints.socialAccounts}/$id/disconnect',
      );

      if (response.data['success'] == true) {
        return Result.success(true);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถยกเลิกการเชื่อมต่อได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Refresh account token
  Future<Result<SocialAccount>> refreshAccountToken(int id) async {
    try {
      final response = await apiService.post(
        '${ApiEndpoints.socialAccounts}/$id/refresh',
      );

      if (response.data['success'] == true) {
        return Result.success(SocialAccount.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถรีเฟรชโทเค็นได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Sync account data from platform
  Future<Result<SocialAccount>> syncAccount(int id) async {
    try {
      final response = await apiService.post(
        '${ApiEndpoints.socialAccounts}/$id/sync',
      );

      if (response.data['success'] == true) {
        return Result.success(SocialAccount.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถซิงค์ข้อมูลได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get account analytics
  Future<Result<Map<String, dynamic>>> getAccountAnalytics(
    int id, {
    String period = '7d',
  }) async {
    try {
      final response = await apiService.get(
        '${ApiEndpoints.socialAccounts}/$id/analytics',
        queryParameters: {'period': period},
      );

      if (response.data['success'] == true) {
        return Result.success(response.data['data'] as Map<String, dynamic>);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดสถิติได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }
}
