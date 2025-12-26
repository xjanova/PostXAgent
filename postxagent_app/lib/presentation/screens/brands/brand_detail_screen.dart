import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';

import '../../../core/constants/app_constants.dart';

class BrandDetailScreen extends ConsumerStatefulWidget {
  final String brandId;

  const BrandDetailScreen({super.key, required this.brandId});

  @override
  ConsumerState<BrandDetailScreen> createState() => _BrandDetailScreenState();
}

class _BrandDetailScreenState extends ConsumerState<BrandDetailScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  // Sample brand data
  final Map<String, dynamic> _brand = {
    'id': '1',
    'name': 'ร้านขายของออนไลน์',
    'description': 'ร้านค้าออนไลน์ขายสินค้าหลากหลายประเภท ทั้งเสื้อผ้า เครื่องประดับ และอุปกรณ์อิเล็กทรอนิกส์',
    'website': 'https://myshop.com',
    'logo': null,
    'is_active': true,
    'created_at': '2024-01-15',
    'stats': {
      'total_posts': 124,
      'published_posts': 98,
      'scheduled_posts': 12,
      'total_campaigns': 8,
      'active_campaigns': 3,
      'total_reach': 45200,
      'total_engagement': 8920,
    },
  };

  final List<Map<String, dynamic>> _accounts = [
    {
      'id': '1',
      'platform': 'facebook',
      'name': 'ร้านขายของออนไลน์ Official',
      'username': '@myshop_official',
      'followers': 15420,
      'is_connected': true,
    },
    {
      'id': '2',
      'platform': 'instagram',
      'name': 'MyShop_TH',
      'username': '@myshop_th',
      'followers': 8930,
      'is_connected': true,
    },
    {
      'id': '3',
      'platform': 'tiktok',
      'name': 'MyShop',
      'username': '@myshop',
      'followers': 25600,
      'is_connected': true,
    },
    {
      'id': '4',
      'platform': 'twitter',
      'name': 'MyShop Thailand',
      'username': '@myshop_th',
      'followers': 3200,
      'is_connected': false,
    },
  ];

  final List<Map<String, dynamic>> _recentPosts = [
    {
      'id': '1',
      'content': 'โปรโมชั่นสุดพิเศษต้อนรับปีใหม่! ลดราคาทุกชิ้นสูงสุด 50%',
      'platform': 'facebook',
      'status': 'published',
      'likes': 245,
      'comments': 32,
      'shares': 18,
      'created_at': '2024-12-22',
    },
    {
      'id': '2',
      'content': 'สินค้ามาใหม่! คอลเลคชั่นฤดูหนาว 2025',
      'platform': 'instagram',
      'status': 'scheduled',
      'scheduled_at': '2024-12-25 10:00',
      'created_at': '2024-12-20',
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
    return Scaffold(
      body: NestedScrollView(
        headerSliverBuilder: (context, innerBoxIsScrolled) => [
          SliverAppBar(
            expandedHeight: 200,
            pinned: true,
            backgroundColor: AppColors.background,
            flexibleSpace: FlexibleSpaceBar(
              background: Container(
                decoration: const BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [
                      AppColors.primary,
                      AppColors.secondary,
                    ],
                  ),
                ),
                child: SafeArea(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      const SizedBox(height: 40),
                      Container(
                        width: 80,
                        height: 80,
                        decoration: BoxDecoration(
                          color: Colors.white,
                          borderRadius: BorderRadius.circular(AppConstants.radiusLg),
                          boxShadow: [
                            BoxShadow(
                              color: Colors.black.withValues(alpha:0.2),
                              blurRadius: 16,
                              offset: const Offset(0, 8),
                            ),
                          ],
                        ),
                        child: Center(
                          child: Text(
                            _brand['name'].toString().substring(0, 1),
                            style: const TextStyle(
                              fontSize: 36,
                              fontWeight: FontWeight.bold,
                              color: AppColors.primary,
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(height: 12),
                      Text(
                        _brand['name'],
                        style: const TextStyle(
                          fontSize: 20,
                          fontWeight: FontWeight.bold,
                          color: Colors.white,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
            actions: [
              IconButton(
                icon: const Icon(Iconsax.edit_2),
                onPressed: () {
                  context.push('/brands/${widget.brandId}/edit');
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
                      // TODO: Duplicate brand
                      break;
                    case 'archive':
                      // TODO: Archive brand
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
                    value: 'archive',
                    child: Row(
                      children: [
                        Icon(Iconsax.archive_1, size: 18, color: AppColors.textSecondary),
                        SizedBox(width: 10),
                        Text('เก็บถาวร'),
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
                  Tab(text: 'บัญชี'),
                  Tab(text: 'โพสต์'),
                ],
              ),
            ),
          ),
        ],
        body: TabBarView(
          controller: _tabController,
          children: [
            _buildOverviewTab(),
            _buildAccountsTab(),
            _buildPostsTab(),
          ],
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () {
          context.push('/posts/create?brand_id=${widget.brandId}');
        },
        icon: const Icon(Iconsax.add),
        label: const Text('สร้างโพสต์'),
      ),
    );
  }

  Widget _buildOverviewTab() {
    final stats = _brand['stats'] as Map<String, dynamic>;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Description Card
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
                  _brand['description'],
                  style: const TextStyle(
                    fontSize: 14,
                    color: AppColors.textSecondary,
                    height: 1.5,
                  ),
                ),
                if (_brand['website'] != null) ...[
                  const SizedBox(height: AppConstants.spacingMd),
                  Row(
                    children: [
                      const Icon(Iconsax.global, size: 16, color: AppColors.primary),
                      const SizedBox(width: 8),
                      Text(
                        _brand['website'],
                        style: const TextStyle(
                          fontSize: 14,
                          color: AppColors.primary,
                        ),
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),

          // Stats Grid
          GridView.count(
            shrinkWrap: true,
            physics: const NeverScrollableScrollPhysics(),
            crossAxisCount: 2,
            mainAxisSpacing: 12,
            crossAxisSpacing: 12,
            childAspectRatio: 1.5,
            children: [
              _buildStatCard(
                'โพสต์ทั้งหมด',
                stats['total_posts'].toString(),
                Iconsax.document_text,
                AppColors.primary,
              ),
              _buildStatCard(
                'โพสต์สำเร็จ',
                stats['published_posts'].toString(),
                Iconsax.tick_circle,
                AppColors.success,
              ),
              _buildStatCard(
                'แคมเปญทั้งหมด',
                stats['total_campaigns'].toString(),
                Iconsax.chart_2,
                AppColors.secondary,
              ),
              _buildStatCard(
                'แคมเปญกำลังทำงาน',
                stats['active_campaigns'].toString(),
                Iconsax.activity,
                AppColors.info,
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spacingMd),

          // Engagement Card
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
                  'Engagement รวม',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                const SizedBox(height: AppConstants.spacingMd),
                Row(
                  children: [
                    Expanded(
                      child: _buildEngagementItem(
                        'การเข้าถึง',
                        _formatNumber(stats['total_reach']),
                        Iconsax.eye,
                        AppColors.info,
                      ),
                    ),
                    Expanded(
                      child: _buildEngagementItem(
                        'Engagement',
                        _formatNumber(stats['total_engagement']),
                        Iconsax.heart,
                        AppColors.error,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: AppConstants.spacingLg),
        ],
      ),
    );
  }

  Widget _buildAccountsTab() {
    return ListView.builder(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      itemCount: _accounts.length + 1, // +1 for add button
      itemBuilder: (context, index) {
        if (index == _accounts.length) {
          return Container(
            margin: const EdgeInsets.only(bottom: 80),
            child: OutlinedButton.icon(
              onPressed: () {
                context.push('/accounts/connect?brand_id=${widget.brandId}');
              },
              icon: const Icon(Iconsax.add),
              label: const Text('เชื่อมต่อบัญชีใหม่'),
              style: OutlinedButton.styleFrom(
                padding: const EdgeInsets.all(AppConstants.spacingMd),
              ),
            ),
          );
        }

        final account = _accounts[index];
        return _buildAccountCard(account);
      },
    );
  }

  Widget _buildAccountCard(Map<String, dynamic> account) {
    final platform = account['platform'] as String;
    final isConnected = account['is_connected'] as bool;
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
      child: Row(
        children: [
          Container(
            width: 48,
            height: 48,
            decoration: BoxDecoration(
              color: color.withValues(alpha:0.15),
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            ),
            child: Icon(
              _getPlatformIcon(platform),
              color: color,
              size: 24,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        account['name'],
                        style: const TextStyle(
                          fontSize: 15,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                      ),
                    ),
                    Container(
                      width: 8,
                      height: 8,
                      decoration: BoxDecoration(
                        color: isConnected ? AppColors.success : AppColors.error,
                        shape: BoxShape.circle,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 2),
                Text(
                  account['username'],
                  style: const TextStyle(
                    fontSize: 13,
                    color: AppColors.textMuted,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  '${_formatNumber(account['followers'])} ผู้ติดตาม',
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          IconButton(
            icon: const Icon(Iconsax.setting_2, size: 20),
            color: AppColors.textMuted,
            onPressed: () {
              // TODO: Account settings
            },
          ),
        ],
      ),
    );
  }

  Widget _buildPostsTab() {
    return ListView.builder(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      itemCount: _recentPosts.length,
      itemBuilder: (context, index) {
        final post = _recentPosts[index];
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
                Icon(_getPlatformIcon(platform), color: color, size: 18),
                const SizedBox(width: 8),
                Text(
                  platformConfig?.name ?? platform,
                  style: TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                    color: color,
                  ),
                ),
                const Spacer(),
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
            if (status == 'published' && post['likes'] != null) ...[
              const SizedBox(height: AppConstants.spacingMd),
              Row(
                children: [
                  _buildMetricChip(Iconsax.heart, '${post['likes']}', AppColors.error),
                  const SizedBox(width: 12),
                  _buildMetricChip(Iconsax.message, '${post['comments']}', AppColors.info),
                  const SizedBox(width: 12),
                  _buildMetricChip(Iconsax.share, '${post['shares']}', AppColors.success),
                ],
              ),
            ],
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
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(icon, color: color, size: 24),
          const Spacer(),
          Text(
            value,
            style: const TextStyle(
              fontSize: 24,
              fontWeight: FontWeight.bold,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 2),
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

  Widget _buildEngagementItem(String label, String value, IconData icon, Color color) {
    return Column(
      children: [
        Icon(icon, color: color, size: 28),
        const SizedBox(height: 8),
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
    );
  }

  Widget _buildMetricChip(IconData icon, String value, Color color) {
    return Row(
      children: [
        Icon(icon, size: 14, color: color),
        const SizedBox(width: 4),
        Text(
          value,
          style: TextStyle(
            fontSize: 12,
            color: color,
            fontWeight: FontWeight.w500,
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
          'คุณต้องการลบแบรนด์นี้หรือไม่? การดำเนินการนี้จะลบโพสต์และแคมเปญทั้งหมดที่เกี่ยวข้อง',
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
              // TODO: Delete brand
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
