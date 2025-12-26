import 'dart:io';
import 'package:dio/dio.dart';
import 'package:flutter/foundation.dart';
import '../models/api_response.dart';
import '../../core/constants/app_constants.dart';

/// Main API Service for communicating with Laravel backend
class ApiService {
  late final Dio _dio;
  String? _authToken;

  ApiService({String? baseUrl}) {
    _dio = Dio(
      BaseOptions(
        baseUrl: baseUrl ?? AppConstants.baseUrl,
        connectTimeout: AppConstants.apiTimeout,
        receiveTimeout: AppConstants.apiTimeout,
        sendTimeout: AppConstants.apiTimeout,
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json',
        },
      ),
    );

    // Add interceptors
    _dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) {
          // Add auth token if available
          if (_authToken != null) {
            options.headers['Authorization'] = 'Bearer $_authToken';
          }
          if (kDebugMode) {
            print('API Request: ${options.method} ${options.path}');
          }
          return handler.next(options);
        },
        onResponse: (response, handler) {
          if (kDebugMode) {
            print('API Response: ${response.statusCode} ${response.requestOptions.path}');
          }
          return handler.next(response);
        },
        onError: (error, handler) {
          if (kDebugMode) {
            print('API Error: ${error.response?.statusCode} ${error.message}');
          }
          return handler.next(error);
        },
      ),
    );
  }

  /// Set authentication token
  void setAuthToken(String? token) {
    _authToken = token;
  }

  /// Clear authentication token
  void clearAuthToken() {
    _authToken = null;
  }

  /// Update base URL
  void updateBaseUrl(String baseUrl) {
    _dio.options.baseUrl = baseUrl;
  }

  /// GET request
  Future<Response<T>> get<T>(
    String path, {
    Map<String, dynamic>? queryParameters,
    Options? options,
    CancelToken? cancelToken,
  }) async {
    try {
      return await _dio.get<T>(
        path,
        queryParameters: queryParameters,
        options: options,
        cancelToken: cancelToken,
      );
    } on DioException catch (e) {
      throw _handleError(e);
    }
  }

  /// POST request
  Future<Response<T>> post<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
    CancelToken? cancelToken,
  }) async {
    try {
      return await _dio.post<T>(
        path,
        data: data,
        queryParameters: queryParameters,
        options: options,
        cancelToken: cancelToken,
      );
    } on DioException catch (e) {
      throw _handleError(e);
    }
  }

  /// PUT request
  Future<Response<T>> put<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
    CancelToken? cancelToken,
  }) async {
    try {
      return await _dio.put<T>(
        path,
        data: data,
        queryParameters: queryParameters,
        options: options,
        cancelToken: cancelToken,
      );
    } on DioException catch (e) {
      throw _handleError(e);
    }
  }

  /// DELETE request
  Future<Response<T>> delete<T>(
    String path, {
    dynamic data,
    Map<String, dynamic>? queryParameters,
    Options? options,
    CancelToken? cancelToken,
  }) async {
    try {
      return await _dio.delete<T>(
        path,
        data: data,
        queryParameters: queryParameters,
        options: options,
        cancelToken: cancelToken,
      );
    } on DioException catch (e) {
      throw _handleError(e);
    }
  }

  /// Upload file
  Future<Response<T>> uploadFile<T>(
    String path, {
    required File file,
    required String fieldName,
    Map<String, dynamic>? data,
    CancelToken? cancelToken,
    void Function(int, int)? onSendProgress,
  }) async {
    try {
      final fileName = file.path.split('/').last;
      final formData = FormData.fromMap({
        fieldName: await MultipartFile.fromFile(
          file.path,
          filename: fileName,
        ),
        if (data != null) ...data,
      });

      return await _dio.post<T>(
        path,
        data: formData,
        cancelToken: cancelToken,
        onSendProgress: onSendProgress,
        options: Options(
          headers: {
            'Content-Type': 'multipart/form-data',
          },
        ),
      );
    } on DioException catch (e) {
      throw _handleError(e);
    }
  }

  /// Handle Dio errors
  Exception _handleError(DioException error) {
    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
        return const TimeoutError();

      case DioExceptionType.connectionError:
        return const NetworkError();

      case DioExceptionType.badResponse:
        final response = error.response;
        if (response != null) {
          final data = response.data;
          String message = 'เกิดข้อผิดพลาด';
          List<String>? errors;
          Map<String, List<String>>? validationErrors;

          if (data is Map<String, dynamic>) {
            message = data['message'] ?? data['error'] ?? message;

            if (data['errors'] is List) {
              errors = List<String>.from(data['errors']);
            } else if (data['errors'] is Map) {
              validationErrors = (data['errors'] as Map<String, dynamic>).map(
                (key, value) => MapEntry(
                  key,
                  value is List
                      ? List<String>.from(value)
                      : [value.toString()],
                ),
              );
            }
          }

          return ApiError(
            message: message,
            statusCode: response.statusCode,
            data: data is Map<String, dynamic> ? data : null,
            errors: errors,
            validationErrors: validationErrors,
          );
        }
        return ApiError(
          message: error.message ?? 'เกิดข้อผิดพลาด',
          statusCode: error.response?.statusCode,
        );

      case DioExceptionType.cancel:
        return const ApiError(message: 'คำขอถูกยกเลิก');

      default:
        return ApiError(
          message: error.message ?? 'เกิดข้อผิดพลาดที่ไม่ทราบสาเหตุ',
        );
    }
  }
}

/// API Endpoints
class ApiEndpoints {
  ApiEndpoints._();

  // Auth
  static const String login = '/auth/login';
  static const String register = '/auth/register';
  static const String logout = '/auth/logout';
  static const String me = '/auth/me';
  static const String updateProfile = '/auth/profile';
  static const String updatePassword = '/auth/password';
  static const String forgotPassword = '/auth/forgot-password';
  static const String resetPassword = '/auth/reset-password';

  // Brands
  static const String brands = '/brands';
  static String brand(int id) => '/brands/$id';

  // Posts
  static const String posts = '/posts';
  static String post(int id) => '/posts/$id';
  static String postMetrics(int id) => '/posts/$id/metrics';
  static String publishPost(int id) => '/posts/$id/publish';
  static const String generateContent = '/posts/generate-content';
  static const String generateImage = '/posts/generate-image';
  static const String bulkDeletePosts = '/posts/bulk';
  static const String bulkPublishPosts = '/posts/bulk/publish';
  static const String bulkSchedulePosts = '/posts/bulk/schedule';

  // Campaigns
  static const String campaigns = '/campaigns';
  static String campaign(int id) => '/campaigns/$id';
  static String startCampaign(int id) => '/campaigns/$id/start';
  static String pauseCampaign(int id) => '/campaigns/$id/pause';
  static String stopCampaign(int id) => '/campaigns/$id/stop';

  // Social Accounts
  static const String socialAccounts = '/social-accounts';
  static String connectAccount(String platform) => '/social-accounts/$platform/connect';
  static String disconnectAccount(int id) => '/social-accounts/$id';
  static String refreshToken(int id) => '/social-accounts/$id/refresh';

  // Account Pools
  static const String accountPools = '/account-pools';
  static String accountPool(int id) => '/account-pools/$id';
  static const String poolHealthReport = '/account-pools/health-report';
  static String poolStatistics(int id) => '/account-pools/$id/statistics';
  static String poolLogs(int id) => '/account-pools/$id/logs';
  static String poolNextAccount(int id) => '/account-pools/$id/next-account';
  static String poolAccounts(int id) => '/account-pools/$id/accounts';

  // Analytics
  static const String analyticsOverview = '/analytics/overview';
  static const String analyticsPosts = '/analytics/posts';
  static const String analyticsEngagement = '/analytics/engagement';
  static const String analyticsPlatforms = '/analytics/platforms';
  static const String analyticsSummary = '/analytics/summary';
  static const String analyticsByPlatform = '/analytics/by-platform';
  static const String analytics = '/analytics';
  static const String analyticsExport = '/analytics/export';
  static const String postsGenerate = '/posts/generate';
  static String analyticsBrand(int id) => '/analytics/brands/$id';

  // Rentals
  static const String rentalStatus = '/rentals/status';
  static const String rentalHistory = '/rentals/history';
  static const String rentalPackages = '/rentals/packages';
  static String rentalPackage(int id) => '/rentals/packages/$id';
  static const String rentalCheckout = '/rentals/checkout';
  static const String validatePromo = '/rentals/validate-promo';
  static String cancelRental(int id) => '/rentals/$id/cancel';
  static const String paymentMethods = '/rentals/payment-methods';
  static const String rentalInvoices = '/rentals/invoices';

  // Subscriptions
  static const String subscriptionStatus = '/subscription/status';
  static const String subscriptionCheckout = '/subscription/checkout';
  static const String subscriptionCancel = '/subscription/cancel';
  static const String subscriptionInvoices = '/subscription/invoices';

  // Notifications
  static const String notifications = '/notifications';
  static const String unreadCount = '/notifications/unread-count';
  static String readNotification(String id) => '/notifications/$id/read';
  static const String readAllNotifications = '/notifications/read-all';
  static String deleteNotification(String id) => '/notifications/$id';
  static const String notificationPreferences = '/notifications/preferences';

  // Export
  static const String exportPosts = '/export/posts';
  static const String exportCampaigns = '/export/campaigns';
  static const String exportAnalytics = '/export/analytics';

  // AI Manager Status (Public)
  static const String aiManagerStatus = '/ai-manager/status';
  static const String aiManagerStats = '/ai-manager/stats';
  static const String aiManagerWorkers = '/ai-manager/workers';
}
