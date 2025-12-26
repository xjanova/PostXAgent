import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:iconsax/iconsax.dart';

import '../../../core/constants/app_constants.dart';

class PostDetailScreen extends ConsumerWidget {
  final String postId;

  const PostDetailScreen({super.key, required this.postId});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('รายละเอียดโพสต์'),
        actions: [
          IconButton(
            icon: const Icon(Iconsax.edit_2),
            onPressed: () {
              // TODO: Navigate to edit
            },
          ),
          PopupMenuButton<String>(
            icon: const Icon(Iconsax.more),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            ),
            color: AppColors.card,
            onSelected: (value) {
              switch (value) {
                case 'duplicate':
                  // TODO: Duplicate post
                  break;
                case 'delete':
                  _showDeleteConfirm(context);
                  break;
              }
            },
            itemBuilder: (context) => [
              const PopupMenuItem(
                value: 'duplicate',
                child: Row(
                  children: [
                    Icon(Iconsax.copy, size: 18, color: AppColors.textSecondary),
                    SizedBox(width: 10),
                    Text('ทำซ้ำ'),
                  ],
                ),
              ),
              const PopupMenuItem(
                value: 'delete',
                child: Row(
                  children: [
                    Icon(Iconsax.trash, size: 18, color: AppColors.error),
                    SizedBox(width: 10),
                    Text('ลบ', style: TextStyle(color: AppColors.error)),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppConstants.spacingMd),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Status Card
            _buildStatusCard(),
            const SizedBox(height: AppConstants.spacingMd),

            // Content Card
            _buildContentCard(),
            const SizedBox(height: AppConstants.spacingMd),

            // Metrics Card
            _buildMetricsCard(),
            const SizedBox(height: AppConstants.spacingMd),

            // Schedule Card
            _buildScheduleCard(),
            const SizedBox(height: AppConstants.spacingLg),
          ],
        ),
      ),
      bottomNavigationBar: Builder(builder: (context) => _buildBottomActions(context)),
    );
  }

  Widget _buildStatusCard() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Row(
        children: [
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              color: AppColors.facebook.withValues(alpha:0.15),
              borderRadius: BorderRadius.circular(AppConstants.radiusSm),
            ),
            child: const Icon(Icons.facebook, color: AppColors.facebook, size: 24),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    const Text(
                      'Facebook',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(width: 8),
                    Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                      decoration: BoxDecoration(
                        color: AppColors.success.withValues(alpha:0.15),
                        borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                      ),
                      child: const Text(
                        'โพสต์แล้ว',
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.w500,
                          color: AppColors.success,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                const Text(
                  'โพสต์เมื่อ 22 ธ.ค. 2024 เวลา 14:30',
                  style: TextStyle(
                    fontSize: 12,
                    color: AppColors.textMuted,
                  ),
                ),
              ],
            ),
          ),
          IconButton(
            icon: const Icon(Iconsax.export_1, color: AppColors.primary),
            onPressed: () {
              // TODO: Open post URL
            },
          ),
        ],
      ),
    );
  }

  Widget _buildContentCard() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              const Text(
                'เนื้อหา',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(width: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                decoration: BoxDecoration(
                  color: AppColors.secondary.withValues(alpha:0.15),
                  borderRadius: BorderRadius.circular(4),
                ),
                child: const Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Iconsax.magic_star, size: 12, color: AppColors.secondary),
                    SizedBox(width: 4),
                    Text(
                      'AI Generated',
                      style: TextStyle(
                        fontSize: 10,
                        fontWeight: FontWeight.w500,
                        color: AppColors.secondary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spacingMd),
          const Text(
            'โปรโมชั่นสุดพิเศษต้อนรับปีใหม่! 🎉\n\nลดราคาสินค้าทุกชิ้นสูงสุด 50% เฉพาะช่วงวันที่ 25-31 ธันวาคม 2024 นี้เท่านั้น!\n\n✨ สิทธิพิเศษสำหรับลูกค้า:\n- ลด 50% ทุกชิ้น\n- ส่งฟรีทั่วไทย\n- แลกพอยต์ได้ 2 เท่า\n\nอย่าพลาด! สั่งซื้อเลยวันนี้ 🛒\n\n#โปรโมชั่น #ลดราคา #ปีใหม่2025 #ช้อปสนุก',
            style: TextStyle(
              fontSize: 14,
              color: AppColors.textPrimary,
              height: 1.6,
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          // Media Preview
          Container(
            height: 200,
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            ),
            child: const Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  Icon(Iconsax.gallery, size: 48, color: AppColors.textMuted),
                  SizedBox(height: 8),
                  Text(
                    'รูปภาพโปรโมชั่น',
                    style: TextStyle(color: AppColors.textMuted),
                  ),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildMetricsCard() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text(
                'Engagement',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    colors: [
                      AppColors.viral.withValues(alpha:0.2),
                      AppColors.trending.withValues(alpha:0.2),
                    ],
                  ),
                  borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                ),
                child: const Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Iconsax.flash_1, size: 14, color: AppColors.viral),
                    SizedBox(width: 4),
                    Text(
                      'Viral Score: 85',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: AppColors.viral,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spacingMd),
          Row(
            children: [
              Expanded(child: _metricBox(Iconsax.heart, '2,456', 'Likes', AppColors.error)),
              const SizedBox(width: 12),
              Expanded(child: _metricBox(Iconsax.message, '189', 'Comments', AppColors.info)),
              const SizedBox(width: 12),
              Expanded(child: _metricBox(Iconsax.share, '567', 'Shares', AppColors.success)),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(child: _metricBox(Iconsax.eye, '15.2K', 'Views', AppColors.textSecondary)),
              const SizedBox(width: 12),
              Expanded(child: _metricBox(Iconsax.people, '8.5K', 'Reach', AppColors.primary)),
              const SizedBox(width: 12),
              Expanded(child: _metricBox(Iconsax.chart_1, '4.2%', 'Rate', AppColors.secondary)),
            ],
          ),
        ],
      ),
    );
  }

  Widget _metricBox(IconData icon, String value, String label, Color color) {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingSm),
      decoration: BoxDecoration(
        color: color.withValues(alpha:0.1),
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      ),
      child: Column(
        children: [
          Icon(icon, color: color, size: 20),
          const SizedBox(height: 4),
          Text(
            value,
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.bold,
              color: color,
            ),
          ),
          Text(
            label,
            style: const TextStyle(
              fontSize: 10,
              color: AppColors.textMuted,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildScheduleCard() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'ข้อมูลเพิ่มเติม',
            style: TextStyle(
              fontSize: 16,
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          _infoRow('แบรนด์', 'ร้านขายของออนไลน์'),
          _infoRow('แคมเปญ', 'โปรโมชั่นปีใหม่ 2025'),
          _infoRow('สร้างเมื่อ', '20 ธ.ค. 2024 10:30'),
          _infoRow('โพสต์เมื่อ', '22 ธ.ค. 2024 14:30'),
          _infoRow('AI Provider', 'Google Gemini'),
        ],
      ),
    );
  }

  Widget _infoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(
            label,
            style: const TextStyle(
              fontSize: 13,
              color: AppColors.textMuted,
            ),
          ),
          Text(
            value,
            style: const TextStyle(
              fontSize: 13,
              fontWeight: FontWeight.w500,
              color: AppColors.textPrimary,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBottomActions(BuildContext context) {
    return Container(
      padding: EdgeInsets.only(
        left: AppConstants.spacingMd,
        right: AppConstants.spacingMd,
        bottom: AppConstants.spacingMd + MediaQuery.of(context).padding.bottom,
        top: AppConstants.spacingSm,
      ),
      decoration: const BoxDecoration(
        color: AppColors.backgroundSecondary,
        border: Border(top: BorderSide(color: AppColors.border)),
      ),
      child: Row(
        children: [
          Expanded(
            child: OutlinedButton.icon(
              onPressed: () {
                // TODO: View comments
              },
              icon: const Icon(Iconsax.message),
              label: const Text('ดูคอมเมนต์ (189)'),
              style: OutlinedButton.styleFrom(
                padding: const EdgeInsets.symmetric(vertical: 12),
              ),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: ElevatedButton.icon(
              onPressed: () {
                // TODO: View analytics
              },
              icon: const Icon(Iconsax.chart_2),
              label: const Text('ดูสถิติเพิ่มเติม'),
              style: ElevatedButton.styleFrom(
                padding: const EdgeInsets.symmetric(vertical: 12),
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _showDeleteConfirm(BuildContext context) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: AppColors.card,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        ),
        title: const Text('ยืนยันการลบ'),
        content: const Text('คุณต้องการลบโพสต์นี้หรือไม่? การดำเนินการนี้ไม่สามารถย้อนกลับได้'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('ยกเลิก'),
          ),
          ElevatedButton(
            onPressed: () {
              Navigator.pop(context);
              Navigator.pop(context);
              // TODO: Delete post
            },
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.error,
            ),
            child: const Text('ลบ'),
          ),
        ],
      ),
    );
  }
}
