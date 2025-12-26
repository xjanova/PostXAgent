// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'campaign.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Campaign _$CampaignFromJson(Map<String, dynamic> json) => Campaign(
      id: (json['id'] as num).toInt(),
      userId: (json['user_id'] as num).toInt(),
      brandId: (json['brand_id'] as num?)?.toInt(),
      name: json['name'] as String,
      description: json['description'] as String?,
      type: json['type'] as String?,
      goal: json['goal'] as String?,
      targetPlatforms: (json['target_platforms'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      contentThemes: (json['content_themes'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      postingSchedule: json['posting_schedule'] as Map<String, dynamic>?,
      aiSettings: json['ai_settings'] as Map<String, dynamic>?,
      budget: (json['budget'] as num?)?.toDouble(),
      startDate: json['start_date'] == null
          ? null
          : DateTime.parse(json['start_date'] as String),
      endDate: json['end_date'] == null
          ? null
          : DateTime.parse(json['end_date'] as String),
      status: json['status'] as String,
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      postsCount: (json['posts_count'] as num?)?.toInt(),
      publishedPostsCount: (json['published_posts_count'] as num?)?.toInt(),
      scheduledPostsCount: (json['scheduled_posts_count'] as num?)?.toInt(),
      brand: json['brand'] == null
          ? null
          : CampaignBrand.fromJson(json['brand'] as Map<String, dynamic>),
    );

Map<String, dynamic> _$CampaignToJson(Campaign instance) => <String, dynamic>{
      'id': instance.id,
      'user_id': instance.userId,
      'brand_id': instance.brandId,
      'name': instance.name,
      'description': instance.description,
      'type': instance.type,
      'goal': instance.goal,
      'target_platforms': instance.targetPlatforms,
      'content_themes': instance.contentThemes,
      'posting_schedule': instance.postingSchedule,
      'ai_settings': instance.aiSettings,
      'budget': instance.budget,
      'start_date': instance.startDate?.toIso8601String(),
      'end_date': instance.endDate?.toIso8601String(),
      'status': instance.status,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
      'posts_count': instance.postsCount,
      'published_posts_count': instance.publishedPostsCount,
      'scheduled_posts_count': instance.scheduledPostsCount,
      'brand': instance.brand,
    };

CampaignBrand _$CampaignBrandFromJson(Map<String, dynamic> json) =>
    CampaignBrand(
      id: (json['id'] as num).toInt(),
      name: json['name'] as String,
      logoUrl: json['logo_url'] as String?,
    );

Map<String, dynamic> _$CampaignBrandToJson(CampaignBrand instance) =>
    <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'logo_url': instance.logoUrl,
    };

CreateCampaignRequest _$CreateCampaignRequestFromJson(
        Map<String, dynamic> json) =>
    CreateCampaignRequest(
      brandId: (json['brand_id'] as num?)?.toInt(),
      name: json['name'] as String,
      description: json['description'] as String?,
      type: json['type'] as String?,
      goal: json['goal'] as String?,
      targetPlatforms: (json['target_platforms'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      contentThemes: (json['content_themes'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      postingSchedule: json['posting_schedule'] as Map<String, dynamic>?,
      budget: (json['budget'] as num?)?.toDouble(),
      startDate: json['start_date'] == null
          ? null
          : DateTime.parse(json['start_date'] as String),
      endDate: json['end_date'] == null
          ? null
          : DateTime.parse(json['end_date'] as String),
    );

Map<String, dynamic> _$CreateCampaignRequestToJson(
        CreateCampaignRequest instance) =>
    <String, dynamic>{
      'brand_id': instance.brandId,
      'name': instance.name,
      'description': instance.description,
      'type': instance.type,
      'goal': instance.goal,
      'target_platforms': instance.targetPlatforms,
      'content_themes': instance.contentThemes,
      'posting_schedule': instance.postingSchedule,
      'budget': instance.budget,
      'start_date': instance.startDate?.toIso8601String(),
      'end_date': instance.endDate?.toIso8601String(),
    };
