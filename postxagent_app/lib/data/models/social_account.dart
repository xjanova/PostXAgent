import 'package:json_annotation/json_annotation.dart';

part 'social_account.g.dart';

@JsonSerializable()
class SocialAccount {
  final int id;
  @JsonKey(name: 'user_id')
  final int userId;
  @JsonKey(name: 'brand_id')
  final int? brandId;
  final String platform;
  @JsonKey(name: 'platform_user_id')
  final String? platformUserId;
  @JsonKey(name: 'platform_username')
  final String? platformUsername;
  @JsonKey(name: 'display_name')
  final String? displayName;
  @JsonKey(name: 'profile_url')
  final String? profileUrl;
  @JsonKey(name: 'avatar_url')
  final String? avatarUrl;
  @JsonKey(name: 'token_expires_at')
  final DateTime? tokenExpiresAt;
  final Map<String, dynamic>? metadata;
  @JsonKey(name: 'is_active')
  final bool isActive;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  // Counts
  @JsonKey(name: 'posts_count')
  final int? postsCount;
  @JsonKey(name: 'followers_count')
  final int? followersCount;

  const SocialAccount({
    required this.id,
    required this.userId,
    this.brandId,
    required this.platform,
    this.platformUserId,
    this.platformUsername,
    this.displayName,
    this.profileUrl,
    this.avatarUrl,
    this.tokenExpiresAt,
    this.metadata,
    this.isActive = true,
    required this.createdAt,
    required this.updatedAt,
    this.postsCount,
    this.followersCount,
  });

  factory SocialAccount.fromJson(Map<String, dynamic> json) =>
      _$SocialAccountFromJson(json);
  Map<String, dynamic> toJson() => _$SocialAccountToJson(this);

  SocialAccount copyWith({
    int? id,
    int? userId,
    int? brandId,
    String? platform,
    String? platformUserId,
    String? platformUsername,
    String? displayName,
    String? profileUrl,
    String? avatarUrl,
    DateTime? tokenExpiresAt,
    Map<String, dynamic>? metadata,
    bool? isActive,
    DateTime? createdAt,
    DateTime? updatedAt,
    int? postsCount,
    int? followersCount,
  }) {
    return SocialAccount(
      id: id ?? this.id,
      userId: userId ?? this.userId,
      brandId: brandId ?? this.brandId,
      platform: platform ?? this.platform,
      platformUserId: platformUserId ?? this.platformUserId,
      platformUsername: platformUsername ?? this.platformUsername,
      displayName: displayName ?? this.displayName,
      profileUrl: profileUrl ?? this.profileUrl,
      avatarUrl: avatarUrl ?? this.avatarUrl,
      tokenExpiresAt: tokenExpiresAt ?? this.tokenExpiresAt,
      metadata: metadata ?? this.metadata,
      isActive: isActive ?? this.isActive,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      postsCount: postsCount ?? this.postsCount,
      followersCount: followersCount ?? this.followersCount,
    );
  }

  bool get isTokenExpired {
    if (tokenExpiresAt == null) return false;
    return tokenExpiresAt!.isBefore(DateTime.now());
  }

  bool get needsRefresh {
    if (tokenExpiresAt == null) return false;
    final warningTime = DateTime.now().add(const Duration(days: 7));
    return tokenExpiresAt!.isBefore(warningTime);
  }

  String get name => displayName ?? platformUsername ?? platform;
}

@JsonSerializable()
class AccountPool {
  final int id;
  @JsonKey(name: 'brand_id')
  final int brandId;
  final String platform;
  final String name;
  final String? description;
  @JsonKey(name: 'rotation_strategy')
  final String rotationStrategy;
  @JsonKey(name: 'cooldown_minutes')
  final int cooldownMinutes;
  @JsonKey(name: 'max_posts_per_day')
  final int maxPostsPerDay;
  @JsonKey(name: 'auto_failover')
  final bool autoFailover;
  @JsonKey(name: 'is_active')
  final bool isActive;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  // Counts
  @JsonKey(name: 'members_count')
  final int? membersCount;
  @JsonKey(name: 'active_members_count')
  final int? activeMembersCount;

  const AccountPool({
    required this.id,
    required this.brandId,
    required this.platform,
    required this.name,
    this.description,
    this.rotationStrategy = 'round_robin',
    this.cooldownMinutes = 30,
    this.maxPostsPerDay = 10,
    this.autoFailover = true,
    this.isActive = true,
    required this.createdAt,
    required this.updatedAt,
    this.membersCount,
    this.activeMembersCount,
  });

  factory AccountPool.fromJson(Map<String, dynamic> json) =>
      _$AccountPoolFromJson(json);
  Map<String, dynamic> toJson() => _$AccountPoolToJson(this);

  AccountPool copyWith({
    int? id,
    int? brandId,
    String? platform,
    String? name,
    String? description,
    String? rotationStrategy,
    int? cooldownMinutes,
    int? maxPostsPerDay,
    bool? autoFailover,
    bool? isActive,
    DateTime? createdAt,
    DateTime? updatedAt,
    int? membersCount,
    int? activeMembersCount,
  }) {
    return AccountPool(
      id: id ?? this.id,
      brandId: brandId ?? this.brandId,
      platform: platform ?? this.platform,
      name: name ?? this.name,
      description: description ?? this.description,
      rotationStrategy: rotationStrategy ?? this.rotationStrategy,
      cooldownMinutes: cooldownMinutes ?? this.cooldownMinutes,
      maxPostsPerDay: maxPostsPerDay ?? this.maxPostsPerDay,
      autoFailover: autoFailover ?? this.autoFailover,
      isActive: isActive ?? this.isActive,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      membersCount: membersCount ?? this.membersCount,
      activeMembersCount: activeMembersCount ?? this.activeMembersCount,
    );
  }

  double get healthPercentage {
    if (membersCount == null || membersCount == 0) return 0;
    if (activeMembersCount == null) return 0;
    return (activeMembersCount! / membersCount!) * 100;
  }
}

@JsonSerializable()
class AccountPoolMember {
  final int id;
  @JsonKey(name: 'account_pool_id')
  final int accountPoolId;
  @JsonKey(name: 'social_account_id')
  final int socialAccountId;
  final int priority;
  final int weight;
  final String status;
  @JsonKey(name: 'cooldown_until')
  final DateTime? cooldownUntil;
  @JsonKey(name: 'last_used_at')
  final DateTime? lastUsedAt;
  @JsonKey(name: 'posts_today')
  final int postsToday;
  @JsonKey(name: 'total_posts')
  final int totalPosts;
  @JsonKey(name: 'success_count')
  final int successCount;
  @JsonKey(name: 'failure_count')
  final int failureCount;
  @JsonKey(name: 'consecutive_failures')
  final int consecutiveFailures;
  @JsonKey(name: 'last_error')
  final String? lastError;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  // Relations
  @JsonKey(name: 'social_account')
  final SocialAccount? socialAccount;

  const AccountPoolMember({
    required this.id,
    required this.accountPoolId,
    required this.socialAccountId,
    this.priority = 0,
    this.weight = 1,
    this.status = 'active',
    this.cooldownUntil,
    this.lastUsedAt,
    this.postsToday = 0,
    this.totalPosts = 0,
    this.successCount = 0,
    this.failureCount = 0,
    this.consecutiveFailures = 0,
    this.lastError,
    required this.createdAt,
    required this.updatedAt,
    this.socialAccount,
  });

  factory AccountPoolMember.fromJson(Map<String, dynamic> json) =>
      _$AccountPoolMemberFromJson(json);
  Map<String, dynamic> toJson() => _$AccountPoolMemberToJson(this);

  double get successRate {
    final total = successCount + failureCount;
    if (total == 0) return 100;
    return (successCount / total) * 100;
  }

  bool get isInCooldown {
    if (cooldownUntil == null) return false;
    return cooldownUntil!.isAfter(DateTime.now());
  }
}

/// Rotation Strategies
class RotationStrategies {
  static const Map<String, String> all = {
    'round_robin': 'สลับเรียงลำดับ',
    'random': 'สุ่ม',
    'least_used': 'ใช้น้อยสุดก่อน',
    'priority': 'ตามลำดับความสำคัญ',
  };
}

/// Account Status
class AccountMemberStatus {
  static const String active = 'active';
  static const String cooldown = 'cooldown';
  static const String suspended = 'suspended';
  static const String banned = 'banned';
  static const String error = 'error';

  static const Map<String, String> labels = {
    active: 'ใช้งานได้',
    cooldown: 'พักชั่วคราว',
    suspended: 'ถูกระงับ',
    banned: 'ถูกแบน',
    error: 'มีปัญหา',
  };
}
