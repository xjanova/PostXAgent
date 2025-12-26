// GENERATED CODE - DO NOT MODIFY BY HAND

part of 'notification.dart';

// **************************************************************************
// JsonSerializableGenerator
// **************************************************************************

AppNotification _$AppNotificationFromJson(Map<String, dynamic> json) =>
    AppNotification(
      id: json['id'] as String,
      type: json['type'] as String,
      title: json['title'] as String,
      body: json['body'] as String?,
      data: json['data'] as Map<String, dynamic>?,
      readAt: json['read_at'] == null
          ? null
          : DateTime.parse(json['read_at'] as String),
      createdAt: DateTime.parse(json['created_at'] as String),
    );

Map<String, dynamic> _$AppNotificationToJson(AppNotification instance) =>
    <String, dynamic>{
      'id': instance.id,
      'type': instance.type,
      'title': instance.title,
      'body': instance.body,
      'data': instance.data,
      'read_at': instance.readAt?.toIso8601String(),
      'created_at': instance.createdAt.toIso8601String(),
    };

NotificationPreferences _$NotificationPreferencesFromJson(
        Map<String, dynamic> json) =>
    NotificationPreferences(
      postPublished: json['post_published'] as bool? ?? true,
      postFailed: json['post_failed'] as bool? ?? true,
      campaignCompleted: json['campaign_completed'] as bool? ?? true,
      newComment: json['new_comment'] as bool? ?? true,
      viralAlert: json['viral_alert'] as bool? ?? true,
      rentalExpiring: json['rental_expiring'] as bool? ?? true,
      quotaWarning: json['quota_warning'] as bool? ?? true,
      accountIssue: json['account_issue'] as bool? ?? true,
      pushEnabled: json['push_enabled'] as bool? ?? true,
      emailEnabled: json['email_enabled'] as bool? ?? false,
    );

Map<String, dynamic> _$NotificationPreferencesToJson(
        NotificationPreferences instance) =>
    <String, dynamic>{
      'post_published': instance.postPublished,
      'post_failed': instance.postFailed,
      'campaign_completed': instance.campaignCompleted,
      'new_comment': instance.newComment,
      'viral_alert': instance.viralAlert,
      'rental_expiring': instance.rentalExpiring,
      'quota_warning': instance.quotaWarning,
      'account_issue': instance.accountIssue,
      'push_enabled': instance.pushEnabled,
      'email_enabled': instance.emailEnabled,
    };
