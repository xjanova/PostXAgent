import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';

import '../../../core/constants/app_constants.dart';

class NotificationsScreen extends ConsumerStatefulWidget {
  const NotificationsScreen({super.key});

  @override
  ConsumerState<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends ConsumerState<NotificationsScreen> {
  // Sample notifications
  final List<Map<String, dynamic>> _notifications = [
    {
      'id': '1',
      'type': 'post_published',
      'title': 'โพสต์สำเร็จ',
      'body': 'โพสต์ "โปรโมชั่นปีใหม่" ถูกโพสต์ไปยัง Facebook เรียบร้อยแล้ว',
      'read_at': null,
      'created_at': DateTime.now().subtract(const Duration(minutes: 5)),
      'data': {'post_id': '1'},
    },
    {
      'id': '2',
      'type': 'viral_alert',
      'title': 'โพสต์กำลังไวรัล!',
      'body': 'โพสต์ของคุณมีการเข้าถึงสูงผิดปกติ 🔥 มียอด engagement เพิ่มขึ้น 250%',
      'read_at': null,
      'created_at': DateTime.now().subtract(const Duration(hours: 1)),
      'data': {'post_id': '2'},
    },
    {
      'id': '3',
      'type': 'new_comment',
      'title': 'มีคอมเมนต์ใหม่',
      'body': 'มีคนคอมเมนต์โพสต์ "Flash Sale" ของคุณ: "สนใจค่ะ ส่งรายละเอียดเพิ่มเติมหน่อย"',
      'read_at': DateTime.now().subtract(const Duration(hours: 2)),
      'created_at': DateTime.now().subtract(const Duration(hours: 2)),
      'data': {'post_id': '3', 'comment_id': '1'},
    },
    {
      'id': '4',
      'type': 'quota_warning',
      'title': 'โควต้าใกล้หมด',
      'body': 'คุณใช้โควต้า AI Generations ไปแล้ว 80% แนะนำให้อัพเกรดแพ็กเกจ',
      'read_at': DateTime.now().subtract(const Duration(hours: 3)),
      'created_at': DateTime.now().subtract(const Duration(hours: 3)),
      'data': null,
    },
    {
      'id': '5',
      'type': 'campaign_completed',
      'title': 'แคมเปญเสร็จสิ้น',
      'body': 'แคมเปญ "คริสต์มาส 2024" เสร็จสิ้นแล้ว ดูผลลัพธ์ได้ที่หน้าสถิติ',
      'read_at': DateTime.now().subtract(const Duration(days: 1)),
      'created_at': DateTime.now().subtract(const Duration(days: 1)),
      'data': {'campaign_id': '2'},
    },
    {
      'id': '6',
      'type': 'rental_expiring',
      'title': 'แพ็กเกจใกล้หมดอายุ',
      'body': 'แพ็กเกจ Pro ของคุณจะหมดอายุใน 10 วัน ต่ออายุเพื่อใช้งานต่อเนื่อง',
      'read_at': DateTime.now().subtract(const Duration(days: 2)),
      'created_at': DateTime.now().subtract(const Duration(days: 2)),
      'data': null,
    },
    {
      'id': '7',
      'type': 'post_failed',
      'title': 'โพสต์ล้มเหลว',
      'body': 'ไม่สามารถโพสต์ไปยัง Twitter ได้ กรุณาตรวจสอบการเชื่อมต่อบัญชี',
      'read_at': DateTime.now().subtract(const Duration(days: 3)),
      'created_at': DateTime.now().subtract(const Duration(days: 3)),
      'data': {'post_id': '4'},
    },
  ];

  int get _unreadCount => _notifications.where((n) => n['read_at'] == null).length;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('การแจ้งเตือน'),
        actions: [
          if (_unreadCount > 0)
            TextButton(
              onPressed: _markAllAsRead,
              child: const Text('อ่านทั้งหมด'),
            ),
          IconButton(
            icon: const Icon(Iconsax.setting_2),
            onPressed: () => _showNotificationSettings(context),
            tooltip: 'ตั้งค่าการแจ้งเตือน',
          ),
        ],
      ),
      body: _notifications.isEmpty
          ? _buildEmptyState()
          : ListView.builder(
              padding: const EdgeInsets.symmetric(vertical: AppConstants.spacingSm),
              itemCount: _notifications.length,
              itemBuilder: (context, index) {
                return _buildNotificationItem(_notifications[index]);
              },
            ),
    );
  }

  Widget _buildEmptyState() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            Iconsax.notification,
            size: 64,
            color: AppColors.textMuted.withValues(alpha:0.5),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          const Text(
            'ไม่มีการแจ้งเตือน',
            style: TextStyle(
              fontSize: 16,
              color: AppColors.textMuted,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildNotificationItem(Map<String, dynamic> notification) {
    final type = notification['type'] as String;
    final isUnread = notification['read_at'] == null;
    final createdAt = notification['created_at'] as DateTime;

    final config = _getNotificationConfig(type);

    return Dismissible(
      key: Key(notification['id']),
      direction: DismissDirection.endToStart,
      background: Container(
        alignment: Alignment.centerRight,
        padding: const EdgeInsets.only(right: AppConstants.spacingMd),
        color: AppColors.error,
        child: const Icon(Iconsax.trash, color: Colors.white),
      ),
      onDismissed: (direction) {
        setState(() {
          _notifications.removeWhere((n) => n['id'] == notification['id']);
        });
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('ลบการแจ้งเตือนแล้ว'),
            backgroundColor: AppColors.error,
          ),
        );
      },
      child: InkWell(
        onTap: () => _handleNotificationTap(notification),
        child: Container(
          padding: const EdgeInsets.all(AppConstants.spacingMd),
          decoration: BoxDecoration(
            color: isUnread ? AppColors.primary.withValues(alpha:0.05) : Colors.transparent,
            border: const Border(
              bottom: BorderSide(color: AppColors.border, width: 0.5),
            ),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Icon
              Container(
                width: 44,
                height: 44,
                decoration: BoxDecoration(
                  color: config['color'].withValues(alpha:0.15),
                  borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                ),
                child: Icon(config['icon'], color: config['color'], size: 22),
              ),
              const SizedBox(width: 12),

              // Content
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: Text(
                            notification['title'],
                            style: TextStyle(
                              fontSize: 14,
                              fontWeight: isUnread ? FontWeight.w600 : FontWeight.w500,
                              color: AppColors.textPrimary,
                            ),
                          ),
                        ),
                        Text(
                          _formatTimeAgo(createdAt),
                          style: const TextStyle(
                            fontSize: 11,
                            color: AppColors.textMuted,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Text(
                      notification['body'],
                      style: const TextStyle(
                        fontSize: 13,
                        color: AppColors.textSecondary,
                        height: 1.4,
                      ),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ],
                ),
              ),

              // Unread indicator
              if (isUnread)
                Container(
                  width: 8,
                  height: 8,
                  margin: const EdgeInsets.only(left: 8, top: 4),
                  decoration: const BoxDecoration(
                    color: AppColors.primary,
                    shape: BoxShape.circle,
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }

  Map<String, dynamic> _getNotificationConfig(String type) {
    switch (type) {
      case 'post_published':
        return {'icon': Iconsax.tick_circle, 'color': AppColors.success};
      case 'post_failed':
        return {'icon': Iconsax.close_circle, 'color': AppColors.error};
      case 'viral_alert':
        return {'icon': Iconsax.flash_1, 'color': AppColors.viral};
      case 'new_comment':
        return {'icon': Iconsax.message, 'color': AppColors.info};
      case 'quota_warning':
        return {'icon': Iconsax.warning_2, 'color': AppColors.warning};
      case 'campaign_completed':
        return {'icon': Iconsax.chart_2, 'color': AppColors.primary};
      case 'rental_expiring':
        return {'icon': Iconsax.calendar_1, 'color': AppColors.warning};
      case 'account_issue':
        return {'icon': Iconsax.shield_cross, 'color': AppColors.error};
      default:
        return {'icon': Iconsax.notification, 'color': AppColors.textMuted};
    }
  }

  String _formatTimeAgo(DateTime dateTime) {
    final now = DateTime.now();
    final difference = now.difference(dateTime);

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

  void _markAllAsRead() {
    setState(() {
      for (var notification in _notifications) {
        notification['read_at'] = DateTime.now();
      }
    });
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('อ่านการแจ้งเตือนทั้งหมดแล้ว'),
        backgroundColor: AppColors.success,
      ),
    );
  }

  void _handleNotificationTap(Map<String, dynamic> notification) {
    // Mark as read
    setState(() {
      notification['read_at'] = DateTime.now();
    });

    // Navigate based on type
    final type = notification['type'] as String;
    final data = notification['data'] as Map<String, dynamic>?;

    switch (type) {
      case 'post_published':
      case 'post_failed':
      case 'viral_alert':
      case 'new_comment':
        if (data?['post_id'] != null) {
          context.push('/posts/${data!['post_id']}');
        }
        break;
      case 'campaign_completed':
        if (data?['campaign_id'] != null) {
          context.push('/campaigns/${data!['campaign_id']}');
        }
        break;
      case 'quota_warning':
      case 'rental_expiring':
        context.push('/subscription');
        break;
      case 'account_issue':
        context.push('/accounts');
        break;
    }
  }

  void _showNotificationSettings(BuildContext context) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.card,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppConstants.radiusLg),
        ),
      ),
      builder: (context) => DraggableScrollableSheet(
        initialChildSize: 0.7,
        minChildSize: 0.5,
        maxChildSize: 0.9,
        expand: false,
        builder: (context, scrollController) => _NotificationSettingsSheet(
          scrollController: scrollController,
        ),
      ),
    );
  }
}

class _NotificationSettingsSheet extends StatefulWidget {
  final ScrollController scrollController;

  const _NotificationSettingsSheet({required this.scrollController});

  @override
  State<_NotificationSettingsSheet> createState() => _NotificationSettingsSheetState();
}

class _NotificationSettingsSheetState extends State<_NotificationSettingsSheet> {
  bool _pushEnabled = true;
  bool _emailEnabled = false;
  bool _postPublished = true;
  bool _postFailed = true;
  bool _viralAlert = true;
  bool _newComment = true;
  bool _campaignCompleted = true;
  bool _rentalExpiring = true;
  bool _quotaWarning = true;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Container(
          margin: const EdgeInsets.only(top: 12),
          width: 40,
          height: 4,
          decoration: BoxDecoration(
            color: AppColors.border,
            borderRadius: BorderRadius.circular(2),
          ),
        ),
        Padding(
          padding: const EdgeInsets.all(AppConstants.spacingMd),
          child: Row(
            children: [
              const Text(
                'ตั้งค่าการแจ้งเตือน',
                style: TextStyle(
                  fontSize: 18,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
              ),
              const Spacer(),
              IconButton(
                icon: const Icon(Iconsax.close_circle, color: AppColors.textMuted),
                onPressed: () => Navigator.pop(context),
              ),
            ],
          ),
        ),
        const Divider(color: AppColors.border, height: 1),
        Expanded(
          child: ListView(
            controller: widget.scrollController,
            padding: const EdgeInsets.all(AppConstants.spacingMd),
            children: [
              // Notification Channels
              const Text(
                'ช่องทางการแจ้งเตือน',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: AppConstants.spacingSm),
              _buildSwitchTile(
                'Push Notifications',
                'รับการแจ้งเตือนผ่านแอพ',
                Iconsax.notification,
                _pushEnabled,
                (value) => setState(() => _pushEnabled = value),
              ),
              _buildSwitchTile(
                'Email',
                'รับการแจ้งเตือนผ่านอีเมล',
                Iconsax.sms,
                _emailEnabled,
                (value) => setState(() => _emailEnabled = value),
              ),
              const SizedBox(height: AppConstants.spacingMd),

              // Notification Types
              const Text(
                'ประเภทการแจ้งเตือน',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: AppConstants.spacingSm),
              _buildSwitchTile(
                'โพสต์สำเร็จ',
                'เมื่อโพสต์ถูกเผยแพร่เรียบร้อย',
                Iconsax.tick_circle,
                _postPublished,
                (value) => setState(() => _postPublished = value),
              ),
              _buildSwitchTile(
                'โพสต์ล้มเหลว',
                'เมื่อโพสต์ไม่สามารถเผยแพร่ได้',
                Iconsax.close_circle,
                _postFailed,
                (value) => setState(() => _postFailed = value),
              ),
              _buildSwitchTile(
                'Viral Alert',
                'เมื่อโพสต์มี engagement สูงผิดปกติ',
                Iconsax.flash_1,
                _viralAlert,
                (value) => setState(() => _viralAlert = value),
              ),
              _buildSwitchTile(
                'คอมเมนต์ใหม่',
                'เมื่อมีคนคอมเมนต์โพสต์ของคุณ',
                Iconsax.message,
                _newComment,
                (value) => setState(() => _newComment = value),
              ),
              _buildSwitchTile(
                'แคมเปญเสร็จสิ้น',
                'เมื่อแคมเปญทำงานเสร็จสมบูรณ์',
                Iconsax.chart_2,
                _campaignCompleted,
                (value) => setState(() => _campaignCompleted = value),
              ),
              _buildSwitchTile(
                'แพ็กเกจใกล้หมด',
                'เมื่อแพ็กเกจใกล้หมดอายุ',
                Iconsax.calendar_1,
                _rentalExpiring,
                (value) => setState(() => _rentalExpiring = value),
              ),
              _buildSwitchTile(
                'โควต้าใกล้หมด',
                'เมื่อโควต้าการใช้งานใกล้ถึงขีดจำกัด',
                Iconsax.warning_2,
                _quotaWarning,
                (value) => setState(() => _quotaWarning = value),
              ),
              SizedBox(height: MediaQuery.of(context).padding.bottom + AppConstants.spacingMd),
            ],
          ),
        ),
      ],
    );
  }

  Widget _buildSwitchTile(
    String title,
    String subtitle,
    IconData icon,
    bool value,
    Function(bool) onChanged,
  ) {
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      ),
      child: ListTile(
        leading: Icon(icon, color: AppColors.textSecondary, size: 22),
        title: Text(
          title,
          style: const TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w500,
            color: AppColors.textPrimary,
          ),
        ),
        subtitle: Text(
          subtitle,
          style: const TextStyle(
            fontSize: 12,
            color: AppColors.textMuted,
          ),
        ),
        trailing: Switch(
          value: value,
          onChanged: onChanged,
        ),
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      ),
    );
  }
}
