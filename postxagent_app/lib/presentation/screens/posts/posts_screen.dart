import 'dart:async';
import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';
import 'package:shimmer/shimmer.dart';
import 'package:timeago/timeago.dart' as timeago;

import '../../../core/constants/app_constants.dart';
import '../../../core/router/app_router.dart';
import '../../../data/models/post.dart';
import '../../../providers/post_provider.dart';
import '../../../providers/brand_provider.dart';
import '../../widgets/common/custom_text_field.dart';
import '../../widgets/common/custom_button.dart';

class PostsScreen extends ConsumerStatefulWidget {
  const PostsScreen({super.key});

  @override
  ConsumerState<PostsScreen> createState() => _PostsScreenState();
}

class _PostsScreenState extends ConsumerState<PostsScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final _searchController = TextEditingController();
  final _scrollController = ScrollController();
  String _selectedPlatform = 'all';
  Timer? _searchDebounce;

  final List<_TabItem> _tabs = const [
    _TabItem(null, 'ทั้งหมด', null),
    _TabItem('draft', 'แบบร่าง', AppColors.textMuted),
    _TabItem('scheduled', 'รอโพสต์', AppColors.info),
    _TabItem('published', 'โพสต์แล้ว', AppColors.success),
    _TabItem('failed', 'ล้มเหลว', AppColors.error),
  ];

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: _tabs.length, vsync: this);
    _tabController.addListener(_onTabChanged);
    _scrollController.addListener(_onScroll);

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _loadPosts();
    });
  }

  @override
  void dispose() {
    _tabController.removeListener(_onTabChanged);
    _tabController.dispose();
    _searchController.dispose();
    _scrollController.dispose();
    _searchDebounce?.cancel();
    super.dispose();
  }

  void _onTabChanged() {
    if (_tabController.indexIsChanging) return;
    final status = _tabs[_tabController.index].status;
    ref.read(postsNotifierProvider.notifier).setStatusFilter(status);
  }

  void _onScroll() {
    if (_scrollController.position.pixels >=
        _scrollController.position.maxScrollExtent - 200) {
      ref.read(postsNotifierProvider.notifier).loadMore();
    }
  }

  Future<void> _loadPosts() async {
    await ref.read(postsNotifierProvider.notifier).loadPosts(refresh: true);
  }

  void _onSearchChanged(String value) {
    _searchDebounce?.cancel();
    _searchDebounce = Timer(const Duration(milliseconds: 500), () {
      ref.read(postsNotifierProvider.notifier).search(value);
    });
  }

  void _onPlatformChanged(String? platform) {
    setState(() => _selectedPlatform = platform ?? 'all');
    ref.read(postsNotifierProvider.notifier).setPlatformFilter(
          platform == 'all' ? null : platform,
        );
  }

  @override
  Widget build(BuildContext context) {
    final postsState = ref.watch(postsNotifierProvider);

    return Scaffold(
      appBar: _buildAppBar(),
      body: Column(
        children: [
          _buildFilters(),
          Expanded(
            child: RefreshIndicator(
              onRefresh: _loadPosts,
              color: AppColors.primary,
              backgroundColor: AppColors.card,
              child: _buildBody(postsState),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildBody(PostsState state) {
    if (state.isLoading && state.posts.isEmpty) {
      return _buildShimmerList();
    }

    if (state.error != null && state.posts.isEmpty) {
      return _buildErrorState(state.error!);
    }

    if (state.posts.isEmpty) {
      return _buildEmptyState();
    }

    return ListView.builder(
      controller: _scrollController,
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      itemCount: state.posts.length + (state.isLoadingMore ? 1 : 0),
      itemBuilder: (context, index) {
        if (index >= state.posts.length) {
          return _buildLoadingMoreIndicator();
        }
        return Padding(
          padding: const EdgeInsets.only(bottom: AppConstants.spacingMd),
          child: _buildPostCard(state.posts[index]),
        );
      },
    );
  }

  Widget _buildShimmerList() {
    return Shimmer.fromColors(
      baseColor: AppColors.shimmerBase,
      highlightColor: AppColors.shimmerHighlight,
      child: ListView.builder(
        padding: const EdgeInsets.all(AppConstants.spacingMd),
        itemCount: 5,
        itemBuilder: (context, index) {
          return Padding(
            padding: const EdgeInsets.only(bottom: AppConstants.spacingMd),
            child: _buildShimmerCard(),
          );
        },
      ),
    );
  }

  Widget _buildShimmerCard() {
    return Container(
      height: 160,
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
      ),
    );
  }

  Widget _buildLoadingMoreIndicator() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      alignment: Alignment.center,
      child: const SizedBox(
        width: 24,
        height: 24,
        child: CircularProgressIndicator(
          strokeWidth: 2,
          color: AppColors.primary,
        ),
      ),
    );
  }

  Widget _buildEmptyState() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            width: 80,
            height: 80,
            decoration: BoxDecoration(
              color: AppColors.primary.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(40),
            ),
            child: const Icon(
              Iconsax.document_text,
              size: 40,
              color: AppColors.primary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          const Text(
            'ยังไม่มีโพสต์',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingSm),
          const Text(
            'เริ่มสร้างโพสต์แรกของคุณ',
            style: TextStyle(
              fontSize: 14,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingLg),
          CustomButton(
            onPressed: () => context.push(AppRoutes.createPost),
            label: 'สร้างโพสต์',
            icon: Iconsax.add,
          ),
        ],
      ),
    );
  }

  Widget _buildErrorState(String error) {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(
            Iconsax.warning_2,
            size: 64,
            color: AppColors.error,
          ),
          const SizedBox(height: AppConstants.spacingMd),
          Text(
            error,
            style: const TextStyle(
              fontSize: 14,
              color: AppColors.textSecondary,
            ),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppConstants.spacingLg),
          CustomButton(
            onPressed: _loadPosts,
            label: 'ลองใหม่',
            icon: Iconsax.refresh,
          ),
        ],
      ),
    );
  }

  AppBar _buildAppBar() {
    return AppBar(
      leading: Builder(
        builder: (context) => IconButton(
          icon: const Icon(Iconsax.menu_1),
          onPressed: () => Scaffold.of(context).openDrawer(),
        ),
      ),
      title: const Text('โพสต์ทั้งหมด'),
      actions: [
        IconButton(
          icon: const Icon(Iconsax.filter),
          onPressed: _showFilterSheet,
        ),
        IconButton(
          icon: const Icon(Iconsax.add),
          onPressed: () => context.push(AppRoutes.createPost),
        ),
      ],
      bottom: TabBar(
        controller: _tabController,
        isScrollable: true,
        tabAlignment: TabAlignment.start,
        tabs: _tabs.map((tab) {
          return Tab(
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                if (tab.color != null)
                  Container(
                    width: 8,
                    height: 8,
                    margin: const EdgeInsets.only(right: 6),
                    decoration: BoxDecoration(
                      color: tab.color,
                      shape: BoxShape.circle,
                    ),
                  ),
                Text(tab.label),
              ],
            ),
          );
        }).toList(),
      ),
    );
  }

  Widget _buildFilters() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: const BoxDecoration(
        color: AppColors.backgroundSecondary,
        border: Border(bottom: BorderSide(color: AppColors.border)),
      ),
      child: Row(
        children: [
          Expanded(
            child: SearchField(
              controller: _searchController,
              hint: 'ค้นหาโพสต์...',
              onChanged: _onSearchChanged,
            ),
          ),
          const SizedBox(width: AppConstants.spacingSm),
          _buildPlatformFilter(),
        ],
      ),
    );
  }

  Widget _buildPlatformFilter() {
    return PopupMenuButton<String>(
      initialValue: _selectedPlatform,
      onSelected: _onPlatformChanged,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      ),
      color: AppColors.card,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
        decoration: BoxDecoration(
          color: AppColors.surface,
          borderRadius: BorderRadius.circular(AppConstants.radiusFull),
          border: Border.all(color: AppColors.border),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              _getPlatformIcon(_selectedPlatform),
              size: 18,
              color: _selectedPlatform == 'all'
                  ? AppColors.textSecondary
                  : Platforms.all[_selectedPlatform]?.color ??
                      AppColors.textSecondary,
            ),
            const SizedBox(width: 6),
            const Icon(Iconsax.arrow_down_1,
                size: 16, color: AppColors.textMuted),
          ],
        ),
      ),
      itemBuilder: (context) => [
        _buildPlatformMenuItem('all', 'ทุกแพลตฟอร์ม'),
        ...Platforms.all.entries.map(
          (e) => _buildPlatformMenuItem(e.key, e.value.nameTh),
        ),
      ],
    );
  }

  PopupMenuItem<String> _buildPlatformMenuItem(String value, String label) {
    final isSelected = _selectedPlatform == value;
    final color = value == 'all' ? null : Platforms.all[value]?.color;

    return PopupMenuItem<String>(
      value: value,
      child: Row(
        children: [
          Icon(
            _getPlatformIcon(value),
            size: 18,
            color: color ?? AppColors.textSecondary,
          ),
          const SizedBox(width: 10),
          Text(
            label,
            style: TextStyle(
              color: isSelected ? AppColors.primary : AppColors.textPrimary,
              fontWeight: isSelected ? FontWeight.w500 : FontWeight.normal,
            ),
          ),
          if (isSelected) ...[
            const Spacer(),
            const Icon(Iconsax.tick_circle, size: 18, color: AppColors.primary),
          ],
        ],
      ),
    );
  }

  Widget _buildPostCard(Post post) {
    final platformConfig = Platforms.all[post.platform];
    final statusColor = PostStatus.getColor(post.status);

    return InkWell(
      onTap: () => context.push('/posts/${post.id}'),
      borderRadius: BorderRadius.circular(AppConstants.radiusLg),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        child: BackdropFilter(
          filter: ImageFilter.blur(sigmaX: 5, sigmaY: 5),
          child: Container(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  AppColors.card,
                  AppColors.card.withValues(alpha: 0.9),
                ],
              ),
              borderRadius: BorderRadius.circular(AppConstants.radiusLg),
              border: Border.all(color: AppColors.border),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Padding(
                  padding: const EdgeInsets.all(AppConstants.spacingMd),
                  child: Row(
                    children: [
                      Container(
                        width: 44,
                        height: 44,
                        decoration: BoxDecoration(
                          color: platformConfig?.color.withValues(alpha: 0.15) ??
                              AppColors.surface,
                          borderRadius:
                              BorderRadius.circular(AppConstants.radiusSm),
                        ),
                        child: Icon(
                          _getPlatformIcon(post.platform),
                          color: platformConfig?.color ?? AppColors.textMuted,
                          size: 22,
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              post.brand?.name ?? 'ไม่ระบุแบรนด์',
                              style: const TextStyle(
                                fontSize: 14,
                                fontWeight: FontWeight.w600,
                                color: AppColors.textPrimary,
                              ),
                            ),
                            const SizedBox(height: 4),
                            Text(
                              _getTimeDisplay(post),
                              style: const TextStyle(
                                fontSize: 12,
                                color: AppColors.textMuted,
                              ),
                            ),
                          ],
                        ),
                      ),
                      _buildStatusBadge(post.status, statusColor),
                    ],
                  ),
                ),
                if (post.contentText != null && post.contentText!.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(
                        horizontal: AppConstants.spacingMd),
                    child: Text(
                      post.contentText!,
                      style: const TextStyle(
                        fontSize: 14,
                        color: AppColors.textPrimary,
                        height: 1.4,
                      ),
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                if (post.hashtags != null && post.hashtags!.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: AppConstants.spacingMd,
                      vertical: AppConstants.spacingXs,
                    ),
                    child: Wrap(
                      spacing: 4,
                      children: post.hashtags!
                          .take(3)
                          .map((tag) => Text(
                                '#$tag',
                                style: const TextStyle(
                                  fontSize: 12,
                                  color: AppColors.primary,
                                ),
                              ))
                          .toList(),
                    ),
                  ),
                if (post.status == 'published' && post.metrics != null)
                  _buildMetricsRow(post.metrics!)
                else
                  _buildActionButtons(post),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildStatusBadge(String status, Color color) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(AppConstants.radiusFull),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (status == 'scheduled')
            const Icon(Iconsax.clock, size: 12, color: AppColors.info),
          if (status == 'scheduled') const SizedBox(width: 4),
          Text(
            PostStatus.getLabel(status),
            style: TextStyle(
              fontSize: 11,
              fontWeight: FontWeight.w500,
              color: color,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildMetricsRow(PostMetrics metrics) {
    return Padding(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      child: Row(
        children: [
          _metricItem(Iconsax.heart, metrics.likes ?? 0),
          const SizedBox(width: 16),
          _metricItem(Iconsax.message, metrics.comments ?? 0),
          const SizedBox(width: 16),
          _metricItem(Iconsax.share, metrics.shares ?? 0),
          if (metrics.views != null && metrics.views! > 0) ...[
            const SizedBox(width: 16),
            _metricItem(Iconsax.eye, metrics.views!),
          ],
        ],
      ),
    );
  }

  Widget _buildActionButtons(Post post) {
    return Padding(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.end,
        children: [
          CustomTextButton(
            onPressed: () => context.push('/posts/${post.id}/edit'),
            label: 'แก้ไข',
            icon: Iconsax.edit_2,
          ),
          if (post.status == 'draft' || post.status == 'scheduled')
            CustomTextButton(
              onPressed: () => _publishPost(post.id),
              label: 'โพสต์เลย',
              icon: Iconsax.send_1,
              color: AppColors.success,
            ),
          if (post.status == 'failed')
            CustomTextButton(
              onPressed: () => _publishPost(post.id),
              label: 'ลองใหม่',
              icon: Iconsax.refresh,
              color: AppColors.warning,
            ),
        ],
      ),
    );
  }

  Future<void> _publishPost(int id) async {
    final success = await ref.read(postsNotifierProvider.notifier).publishPost(id);
    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(success ? 'โพสต์สำเร็จ' : 'ไม่สามารถโพสต์ได้'),
          backgroundColor: success ? AppColors.success : AppColors.error,
        ),
      );
    }
  }

  String _getTimeDisplay(Post post) {
    if (post.status == 'scheduled' && post.scheduledAt != null) {
      return 'กำหนดโพสต์ ${_formatScheduledTime(post.scheduledAt!)}';
    }
    if (post.publishedAt != null) {
      return timeago.format(post.publishedAt!, locale: 'th');
    }
    return timeago.format(post.createdAt, locale: 'th');
  }

  String _formatScheduledTime(DateTime date) {
    final now = DateTime.now();
    final isToday = date.year == now.year &&
        date.month == now.month &&
        date.day == now.day;
    final isTomorrow = date.year == now.year &&
        date.month == now.month &&
        date.day == now.day + 1;

    final time =
        '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';

    if (isToday) return 'วันนี้ $time';
    if (isTomorrow) return 'พรุ่งนี้ $time';
    return '${date.day}/${date.month} $time';
  }

  Widget _metricItem(IconData icon, int value) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 16, color: AppColors.textMuted),
        const SizedBox(width: 4),
        Text(
          _formatNumber(value),
          style: const TextStyle(
            fontSize: 13,
            color: AppColors.textSecondary,
          ),
        ),
      ],
    );
  }

  String _formatNumber(int number) {
    if (number >= 1000000) {
      return '${(number / 1000000).toStringAsFixed(1)}M';
    }
    if (number >= 1000) {
      return '${(number / 1000).toStringAsFixed(1)}K';
    }
    return number.toString();
  }

  void _showFilterSheet() {
    final brandsState = ref.read(brandsNotifierProvider);

    showModalBottomSheet(
      context: context,
      backgroundColor: AppColors.card,
      shape: const RoundedRectangleBorder(
        borderRadius:
            BorderRadius.vertical(top: Radius.circular(AppConstants.radiusLg)),
      ),
      builder: (context) => Container(
        padding: const EdgeInsets.all(AppConstants.spacingLg),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text(
                  'ตัวกรอง',
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                TextButton(
                  onPressed: () {
                    ref.read(postsNotifierProvider.notifier).clearFilters();
                    Navigator.pop(context);
                  },
                  child: const Text('ล้างตัวกรอง'),
                ),
              ],
            ),
            const SizedBox(height: AppConstants.spacingLg),
            const Text(
              'เรียงตาม',
              style: TextStyle(
                fontSize: 14,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppConstants.spacingSm),
            Wrap(
              spacing: 8,
              children: [
                CustomChip(label: 'ล่าสุด', isSelected: true, onTap: () {}),
                CustomChip(label: 'เก่าสุด', onTap: () {}),
                CustomChip(label: 'Engagement สูง', onTap: () {}),
              ],
            ),
            const SizedBox(height: AppConstants.spacingMd),
            const Text(
              'แบรนด์',
              style: TextStyle(
                fontSize: 14,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppConstants.spacingSm),
            Wrap(
              spacing: 8,
              children: [
                CustomChip(label: 'ทั้งหมด', isSelected: true, onTap: () {}),
                ...brandsState.brands.map(
                  (brand) => CustomChip(label: brand.name, onTap: () {}),
                ),
              ],
            ),
            const SizedBox(height: AppConstants.spacingLg),
            CustomButton(
              onPressed: () => Navigator.pop(context),
              label: 'ใช้ตัวกรอง',
              width: double.infinity,
            ),
          ],
        ),
      ),
    );
  }

  IconData _getPlatformIcon(String platform) {
    switch (platform) {
      case 'all':
        return Iconsax.global;
      case 'facebook':
        return Icons.facebook;
      case 'instagram':
        return Icons.camera_alt;
      case 'tiktok':
        return Icons.music_note;
      case 'twitter':
        return Icons.alternate_email;
      case 'line':
        return Icons.chat_bubble;
      case 'youtube':
        return Icons.play_circle_filled;
      case 'threads':
        return Icons.alternate_email;
      case 'linkedin':
        return Icons.work;
      case 'pinterest':
        return Icons.push_pin;
      default:
        return Icons.public;
    }
  }
}

class _TabItem {
  final String? status;
  final String label;
  final Color? color;

  const _TabItem(this.status, this.label, this.color);
}
