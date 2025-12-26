import 'package:json_annotation/json_annotation.dart';

part 'api_response.g.dart';

/// Generic API Response wrapper
@JsonSerializable(genericArgumentFactories: true)
class ApiResponse<T> {
  final bool success;
  final T? data;
  final String? message;
  final List<String>? errors;
  final Map<String, List<String>>? validationErrors;

  const ApiResponse({
    required this.success,
    this.data,
    this.message,
    this.errors,
    this.validationErrors,
  });

  factory ApiResponse.fromJson(
    Map<String, dynamic> json,
    T Function(Object? json) fromJsonT,
  ) =>
      _$ApiResponseFromJson(json, fromJsonT);

  Map<String, dynamic> toJson(Object Function(T value) toJsonT) =>
      _$ApiResponseToJson(this, toJsonT);

  bool get hasError => !success || errors != null || validationErrors != null;

  String get errorMessage {
    if (message != null) return message!;
    if (errors != null && errors!.isNotEmpty) return errors!.first;
    if (validationErrors != null && validationErrors!.isNotEmpty) {
      return validationErrors!.values.first.first;
    }
    return 'เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง';
  }
}

/// Paginated API Response
@JsonSerializable(genericArgumentFactories: true)
class PaginatedResponse<T> {
  final List<T> data;
  @JsonKey(name: 'current_page')
  final int currentPage;
  @JsonKey(name: 'last_page')
  final int lastPage;
  @JsonKey(name: 'per_page')
  final int perPage;
  final int total;
  @JsonKey(name: 'from')
  final int? from;
  @JsonKey(name: 'to')
  final int? to;

  const PaginatedResponse({
    required this.data,
    required this.currentPage,
    required this.lastPage,
    required this.perPage,
    required this.total,
    this.from,
    this.to,
  });

  factory PaginatedResponse.fromJson(
    Map<String, dynamic> json,
    T Function(Object? json) fromJsonT,
  ) =>
      _$PaginatedResponseFromJson(json, fromJsonT);

  Map<String, dynamic> toJson(Object Function(T value) toJsonT) =>
      _$PaginatedResponseToJson(this, toJsonT);

  bool get hasNextPage => currentPage < lastPage;
  bool get hasPreviousPage => currentPage > 1;
  bool get isEmpty => data.isEmpty;
  int get length => data.length;
}

/// Simple success response
@JsonSerializable()
class SuccessResponse {
  final bool success;
  final String? message;

  const SuccessResponse({
    required this.success,
    this.message,
  });

  factory SuccessResponse.fromJson(Map<String, dynamic> json) =>
      _$SuccessResponseFromJson(json);
  Map<String, dynamic> toJson() => _$SuccessResponseToJson(this);
}

/// API Error
class ApiError implements Exception {
  final String message;
  final int? statusCode;
  final Map<String, dynamic>? data;
  final List<String>? errors;
  final Map<String, List<String>>? validationErrors;

  const ApiError({
    required this.message,
    this.statusCode,
    this.data,
    this.errors,
    this.validationErrors,
  });

  @override
  String toString() => message;

  bool get isUnauthorized => statusCode == 401;
  bool get isForbidden => statusCode == 403;
  bool get isNotFound => statusCode == 404;
  bool get isValidationError => statusCode == 422;
  bool get isServerError => statusCode != null && statusCode! >= 500;

  String get displayMessage {
    if (validationErrors != null && validationErrors!.isNotEmpty) {
      return validationErrors!.values.first.first;
    }
    if (errors != null && errors!.isNotEmpty) {
      return errors!.first;
    }
    return message;
  }
}

/// Network error
class NetworkError implements Exception {
  final String message;

  const NetworkError([this.message = 'ไม่สามารถเชื่อมต่อเครือข่ายได้']);

  @override
  String toString() => message;
}

/// Timeout error
class TimeoutError implements Exception {
  final String message;

  const TimeoutError([this.message = 'การเชื่อมต่อหมดเวลา กรุณาลองใหม่']);

  @override
  String toString() => message;
}
