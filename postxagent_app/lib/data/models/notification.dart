import 'package:json_annotation/json_annotation.dart';

part 'notification.g.dart';

@JsonSerializable()
class AppNotification {
  final String id;
  final String type;
  final String title;
  final String? body;
  final Map<String, dynamic>? data;
  @JsonKey(name: 'read_at')
  final DateTime? readAt;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;

  const AppNotification({
    required this.id,
    required this.type,
    required this.title,
    this.body,
    this.data,
    this.readAt,
    required this.createdAt,
  });

  factory AppNotification.fromJson(Map<String, dynamic> json) =>
      _$AppNotificationFromJson(json);
  Map<String, dynamic> toJson() => _$AppNotificationToJson(this);

  bool get isRead => readAt != null;
  bool get isUnread => readAt == null;

  String get timeAgo {
    final now = DateTime.now();
    final difference = now.difference(createdAt);

    if (difference.inDays > 30) {
      return '${(difference.inDays / 30).floor()} เดือนที่แล้ว';
    } else if (difference.inDays > 7) {
      return '${(difference.inDays / 7).floor()} สัปดาห์ที่แล้ว';
    } else if (difference.inDays > 0) {
      return '${difference.inDays} วันที่แล้ว';
    } else if (difference.inHours > 0) {
      return '${difference.inHours} ชั่วโมงที่แล้ว';
    } else if (difference.inMinutes > 0) {
      return '${difference.inMinutes} นาทีที่แล้ว';
    } else {
      return 'เมื่อสักครู่';
    }
  }
}

@JsonSerializable()
class NotificationPreferences {
  @JsonKey(name: 'post_published')
  final bool postPublished;
  @JsonKey(name: 'post_failed')
  final bool postFailed;
  @JsonKey(name: 'campaign_completed')
  final bool campaignCompleted;
  @JsonKey(name: 'new_comment')
  final bool newComment;
  @JsonKey(name: 'viral_alert')
  final bool viralAlert;
  @JsonKey(name: 'rental_expiring')
  final bool rentalExpiring;
  @JsonKey(name: 'quota_warning')
  final bool quotaWarning;
  @JsonKey(name: 'account_issue')
  final bool accountIssue;
  @JsonKey(name: 'push_enabled')
  final bool pushEnabled;
  @JsonKey(name: 'email_enabled')
  final bool emailEnabled;

  const NotificationPreferences({
    this.postPublished = true,
    this.postFailed = true,
    this.campaignCompleted = true,
    this.newComment = true,
    this.viralAlert = true,
    this.rentalExpiring = true,
    this.quotaWarning = true,
    this.accountIssue = true,
    this.pushEnabled = true,
    this.emailEnabled = false,
  });

  factory NotificationPreferences.fromJson(Map<String, dynamic> json) =>
      _$NotificationPreferencesFromJson(json);
  Map<String, dynamic> toJson() => _$NotificationPreferencesToJson(this);

  NotificationPreferences copyWith({
    bool? postPublished,
    bool? postFailed,
    bool? campaignCompleted,
    bool? newComment,
    bool? viralAlert,
    bool? rentalExpiring,
    bool? quotaWarning,
    bool? accountIssue,
    bool? pushEnabled,
    bool? emailEnabled,
  }) {
    return NotificationPreferences(
      postPublished: postPublished ?? this.postPublished,
      postFailed: postFailed ?? this.postFailed,
      campaignCompleted: campaignCompleted ?? this.campaignCompleted,
      newComment: newComment ?? this.newComment,
      viralAlert: viralAlert ?? this.viralAlert,
      rentalExpiring: rentalExpiring ?? this.rentalExpiring,
      quotaWarning: quotaWarning ?? this.quotaWarning,
      accountIssue: accountIssue ?? this.accountIssue,
      pushEnabled: pushEnabled ?? this.pushEnabled,
      emailEnabled: emailEnabled ?? this.emailEnabled,
    );
  }
}

/// Notification Types
class NotificationTypes {
  static const String postPublished = 'post_published';
  static const String postFailed = 'post_failed';
  static const String postScheduled = 'post_scheduled';
  static const String campaignStarted = 'campaign_started';
  static const String campaignCompleted = 'campaign_completed';
  static const String newComment = 'new_comment';
  static const String commentReply = 'comment_reply';
  static const String viralAlert = 'viral_alert';
  static const String rentalExpiring = 'rental_expiring';
  static const String rentalExpired = 'rental_expired';
  static const String quotaWarning = 'quota_warning';
  static const String quotaExceeded = 'quota_exceeded';
  static const String accountConnected = 'account_connected';
  static const String accountDisconnected = 'account_disconnected';
  static const String accountIssue = 'account_issue';
  static const String paymentSuccess = 'payment_success';
  static const String paymentFailed = 'payment_failed';
  static const String systemUpdate = 'system_update';

  static const Map<String, String> labels = {
    postPublished: 'โพสต์สำเร็จ',
    postFailed: 'โพสต์ล้มเหลว',
    postScheduled: 'ตั้งเวลาโพสต์',
    campaignStarted: 'เริ่มแคมเปญ',
    campaignCompleted: 'แคมเปญเสร็จสิ้น',
    newComment: 'คอมเมนต์ใหม่',
    commentReply: 'ตอบกลับคอมเมนต์',
    viralAlert: 'แจ้งเตือน Viral',
    rentalExpiring: 'แพ็กเกจใกล้หมด',
    rentalExpired: 'แพ็กเกจหมดอายุ',
    quotaWarning: 'โควต้าใกล้หมด',
    quotaExceeded: 'โควต้าเกินกำหนด',
    accountConnected: 'เชื่อมต่อบัญชี',
    accountDisconnected: 'ยกเลิกการเชื่อมต่อ',
    accountIssue: 'ปัญหาบัญชี',
    paymentSuccess: 'ชำระเงินสำเร็จ',
    paymentFailed: 'ชำระเงินล้มเหลว',
    systemUpdate: 'อัพเดทระบบ',
  };
}
