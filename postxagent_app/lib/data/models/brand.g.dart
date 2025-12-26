// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'brand.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Brand _$BrandFromJson(Map<String, dynamic> json) => Brand(
      id: (json['id'] as num).toInt(),
      userId: (json['user_id'] as num).toInt(),
      name: json['name'] as String,
      description: json['description'] as String?,
      industry: json['industry'] as String?,
      targetAudience: json['target_audience'] as String?,
      tone: json['tone'] as String?,
      logoUrl: json['logo_url'] as String?,
      brandColors: (json['brand_colors'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      keywords: (json['keywords'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      hashtags: (json['hashtags'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      websiteUrl: json['website_url'] as String?,
      settings: json['settings'] as Map<String, dynamic>?,
      isActive: json['is_active'] as bool? ?? true,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      postsCount: (json['posts_count'] as num?)?.toInt(),
      campaignsCount: (json['campaigns_count'] as num?)?.toInt(),
      socialAccountsCount: (json['social_accounts_count'] as num?)?.toInt(),
    );

Map<String, dynamic> _$BrandToJson(Brand instance) => <String, dynamic>{
      'id': instance.id,
      'user_id': instance.userId,
      'name': instance.name,
      'description': instance.description,
      'industry': instance.industry,
      'target_audience': instance.targetAudience,
      'tone': instance.tone,
      'logo_url': instance.logoUrl,
      'brand_colors': instance.brandColors,
      'keywords': instance.keywords,
      'hashtags': instance.hashtags,
      'website_url': instance.websiteUrl,
      'settings': instance.settings,
      'is_active': instance.isActive,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
      'posts_count': instance.postsCount,
      'campaigns_count': instance.campaignsCount,
      'social_accounts_count': instance.socialAccountsCount,
    };

BrandRequest _$BrandRequestFromJson(Map<String, dynamic> json) => BrandRequest(
      name: json['name'] as String,
      description: json['description'] as String?,
      industry: json['industry'] as String?,
      targetAudience: json['target_audience'] as String?,
      tone: json['tone'] as String?,
      logoUrl: json['logo_url'] as String?,
      brandColors: (json['brand_colors'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      keywords: (json['keywords'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      hashtags: (json['hashtags'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      websiteUrl: json['website_url'] as String?,
    );

Map<String, dynamic> _$BrandRequestToJson(BrandRequest instance) =>
    <String, dynamic>{
      'name': instance.name,
      'description': instance.description,
      'industry': instance.industry,
      'target_audience': instance.targetAudience,
      'tone': instance.tone,
      'logo_url': instance.logoUrl,
      'brand_colors': instance.brandColors,
      'keywords': instance.keywords,
      'hashtags': instance.hashtags,
      'website_url': instance.websiteUrl,
    };
