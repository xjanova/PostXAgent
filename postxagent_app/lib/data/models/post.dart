import 'package:json_annotation/json_annotation.dart';

part 'post.g.dart';

@JsonSerializable()
class Post {
  final int id;
  @JsonKey(name: 'user_id')
  final int userId;
  @JsonKey(name: 'brand_id')
  final int? brandId;
  @JsonKey(name: 'campaign_id')
  final int? campaignId;
  @JsonKey(name: 'social_account_id')
  final int? socialAccountId;

  // Content
  @JsonKey(name: 'content_text')
  final String? contentText;
  @JsonKey(name: 'content_type')
  final String contentType;
  @JsonKey(name: 'media_urls')
  final List<String>? mediaUrls;
  final List<String>? hashtags;

  // Platform
  final String platform;
  @JsonKey(name: 'platform_post_id')
  final String? platformPostId;
  @JsonKey(name: 'platform_url')
  final String? platformUrl;

  // Schedule
  @JsonKey(name: 'scheduled_at')
  final DateTime? scheduledAt;
  @JsonKey(name: 'published_at')
  final DateTime? publishedAt;
  final String status;

  // Metrics
  final PostMetrics? metrics;
  @JsonKey(name: 'viral_score')
  final int? viralScore;
  @JsonKey(name: 'viral_factors')
  final List<String>? viralFactors;
  @JsonKey(name: 'is_viral')
  final bool isViral;
  @JsonKey(name: 'engagement_velocity')
  final double? engagementVelocity;

  // AI
  @JsonKey(name: 'ai_generated')
  final bool aiGenerated;
  @JsonKey(name: 'ai_provider')
  final String? aiProvider;
  @JsonKey(name: 'ai_prompt')
  final String? aiPrompt;

  // Comments
  @JsonKey(name: 'comments_fetched_count')
  final int? commentsFetchedCount;
  @JsonKey(name: 'comments_replied_count')
  final int? commentsRepliedCount;

  // Timestamps
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  // Relations
  final PostBrand? brand;

  const Post({
    required this.id,
    required this.userId,
    this.brandId,
    this.campaignId,
    this.socialAccountId,
    this.contentText,
    this.contentType = 'text',
    this.mediaUrls,
    this.hashtags,
    required this.platform,
    this.platformPostId,
    this.platformUrl,
    this.scheduledAt,
    this.publishedAt,
    required this.status,
    this.metrics,
    this.viralScore,
    this.viralFactors,
    this.isViral = false,
    this.engagementVelocity,
    this.aiGenerated = false,
    this.aiProvider,
    this.aiPrompt,
    this.commentsFetchedCount,
    this.commentsRepliedCount,
    required this.createdAt,
    required this.updatedAt,
    this.brand,
  });

  factory Post.fromJson(Map<String, dynamic> json) => _$PostFromJson(json);
  Map<String, dynamic> toJson() => _$PostToJson(this);

  Post copyWith({
    int? id,
    int? userId,
    int? brandId,
    int? campaignId,
    int? socialAccountId,
    String? contentText,
    String? contentType,
    List<String>? mediaUrls,
    List<String>? hashtags,
    String? platform,
    String? platformPostId,
    String? platformUrl,
    DateTime? scheduledAt,
    DateTime? publishedAt,
    String? status,
    PostMetrics? metrics,
    int? viralScore,
    List<String>? viralFactors,
    bool? isViral,
    double? engagementVelocity,
    bool? aiGenerated,
    String? aiProvider,
    String? aiPrompt,
    int? commentsFetchedCount,
    int? commentsRepliedCount,
    DateTime? createdAt,
    DateTime? updatedAt,
    PostBrand? brand,
  }) {
    return Post(
      id: id ?? this.id,
      userId: userId ?? this.userId,
      brandId: brandId ?? this.brandId,
      campaignId: campaignId ?? this.campaignId,
      socialAccountId: socialAccountId ?? this.socialAccountId,
      contentText: contentText ?? this.contentText,
      contentType: contentType ?? this.contentType,
      mediaUrls: mediaUrls ?? this.mediaUrls,
      hashtags: hashtags ?? this.hashtags,
      platform: platform ?? this.platform,
      platformPostId: platformPostId ?? this.platformPostId,
      platformUrl: platformUrl ?? this.platformUrl,
      scheduledAt: scheduledAt ?? this.scheduledAt,
      publishedAt: publishedAt ?? this.publishedAt,
      status: status ?? this.status,
      metrics: metrics ?? this.metrics,
      viralScore: viralScore ?? this.viralScore,
      viralFactors: viralFactors ?? this.viralFactors,
      isViral: isViral ?? this.isViral,
      engagementVelocity: engagementVelocity ?? this.engagementVelocity,
      aiGenerated: aiGenerated ?? this.aiGenerated,
      aiProvider: aiProvider ?? this.aiProvider,
      aiPrompt: aiPrompt ?? this.aiPrompt,
      commentsFetchedCount: commentsFetchedCount ?? this.commentsFetchedCount,
      commentsRepliedCount: commentsRepliedCount ?? this.commentsRepliedCount,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      brand: brand ?? this.brand,
    );
  }

  String get excerpt {
    if (contentText == null || contentText!.isEmpty) return '';
    if (contentText!.length <= 100) return contentText!;
    return '${contentText!.substring(0, 100)}...';
  }

  String? get firstMediaUrl {
    if (mediaUrls == null || mediaUrls!.isEmpty) return null;
    return mediaUrls!.first;
  }

  int get totalEngagement {
    if (metrics == null) return 0;
    return (metrics!.likes ?? 0) +
        (metrics!.comments ?? 0) +
        (metrics!.shares ?? 0);
  }
}

@JsonSerializable()
class PostMetrics {
  final int? likes;
  final int? comments;
  final int? shares;
  final int? views;
  final int? saves;
  final int? clicks;
  final int? reach;
  final int? impressions;
  @JsonKey(name: 'engagement_rate')
  final double? engagementRate;

  const PostMetrics({
    this.likes,
    this.comments,
    this.shares,
    this.views,
    this.saves,
    this.clicks,
    this.reach,
    this.impressions,
    this.engagementRate,
  });

  factory PostMetrics.fromJson(Map<String, dynamic> json) =>
      _$PostMetricsFromJson(json);
  Map<String, dynamic> toJson() => _$PostMetricsToJson(this);
}

@JsonSerializable()
class PostBrand {
  final int id;
  final String name;
  @JsonKey(name: 'logo_url')
  final String? logoUrl;

  const PostBrand({
    required this.id,
    required this.name,
    this.logoUrl,
  });

  factory PostBrand.fromJson(Map<String, dynamic> json) =>
      _$PostBrandFromJson(json);
  Map<String, dynamic> toJson() => _$PostBrandToJson(this);
}

@JsonSerializable()
class CreatePostRequest {
  @JsonKey(name: 'brand_id')
  final int? brandId;
  @JsonKey(name: 'campaign_id')
  final int? campaignId;
  @JsonKey(name: 'content_text')
  final String? contentText;
  @JsonKey(name: 'content_type')
  final String contentType;
  @JsonKey(name: 'media_urls')
  final List<String>? mediaUrls;
  final List<String>? hashtags;
  final List<String> platforms;
  @JsonKey(name: 'scheduled_at')
  final DateTime? scheduledAt;
  @JsonKey(name: 'publish_now')
  final bool publishNow;

  const CreatePostRequest({
    this.brandId,
    this.campaignId,
    this.contentText,
    this.contentType = 'text',
    this.mediaUrls,
    this.hashtags,
    required this.platforms,
    this.scheduledAt,
    this.publishNow = false,
  });

  factory CreatePostRequest.fromJson(Map<String, dynamic> json) =>
      _$CreatePostRequestFromJson(json);
  Map<String, dynamic> toJson() => _$CreatePostRequestToJson(this);
}

@JsonSerializable()
class GenerateContentRequest {
  @JsonKey(name: 'brand_id')
  final int? brandId;
  final String? topic;
  final String? keywords;
  final String tone;
  final String length;
  final String language;
  final String? platform;

  const GenerateContentRequest({
    this.brandId,
    this.topic,
    this.keywords,
    this.tone = 'friendly',
    this.length = 'medium',
    this.language = 'th',
    this.platform,
  });

  factory GenerateContentRequest.fromJson(Map<String, dynamic> json) =>
      _$GenerateContentRequestFromJson(json);
  Map<String, dynamic> toJson() => _$GenerateContentRequestToJson(this);
}

@JsonSerializable()
class GenerateContentResponse {
  final bool success;
  final String? content;
  final List<String>? hashtags;
  final String? message;

  const GenerateContentResponse({
    required this.success,
    this.content,
    this.hashtags,
    this.message,
  });

  factory GenerateContentResponse.fromJson(Map<String, dynamic> json) =>
      _$GenerateContentResponseFromJson(json);
  Map<String, dynamic> toJson() => _$GenerateContentResponseToJson(this);
}

/// Content types
class ContentTypes {
  static const String text = 'text';
  static const String image = 'image';
  static const String video = 'video';
  static const String carousel = 'carousel';
  static const String story = 'story';
  static const String reel = 'reel';

  static const Map<String, String> labels = {
    text: 'ข้อความ',
    image: 'รูปภาพ',
    video: 'วิดีโอ',
    carousel: 'แกลเลอรี่',
    story: 'สตอรี่',
    reel: 'รีล',
  };
}

/// Content length options
class ContentLengths {
  static const Map<String, String> all = {
    'short': 'สั้น',
    'medium': 'ปานกลาง',
    'long': 'ยาว',
  };
}

/// Content tones for AI generation
class ContentTones {
  static const Map<String, String> all = {
    'friendly': 'เป็นกันเอง',
    'professional': 'มืออาชีพ',
    'exciting': 'น่าตื่นเต้น',
    'promotional': 'โปรโมท',
    'informative': 'ให้ข้อมูล',
    'humorous': 'ตลกขบขัน',
  };
}
