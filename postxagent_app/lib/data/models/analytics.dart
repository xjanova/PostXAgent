import 'package:json_annotation/json_annotation.dart';

part 'analytics.g.dart';

@JsonSerializable()
class AnalyticsOverview {
  @JsonKey(name: 'total_posts')
  final int totalPosts;
  @JsonKey(name: 'total_likes')
  final int totalLikes;
  @JsonKey(name: 'total_comments')
  final int totalComments;
  @JsonKey(name: 'total_shares')
  final int totalShares;
  @JsonKey(name: 'total_views')
  final int totalViews;
  @JsonKey(name: 'total_reach')
  final int totalReach;
  @JsonKey(name: 'average_engagement_rate')
  final double averageEngagementRate;
  @JsonKey(name: 'viral_posts_count')
  final int viralPostsCount;
  @JsonKey(name: 'scheduled_posts_count')
  final int scheduledPostsCount;
  @JsonKey(name: 'published_today')
  final int publishedToday;
  @JsonKey(name: 'growth_percentage')
  final double? growthPercentage;

  const AnalyticsOverview({
    this.totalPosts = 0,
    this.totalLikes = 0,
    this.totalComments = 0,
    this.totalShares = 0,
    this.totalViews = 0,
    this.totalReach = 0,
    this.averageEngagementRate = 0,
    this.viralPostsCount = 0,
    this.scheduledPostsCount = 0,
    this.publishedToday = 0,
    this.growthPercentage,
  });

  factory AnalyticsOverview.fromJson(Map<String, dynamic> json) =>
      _$AnalyticsOverviewFromJson(json);
  Map<String, dynamic> toJson() => _$AnalyticsOverviewToJson(this);

  int get totalEngagement => totalLikes + totalComments + totalShares;

  String get formattedLikes => _formatNumber(totalLikes);
  String get formattedComments => _formatNumber(totalComments);
  String get formattedShares => _formatNumber(totalShares);
  String get formattedViews => _formatNumber(totalViews);
  String get formattedReach => _formatNumber(totalReach);
  String get formattedEngagement => _formatNumber(totalEngagement);

  String _formatNumber(int number) {
    if (number >= 1000000) {
      return '${(number / 1000000).toStringAsFixed(1)}M';
    } else if (number >= 1000) {
      return '${(number / 1000).toStringAsFixed(1)}K';
    }
    return number.toString();
  }
}

@JsonSerializable()
class PlatformStats {
  final String platform;
  @JsonKey(name: 'posts_count')
  final int postsCount;
  @JsonKey(name: 'total_likes')
  final int totalLikes;
  @JsonKey(name: 'total_comments')
  final int totalComments;
  @JsonKey(name: 'total_shares')
  final int totalShares;
  @JsonKey(name: 'total_views')
  final int totalViews;
  @JsonKey(name: 'average_engagement_rate')
  final double averageEngagementRate;
  @JsonKey(name: 'success_rate')
  final double successRate;
  @JsonKey(name: 'growth_percentage')
  final double? growthPercentage;

  const PlatformStats({
    required this.platform,
    this.postsCount = 0,
    this.totalLikes = 0,
    this.totalComments = 0,
    this.totalShares = 0,
    this.totalViews = 0,
    this.averageEngagementRate = 0,
    this.successRate = 0,
    this.growthPercentage,
  });

  factory PlatformStats.fromJson(Map<String, dynamic> json) =>
      _$PlatformStatsFromJson(json);
  Map<String, dynamic> toJson() => _$PlatformStatsToJson(this);

  int get totalEngagement => totalLikes + totalComments + totalShares;
}

@JsonSerializable()
class EngagementData {
  final DateTime date;
  final int likes;
  final int comments;
  final int shares;
  final int views;
  @JsonKey(name: 'engagement_rate')
  final double engagementRate;

  const EngagementData({
    required this.date,
    this.likes = 0,
    this.comments = 0,
    this.shares = 0,
    this.views = 0,
    this.engagementRate = 0,
  });

  factory EngagementData.fromJson(Map<String, dynamic> json) =>
      _$EngagementDataFromJson(json);
  Map<String, dynamic> toJson() => _$EngagementDataToJson(this);

  int get totalEngagement => likes + comments + shares;
}

@JsonSerializable()
class PostAnalytics {
  final int id;
  final String? content;
  final String platform;
  @JsonKey(name: 'published_at')
  final DateTime? publishedAt;
  final int likes;
  final int comments;
  final int shares;
  final int views;
  @JsonKey(name: 'engagement_rate')
  final double engagementRate;
  @JsonKey(name: 'viral_score')
  final int? viralScore;
  @JsonKey(name: 'is_viral')
  final bool isViral;

  const PostAnalytics({
    required this.id,
    this.content,
    required this.platform,
    this.publishedAt,
    this.likes = 0,
    this.comments = 0,
    this.shares = 0,
    this.views = 0,
    this.engagementRate = 0,
    this.viralScore,
    this.isViral = false,
  });

  factory PostAnalytics.fromJson(Map<String, dynamic> json) =>
      _$PostAnalyticsFromJson(json);
  Map<String, dynamic> toJson() => _$PostAnalyticsToJson(this);

  int get totalEngagement => likes + comments + shares;

  String get excerpt {
    if (content == null || content!.isEmpty) return '';
    if (content!.length <= 50) return content!;
    return '${content!.substring(0, 50)}...';
  }
}

@JsonSerializable()
class DashboardStats {
  @JsonKey(name: 'total_posts')
  final int totalPosts;
  @JsonKey(name: 'scheduled_posts')
  final int scheduledPosts;
  @JsonKey(name: 'viral_posts')
  final int viralPosts;
  @JsonKey(name: 'success_rate')
  final double successRate;
  @JsonKey(name: 'total_brands')
  final int totalBrands;
  @JsonKey(name: 'active_campaigns')
  final int activeCampaigns;
  @JsonKey(name: 'connected_accounts')
  final int connectedAccounts;

  const DashboardStats({
    this.totalPosts = 0,
    this.scheduledPosts = 0,
    this.viralPosts = 0,
    this.successRate = 0,
    this.totalBrands = 0,
    this.activeCampaigns = 0,
    this.connectedAccounts = 0,
  });

  factory DashboardStats.fromJson(Map<String, dynamic> json) =>
      _$DashboardStatsFromJson(json);
  Map<String, dynamic> toJson() => _$DashboardStatsToJson(this);
}

/// Analytics period options
class AnalyticsPeriods {
  static const Map<String, String> all = {
    'today': 'วันนี้',
    '7days': '7 วันที่ผ่านมา',
    '30days': '30 วันที่ผ่านมา',
    '90days': '3 เดือน',
    'year': 'ปีนี้',
    'all': 'ทั้งหมด',
  };
}

/// Dashboard Summary for the main dashboard screen
class DashboardSummary {
  final int totalPosts;
  final int publishedPosts;
  final int scheduledPosts;
  final int viralPosts;
  final double engagementRate;
  final int totalBrands;
  final int activeCampaigns;
  final int connectedAccounts;
  final int postsToday;
  final int postsThisWeek;

  const DashboardSummary({
    this.totalPosts = 0,
    this.publishedPosts = 0,
    this.scheduledPosts = 0,
    this.viralPosts = 0,
    this.engagementRate = 0.0,
    this.totalBrands = 0,
    this.activeCampaigns = 0,
    this.connectedAccounts = 0,
    this.postsToday = 0,
    this.postsThisWeek = 0,
  });

  factory DashboardSummary.fromJson(Map<String, dynamic> json) {
    return DashboardSummary(
      totalPosts: json['total_posts'] ?? 0,
      publishedPosts: json['published_posts'] ?? 0,
      scheduledPosts: json['scheduled_posts'] ?? 0,
      viralPosts: json['viral_posts'] ?? 0,
      engagementRate: (json['engagement_rate'] ?? 0).toDouble(),
      totalBrands: json['total_brands'] ?? 0,
      activeCampaigns: json['active_campaigns'] ?? 0,
      connectedAccounts: json['connected_accounts'] ?? 0,
      postsToday: json['posts_today'] ?? 0,
      postsThisWeek: json['posts_this_week'] ?? 0,
    );
  }
}

/// Extended AnalyticsOverview with growth metrics
extension AnalyticsOverviewExt on AnalyticsOverview {
  int get totalClicks => 0;
  double get engagementRate => averageEngagementRate;
  double get reachGrowth => growthPercentage ?? 0;
  double get engagementGrowth => growthPercentage ?? 0;
  double get clicksGrowth => 0;
  double get engagementRateGrowth => 0;
}

/// Platform Analytics for analytics screen
class PlatformAnalytics {
  final String platform;
  final int postsCount;
  final int reach;
  final int engagement;
  final int followers;
  final double growth;

  const PlatformAnalytics({
    required this.platform,
    this.postsCount = 0,
    this.reach = 0,
    this.engagement = 0,
    this.followers = 0,
    this.growth = 0,
  });

  factory PlatformAnalytics.fromJson(Map<String, dynamic> json) {
    return PlatformAnalytics(
      platform: json['platform'] ?? '',
      postsCount: json['posts_count'] ?? 0,
      reach: json['reach'] ?? 0,
      engagement: json['engagement'] ?? 0,
      followers: json['followers'] ?? 0,
      growth: (json['growth'] ?? 0).toDouble(),
    );
  }
}

/// Post Performance for analytics screen
class PostPerformance {
  final int postId;
  final String platform;
  final String? contentPreview;
  final int reach;
  final int engagement;
  final double engagementRate;

  const PostPerformance({
    required this.postId,
    required this.platform,
    this.contentPreview,
    this.reach = 0,
    this.engagement = 0,
    this.engagementRate = 0,
  });

  factory PostPerformance.fromJson(Map<String, dynamic> json) {
    return PostPerformance(
      postId: json['post_id'] ?? json['id'] ?? 0,
      platform: json['platform'] ?? '',
      contentPreview: json['content_preview'] ?? json['content'],
      reach: json['reach'] ?? 0,
      engagement: json['engagement'] ?? 0,
      engagementRate: (json['engagement_rate'] ?? 0).toDouble(),
    );
  }
}

/// Engagement Trend for charts
class EngagementTrend {
  final DateTime date;
  final int reach;
  final int engagement;

  const EngagementTrend({
    required this.date,
    this.reach = 0,
    this.engagement = 0,
  });

  factory EngagementTrend.fromJson(Map<String, dynamic> json) {
    return EngagementTrend(
      date: DateTime.parse(json['date']),
      reach: json['reach'] ?? 0,
      engagement: json['engagement'] ?? 0,
    );
  }
}

/// Viral Post model
class ViralPost {
  final int id;
  final String platform;
  final String? content;
  final int viralScore;
  final int reach;
  final int engagement;

  const ViralPost({
    required this.id,
    required this.platform,
    this.content,
    this.viralScore = 0,
    this.reach = 0,
    this.engagement = 0,
  });

  factory ViralPost.fromJson(Map<String, dynamic> json) {
    return ViralPost(
      id: json['id'] ?? 0,
      platform: json['platform'] ?? '',
      content: json['content'],
      viralScore: json['viral_score'] ?? 0,
      reach: json['reach'] ?? 0,
      engagement: json['engagement'] ?? 0,
    );
  }
}
