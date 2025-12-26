// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'rental.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

RentalPackage _$RentalPackageFromJson(Map<String, dynamic> json) =>
    RentalPackage(
      id: (json['id'] as num).toInt(),
      name: json['name'] as String,
      nameTh: json['name_th'] as String?,
      description: json['description'] as String?,
      descriptionTh: json['description_th'] as String?,
      durationType: json['duration_type'] as String,
      durationValue: (json['duration_value'] as num).toInt(),
      price: (json['price'] as num).toDouble(),
      originalPrice: (json['original_price'] as num?)?.toDouble(),
      currency: json['currency'] as String? ?? 'THB',
      postsLimit: (json['posts_limit'] as num).toInt(),
      brandsLimit: (json['brands_limit'] as num).toInt(),
      platformsLimit: (json['platforms_limit'] as num).toInt(),
      aiGenerationsLimit: (json['ai_generations_limit'] as num).toInt(),
      accountsPerPlatform: (json['accounts_per_platform'] as num?)?.toInt(),
      scheduledPostsLimit: (json['scheduled_posts_limit'] as num?)?.toInt(),
      teamMembersLimit: (json['team_members_limit'] as num?)?.toInt(),
      features: (json['features'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      includedPlatforms: (json['included_platforms'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      isActive: json['is_active'] as bool? ?? true,
      isFeatured: json['is_featured'] as bool? ?? false,
      isPopular: json['is_popular'] as bool? ?? false,
      sortOrder: (json['sort_order'] as num?)?.toInt() ?? 0,
      hasTrial: json['has_trial'] as bool? ?? false,
      trialDays: (json['trial_days'] as num?)?.toInt(),
    );

Map<String, dynamic> _$RentalPackageToJson(RentalPackage instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'name_th': instance.nameTh,
      'description': instance.description,
      'description_th': instance.descriptionTh,
      'duration_type': instance.durationType,
      'duration_value': instance.durationValue,
      'price': instance.price,
      'original_price': instance.originalPrice,
      'currency': instance.currency,
      'posts_limit': instance.postsLimit,
      'brands_limit': instance.brandsLimit,
      'platforms_limit': instance.platformsLimit,
      'ai_generations_limit': instance.aiGenerationsLimit,
      'accounts_per_platform': instance.accountsPerPlatform,
      'scheduled_posts_limit': instance.scheduledPostsLimit,
      'team_members_limit': instance.teamMembersLimit,
      'features': instance.features,
      'included_platforms': instance.includedPlatforms,
      'is_active': instance.isActive,
      'is_featured': instance.isFeatured,
      'is_popular': instance.isPopular,
      'sort_order': instance.sortOrder,
      'has_trial': instance.hasTrial,
      'trial_days': instance.trialDays,
    };

UserRental _$UserRentalFromJson(Map<String, dynamic> json) => UserRental(
      id: (json['id'] as num).toInt(),
      userId: (json['user_id'] as num).toInt(),
      rentalPackageId: (json['rental_package_id'] as num).toInt(),
      startsAt: DateTime.parse(json['starts_at'] as String),
      expiresAt: DateTime.parse(json['expires_at'] as String),
      cancelledAt: json['cancelled_at'] == null
          ? null
          : DateTime.parse(json['cancelled_at'] as String),
      amountPaid: (json['amount_paid'] as num).toDouble(),
      currency: json['currency'] as String? ?? 'THB',
      paymentMethod: json['payment_method'] as String?,
      paymentReference: json['payment_reference'] as String?,
      postsUsed: (json['posts_used'] as num?)?.toInt() ?? 0,
      aiGenerationsUsed: (json['ai_generations_used'] as num?)?.toInt() ?? 0,
      usageStats: json['usage_stats'] as Map<String, dynamic>?,
      autoRenew: json['auto_renew'] as bool? ?? false,
      nextRenewalAt: json['next_renewal_at'] == null
          ? null
          : DateTime.parse(json['next_renewal_at'] as String),
      status: json['status'] as String,
      notes: json['notes'] as String?,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      rentalPackage: json['rental_package'] == null
          ? null
          : RentalPackage.fromJson(
              json['rental_package'] as Map<String, dynamic>),
    );

Map<String, dynamic> _$UserRentalToJson(UserRental instance) =>
    <String, dynamic>{
      'id': instance.id,
      'user_id': instance.userId,
      'rental_package_id': instance.rentalPackageId,
      'starts_at': instance.startsAt.toIso8601String(),
      'expires_at': instance.expiresAt.toIso8601String(),
      'cancelled_at': instance.cancelledAt?.toIso8601String(),
      'amount_paid': instance.amountPaid,
      'currency': instance.currency,
      'payment_method': instance.paymentMethod,
      'payment_reference': instance.paymentReference,
      'posts_used': instance.postsUsed,
      'ai_generations_used': instance.aiGenerationsUsed,
      'usage_stats': instance.usageStats,
      'auto_renew': instance.autoRenew,
      'next_renewal_at': instance.nextRenewalAt?.toIso8601String(),
      'status': instance.status,
      'notes': instance.notes,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
      'rental_package': instance.rentalPackage,
    };

RentalStatus _$RentalStatusFromJson(Map<String, dynamic> json) => RentalStatus(
      hasActiveRental: json['has_active_rental'] as bool,
      currentRental: json['current_rental'] == null
          ? null
          : UserRental.fromJson(json['current_rental'] as Map<String, dynamic>),
      postsUsed: (json['posts_used'] as num?)?.toInt() ?? 0,
      postsLimit: (json['posts_limit'] as num?)?.toInt() ?? 0,
      aiGenerationsUsed: (json['ai_generations_used'] as num?)?.toInt() ?? 0,
      aiGenerationsLimit: (json['ai_generations_limit'] as num?)?.toInt() ?? 0,
      brandsUsed: (json['brands_used'] as num?)?.toInt() ?? 0,
      brandsLimit: (json['brands_limit'] as num?)?.toInt() ?? 0,
      daysRemaining: (json['days_remaining'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$RentalStatusToJson(RentalStatus instance) =>
    <String, dynamic>{
      'has_active_rental': instance.hasActiveRental,
      'current_rental': instance.currentRental,
      'posts_used': instance.postsUsed,
      'posts_limit': instance.postsLimit,
      'ai_generations_used': instance.aiGenerationsUsed,
      'ai_generations_limit': instance.aiGenerationsLimit,
      'brands_used': instance.brandsUsed,
      'brands_limit': instance.brandsLimit,
      'days_remaining': instance.daysRemaining,
    };
