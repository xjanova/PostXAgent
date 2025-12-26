// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'post.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

Post _$PostFromJson(Map<String, dynamic> json) => Post(
      id: (json['id'] as num).toInt(),
      userId: (json['user_id'] as num).toInt(),
      brandId: (json['brand_id'] as num?)?.toInt(),
      campaignId: (json['campaign_id'] as num?)?.toInt(),
      socialAccountId: (json['social_account_id'] as num?)?.toInt(),
      contentText: json['content_text'] as String?,
      contentType: json['content_type'] as String? ?? 'text',
      mediaUrls: (json['media_urls'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      hashtags: (json['hashtags'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      platform: json['platform'] as String,
      platformPostId: json['platform_post_id'] as String?,
      platformUrl: json['platform_url'] as String?,
      scheduledAt: json['scheduled_at'] == null
          ? null
          : DateTime.parse(json['scheduled_at'] as String),
      publishedAt: json['published_at'] == null
          ? null
          : DateTime.parse(json['published_at'] as String),
      status: json['status'] as String,
      metrics: json['metrics'] == null
          ? null
          : PostMetrics.fromJson(json['metrics'] as Map<String, dynamic>),
      viralScore: (json['viral_score'] as num?)?.toInt(),
      viralFactors: (json['viral_factors'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      isViral: json['is_viral'] as bool? ?? false,
      engagementVelocity: (json['engagement_velocity'] as num?)?.toDouble(),
      aiGenerated: json['ai_generated'] as bool? ?? false,
      aiProvider: json['ai_provider'] as String?,
      aiPrompt: json['ai_prompt'] as String?,
      commentsFetchedCount: (json['comments_fetched_count'] as num?)?.toInt(),
      commentsRepliedCount: (json['comments_replied_count'] as num?)?.toInt(),
      createdAt: DateTime.parse(json['created_at'] as String),
      updatedAt: DateTime.parse(json['updated_at'] as String),
      brand: json['brand'] == null
          ? null
          : PostBrand.fromJson(json['brand'] as Map<String, dynamic>),
    );

Map<String, dynamic> _$PostToJson(Post instance) => <String, dynamic>{
      'id': instance.id,
      'user_id': instance.userId,
      'brand_id': instance.brandId,
      'campaign_id': instance.campaignId,
      'social_account_id': instance.socialAccountId,
      'content_text': instance.contentText,
      'content_type': instance.contentType,
      'media_urls': instance.mediaUrls,
      'hashtags': instance.hashtags,
      'platform': instance.platform,
      'platform_post_id': instance.platformPostId,
      'platform_url': instance.platformUrl,
      'scheduled_at': instance.scheduledAt?.toIso8601String(),
      'published_at': instance.publishedAt?.toIso8601String(),
      'status': instance.status,
      'metrics': instance.metrics,
      'viral_score': instance.viralScore,
      'viral_factors': instance.viralFactors,
      'is_viral': instance.isViral,
      'engagement_velocity': instance.engagementVelocity,
      'ai_generated': instance.aiGenerated,
      'ai_provider': instance.aiProvider,
      'ai_prompt': instance.aiPrompt,
      'comments_fetched_count': instance.commentsFetchedCount,
      'comments_replied_count': instance.commentsRepliedCount,
      'created_at': instance.createdAt.toIso8601String(),
      'updated_at': instance.updatedAt.toIso8601String(),
      'brand': instance.brand,
    };

PostMetrics _$PostMetricsFromJson(Map<String, dynamic> json) => PostMetrics(
      likes: (json['likes'] as num?)?.toInt(),
      comments: (json['comments'] as num?)?.toInt(),
      shares: (json['shares'] as num?)?.toInt(),
      views: (json['views'] as num?)?.toInt(),
      saves: (json['saves'] as num?)?.toInt(),
      clicks: (json['clicks'] as num?)?.toInt(),
      reach: (json['reach'] as num?)?.toInt(),
      impressions: (json['impressions'] as num?)?.toInt(),
      engagementRate: (json['engagement_rate'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$PostMetricsToJson(PostMetrics instance) =>
    <String, dynamic>{
      'likes': instance.likes,
      'comments': instance.comments,
      'shares': instance.shares,
      'views': instance.views,
      'saves': instance.saves,
      'clicks': instance.clicks,
      'reach': instance.reach,
      'impressions': instance.impressions,
      'engagement_rate': instance.engagementRate,
    };

PostBrand _$PostBrandFromJson(Map<String, dynamic> json) => PostBrand(
      id: (json['id'] as num).toInt(),
      name: json['name'] as String,
      logoUrl: json['logo_url'] as String?,
    );

Map<String, dynamic> _$PostBrandToJson(PostBrand instance) => <String, dynamic>{
      'id': instance.id,
      'name': instance.name,
      'logo_url': instance.logoUrl,
    };

CreatePostRequest _$CreatePostRequestFromJson(Map<String, dynamic> json) =>
    CreatePostRequest(
      brandId: (json['brand_id'] as num?)?.toInt(),
      campaignId: (json['campaign_id'] as num?)?.toInt(),
      contentText: json['content_text'] as String?,
      contentType: json['content_type'] as String? ?? 'text',
      mediaUrls: (json['media_urls'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      hashtags: (json['hashtags'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      platforms:
          (json['platforms'] as List<dynamic>).map((e) => e as String).toList(),
      scheduledAt: json['scheduled_at'] == null
          ? null
          : DateTime.parse(json['scheduled_at'] as String),
      publishNow: json['publish_now'] as bool? ?? false,
    );

Map<String, dynamic> _$CreatePostRequestToJson(CreatePostRequest instance) =>
    <String, dynamic>{
      'brand_id': instance.brandId,
      'campaign_id': instance.campaignId,
      'content_text': instance.contentText,
      'content_type': instance.contentType,
      'media_urls': instance.mediaUrls,
      'hashtags': instance.hashtags,
      'platforms': instance.platforms,
      'scheduled_at': instance.scheduledAt?.toIso8601String(),
      'publish_now': instance.publishNow,
    };

GenerateContentRequest _$GenerateContentRequestFromJson(
        Map<String, dynamic> json) =>
    GenerateContentRequest(
      brandId: (json['brand_id'] as num?)?.toInt(),
      topic: json['topic'] as String?,
      keywords: json['keywords'] as String?,
      tone: json['tone'] as String? ?? 'friendly',
      length: json['length'] as String? ?? 'medium',
      language: json['language'] as String? ?? 'th',
      platform: json['platform'] as String?,
    );

Map<String, dynamic> _$GenerateContentRequestToJson(
        GenerateContentRequest instance) =>
    <String, dynamic>{
      'brand_id': instance.brandId,
      'topic': instance.topic,
      'keywords': instance.keywords,
      'tone': instance.tone,
      'length': instance.length,
      'language': instance.language,
      'platform': instance.platform,
    };

GenerateContentResponse _$GenerateContentResponseFromJson(
        Map<String, dynamic> json) =>
    GenerateContentResponse(
      success: json['success'] as bool,
      content: json['content'] as String?,
      hashtags: (json['hashtags'] as List<dynamic>?)
          ?.map((e) => e as String)
          .toList(),
      message: json['message'] as String?,
    );

Map<String, dynamic> _$GenerateContentResponseToJson(
        GenerateContentResponse instance) =>
    <String, dynamic>{
      'success': instance.success,
      'content': instance.content,
      'hashtags': instance.hashtags,
      'message': instance.message,
    };
