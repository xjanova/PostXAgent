import 'package:json_annotation/json_annotation.dart';

part 'user.g.dart';

@JsonSerializable()
class User {
  final int id;
  final String name;
  final String email;
  final String? phone;
  @JsonKey(name: 'company_name')
  final String? companyName;
  @JsonKey(name: 'avatar_url')
  final String? avatarUrl;
  final String? timezone;
  final String language;
  @JsonKey(name: 'is_active')
  final bool isActive;
  @JsonKey(name: 'email_verified_at')
  final DateTime? emailVerifiedAt;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  // Computed from rental
  @JsonKey(name: 'has_active_rental')
  final bool? hasActiveRental;
  @JsonKey(name: 'current_package')
  final String? currentPackage;

  const User({
    required this.id,
    required this.name,
    required this.email,
    this.phone,
    this.companyName,
    this.avatarUrl,
    this.timezone,
    this.language = 'th',
    this.isActive = true,
    this.emailVerifiedAt,
    required this.createdAt,
    required this.updatedAt,
    this.hasActiveRental,
    this.currentPackage,
  });

  factory User.fromJson(Map<String, dynamic> json) => _$UserFromJson(json);
  Map<String, dynamic> toJson() => _$UserToJson(this);

  User copyWith({
    int? id,
    String? name,
    String? email,
    String? phone,
    String? companyName,
    String? avatarUrl,
    String? timezone,
    String? language,
    bool? isActive,
    DateTime? emailVerifiedAt,
    DateTime? createdAt,
    DateTime? updatedAt,
    bool? hasActiveRental,
    String? currentPackage,
  }) {
    return User(
      id: id ?? this.id,
      name: name ?? this.name,
      email: email ?? this.email,
      phone: phone ?? this.phone,
      companyName: companyName ?? this.companyName,
      avatarUrl: avatarUrl ?? this.avatarUrl,
      timezone: timezone ?? this.timezone,
      language: language ?? this.language,
      isActive: isActive ?? this.isActive,
      emailVerifiedAt: emailVerifiedAt ?? this.emailVerifiedAt,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      hasActiveRental: hasActiveRental ?? this.hasActiveRental,
      currentPackage: currentPackage ?? this.currentPackage,
    );
  }

  String get initials {
    final parts = name.split(' ');
    if (parts.length >= 2) {
      return '${parts[0][0]}${parts[1][0]}'.toUpperCase();
    }
    return name.substring(0, name.length >= 2 ? 2 : 1).toUpperCase();
  }

  bool get isVerified => emailVerifiedAt != null;
}

@JsonSerializable()
class AuthResponse {
  final bool success;
  final String? message;
  final AuthData? data;
  final List<String>? errors;

  const AuthResponse({
    required this.success,
    this.message,
    this.data,
    this.errors,
  });

  factory AuthResponse.fromJson(Map<String, dynamic> json) =>
      _$AuthResponseFromJson(json);
  Map<String, dynamic> toJson() => _$AuthResponseToJson(this);
}

@JsonSerializable()
class AuthData {
  final User user;
  final String token;
  @JsonKey(name: 'token_type')
  final String tokenType;

  const AuthData({
    required this.user,
    required this.token,
    this.tokenType = 'Bearer',
  });

  factory AuthData.fromJson(Map<String, dynamic> json) =>
      _$AuthDataFromJson(json);
  Map<String, dynamic> toJson() => _$AuthDataToJson(this);
}

@JsonSerializable()
class LoginRequest {
  final String email;
  final String password;
  @JsonKey(name: 'remember_me')
  final bool rememberMe;

  const LoginRequest({
    required this.email,
    required this.password,
    this.rememberMe = false,
  });

  factory LoginRequest.fromJson(Map<String, dynamic> json) =>
      _$LoginRequestFromJson(json);
  Map<String, dynamic> toJson() => _$LoginRequestToJson(this);
}

@JsonSerializable()
class RegisterRequest {
  final String name;
  final String email;
  final String password;
  @JsonKey(name: 'password_confirmation')
  final String passwordConfirmation;
  final String? phone;
  @JsonKey(name: 'company_name')
  final String? companyName;

  const RegisterRequest({
    required this.name,
    required this.email,
    required this.password,
    required this.passwordConfirmation,
    this.phone,
    this.companyName,
  });

  factory RegisterRequest.fromJson(Map<String, dynamic> json) =>
      _$RegisterRequestFromJson(json);
  Map<String, dynamic> toJson() => _$RegisterRequestToJson(this);
}
