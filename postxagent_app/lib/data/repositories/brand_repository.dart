import 'dart:io';

import '../models/brand.dart';
import '../services/api_service.dart';
import 'base_repository.dart';

class BrandRepository extends BaseRepository {
  BrandRepository(super.apiService);

  /// Get all brands with pagination
  Future<Result<PaginatedResponse<Brand>>> getBrands({
    int page = 1,
    int perPage = 20,
    String? search,
    bool? isActive,
    String? sortBy,
    String? sortOrder,
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'page': page,
        'per_page': perPage,
        if (search != null && search.isNotEmpty) 'search': search,
        if (isActive != null) 'is_active': isActive,
        if (sortBy != null) 'sort_by': sortBy,
        if (sortOrder != null) 'sort_order': sortOrder,
      };

      final response = await apiService.get(
        ApiEndpoints.brands,
        queryParameters: queryParams,
      );

      if (response.data['success'] == true) {
        final paginatedResponse = parsePaginatedResponse<Brand>(
          response.data,
          (json) => Brand.fromJson(json),
        );
        return Result.success(paginatedResponse);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดแบรนด์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get all brands (no pagination - for dropdowns)
  Future<Result<List<Brand>>> getAllBrands() async {
    try {
      final response = await apiService.get(
        ApiEndpoints.brands,
        queryParameters: {'per_page': 100},
      );

      if (response.data['success'] == true) {
        final items = (response.data['data'] as List?)
                ?.map((item) => Brand.fromJson(item as Map<String, dynamic>))
                .toList() ??
            [];
        return Result.success(items);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดแบรนด์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get single brand by ID
  Future<Result<Brand>> getBrand(int id) async {
    try {
      final response = await apiService.get('${ApiEndpoints.brands}/$id');

      if (response.data['success'] == true) {
        return Result.success(Brand.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่พบแบรนด์');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Create new brand
  Future<Result<Brand>> createBrand(BrandRequest request) async {
    try {
      final response = await apiService.post(
        ApiEndpoints.brands,
        data: request.toJson(),
      );

      if (response.data['success'] == true) {
        return Result.success(Brand.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถสร้างแบรนด์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Update brand
  Future<Result<Brand>> updateBrand(int id, BrandRequest request) async {
    try {
      final response = await apiService.put(
        '${ApiEndpoints.brands}/$id',
        data: request.toJson(),
      );

      if (response.data['success'] == true) {
        return Result.success(Brand.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถอัพเดทแบรนด์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Delete brand
  Future<Result<bool>> deleteBrand(int id) async {
    try {
      final response = await apiService.delete('${ApiEndpoints.brands}/$id');

      if (response.data['success'] == true) {
        return Result.success(true);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถลบแบรนด์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Toggle brand active status
  Future<Result<Brand>> toggleBrandStatus(int id) async {
    try {
      final response = await apiService.post('${ApiEndpoints.brands}/$id/toggle');

      if (response.data['success'] == true) {
        return Result.success(Brand.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถเปลี่ยนสถานะได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Upload brand logo
  Future<Result<String>> uploadLogo(int brandId, File file) async {
    try {
      final response = await apiService.uploadFile(
        '${ApiEndpoints.brands}/$brandId/logo',
        file: file,
        fieldName: 'logo',
      );

      if (response.data['success'] == true) {
        return Result.success(response.data['data']['logo_url']);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถอัพโหลดโลโก้ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }
}
