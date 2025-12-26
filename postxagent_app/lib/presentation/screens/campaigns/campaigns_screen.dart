import 'dart:async';
import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';
import 'package:shimmer/shimmer.dart';
import 'package:intl/intl.dart';

import '../../../core/constants/app_constants.dart';
import '../../../data/models/campaign.dart';
import '../../../providers/campaign_provider.dart';
import '../../../providers/brand_provider.dart';

class CampaignsScreen extends ConsumerStatefulWidget {
  const CampaignsScreen({super.key});

  @override
  ConsumerState<CampaignsScreen> createState() => _CampaignsScreenState();
}

class _CampaignsScreenState extends ConsumerState<CampaignsScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final _searchController = TextEditingController();
  final _scrollController = ScrollController();
  Timer? _searchDebounce;

  final List<_TabItem> _tabs = const [
    _TabItem(null, 'ทั้งหมด', AppColors.primary),
    _TabItem('active', 'กำลังทำงาน', AppColors.success),
    _TabItem('scheduled', 'ตั้งเวลา', AppColors.info),
    _TabItem('completed', 'เสร็จสิ้น', AppColors.textMuted),
    _TabItem('draft', 'แบบร่าง', AppColors.warning),
  ];

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: _tabs.length, vsync: this);
    _tabController.addListener(_onTabChanged);
    _scrollController.addListener(_onScroll);

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _loadCampaigns();
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
    ref.read(campaignsNotifierProvider.notifier).setStatusFilter(status);
  }

  void _onScroll() {
    if (_scrollController.position.pixels >=
        _scrollController.position.maxScrollExtent - 200) {
      ref.read(campaignsNotifierProvider.notifier).loadMore();
    }
  }

  Future<void> _loadCampaigns() async {
    await ref.read(campaignsNotifierProvider.notifier).loadCampaigns(refresh: true);
  }

  void _onSearchChanged(String value) {
    _searchDebounce?.cancel();
    _searchDebounce = Timer(const Duration(milliseconds: 500), () {
      ref.read(campaignsNotifierProvider.notifier).search(value);
    });
  }

  @override
  Widget build(BuildContext context) {
    final campaignsState = ref.watch(campaignsNotifierProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('แคมเปญ'),
        actions: [
          IconButton(
            icon: const Icon(Iconsax.filter),
            onPressed: () => _showFilterSheet(context),
          ),
        ],
        bottom: TabBar(
          controller: _tabController,
          isScrollable: true,
          tabAlignment: TabAlignment.start,
          tabs: _tabs.map((tab) {
            return Tab(
              child: Row(
                children: [
                  Text(tab.label),
                  const SizedBox(width: 6),
                  _buildCountBadge(
                    _getCountForStatus(campaignsState.campaigns, tab.status),
                    tab.color,
                  ),
                ],
              ),
            );
          }).toList(),
        ),
      ),
      body: RefreshIndicator(
        onRefresh: _loadCampaigns,
        color: AppColors.primary,
        backgroundColor: AppColors.card,
        child: Column(
          children: [
            // Search
            Padding(
              padding: const EdgeInsets.all(AppConstants.spacingMd),
              child: TextField(
                controller: _searchController,
                decoration: InputDecoration(
                  hintText: 'ค้นหาแคมเปญ...',
                  prefixIcon: const Icon(Iconsax.search_normal, size: 20),
                  suffixIcon: _searchController.text.isNotEmpty
                      ? IconButton(
                          icon: const Icon(Iconsax.close_circle, size: 20),
                          onPressed: () {
                            _searchController.clear();
                            ref.read(campaignsNotifierProvider.notifier).search('');
                          },
                        )
                      : null,
                ),
                onChanged: _onSearchChanged,
              ),
            ),

            // Content
            Expanded(
              child: _buildBody(campaignsState),
            ),
          ],
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () {
          context.push('/campaigns/create');
        },
        icon: const Icon(Iconsax.add),
        label: const Text('สร้างแคมเปญ'),
      ),
    );
  }

  int _getCountForStatus(List<Campaign> campaigns, String? status) {
    if (status == null) return campaigns.length;
    return campaigns.where((c) => c.status == status).length;
  }

  Widget _buildBody(CampaignsState state) {
    if (state.isLoading && state.campaigns.isEmpty) {
      return _buildShimmerList();
    }

    if (state.error != null && state.campaigns.isEmpty) {
      return _buildErrorState(state.error!);
    }

    if (state.campaigns.isEmpty) {
      return _buildEmptyState();
    }

    return ListView.builder(
      controller: _scrollController,
      padding: const EdgeInsets.symmetric(horizontal: AppConstants.spacingMd),
      itemCount: state.campaigns.length + (state.isLoadingMore ? 1 : 0),
      itemBuilder: (context, index) {
        if (index >= state.campaigns.length) {
          return _buildLoadingMoreIndicator();
        }
        return _buildCampaignCard(state.campaigns[index]);
      },
    );
  }

  Widget _buildShimmerList() {
    return Shimmer.fromColors(
      baseColor: AppColors.shimmerBase,
      highlightColor: AppColors.shimmerHighlight,
      child: ListView.builder(
        padding: const EdgeInsets.symmetric(horizontal: AppConstants.spacingMd),
        itemCount: 4,
        itemBuilder: (context, index) {
          return Container(
            height: 200,
            margin: const EdgeInsets.only(bottom: AppConstants.spacingMd),
            decoration: BoxDecoration(
              color: AppColors.card,
              borderRadius: BorderRadius.circular(AppConstants.radiusLg),
            ),
          );
        },
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
              color: AppColors.info.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(40),
            ),
            child: const Icon(
              Iconsax.chart_2,
              size: 40,
              color: AppColors.info,
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          const Text(
            'ยังไม่มีแคมเปญ',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingSm),
          const Text(
            'สร้างแคมเปญแรกเพื่อเริ่มโปรโมทแบรนด์',
            style: TextStyle(
              fontSize: 14,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingLg),
          ElevatedButton.icon(
            onPressed: () => context.push('/campaigns/create'),
            icon: const Icon(Iconsax.add),
            label: const Text('สร้างแคมเปญ'),
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
          ElevatedButton.icon(
            onPressed: _loadCampaigns,
            icon: const Icon(Iconsax.refresh),
            label: const Text('ลองใหม่'),
          ),
        ],
      ),
    );
  }

  Widget _buildCountBadge(int count, Color color) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.2),
        borderRadius: BorderRadius.circular(AppConstants.radiusFull),
      ),
      child: Text(
        count.toString(),
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w600,
          color: color,
        ),
      ),
    );
  }

  Widget _buildCampaignCard(Campaign campaign) {
    final platforms = campaign.targetPlatforms ?? [];
    final totalPosts = campaign.postsCount ?? 0;
    final publishedPosts = campaign.publishedPostsCount ?? 0;
    final progress = campaign.progress;
    final dateFormat = DateFormat('dd/MM/yy');

    return Container(
      margin: const EdgeInsets.only(bottom: AppConstants.spacingMd),
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
            child: InkWell(
              onTap: () {
                context.push('/campaigns/${campaign.id}');
              },
              borderRadius: BorderRadius.circular(AppConstants.radiusLg),
              child: Padding(
                padding: const EdgeInsets.all(AppConstants.spacingMd),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    // Header
                    Row(
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: Text(
                                      campaign.name,
                                      style: const TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w600,
                                        color: AppColors.textPrimary,
                                      ),
                                    ),
                                  ),
                                  _buildStatusBadge(campaign.status),
                                ],
                              ),
                              const SizedBox(height: 4),
                              Text(
                                campaign.brand?.name ?? 'ไม่ระบุแบรนด์',
                                style: const TextStyle(
                                  fontSize: 13,
                                  color: AppColors.textMuted,
                                ),
                              ),
                            ],
                          ),
                        ),
                        PopupMenuButton<String>(
                          icon: const Icon(Iconsax.more, size: 20),
                          shape: RoundedRectangleBorder(
                            borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                          ),
                          color: AppColors.card,
                          onSelected: (value) => _handleCampaignAction(campaign, value),
                          itemBuilder: (context) => _buildCampaignMenuItems(campaign),
                        ),
                      ],
                    ),
                    const SizedBox(height: AppConstants.spacingMd),

                    // Description
                    if (campaign.description != null)
                      Text(
                        campaign.description!,
                        style: const TextStyle(
                          fontSize: 14,
                          color: AppColors.textSecondary,
                          height: 1.4,
                        ),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                    const SizedBox(height: AppConstants.spacingMd),

                    // Platforms
                    Row(
                      children: [
                        ...platforms.take(4).map((p) {
                          final config = Platforms.all[p];
                          return Container(
                            margin: const EdgeInsets.only(right: 6),
                            padding: const EdgeInsets.all(6),
                            decoration: BoxDecoration(
                              color: (config?.color ?? AppColors.textMuted)
                                  .withValues(alpha: 0.15),
                              borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                            ),
                            child: Icon(
                              _getPlatformIcon(p),
                              size: 16,
                              color: config?.color ?? AppColors.textMuted,
                            ),
                          );
                        }),
                        if (platforms.length > 4)
                          Container(
                            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
                            decoration: BoxDecoration(
                              color: AppColors.surface,
                              borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                            ),
                            child: Text(
                              '+${platforms.length - 4}',
                              style: const TextStyle(
                                fontSize: 12,
                                color: AppColors.textMuted,
                              ),
                            ),
                          ),
                        const Spacer(),
                        if (campaign.startDate != null && campaign.endDate != null)
                          Text(
                            '${dateFormat.format(campaign.startDate!)} - ${dateFormat.format(campaign.endDate!)}',
                            style: const TextStyle(
                              fontSize: 11,
                              color: AppColors.textMuted,
                            ),
                          ),
                      ],
                    ),
                    const SizedBox(height: AppConstants.spacingMd),
                    const Divider(color: AppColors.border, height: 1),
                    const SizedBox(height: AppConstants.spacingMd),

                    // Progress & Stats
                    Row(
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                                children: [
                                  const Text(
                                    'ความคืบหน้า',
                                    style: TextStyle(
                                      fontSize: 12,
                                      color: AppColors.textMuted,
                                    ),
                                  ),
                                  Text(
                                    '$publishedPosts / $totalPosts โพสต์',
                                    style: const TextStyle(
                                      fontSize: 12,
                                      color: AppColors.textSecondary,
                                    ),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 6),
                              ClipRRect(
                                borderRadius:
                                    BorderRadius.circular(AppConstants.radiusFull),
                                child: LinearProgressIndicator(
                                  value: progress,
                                  backgroundColor: AppColors.surface,
                                  valueColor: AlwaysStoppedAnimation(
                                    progress >= 1.0
                                        ? AppColors.success
                                        : AppColors.primary,
                                  ),
                                  minHeight: 6,
                                ),
                              ),
                            ],
                          ),
                        ),
                        const SizedBox(width: AppConstants.spacingLg),
                        if (campaign.daysRemaining > 0)
                          _buildStatColumn(
                            'เหลือ',
                            '${campaign.daysRemaining} วัน',
                            AppColors.warning,
                          ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildStatusBadge(String status) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 8,
        vertical: 2,
      ),
      decoration: BoxDecoration(
        color: CampaignStatus.getColor(status).withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(AppConstants.radiusFull),
      ),
      child: Text(
        CampaignStatus.getLabel(status),
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w500,
          color: CampaignStatus.getColor(status),
        ),
      ),
    );
  }

  List<PopupMenuItem<String>> _buildCampaignMenuItems(Campaign campaign) {
    final items = <PopupMenuItem<String>>[];

    if (campaign.status == 'draft' || campaign.status == 'scheduled') {
      items.add(const PopupMenuItem(
        value: 'start',
        child: Row(
          children: [
            Icon(Iconsax.play, size: 18, color: AppColors.success),
            SizedBox(width: 8),
            Text('เริ่มแคมเปญ'),
          ],
        ),
      ));
    }

    if (campaign.isActive) {
      items.add(const PopupMenuItem(
        value: 'pause',
        child: Row(
          children: [
            Icon(Iconsax.pause, size: 18, color: AppColors.warning),
            SizedBox(width: 8),
            Text('หยุดชั่วคราว'),
          ],
        ),
      ));
    }

    if (campaign.isPaused) {
      items.add(const PopupMenuItem(
        value: 'resume',
        child: Row(
          children: [
            Icon(Iconsax.play, size: 18, color: AppColors.success),
            SizedBox(width: 8),
            Text('ดำเนินการต่อ'),
          ],
        ),
      ));
    }

    if (campaign.isActive || campaign.isPaused) {
      items.add(const PopupMenuItem(
        value: 'complete',
        child: Row(
          children: [
            Icon(Iconsax.tick_circle, size: 18, color: AppColors.info),
            SizedBox(width: 8),
            Text('เสร็จสิ้น'),
          ],
        ),
      ));
    }

    items.add(const PopupMenuItem(
      value: 'edit',
      child: Row(
        children: [
          Icon(Iconsax.edit_2, size: 18, color: AppColors.textSecondary),
          SizedBox(width: 8),
          Text('แก้ไข'),
        ],
      ),
    ));

    items.add(const PopupMenuItem(
      value: 'delete',
      child: Row(
        children: [
          Icon(Iconsax.trash, size: 18, color: AppColors.error),
          SizedBox(width: 8),
          Text('ลบ'),
        ],
      ),
    ));

    return items;
  }

  Future<void> _handleCampaignAction(Campaign campaign, String action) async {
    bool success = false;
    String message = '';

    switch (action) {
      case 'start':
        success = await ref.read(campaignsNotifierProvider.notifier).startCampaign(campaign.id);
        message = success ? 'เริ่มแคมเปญแล้ว' : 'ไม่สามารถเริ่มแคมเปญได้';
        break;
      case 'pause':
        success = await ref.read(campaignsNotifierProvider.notifier).pauseCampaign(campaign.id);
        message = success ? 'หยุดแคมเปญชั่วคราวแล้ว' : 'ไม่สามารถหยุดแคมเปญได้';
        break;
      case 'resume':
        success = await ref.read(campaignsNotifierProvider.notifier).resumeCampaign(campaign.id);
        message = success ? 'ดำเนินการแคมเปญต่อแล้ว' : 'ไม่สามารถดำเนินการต่อได้';
        break;
      case 'complete':
        success = await ref.read(campaignsNotifierProvider.notifier).completeCampaign(campaign.id);
        message = success ? 'เสร็จสิ้นแคมเปญแล้ว' : 'ไม่สามารถเสร็จสิ้นแคมเปญได้';
        break;
      case 'edit':
        context.push('/campaigns/${campaign.id}/edit');
        return;
      case 'delete':
        _showDeleteConfirmation(campaign);
        return;
    }

    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(message),
          backgroundColor: success ? AppColors.success : AppColors.error,
        ),
      );
    }
  }

  void _showDeleteConfirmation(Campaign campaign) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: AppColors.card,
        title: const Text('ยืนยันการลบ'),
        content: Text('คุณต้องการลบแคมเปญ "${campaign.name}" หรือไม่?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('ยกเลิก'),
          ),
          ElevatedButton(
            onPressed: () async {
              Navigator.pop(context);
              final scaffoldMessenger = ScaffoldMessenger.of(context);
              final success = await ref
                  .read(campaignsNotifierProvider.notifier)
                  .deleteCampaign(campaign.id);
              if (mounted) {
                scaffoldMessenger.showSnackBar(
                  SnackBar(
                    content: Text(success ? 'ลบแคมเปญแล้ว' : 'ไม่สามารถลบแคมเปญได้'),
                    backgroundColor: success ? AppColors.success : AppColors.error,
                  ),
                );
              }
            },
            style: ElevatedButton.styleFrom(backgroundColor: AppColors.error),
            child: const Text('ลบ'),
          ),
        ],
      ),
    );
  }

  Widget _buildStatColumn(String label, String value, Color color) {
    return Column(
      children: [
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

  void _showFilterSheet(BuildContext context) {
    final brandsState = ref.read(brandsNotifierProvider);

    showModalBottomSheet(
      context: context,
      backgroundColor: AppColors.card,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppConstants.radiusLg),
        ),
      ),
      builder: (context) => Padding(
        padding: const EdgeInsets.all(AppConstants.spacingMd),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 40,
                height: 4,
                decoration: BoxDecoration(
                  color: AppColors.border,
                  borderRadius: BorderRadius.circular(2),
                ),
              ),
            ),
            const SizedBox(height: AppConstants.spacingMd),
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text(
                  'กรองแคมเปญ',
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
                TextButton(
                  onPressed: () {
                    ref.read(campaignsNotifierProvider.notifier).clearFilters();
                    Navigator.pop(context);
                  },
                  child: const Text('ล้างตัวกรอง'),
                ),
              ],
            ),
            const SizedBox(height: AppConstants.spacingMd),
            const Text(
              'แบรนด์',
              style: TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w500,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppConstants.spacingSm),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                ChoiceChip(
                  label: const Text('ทั้งหมด'),
                  selected: true,
                  onSelected: (selected) {},
                ),
                ...brandsState.brands.map(
                  (brand) => ChoiceChip(
                    label: Text(brand.name),
                    selected: false,
                    onSelected: (selected) {
                      if (selected) {
                        ref
                            .read(campaignsNotifierProvider.notifier)
                            .setBrandFilter(brand.id);
                        Navigator.pop(context);
                      }
                    },
                  ),
                ),
              ],
            ),
            const SizedBox(height: AppConstants.spacingLg),
            SizedBox(
              width: double.infinity,
              child: ElevatedButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('ใช้ตัวกรอง'),
              ),
            ),
            SizedBox(height: MediaQuery.of(context).padding.bottom),
          ],
        ),
      ),
    );
  }
}

class _TabItem {
  final String? status;
  final String label;
  final Color color;

  const _TabItem(this.status, this.label, this.color);
}
