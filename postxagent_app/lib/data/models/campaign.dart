import 'package:json_annotation/json_annotation.dart';

part 'campaign.g.dart';

@JsonSerializable()
class Campaign {
  final int id;
  @JsonKey(name: 'user_id')
  final int userId;
  @JsonKey(name: 'brand_id')
  final int? brandId;
  final String name;
  final String? description;
  final String? type;
  final String? goal;
  @JsonKey(name: 'target_platforms')
  final List<String>? targetPlatforms;
  @JsonKey(name: 'content_themes')
  final List<String>? contentThemes;
  @JsonKey(name: 'posting_schedule')
  final Map<String, dynamic>? postingSchedule;
  @JsonKey(name: 'ai_settings')
  final Map<String, dynamic>? aiSettings;
  final double? budget;
  @JsonKey(name: 'start_date')
  final DateTime? startDate;
  @JsonKey(name: 'end_date')
  final DateTime? endDate;
  final String status;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  // Counts
  @JsonKey(name: 'posts_count')
  final int? postsCount;
  @JsonKey(name: 'published_posts_count')
  final int? publishedPostsCount;
  @JsonKey(name: 'scheduled_posts_count')
  final int? scheduledPostsCount;

  // Relations
  final CampaignBrand? brand;

  const Campaign({
    required this.id,
    required this.userId,
    this.brandId,
    required this.name,
    this.description,
    this.type,
    this.goal,
    this.targetPlatforms,
    this.contentThemes,
    this.postingSchedule,
    this.aiSettings,
    this.budget,
    this.startDate,
    this.endDate,
    required this.status,
    required this.createdAt,
    required this.updatedAt,
    this.postsCount,
    this.publishedPostsCount,
    this.scheduledPostsCount,
    this.brand,
  });

  factory Campaign.fromJson(Map<String, dynamic> json) =>
      _$CampaignFromJson(json);
  Map<String, dynamic> toJson() => _$CampaignToJson(this);

  Campaign copyWith({
    int? id,
    int? userId,
    int? brandId,
    String? name,
    String? description,
    String? type,
    String? goal,
    List<String>? targetPlatforms,
    List<String>? contentThemes,
    Map<String, dynamic>? postingSchedule,
    Map<String, dynamic>? aiSettings,
    double? budget,
    DateTime? startDate,
    DateTime? endDate,
    String? status,
    DateTime? createdAt,
    DateTime? updatedAt,
    int? postsCount,
    int? publishedPostsCount,
    int? scheduledPostsCount,
    CampaignBrand? brand,
  }) {
    return Campaign(
      id: id ?? this.id,
      userId: userId ?? this.userId,
      brandId: brandId ?? this.brandId,
      name: name ?? this.name,
      description: description ?? this.description,
      type: type ?? this.type,
      goal: goal ?? this.goal,
      targetPlatforms: targetPlatforms ?? this.targetPlatforms,
      contentThemes: contentThemes ?? this.contentThemes,
      postingSchedule: postingSchedule ?? this.postingSchedule,
      aiSettings: aiSettings ?? this.aiSettings,
      budget: budget ?? this.budget,
      startDate: startDate ?? this.startDate,
      endDate: endDate ?? this.endDate,
      status: status ?? this.status,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      postsCount: postsCount ?? this.postsCount,
      publishedPostsCount: publishedPostsCount ?? this.publishedPostsCount,
      scheduledPostsCount: scheduledPostsCount ?? this.scheduledPostsCount,
      brand: brand ?? this.brand,
    );
  }

  bool get isActive => status == 'active';
  bool get isPaused => status == 'paused';
  bool get isCompleted => status == 'completed';

  double get progress {
    if (postsCount == null || postsCount == 0) return 0;
    if (publishedPostsCount == null) return 0;
    return publishedPostsCount! / postsCount!;
  }

  int get daysRemaining {
    if (endDate == null) return 0;
    final now = DateTime.now();
    if (endDate!.isBefore(now)) return 0;
    return endDate!.difference(now).inDays;
  }
}

@JsonSerializable()
class CampaignBrand {
  final int id;
  final String name;
  @JsonKey(name: 'logo_url')
  final String? logoUrl;

  const CampaignBrand({
    required this.id,
    required this.name,
    this.logoUrl,
  });

  factory CampaignBrand.fromJson(Map<String, dynamic> json) =>
      _$CampaignBrandFromJson(json);
  Map<String, dynamic> toJson() => _$CampaignBrandToJson(this);
}

@JsonSerializable()
class CreateCampaignRequest {
  @JsonKey(name: 'brand_id')
  final int? brandId;
  final String name;
  final String? description;
  final String? type;
  final String? goal;
  @JsonKey(name: 'target_platforms')
  final List<String>? targetPlatforms;
  @JsonKey(name: 'content_themes')
  final List<String>? contentThemes;
  @JsonKey(name: 'posting_schedule')
  final Map<String, dynamic>? postingSchedule;
  final double? budget;
  @JsonKey(name: 'start_date')
  final DateTime? startDate;
  @JsonKey(name: 'end_date')
  final DateTime? endDate;

  const CreateCampaignRequest({
    this.brandId,
    required this.name,
    this.description,
    this.type,
    this.goal,
    this.targetPlatforms,
    this.contentThemes,
    this.postingSchedule,
    this.budget,
    this.startDate,
    this.endDate,
  });

  factory CreateCampaignRequest.fromJson(Map<String, dynamic> json) =>
      _$CreateCampaignRequestFromJson(json);
  Map<String, dynamic> toJson() => _$CreateCampaignRequestToJson(this);
}

/// Campaign Types
class CampaignTypes {
  static const Map<String, String> all = {
    'awareness': 'สร้างการรับรู้',
    'engagement': 'เพิ่ม Engagement',
    'conversion': 'เพิ่มยอดขาย',
    'traffic': 'เพิ่ม Traffic',
    'lead_generation': 'หา Lead',
    'app_promotion': 'โปรโมทแอพ',
    'event': 'โปรโมทอีเว้นท์',
    'product_launch': 'เปิดตัวสินค้า',
    'seasonal': 'แคมเปญตามฤดูกาล',
    'contest': 'แข่งขัน/ชิงรางวัล',
  };
}

/// Campaign Goals
class CampaignGoals {
  static const Map<String, String> all = {
    'increase_followers': 'เพิ่มผู้ติดตาม',
    'increase_engagement': 'เพิ่ม Engagement',
    'drive_sales': 'เพิ่มยอดขาย',
    'brand_awareness': 'สร้างการรับรู้แบรนด์',
    'website_traffic': 'เพิ่ม Traffic เว็บไซต์',
    'app_installs': 'เพิ่มการติดตั้งแอพ',
    'lead_generation': 'หา Lead ใหม่',
    'video_views': 'เพิ่มยอดวิว',
    'message_replies': 'เพิ่มข้อความ',
    'store_visits': 'เพิ่มการมาร้าน',
  };
}
