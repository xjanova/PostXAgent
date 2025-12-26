import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';

import '../../../core/constants/app_constants.dart';

class CampaignDetailScreen extends ConsumerStatefulWidget {
  final String campaignId;

  const CampaignDetailScreen({super.key, required this.campaignId});

  @override
  ConsumerState<CampaignDetailScreen> createState() => _CampaignDetailScreenState();
}

class _CampaignDetailScreenState extends ConsumerState<CampaignDetailScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  // Sample campaign data
  final Map<String, dynamic> _campaign = {
    'id': '1',
    'name': 'โปรโมชั่นปีใหม่ 2025',
    'description': 'แคมเปญโปรโมทสินค้าลดราคาช่วงปีใหม่ ลดสูงสุด 50% ทุกชิ้น พร้อมส่งฟรีทั่วไทย',
    'status': 'active',
    'brand': 'ร้านขายของออนไลน์',
    'total_posts': 15,
    'published_posts': 8,
    'scheduled_posts': 5,
    'draft_posts': 2,
    'platforms': ['facebook', 'instagram', 'tiktok'],
    'start_date': '2024-12-20',
    'end_date': '2025-01-05',
    'budget': 15000,
    'spent': 8500,
    'stats': {
      'total_reach': 45200,
      'total_engagement': 12450,
      'total_clicks': 3200,
      'avg_engagement_rate': 4.2,
    },
  };

  final List<Map<String, dynamic>> _posts = [
    {
      'id': '1',
      'content': 'โปรโมชั่นสุดพิเศษต้อนรับปีใหม่! ลดราคาทุกชิ้นสูงสุด 50%',
      'platform': 'facebook',
      'status': 'published',
      'likes': 245,
      'comments': 32,
      'shares': 18,
      'scheduled_at': null,
      'published_at': '2024-12-22 14:30',
    },
    {
      'id': '2',
      'content': 'Flash Sale! เฉพาะวันนี้เท่านั้น ลดเพิ่มอีก 10%',
      'platform': 'instagram',
      'status': 'published',
      'likes': 189,
      'comments': 28,
      'shares': 15,
      'scheduled_at': null,
      'published_at': '2024-12-21 10:00',
    },
    {
      'id': '3',
      'content': 'สินค้ามาใหม่! คอลเลคชั่นปีใหม่ 2025',
      'platform': 'tiktok',
      'status': 'scheduled',
      'likes': 0,
      'comments': 0,
      'shares': 0,
      'scheduled_at': '2024-12-25 09:00',
      'published_at': null,
    },
  ];

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 3, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final status = _campaign['status'] as String;
    final totalPosts = _campaign['total_posts'] as int;
    final publishedPosts = _campaign['published_posts'] as int;
    final progress = totalPosts > 0 ? publishedPosts / totalPosts : 0.0;

    return Scaffold(
      body: NestedScrollView(
        headerSliverBuilder: (context, innerBoxIsScrolled) => [
          SliverAppBar(
            expandedHeight: 180,
            pinned: true,
            backgroundColor: AppColors.background,
            flexibleSpace: FlexibleSpaceBar(
              background: Container(
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [
                      CampaignStatus.getColor(status),
                      CampaignStatus.getColor(status).withValues(alpha:0.6),
                    ],
                  ),
                ),
                child: SafeArea(
                  child: Padding(
                    padding: const EdgeInsets.all(AppConstants.spacingMd),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.end,
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 10,
                                vertical: 4,
                              ),
                              decoration: BoxDecoration(
                                color: Colors.white.withValues(alpha:0.2),
                                borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                              ),
                              child: Text(
                                CampaignStatus.getLabel(status),
                                style: const TextStyle(
                                  fontSize: 12,
                                  fontWeight: FontWeight.w600,
                                  color: Colors.white,
                                ),
                              ),
                            ),
                            const Spacer(),
                            Text(
                              '${(progress * 100).toInt()}%',
                              style: const TextStyle(
                                fontSize: 24,
                                fontWeight: FontWeight.bold,
                                color: Colors.white,
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 8),
                        Text(
                          _campaign['name'],
                          style: const TextStyle(
                            fontSize: 22,
                            fontWeight: FontWeight.bold,
                            color: Colors.white,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          _campaign['brand'],
                          style: TextStyle(
                            fontSize: 14,
                            color: Colors.white.withValues(alpha:0.8),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
            actions: [
              IconButton(
                icon: const Icon(Iconsax.edit_2),
                onPressed: () {
                  context.push('/campaigns/${widget.campaignId}/edit');
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
                    case 'pause':
                      // TODO: Pause campaign
                      break;
                    case 'duplicate':
                      // TODO: Duplicate campaign
                      break;
                    case 'delete':
                      _showDeleteConfirm(context);
                      break;
                  }
                },
                itemBuilder: (context) => [
                  PopupMenuItem(
                    value: 'pause',
                    child: Row(
                      children: [
                        Icon(
                          status == 'active' ? Iconsax.pause : Iconsax.play,
                          size: 18,
                          color: AppColors.textSecondary,
                        ),
                        const SizedBox(width: 10),
                        Text(status == 'active' ? 'หยุดชั่วคราว' : 'เริ่มต่อ'),
                      ],
                    ),
                  ),
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
          SliverToBoxAdapter(
            child: Container(
              color: AppColors.background,
              child: TabBar(
                controller: _tabController,
                tabs: const [
                  Tab(text: 'ภาพรวม'),
                  Tab(text: 'โพสต์'),
                  Tab(text: 'สถิติ'),
                ],
              ),
            ),
          ),
        ],
        body: TabBarView(
          controller: _tabController,
          children: [
            _buildOverviewTab(),
            _buildPostsTab(),
            _buildStatsTab(),
          ],
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () {
          context.push('/posts/create?campaign_id=${widget.campaignId}');
        },
        icon: const Icon(Iconsax.add),
        label: const Text('เพิ่มโพสต์'),
      ),
    );
  }

  Widget _buildOverviewTab() {
    final totalPosts = _campaign['total_posts'] as int;
    final publishedPosts = _campaign['published_posts'] as int;
    final scheduledPosts = _campaign['scheduled_posts'] as int;
    final draftPosts = _campaign['draft_posts'] as int;
    final platforms = _campaign['platforms'] as List<String>;
    final stats = _campaign['stats'] as Map<String, dynamic>;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Description
          Container(
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
                  'รายละเอียด',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: AppConstants.spacingSm),
                Text(
                  _campaign['description'],
                  style: const TextStyle(
                    fontSize: 14,
                    color: AppColors.textSecondary,
                    height: 1.5,
                  ),
                ),
                const SizedBox(height: AppConstants.spacingMd),
                Row(
                  children: [
                    const Icon(Iconsax.calendar_1, size: 16, color: AppColors.textMuted),
                    const SizedBox(width: 6),
                    Text(
                      '${_campaign['start_date']} - ${_campaign['end_date']}',
                      style: const TextStyle(
                        fontSize: 13,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),

          // Platforms
          Container(
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
                  'แพลตฟอร์ม',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: AppConstants.spacingMd),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: platforms.map((p) {
                    final config = Platforms.all[p];
                    return Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                      decoration: BoxDecoration(
                        color: (config?.color ?? AppColors.textMuted).withValues(alpha:0.15),
                        borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(
                            _getPlatformIcon(p),
                            size: 16,
                            color: config?.color ?? AppColors.textMuted,
                          ),
                          const SizedBox(width: 6),
                          Text(
                            config?.name ?? p,
                            style: TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w500,
                              color: config?.color ?? AppColors.textMuted,
                            ),
                          ),
                        ],
                      ),
                    );
                  }).toList(),
                ),
              ],
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),

          // Post Progress
          Container(
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
                  'ความคืบหน้าโพสต์',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: AppConstants.spacingMd),
                Row(
                  children: [
                    _buildPostCountCard('โพสต์แล้ว', publishedPosts, AppColors.success),
                    const SizedBox(width: 8),
                    _buildPostCountCard('ตั้งเวลา', scheduledPosts, AppColors.info),
                    const SizedBox(width: 8),
                    _buildPostCountCard('แบบร่าง', draftPosts, AppColors.warning),
                  ],
                ),
                const SizedBox(height: AppConstants.spacingMd),
                ClipRRect(
                  borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                  child: LinearProgressIndicator(
                    value: publishedPosts / totalPosts,
                    backgroundColor: AppColors.surface,
                    valueColor: const AlwaysStoppedAnimation(AppColors.success),
                    minHeight: 8,
                  ),
                ),
                const SizedBox(height: AppConstants.spacingSm),
                Text(
                  '$publishedPosts / $totalPosts โพสต์สำเร็จ',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textMuted,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),

          // Quick Stats
          Row(
            children: [
              Expanded(
                child: _buildStatCard(
                  'การเข้าถึง',
                  _formatNumber(stats['total_reach']),
                  Iconsax.eye,
                  AppColors.info,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _buildStatCard(
                  'Engagement',
                  _formatNumber(stats['total_engagement']),
                  Iconsax.heart,
                  AppColors.error,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: _buildStatCard(
                  'คลิก',
                  _formatNumber(stats['total_clicks']),
                  Iconsax.mouse_1,
                  AppColors.success,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _buildStatCard(
                  'Engagement Rate',
                  '${stats['avg_engagement_rate']}%',
                  Iconsax.chart_1,
                  AppColors.secondary,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spacingXl),
        ],
      ),
    );
  }

  Widget _buildPostsTab() {
    return ListView.builder(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      itemCount: _posts.length,
      itemBuilder: (context, index) {
        final post = _posts[index];
        return _buildPostCard(post);
      },
    );
  }

  Widget _buildPostCard(Map<String, dynamic> post) {
    final platform = post['platform'] as String;
    final status = post['status'] as String;
    final platformConfig = Platforms.all[platform];
    final color = platformConfig?.color ?? AppColors.textMuted;

    return Container(
      margin: const EdgeInsets.only(bottom: AppConstants.spacingMd),
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: InkWell(
        onTap: () {
          context.push('/posts/${post['id']}');
        },
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(6),
                  decoration: BoxDecoration(
                    color: color.withValues(alpha:0.15),
                    borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                  ),
                  child: Icon(_getPlatformIcon(platform), size: 16, color: color),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: Text(
                    platformConfig?.name ?? platform,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                      color: AppColors.textSecondary,
                    ),
                  ),
                ),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: PostStatus.getColor(status).withValues(alpha:0.15),
                    borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                  ),
                  child: Text(
                    PostStatus.getLabel(status),
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w500,
                      color: PostStatus.getColor(status),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppConstants.spacingMd),
            Text(
              post['content'],
              style: const TextStyle(
                fontSize: 14,
                color: AppColors.textPrimary,
                height: 1.5,
              ),
              maxLines: 2,
              overflow: TextOverflow.ellipsis,
            ),
            const SizedBox(height: AppConstants.spacingMd),
            Row(
              children: [
                if (status == 'published') ...[
                  _buildMetricChip(Iconsax.heart, '${post['likes']}'),
                  const SizedBox(width: 12),
                  _buildMetricChip(Iconsax.message, '${post['comments']}'),
                  const SizedBox(width: 12),
                  _buildMetricChip(Iconsax.share, '${post['shares']}'),
                ] else if (status == 'scheduled') ...[
                  const Icon(Iconsax.clock, size: 14, color: AppColors.info),
                  const SizedBox(width: 4),
                  Text(
                    post['scheduled_at'] ?? '',
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.info,
                    ),
                  ),
                ],
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildStatsTab() {
    final stats = _campaign['stats'] as Map<String, dynamic>;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Summary Cards
          GridView.count(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            crossAxisCount: 2,
            mainAxisSpacing: 12,
            crossAxisSpacing: 12,
            childAspectRatio: 1.4,
            children: [
              _buildStatDetailCard(
                'การเข้าถึงทั้งหมด',
                _formatNumber(stats['total_reach']),
                '+12.5%',
                true,
                Iconsax.eye,
                AppColors.info,
              ),
              _buildStatDetailCard(
                'Engagement ทั้งหมด',
                _formatNumber(stats['total_engagement']),
                '+8.2%',
                true,
                Iconsax.heart,
                AppColors.error,
              ),
              _buildStatDetailCard(
                'คลิกทั้งหมด',
                _formatNumber(stats['total_clicks']),
                '+15.3%',
                true,
                Iconsax.mouse_1,
                AppColors.success,
              ),
              _buildStatDetailCard(
                'Engagement Rate',
                '${stats['avg_engagement_rate']}%',
                '+2.1%',
                true,
                Iconsax.chart_1,
                AppColors.secondary,
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spacingMd),

          // Platform Breakdown
          Container(
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
                  'ประสิทธิภาพตามแพลตฟอร์ม',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: AppConstants.spacingMd),
                _buildPlatformRow('Facebook', 18500, 5200, AppColors.facebook),
                const SizedBox(height: AppConstants.spacingSm),
                _buildPlatformRow('Instagram', 15200, 4800, AppColors.instagram),
                const SizedBox(height: AppConstants.spacingSm),
                _buildPlatformRow('TikTok', 11500, 2450, AppColors.tiktokAccent),
              ],
            ),
          ),
          const SizedBox(height: AppConstants.spacingXl),
        ],
      ),
    );
  }

  Widget _buildPostCountCard(String label, int count, Color color) {
    return Expanded(
      child: Container(
        padding: const EdgeInsets.all(AppConstants.spacingSm),
        decoration: BoxDecoration(
          color: color.withValues(alpha:0.1),
          borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        ),
        child: Column(
          children: [
            Text(
              count.toString(),
              style: TextStyle(
                fontSize: 24,
                fontWeight: FontWeight.bold,
                color: color,
              ),
            ),
            Text(
              label,
              style: const TextStyle(
                fontSize: 11,
                color: AppColors.textMuted,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildStatCard(String label, String value, IconData icon, Color color) {
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
          Icon(icon, color: color, size: 24),
          const SizedBox(height: AppConstants.spacingSm),
          Text(
            value,
            style: const TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          Text(
            label,
            style: const TextStyle(
              fontSize: 12,
              color: AppColors.textSecondary,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildStatDetailCard(
    String label,
    String value,
    String change,
    bool isPositive,
    IconData icon,
    Color color,
  ) {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Icon(icon, color: color, size: 20),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                decoration: BoxDecoration(
                  color: (isPositive ? AppColors.success : AppColors.error).withValues(alpha:0.15),
                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                ),
                child: Text(
                  change,
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w500,
                    color: isPositive ? AppColors.success : AppColors.error,
                  ),
                ),
              ),
            ],
          ),
          const Spacer(),
          Text(
            value,
            style: const TextStyle(
              fontSize: 22,
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          Text(
            label,
            style: const TextStyle(
              fontSize: 11,
              color: AppColors.textMuted,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPlatformRow(String name, int reach, int engagement, Color color) {
    return Row(
      children: [
        Container(
          width: 8,
          height: 8,
          decoration: BoxDecoration(
            color: color,
            shape: BoxShape.circle,
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          flex: 2,
          child: Text(
            name,
            style: const TextStyle(
              fontSize: 14,
              color: AppColors.textPrimary,
            ),
          ),
        ),
        Expanded(
          child: Text(
            _formatNumber(reach),
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w500,
              color: AppColors.textSecondary,
            ),
            textAlign: TextAlign.right,
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            _formatNumber(engagement),
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w500,
              color: AppColors.textSecondary,
            ),
            textAlign: TextAlign.right,
          ),
        ),
      ],
    );
  }

  Widget _buildMetricChip(IconData icon, String value) {
    return Row(
      children: [
        Icon(icon, size: 14, color: AppColors.textMuted),
        const SizedBox(width: 4),
        Text(
          value,
          style: const TextStyle(
            fontSize: 12,
            color: AppColors.textMuted,
          ),
        ),
      ],
    );
  }

  IconData _getPlatformIcon(String platform) {
    switch (platform) {
      case 'facebook':
        return Icons.facebook;
      case 'instagram':
        return Iconsax.instagram;
      case 'tiktok':
        return Iconsax.video_square;
      case 'twitter':
        return Iconsax.message;
      case 'line':
        return Iconsax.message_text;
      case 'youtube':
        return Iconsax.video_play;
      default:
        return Iconsax.global;
    }
  }

  String _formatNumber(int number) {
    if (number >= 1000000) {
      return '${(number / 1000000).toStringAsFixed(1)}M';
    } else if (number >= 1000) {
      return '${(number / 1000).toStringAsFixed(1)}K';
    }
    return number.toString();
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
        content: const Text(
          'คุณต้องการลบแคมเปญนี้หรือไม่? การดำเนินการนี้จะลบโพสต์ทั้งหมดในแคมเปญ',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('ยกเลิก'),
          ),
          ElevatedButton(
            onPressed: () {
              Navigator.pop(context);
              Navigator.pop(context);
              // TODO: Delete campaign
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
