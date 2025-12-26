import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:iconsax/iconsax.dart';
import 'package:shimmer/shimmer.dart';

import '../../../core/constants/app_constants.dart';
import '../../../data/models/analytics.dart';
import '../../../providers/analytics_provider.dart';
import '../../../providers/brand_provider.dart';

class AnalyticsScreen extends ConsumerStatefulWidget {
  const AnalyticsScreen({super.key});

  @override
  ConsumerState<AnalyticsScreen> createState() => _AnalyticsScreenState();
}

class _AnalyticsScreenState extends ConsumerState<AnalyticsScreen> {
  String _selectedPeriod = '7d';
  int? _selectedBrandId;

  final List<Map<String, String>> _periods = [
    {'key': '7d', 'label': '7 วัน'},
    {'key': '30d', 'label': '30 วัน'},
    {'key': '90d', 'label': '90 วัน'},
    {'key': 'all', 'label': 'ทั้งหมด'},
  ];

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _loadAnalytics();
    });
  }

  Future<void> _loadAnalytics() async {
    await ref.read(analyticsOverviewNotifierProvider.notifier).loadAnalytics();
  }

  void _onPeriodChanged(String period) {
    setState(() => _selectedPeriod = period);
    ref.read(analyticsOverviewNotifierProvider.notifier).setPeriod(period);
  }

  void _onBrandChanged(int? brandId) {
    setState(() => _selectedBrandId = brandId);
    ref.read(analyticsOverviewNotifierProvider.notifier).setBrand(brandId);
  }

  @override
  Widget build(BuildContext context) {
    final analyticsState = ref.watch(analyticsOverviewNotifierProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('วิเคราะห์'),
        actions: [
          IconButton(
            icon: const Icon(Iconsax.export_1),
            onPressed: () => _exportReport(),
            tooltip: 'ส่งออกรายงาน',
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _loadAnalytics,
        color: AppColors.primary,
        backgroundColor: AppColors.card,
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Period & Filter Selector
              _buildFilters(),

              if (analyticsState.isLoading)
                _buildShimmerContent()
              else if (analyticsState.error != null)
                _buildErrorState(analyticsState.error!)
              else ...[
                // Overview Stats
                Padding(
                  padding: const EdgeInsets.all(AppConstants.spacingMd),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'ภาพรวม',
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      const SizedBox(height: AppConstants.spacingMd),
                      _buildOverviewGrid(analyticsState.overview),
                    ],
                  ),
                ),

                // Chart Section
                _buildChartSection(analyticsState.trends),

                // Platform Breakdown
                Padding(
                  padding: const EdgeInsets.all(AppConstants.spacingMd),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'ประสิทธิภาพตามแพลตฟอร์ม',
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      const SizedBox(height: AppConstants.spacingMd),
                      _buildPlatformBreakdown(analyticsState.platformAnalytics),
                    ],
                  ),
                ),

                // Top Posts
                Padding(
                  padding: const EdgeInsets.all(AppConstants.spacingMd),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          const Text(
                            'โพสต์ยอดนิยม',
                            style: TextStyle(
                              fontSize: 18,
                              fontWeight: FontWeight.w600,
                              color: AppColors.textPrimary,
                            ),
                          ),
                          TextButton(
                            onPressed: () {},
                            child: const Text('ดูทั้งหมด'),
                          ),
                        ],
                      ),
                      const SizedBox(height: AppConstants.spacingSm),
                      _buildTopPosts(analyticsState.topPosts),
                    ],
                  ),
                ),
              ],

              const SizedBox(height: AppConstants.spacingXl),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildShimmerContent() {
    return Shimmer.fromColors(
      baseColor: AppColors.shimmerBase,
      highlightColor: AppColors.shimmerHighlight,
      child: Padding(
        padding: const EdgeInsets.all(AppConstants.spacingMd),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Container(
              height: 24,
              width: 100,
              decoration: BoxDecoration(
                color: AppColors.card,
                borderRadius: BorderRadius.circular(AppConstants.radiusSm),
              ),
            ),
            const SizedBox(height: AppConstants.spacingMd),
            GridView.count(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              crossAxisCount: 2,
              mainAxisSpacing: 12,
              crossAxisSpacing: 12,
              childAspectRatio: 1.4,
              children: List.generate(
                4,
                (index) => Container(
                  decoration: BoxDecoration(
                    color: AppColors.card,
                    borderRadius: BorderRadius.circular(AppConstants.radiusLg),
                  ),
                ),
              ),
            ),
            const SizedBox(height: AppConstants.spacingLg),
            Container(
              height: 200,
              decoration: BoxDecoration(
                color: AppColors.card,
                borderRadius: BorderRadius.circular(AppConstants.radiusLg),
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildErrorState(String error) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(AppConstants.spacingXl),
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
              onPressed: _loadAnalytics,
              icon: const Icon(Iconsax.refresh),
              label: const Text('ลองใหม่'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildFilters() {
    final brandsState = ref.watch(brandsNotifierProvider);

    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: const BoxDecoration(
        color: AppColors.backgroundSecondary,
        border: Border(bottom: BorderSide(color: AppColors.border)),
      ),
      child: Column(
        children: [
          // Period Selector
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: _periods.map((period) {
                final isSelected = _selectedPeriod == period['key'];
                return Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: ChoiceChip(
                    label: Text(period['label']!),
                    selected: isSelected,
                    onSelected: (selected) {
                      if (selected) _onPeriodChanged(period['key']!);
                    },
                  ),
                );
              }).toList(),
            ),
          ),
          const SizedBox(height: AppConstants.spacingSm),

          // Brand Filter
          Row(
            children: [
              Expanded(
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  decoration: BoxDecoration(
                    color: AppColors.card,
                    borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: DropdownButtonHideUnderline(
                    child: DropdownButton<int?>(
                      value: _selectedBrandId,
                      isExpanded: true,
                      hint: const Text('แบรนด์ทั้งหมด',
                          style: TextStyle(fontSize: 13)),
                      dropdownColor: AppColors.card,
                      icon: const Icon(Iconsax.arrow_down_1, size: 18),
                      items: [
                        const DropdownMenuItem<int?>(
                          value: null,
                          child: Text('แบรนด์ทั้งหมด'),
                        ),
                        ...brandsState.brands.map(
                          (brand) => DropdownMenuItem<int?>(
                            value: brand.id,
                            child: Text(brand.name),
                          ),
                        ),
                      ],
                      onChanged: _onBrandChanged,
                    ),
                  ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildOverviewGrid(AnalyticsOverview? overview) {
    return GridView.count(
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      crossAxisCount: 2,
      mainAxisSpacing: 12,
      crossAxisSpacing: 12,
      childAspectRatio: 1.4,
      children: [
        _buildStatCard(
          'การเข้าถึง',
          _formatNumber(overview?.totalReach ?? 0),
          overview?.reachGrowth ?? 0,
          Iconsax.eye,
          AppColors.info,
        ),
        _buildStatCard(
          'Engagement',
          _formatNumber(overview?.totalEngagement ?? 0),
          overview?.engagementGrowth ?? 0,
          Iconsax.heart,
          AppColors.error,
        ),
        _buildStatCard(
          'คลิก',
          _formatNumber(overview?.totalClicks ?? 0),
          overview?.clicksGrowth ?? 0,
          Iconsax.mouse_1,
          AppColors.success,
        ),
        _buildStatCard(
          'Engagement Rate',
          '${(overview?.engagementRate ?? 0).toStringAsFixed(1)}%',
          overview?.engagementRateGrowth ?? 0,
          Iconsax.chart_1,
          AppColors.secondary,
        ),
      ],
    );
  }

  Widget _buildStatCard(
    String label,
    String value,
    double change,
    IconData icon,
    Color color,
  ) {
    final isPositive = change >= 0;

    return ClipRRect(
      borderRadius: BorderRadius.circular(AppConstants.radiusLg),
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 5, sigmaY: 5),
        child: Container(
          padding: const EdgeInsets.all(AppConstants.spacingMd),
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [
                color.withValues(alpha: 0.15),
                color.withValues(alpha: 0.05),
              ],
            ),
            borderRadius: BorderRadius.circular(AppConstants.radiusLg),
            border: Border.all(color: color.withValues(alpha: 0.2)),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Icon(icon, color: color, size: 20),
                  if (change != 0)
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 6, vertical: 2),
                      decoration: BoxDecoration(
                        color: (isPositive ? AppColors.success : AppColors.error)
                            .withValues(alpha: 0.15),
                        borderRadius:
                            BorderRadius.circular(AppConstants.radiusSm),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(
                            isPositive
                                ? Iconsax.arrow_up_3
                                : Iconsax.arrow_down,
                            size: 10,
                            color: isPositive
                                ? AppColors.success
                                : AppColors.error,
                          ),
                          const SizedBox(width: 2),
                          Text(
                            '${change.abs().toStringAsFixed(1)}%',
                            style: TextStyle(
                              fontSize: 10,
                              fontWeight: FontWeight.w600,
                              color: isPositive
                                  ? AppColors.success
                                  : AppColors.error,
                            ),
                          ),
                        ],
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
        ),
      ),
    );
  }

  Widget _buildChartSection(List<EngagementTrend> trends) {
    return Container(
      margin: const EdgeInsets.all(AppConstants.spacingMd),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        child: BackdropFilter(
          filter: ImageFilter.blur(sigmaX: 5, sigmaY: 5),
          child: Container(
            padding: const EdgeInsets.all(AppConstants.spacingMd),
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
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    const Text(
                      'แนวโน้มการเข้าถึง',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: AppColors.surface,
                        borderRadius:
                            BorderRadius.circular(AppConstants.radiusSm),
                      ),
                      child: Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Container(
                            width: 8,
                            height: 8,
                            decoration: const BoxDecoration(
                              color: AppColors.primary,
                              shape: BoxShape.circle,
                            ),
                          ),
                          const SizedBox(width: 4),
                          const Text(
                            'Reach',
                            style: TextStyle(
                                fontSize: 11, color: AppColors.textMuted),
                          ),
                          const SizedBox(width: 12),
                          Container(
                            width: 8,
                            height: 8,
                            decoration: const BoxDecoration(
                              color: AppColors.secondary,
                              shape: BoxShape.circle,
                            ),
                          ),
                          const SizedBox(width: 4),
                          const Text(
                            'Engagement',
                            style: TextStyle(
                                fontSize: 11, color: AppColors.textMuted),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: AppConstants.spacingMd),
                // Chart Placeholder - integrate fl_chart here
                Container(
                  height: 200,
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius:
                        BorderRadius.circular(AppConstants.radiusMd),
                  ),
                  child: trends.isEmpty
                      ? const Center(
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Icon(Iconsax.chart_2,
                                  size: 48, color: AppColors.textMuted),
                              SizedBox(height: 8),
                              Text(
                                'ยังไม่มีข้อมูล',
                                style: TextStyle(color: AppColors.textMuted),
                              ),
                            ],
                          ),
                        )
                      : const Center(
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              Icon(Iconsax.chart_2,
                                  size: 48, color: AppColors.textMuted),
                              SizedBox(height: 8),
                              Text(
                                'กราฟแนวโน้ม',
                                style: TextStyle(color: AppColors.textMuted),
                              ),
                              Text(
                                '(fl_chart integration)',
                                style: TextStyle(
                                    fontSize: 11,
                                    color: AppColors.textDisabled),
                              ),
                            ],
                          ),
                        ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildPlatformBreakdown(List<PlatformAnalytics> platforms) {
    if (platforms.isEmpty) {
      return Container(
        padding: const EdgeInsets.all(AppConstants.spacingLg),
        decoration: BoxDecoration(
          color: AppColors.card,
          borderRadius: BorderRadius.circular(AppConstants.radiusLg),
          border: Border.all(color: AppColors.border),
        ),
        child: const Center(
          child: Column(
            children: [
              Icon(Iconsax.chart_2, size: 48, color: AppColors.textMuted),
              SizedBox(height: 8),
              Text(
                'ยังไม่มีข้อมูลแพลตฟอร์ม',
                style: TextStyle(color: AppColors.textMuted),
              ),
            ],
          ),
        ),
      );
    }

    return Column(
      children: platforms.map((platform) {
        final config = Platforms.all[platform.platform];
        final color = config?.color ?? AppColors.textMuted;

        return Container(
          margin: const EdgeInsets.only(bottom: 12),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(AppConstants.radiusLg),
            child: BackdropFilter(
              filter: ImageFilter.blur(sigmaX: 5, sigmaY: 5),
              child: Container(
                padding: const EdgeInsets.all(AppConstants.spacingMd),
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [
                      color.withValues(alpha: 0.1),
                      color.withValues(alpha: 0.02),
                    ],
                  ),
                  borderRadius: BorderRadius.circular(AppConstants.radiusLg),
                  border: Border.all(color: color.withValues(alpha: 0.2)),
                ),
                child: Column(
                  children: [
                    Row(
                      children: [
                        Container(
                          width: 40,
                          height: 40,
                          decoration: BoxDecoration(
                            color: color.withValues(alpha: 0.15),
                            borderRadius:
                                BorderRadius.circular(AppConstants.radiusMd),
                          ),
                          child: Icon(
                            _getPlatformIcon(platform.platform),
                            color: color,
                            size: 20,
                          ),
                        ),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(
                                config?.name ?? platform.platform,
                                style: const TextStyle(
                                  fontSize: 15,
                                  fontWeight: FontWeight.w600,
                                  color: AppColors.textPrimary,
                                ),
                              ),
                              Text(
                                '${platform.postsCount} โพสต์ • ${_formatNumber(platform.followers)} ผู้ติดตาม',
                                style: const TextStyle(
                                  fontSize: 12,
                                  color: AppColors.textMuted,
                                ),
                              ),
                            ],
                          ),
                        ),
                        if (platform.growth != 0)
                          Container(
                            padding: const EdgeInsets.symmetric(
                                horizontal: 8, vertical: 4),
                            decoration: BoxDecoration(
                              color: (platform.growth > 0
                                      ? AppColors.success
                                      : AppColors.error)
                                  .withValues(alpha: 0.15),
                              borderRadius: BorderRadius.circular(
                                  AppConstants.radiusFull),
                            ),
                            child: Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                Icon(
                                  platform.growth > 0
                                      ? Iconsax.arrow_up_3
                                      : Iconsax.arrow_down,
                                  size: 12,
                                  color: platform.growth > 0
                                      ? AppColors.success
                                      : AppColors.error,
                                ),
                                const SizedBox(width: 2),
                                Text(
                                  '${platform.growth.abs().toStringAsFixed(1)}%',
                                  style: TextStyle(
                                    fontSize: 12,
                                    fontWeight: FontWeight.w600,
                                    color: platform.growth > 0
                                        ? AppColors.success
                                        : AppColors.error,
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
                        Expanded(
                          child: _buildMiniStat(
                              'Reach',
                              _formatNumber(platform.reach),
                              AppColors.info),
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: _buildMiniStat(
                            'Engagement',
                            _formatNumber(platform.engagement),
                            AppColors.error,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        );
      }).toList(),
    );
  }

  Widget _buildMiniStat(String label, String value, Color color) {
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 8, horizontal: 12),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.1),
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Text(
            value,
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.bold,
              color: color,
            ),
          ),
          const SizedBox(width: 4),
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

  Widget _buildTopPosts(List<PostPerformance> posts) {
    if (posts.isEmpty) {
      return Container(
        padding: const EdgeInsets.all(AppConstants.spacingLg),
        decoration: BoxDecoration(
          color: AppColors.card,
          borderRadius: BorderRadius.circular(AppConstants.radiusLg),
          border: Border.all(color: AppColors.border),
        ),
        child: const Center(
          child: Column(
            children: [
              Icon(Iconsax.document_text, size: 48, color: AppColors.textMuted),
              SizedBox(height: 8),
              Text(
                'ยังไม่มีโพสต์ยอดนิยม',
                style: TextStyle(color: AppColors.textMuted),
              ),
            ],
          ),
        ),
      );
    }

    return Column(
      children: posts.asMap().entries.map((entry) {
        final index = entry.key;
        final post = entry.value;
        final platform = post.platform;
        final config = Platforms.all[platform];
        final color = config?.color ?? AppColors.textMuted;

        return Container(
          margin: const EdgeInsets.only(bottom: 12),
          child: ClipRRect(
            borderRadius: BorderRadius.circular(AppConstants.radiusLg),
            child: BackdropFilter(
              filter: ImageFilter.blur(sigmaX: 5, sigmaY: 5),
              child: Container(
                padding: const EdgeInsets.all(AppConstants.spacingMd),
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
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      width: 36,
                      height: 36,
                      decoration: BoxDecoration(
                        color: color.withValues(alpha: 0.15),
                        borderRadius:
                            BorderRadius.circular(AppConstants.radiusSm),
                      ),
                      child: Icon(_getPlatformIcon(platform),
                          color: color, size: 18),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            post.contentPreview ?? 'โพสต์ #${post.postId}',
                            style: const TextStyle(
                              fontSize: 14,
                              color: AppColors.textPrimary,
                            ),
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                          ),
                          const SizedBox(height: 8),
                          Row(
                            children: [
                              _buildPostMetric(
                                  Iconsax.eye, _formatNumber(post.reach)),
                              const SizedBox(width: 16),
                              _buildPostMetric(
                                  Iconsax.heart, _formatNumber(post.engagement)),
                              const SizedBox(width: 16),
                              _buildPostMetric(Iconsax.chart_1,
                                  '${post.engagementRate.toStringAsFixed(1)}%'),
                            ],
                          ),
                        ],
                      ),
                    ),
                    // Rank badge
                    Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        gradient: AppColors.primaryGradient,
                        borderRadius:
                            BorderRadius.circular(AppConstants.radiusFull),
                      ),
                      child: Text(
                        '#${index + 1}',
                        style: const TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.bold,
                          color: Colors.white,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        );
      }).toList(),
    );
  }

  Widget _buildPostMetric(IconData icon, String value) {
    return Row(
      children: [
        Icon(icon, size: 14, color: AppColors.textMuted),
        const SizedBox(width: 4),
        Text(
          value,
          style: const TextStyle(
            fontSize: 12,
            color: AppColors.textSecondary,
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

  Future<void> _exportReport() async {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('กำลังส่งออกรายงาน...'),
        backgroundColor: AppColors.info,
      ),
    );

    final url = await ref
        .read(analyticsOverviewNotifierProvider.notifier)
        .exportReport(format: 'pdf');

    if (mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(url != null
              ? 'ส่งออกรายงานสำเร็จ'
              : 'ไม่สามารถส่งออกรายงานได้'),
          backgroundColor: url != null ? AppColors.success : AppColors.error,
        ),
      );
    }
  }
}
