import 'package:json_annotation/json_annotation.dart';

part 'brand.g.dart';

@JsonSerializable()
class Brand {
  final int id;
  @JsonKey(name: 'user_id')
  final int userId;
  final String name;
  final String? description;
  final String? industry;
  @JsonKey(name: 'target_audience')
  final String? targetAudience;
  final String? tone;
  @JsonKey(name: 'logo_url')
  final String? logoUrl;
  @JsonKey(name: 'brand_colors')
  final List<String>? brandColors;
  final List<String>? keywords;
  final List<String>? hashtags;
  @JsonKey(name: 'website_url')
  final String? websiteUrl;
  final Map<String, dynamic>? settings;
  @JsonKey(name: 'is_active')
  final bool isActive;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  // Counts
  @JsonKey(name: 'posts_count')
  final int? postsCount;
  @JsonKey(name: 'campaigns_count')
  final int? campaignsCount;
  @JsonKey(name: 'social_accounts_count')
  final int? socialAccountsCount;

  const Brand({
    required this.id,
    required this.userId,
    required this.name,
    this.description,
    this.industry,
    this.targetAudience,
    this.tone,
    this.logoUrl,
    this.brandColors,
    this.keywords,
    this.hashtags,
    this.websiteUrl,
    this.settings,
    this.isActive = true,
    required this.createdAt,
    required this.updatedAt,
    this.postsCount,
    this.campaignsCount,
    this.socialAccountsCount,
  });

  factory Brand.fromJson(Map<String, dynamic> json) => _$BrandFromJson(json);
  Map<String, dynamic> toJson() => _$BrandToJson(this);

  Brand copyWith({
    int? id,
    int? userId,
    String? name,
    String? description,
    String? industry,
    String? targetAudience,
    String? tone,
    String? logoUrl,
    List<String>? brandColors,
    List<String>? keywords,
    List<String>? hashtags,
    String? websiteUrl,
    Map<String, dynamic>? settings,
    bool? isActive,
    DateTime? createdAt,
    DateTime? updatedAt,
    int? postsCount,
    int? campaignsCount,
    int? socialAccountsCount,
  }) {
    return Brand(
      id: id ?? this.id,
      userId: userId ?? this.userId,
      name: name ?? this.name,
      description: description ?? this.description,
      industry: industry ?? this.industry,
      targetAudience: targetAudience ?? this.targetAudience,
      tone: tone ?? this.tone,
      logoUrl: logoUrl ?? this.logoUrl,
      brandColors: brandColors ?? this.brandColors,
      keywords: keywords ?? this.keywords,
      hashtags: hashtags ?? this.hashtags,
      websiteUrl: websiteUrl ?? this.websiteUrl,
      settings: settings ?? this.settings,
      isActive: isActive ?? this.isActive,
      createdAt: createdAt ?? this.createdAt,
      updatedAt: updatedAt ?? this.updatedAt,
      postsCount: postsCount ?? this.postsCount,
      campaignsCount: campaignsCount ?? this.campaignsCount,
      socialAccountsCount: socialAccountsCount ?? this.socialAccountsCount,
    );
  }

  String get formattedHashtags {
    if (hashtags == null || hashtags!.isEmpty) return '';
    return hashtags!.map((h) => h.startsWith('#') ? h : '#$h').join(' ');
  }

  String get primaryColor {
    if (brandColors != null && brandColors!.isNotEmpty) {
      return brandColors!.first;
    }
    return '#7C4DFF';
  }
}

@JsonSerializable()
class BrandRequest {
  final String name;
  final String? description;
  final String? industry;
  @JsonKey(name: 'target_audience')
  final String? targetAudience;
  final String? tone;
  @JsonKey(name: 'logo_url')
  final String? logoUrl;
  @JsonKey(name: 'brand_colors')
  final List<String>? brandColors;
  final List<String>? keywords;
  final List<String>? hashtags;
  @JsonKey(name: 'website_url')
  final String? websiteUrl;

  const BrandRequest({
    required this.name,
    this.description,
    this.industry,
    this.targetAudience,
    this.tone,
    this.logoUrl,
    this.brandColors,
    this.keywords,
    this.hashtags,
    this.websiteUrl,
  });

  factory BrandRequest.fromJson(Map<String, dynamic> json) =>
      _$BrandRequestFromJson(json);
  Map<String, dynamic> toJson() => _$BrandRequestToJson(this);
}

/// Industries in Thai
class BrandIndustries {
  static const Map<String, String> all = {
    'ecommerce': 'อีคอมเมิร์ซ',
    'food': 'อาหารและเครื่องดื่ม',
    'beauty': 'ความงามและเครื่องสำอาง',
    'fashion': 'แฟชั่นและเสื้อผ้า',
    'health': 'สุขภาพและฟิตเนส',
    'technology': 'เทคโนโลยี',
    'education': 'การศึกษา',
    'travel': 'ท่องเที่ยว',
    'real_estate': 'อสังหาริมทรัพย์',
    'finance': 'การเงินและการลงทุน',
    'entertainment': 'บันเทิง',
    'automotive': 'ยานยนต์',
    'home': 'บ้านและของตกแต่ง',
    'pet': 'สัตว์เลี้ยง',
    'sports': 'กีฬา',
    'other': 'อื่นๆ',
  };
}

/// Tone Options in Thai
class BrandTones {
  static const Map<String, String> all = {
    'friendly': 'เป็นกันเอง',
    'professional': 'มืออาชีพ',
    'playful': 'สนุกสนาน',
    'formal': 'ทางการ',
    'casual': 'สบายๆ',
    'inspiring': 'สร้างแรงบันดาลใจ',
    'educational': 'ให้ความรู้',
    'humorous': 'ตลกขบขัน',
    'luxurious': 'หรูหรา',
    'eco_friendly': 'รักษ์โลก',
  };
}
