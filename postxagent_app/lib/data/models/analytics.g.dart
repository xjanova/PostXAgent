// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'analytics.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AnalyticsOverview _$AnalyticsOverviewFromJson(Map<String, dynamic> json) =>
    AnalyticsOverview(
      totalPosts: (json['total_posts'] as num?)?.toInt() ?? 0,
      totalLikes: (json['total_likes'] as num?)?.toInt() ?? 0,
      totalComments: (json['total_comments'] as num?)?.toInt() ?? 0,
      totalShares: (json['total_shares'] as num?)?.toInt() ?? 0,
      totalViews: (json['total_views'] as num?)?.toInt() ?? 0,
      totalReach: (json['total_reach'] as num?)?.toInt() ?? 0,
      averageEngagementRate:
          (json['average_engagement_rate'] as num?)?.toDouble() ?? 0,
      viralPostsCount: (json['viral_posts_count'] as num?)?.toInt() ?? 0,
      scheduledPostsCount:
          (json['scheduled_posts_count'] as num?)?.toInt() ?? 0,
      publishedToday: (json['published_today'] as num?)?.toInt() ?? 0,
      growthPercentage: (json['growth_percentage'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$AnalyticsOverviewToJson(AnalyticsOverview instance) =>
    <String, dynamic>{
      'total_posts': instance.totalPosts,
      'total_likes': instance.totalLikes,
      'total_comments': instance.totalComments,
      'total_shares': instance.totalShares,
      'total_views': instance.totalViews,
      'total_reach': instance.totalReach,
      'average_engagement_rate': instance.averageEngagementRate,
      'viral_posts_count': instance.viralPostsCount,
      'scheduled_posts_count': instance.scheduledPostsCount,
      'published_today': instance.publishedToday,
      'growth_percentage': instance.growthPercentage,
    };

PlatformStats _$PlatformStatsFromJson(Map<String, dynamic> json) =>
    PlatformStats(
      platform: json['platform'] as String,
      postsCount: (json['posts_count'] as num?)?.toInt() ?? 0,
      totalLikes: (json['total_likes'] as num?)?.toInt() ?? 0,
      totalComments: (json['total_comments'] as num?)?.toInt() ?? 0,
      totalShares: (json['total_shares'] as num?)?.toInt() ?? 0,
      totalViews: (json['total_views'] as num?)?.toInt() ?? 0,
      averageEngagementRate:
          (json['average_engagement_rate'] as num?)?.toDouble() ?? 0,
      successRate: (json['success_rate'] as num?)?.toDouble() ?? 0,
      growthPercentage: (json['growth_percentage'] as num?)?.toDouble(),
    );

Map<String, dynamic> _$PlatformStatsToJson(PlatformStats instance) =>
    <String, dynamic>{
      'platform': instance.platform,
      'posts_count': instance.postsCount,
      'total_likes': instance.totalLikes,
      'total_comments': instance.totalComments,
      'total_shares': instance.totalShares,
      'total_views': instance.totalViews,
      'average_engagement_rate': instance.averageEngagementRate,
      'success_rate': instance.successRate,
      'growth_percentage': instance.growthPercentage,
    };

EngagementData _$EngagementDataFromJson(Map<String, dynamic> json) =>
    EngagementData(
      date: DateTime.parse(json['date'] as String),
      likes: (json['likes'] as num?)?.toInt() ?? 0,
      comments: (json['comments'] as num?)?.toInt() ?? 0,
      shares: (json['shares'] as num?)?.toInt() ?? 0,
      views: (json['views'] as num?)?.toInt() ?? 0,
      engagementRate: (json['engagement_rate'] as num?)?.toDouble() ?? 0,
    );

Map<String, dynamic> _$EngagementDataToJson(EngagementData instance) =>
    <String, dynamic>{
      'date': instance.date.toIso8601String(),
      'likes': instance.likes,
      'comments': instance.comments,
      'shares': instance.shares,
      'views': instance.views,
      'engagement_rate': instance.engagementRate,
    };

PostAnalytics _$PostAnalyticsFromJson(Map<String, dynamic> json) =>
    PostAnalytics(
      id: (json['id'] as num).toInt(),
      content: json['content'] as String?,
      platform: json['platform'] as String,
      publishedAt: json['published_at'] == null
          ? null
          : DateTime.parse(json['published_at'] as String),
      likes: (json['likes'] as num?)?.toInt() ?? 0,
      comments: (json['comments'] as num?)?.toInt() ?? 0,
      shares: (json['shares'] as num?)?.toInt() ?? 0,
      views: (json['views'] as num?)?.toInt() ?? 0,
      engagementRate: (json['engagement_rate'] as num?)?.toDouble() ?? 0,
      viralScore: (json['viral_score'] as num?)?.toInt(),
      isViral: json['is_viral'] as bool? ?? false,
    );

Map<String, dynamic> _$PostAnalyticsToJson(PostAnalytics instance) =>
    <String, dynamic>{
      'id': instance.id,
      'content': instance.content,
      'platform': instance.platform,
      'published_at': instance.publishedAt?.toIso8601String(),
      'likes': instance.likes,
      'comments': instance.comments,
      'shares': instance.shares,
      'views': instance.views,
      'engagement_rate': instance.engagementRate,
      'viral_score': instance.viralScore,
      'is_viral': instance.isViral,
    };

DashboardStats _$DashboardStatsFromJson(Map<String, dynamic> json) =>
    DashboardStats(
      totalPosts: (json['total_posts'] as num?)?.toInt() ?? 0,
      scheduledPosts: (json['scheduled_posts'] as num?)?.toInt() ?? 0,
      viralPosts: (json['viral_posts'] as num?)?.toInt() ?? 0,
      successRate: (json['success_rate'] as num?)?.toDouble() ?? 0,
      totalBrands: (json['total_brands'] as num?)?.toInt() ?? 0,
      activeCampaigns: (json['active_campaigns'] as num?)?.toInt() ?? 0,
      connectedAccounts: (json['connected_accounts'] as num?)?.toInt() ?? 0,
    );

Map<String, dynamic> _$DashboardStatsToJson(DashboardStats instance) =>
    <String, dynamic>{
      'total_posts': instance.totalPosts,
      'scheduled_posts': instance.scheduledPosts,
      'viral_posts': instance.viralPosts,
      'success_rate': instance.successRate,
      'total_brands': instance.totalBrands,
      'active_campaigns': instance.activeCampaigns,
      'connected_accounts': instance.connectedAccounts,
    };
