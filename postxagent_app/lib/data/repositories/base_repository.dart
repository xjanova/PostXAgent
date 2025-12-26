import '../services/api_service.dart';

/// Base repository class with common functionality
abstract class BaseRepository {
  final ApiService apiService;

  BaseRepository(this.apiService);

  /// Parse paginated response
  PaginatedResponse<T> parsePaginatedResponse<T>(
    Map<String, dynamic> data,
    T Function(Map<String, dynamic>) fromJson,
  ) {
    final items = (data['data'] as List?)
            ?.map((item) => fromJson(item as Map<String, dynamic>))
            .toList() ??
        [];

    final meta = data['meta'] as Map<String, dynamic>?;

    return PaginatedResponse(
      items: items,
      currentPage: meta?['current_page'] ?? 1,
      lastPage: meta?['last_page'] ?? 1,
      perPage: meta?['per_page'] ?? 20,
      total: meta?['total'] ?? items.length,
    );
  }
}

/// Generic paginated response
class PaginatedResponse<T> {
  final List<T> items;
  final int currentPage;
  final int lastPage;
  final int perPage;
  final int total;

  const PaginatedResponse({
    required this.items,
    required this.currentPage,
    required this.lastPage,
    required this.perPage,
    required this.total,
  });

  bool get hasMore => currentPage < lastPage;
}

/// Result wrapper for API calls
class Result<T> {
  final T? data;
  final String? error;
  final bool success;

  const Result._({this.data, this.error, required this.success});

  factory Result.success(T data) => Result._(data: data, success: true);
  factory Result.failure(String error) => Result._(error: error, success: false);

  bool get isSuccess => success && data != null;
  bool get isFailure => !success;
}
