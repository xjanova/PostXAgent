import '../models/post.dart';
import '../services/api_service.dart';
import 'base_repository.dart';

class PostRepository extends BaseRepository {
  PostRepository(super.apiService);

  /// Get all posts with pagination and filters
  Future<Result<PaginatedResponse<Post>>> getPosts({
    int page = 1,
    int perPage = 20,
    String? status,
    String? platform,
    int? brandId,
    int? campaignId,
    String? search,
    String? sortBy,
    String? sortOrder,
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'page': page,
        'per_page': perPage,
        if (status != null) 'status': status,
        if (platform != null) 'platform': platform,
        if (brandId != null) 'brand_id': brandId,
        if (campaignId != null) 'campaign_id': campaignId,
        if (search != null && search.isNotEmpty) 'search': search,
        if (sortBy != null) 'sort_by': sortBy,
        if (sortOrder != null) 'sort_order': sortOrder,
      };

      final response = await apiService.get(
        ApiEndpoints.posts,
        queryParameters: queryParams,
      );

      if (response.data['success'] == true) {
        final paginatedResponse = parsePaginatedResponse<Post>(
          response.data,
          (json) => Post.fromJson(json),
        );
        return Result.success(paginatedResponse);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดโพสต์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get single post by ID
  Future<Result<Post>> getPost(int id) async {
    try {
      final response = await apiService.get('${ApiEndpoints.posts}/$id');

      if (response.data['success'] == true) {
        return Result.success(Post.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่พบโพสต์');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Create new post
  Future<Result<Post>> createPost(CreatePostRequest request) async {
    try {
      final response = await apiService.post(
        ApiEndpoints.posts,
        data: request.toJson(),
      );

      if (response.data['success'] == true) {
        return Result.success(Post.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถสร้างโพสต์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Update post
  Future<Result<Post>> updatePost(int id, Map<String, dynamic> data) async {
    try {
      final response = await apiService.put(
        '${ApiEndpoints.posts}/$id',
        data: data,
      );

      if (response.data['success'] == true) {
        return Result.success(Post.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถอัพเดทโพสต์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Delete post
  Future<Result<bool>> deletePost(int id) async {
    try {
      final response = await apiService.delete('${ApiEndpoints.posts}/$id');

      if (response.data['success'] == true) {
        return Result.success(true);
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถลบโพสต์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Publish post immediately
  Future<Result<Post>> publishPost(int id) async {
    try {
      final response = await apiService.post('${ApiEndpoints.posts}/$id/publish');

      if (response.data['success'] == true) {
        return Result.success(Post.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโพสต์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Schedule post
  Future<Result<Post>> schedulePost(int id, DateTime scheduledAt) async {
    try {
      final response = await apiService.post(
        '${ApiEndpoints.posts}/$id/schedule',
        data: {'scheduled_at': scheduledAt.toIso8601String()},
      );

      if (response.data['success'] == true) {
        return Result.success(Post.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถตั้งเวลาโพสต์ได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Generate AI content
  Future<Result<GenerateContentResponse>> generateContent(
    GenerateContentRequest request,
  ) async {
    try {
      final response = await apiService.post(
        ApiEndpoints.postsGenerate,
        data: request.toJson(),
      );

      if (response.data['success'] == true) {
        return Result.success(GenerateContentResponse.fromJson(response.data['data']));
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถสร้างเนื้อหาได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }

  /// Get post statistics
  Future<Result<Map<String, int>>> getPostStats() async {
    try {
      final response = await apiService.get('${ApiEndpoints.posts}/stats');

      if (response.data['success'] == true) {
        final data = response.data['data'] as Map<String, dynamic>;
        return Result.success({
          'total': data['total'] ?? 0,
          'draft': data['draft'] ?? 0,
          'scheduled': data['scheduled'] ?? 0,
          'published': data['published'] ?? 0,
          'failed': data['failed'] ?? 0,
          'viral': data['viral'] ?? 0,
        });
      }

      return Result.failure(response.data['message'] ?? 'ไม่สามารถโหลดสถิติได้');
    } catch (e) {
      return Result.failure(e.toString());
    }
  }
}
