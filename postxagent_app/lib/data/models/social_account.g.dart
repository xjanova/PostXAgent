// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'social_account.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

SocialAccount _$SocialAccountFromJson(Map<String, dynamic> json) =>
    SocialAccount(
      id: (json['id'] as num).toInt(),
      userId: (json['user_id'] as num).toInt(),
      brandId: (json['brand_id'] as num?)?.toInt(),
      platform: json['platform'] as String,
      platformUserId: json['platform_user_id'] as String?,
      platformUsername: json['platform_username'] as String?,
      displayName: json['display_name'] as String?,
      profileUrl: json['profile_url'] as String?,
      avatarUrl: json['avatar_url'] as String?,
      tokenExpiresAt: json['token_expires_at'] == null
          ? null
          : DateTime.parse(json['token_expires_at'] as String),
      metadata: json['metadata'] as Map<String, dynamic>?,
      isActive: json['is_active'] as bool? ?? true,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      postsCount: (json['posts_count'] as num?)?.toInt(),
      followersCount: (json['followers_count'] as num?)?.toInt(),
    );

Map<String, dynamic> _$SocialAccountToJson(SocialAccount instance) =>
    <String, dynamic>{
      'id': instance.id,
      'user_id': instance.userId,
      'brand_id': instance.brandId,
      'platform': instance.platform,
      'platform_user_id': instance.platformUserId,
      'platform_username': instance.platformUsername,
      'display_name': instance.displayName,
      'profile_url': instance.profileUrl,
      'avatar_url': instance.avatarUrl,
      'token_expires_at': instance.tokenExpiresAt?.toIso8601String(),
      'metadata': instance.metadata,
      'is_active': instance.isActive,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
      'posts_count': instance.postsCount,
      'followers_count': instance.followersCount,
    };

AccountPool _$AccountPoolFromJson(Map<String, dynamic> json) => AccountPool(
      id: (json['id'] as num).toInt(),
      brandId: (json['brand_id'] as num).toInt(),
      platform: json['platform'] as String,
      name: json['name'] as String,
      description: json['description'] as String?,
      rotationStrategy: json['rotation_strategy'] as String? ?? 'round_robin',
      cooldownMinutes: (json['cooldown_minutes'] as num?)?.toInt() ?? 30,
      maxPostsPerDay: (json['max_posts_per_day'] as num?)?.toInt() ?? 10,
      autoFailover: json['auto_failover'] as bool? ?? true,
      isActive: json['is_active'] as bool? ?? true,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      membersCount: (json['members_count'] as num?)?.toInt(),
      activeMembersCount: (json['active_members_count'] as num?)?.toInt(),
    );

Map<String, dynamic> _$AccountPoolToJson(AccountPool instance) =>
    <String, dynamic>{
      'id': instance.id,
      'brand_id': instance.brandId,
      'platform': instance.platform,
      'name': instance.name,
      'description': instance.description,
      'rotation_strategy': instance.rotationStrategy,
      'cooldown_minutes': instance.cooldownMinutes,
      'max_posts_per_day': instance.maxPostsPerDay,
      'auto_failover': instance.autoFailover,
      'is_active': instance.isActive,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
      'members_count': instance.membersCount,
      'active_members_count': instance.activeMembersCount,
    };

AccountPoolMember _$AccountPoolMemberFromJson(Map<String, dynamic> json) =>
    AccountPoolMember(
      id: (json['id'] as num).toInt(),
      accountPoolId: (json['account_pool_id'] as num).toInt(),
      socialAccountId: (json['social_account_id'] as num).toInt(),
      priority: (json['priority'] as num?)?.toInt() ?? 0,
      weight: (json['weight'] as num?)?.toInt() ?? 1,
      status: json['status'] as String? ?? 'active',
      cooldownUntil: json['cooldown_until'] == null
          ? null
          : DateTime.parse(json['cooldown_until'] as String),
      lastUsedAt: json['last_used_at'] == null
          ? null
          : DateTime.parse(json['last_used_at'] as String),
      postsToday: (json['posts_today'] as num?)?.toInt() ?? 0,
      totalPosts: (json['total_posts'] as num?)?.toInt() ?? 0,
      successCount: (json['success_count'] as num?)?.toInt() ?? 0,
      failureCount: (json['failure_count'] as num?)?.toInt() ?? 0,
      consecutiveFailures: (json['consecutive_failures'] as num?)?.toInt() ?? 0,
      lastError: json['last_error'] as String?,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      socialAccount: json['social_account'] == null
          ? null
          : SocialAccount.fromJson(
              json['social_account'] as Map<String, dynamic>),
    );

Map<String, dynamic> _$AccountPoolMemberToJson(AccountPoolMember instance) =>
    <String, dynamic>{
      'id': instance.id,
      'account_pool_id': instance.accountPoolId,
      'social_account_id': instance.socialAccountId,
      'priority': instance.priority,
      'weight': instance.weight,
      'status': instance.status,
      'cooldown_until': instance.cooldownUntil?.toIso8601String(),
      'last_used_at': instance.lastUsedAt?.toIso8601String(),
      'posts_today': instance.postsToday,
      'total_posts': instance.totalPosts,
      'success_count': instance.successCount,
      'failure_count': instance.failureCount,
      'consecutive_failures': instance.consecutiveFailures,
      'last_error': instance.lastError,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
      'social_account': instance.socialAccount,
    };
