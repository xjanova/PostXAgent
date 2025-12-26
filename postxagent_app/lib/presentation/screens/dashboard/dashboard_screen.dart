import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';
import 'package:flutter_animate/flutter_animate.dart';

import '../../../core/constants/app_constants.dart';
import '../../../core/router/app_router.dart';
import '../../../providers/auth_provider.dart';
import '../../../providers/analytics_provider.dart';
import '../../../providers/post_provider.dart';
import '../../../providers/notification_provider.dart';
import '../../widgets/cards/stats_card.dart';
import '../../widgets/common/shimmer_loading.dart';

class DashboardScreen extends ConsumerStatefulWidget {
  const DashboardScreen({super.key});

  @override
  ConsumerState<DashboardScreen> createState() => _DashboardScreenState();
}

class _DashboardScreenState extends ConsumerState<DashboardScreen> {
  @override
  void initState() {
    super.initState();
    // Load initial data
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _loadData();
    });
  }

  Future<void> _loadData() async {
    await Future.wait([
      ref.read(dashboardNotifierProvider.notifier).loadSummary(),
      ref.read(postsNotifierProvider.notifier).loadPosts(),
      ref.read(notificationsNotifierProvider.notifier).loadNotifications(),
    ]);
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);
    final dashboardState = ref.watch(dashboardNotifierProvider);
    final postsState = ref.watch(postsNotifierProvider);
    final unreadCount = ref.watch(unreadNotificationsCountProvider);

    return Scaffold(
      extendBodyBehindAppBar: true,
      appBar: _buildAppBar(user, unreadCount),
      drawer: null,
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: [
              Color(0xFF0A0A0F),
              Color(0xFF12121A),
            ],
          ),
        ),
        child: RefreshIndicator(
          onRefresh: _loadData,
          color: AppColors.primary,
          backgroundColor: AppColors.card,
          child: SingleChildScrollView(
            physics: const AlwaysScrollableScrollPhysics(),
            padding: EdgeInsets.only(
              top: MediaQuery.of(context).padding.top + 80,
              left: AppConstants.spacingMd,
              right: AppConstants.spacingMd,
              bottom: AppConstants.spacingMd,
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                // Welcome Card
                _buildWelcomeCard(user)
                    .animate()
                    .fadeIn(duration: 500.ms)
                    .slideY(begin: 0.2, curve: Curves.easeOutQuart),
                const SizedBox(height: AppConstants.spacingLg),

                // Stats Grid
                _buildStatsGrid(dashboardState)
                    .animate()
                    .fadeIn(delay: 150.ms, duration: 500.ms)
                    .slideY(begin: 0.2, curve: Curves.easeOutQuart),
                const SizedBox(height: AppConstants.spacingLg),

                // Usage Progress
                _buildUsageSection(dashboardState)
                    .animate()
                    .fadeIn(delay: 300.ms, duration: 500.ms)
                    .slideY(begin: 0.2, curve: Curves.easeOutQuart),
                const SizedBox(height: AppConstants.spacingLg),

                // Quick Actions
                _buildQuickActions()
                    .animate()
                    .fadeIn(delay: 450.ms, duration: 500.ms)
                    .slideY(begin: 0.2, curve: Curves.easeOutQuart),
                const SizedBox(height: AppConstants.spacingLg),

                // Recent Posts
                _buildRecentPosts(postsState)
                    .animate()
                    .fadeIn(delay: 600.ms, duration: 500.ms)
                    .slideY(begin: 0.2, curve: Curves.easeOutQuart),
                const SizedBox(height: AppConstants.spacingXl),
              ],
            ),
          ),
        ),
      ),
    );
  }

  PreferredSizeWidget _buildAppBar(user, int unreadCount) {
    return PreferredSize(
      preferredSize: const Size.fromHeight(60),
      child: ClipRRect(
        child: BackdropFilter(
          filter: ImageFilter.blur(sigmaX: 10, sigmaY: 10),
          child: Container(
            decoration: BoxDecoration(
              color: AppColors.background.withValues(alpha: 0.8),
              border: Border(
                bottom: BorderSide(
                  color: AppColors.border.withValues(alpha: 0.3),
                ),
              ),
            ),
            child: SafeArea(
              child: Padding(
                padding: const EdgeInsets.symmetric(horizontal: 8),
                child: Row(
                  children: [
                    Builder(
                      builder: (context) => IconButton(
                        icon: const Icon(Iconsax.menu_1, color: AppColors.textPrimary),
                        onPressed: () => Scaffold.of(context).openDrawer(),
                      ),
                    ),
                    const SizedBox(width: 8),
                    // Animated Logo
                    Container(
                      width: 36,
                      height: 36,
                      decoration: BoxDecoration(
                        gradient: AppColors.primaryGradient,
                        borderRadius: BorderRadius.circular(10),
                        boxShadow: [
                          BoxShadow(
                            color: AppColors.primary.withValues(alpha: 0.4),
                            blurRadius: 12,
                            offset: const Offset(0, 4),
                          ),
                        ],
                      ),
                      child: const Center(
                        child: Text(
                          'PX',
                          style: TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.bold,
                            color: Colors.white,
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 12),
                    const Text(
                      'PostXAgent',
                      style: TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                        color: AppColors.textPrimary,
                        letterSpacing: -0.5,
                      ),
                    ),
                    const Spacer(),
                    // Notification with badge
                    Stack(
                      children: [
                        IconButton(
                          icon: const Icon(Iconsax.notification, color: AppColors.textPrimary),
                          onPressed: () => context.push(AppRoutes.notifications),
                        ),
                        if (unreadCount > 0)
                          Positioned(
                            right: 8,
                            top: 8,
                            child: Container(
                              padding: const EdgeInsets.all(4),
                              decoration: BoxDecoration(
                                color: AppColors.error,
                                shape: BoxShape.circle,
                                boxShadow: [
                                  BoxShadow(
                                    color: AppColors.error.withValues(alpha: 0.6),
                                    blurRadius: 6,
                                    spreadRadius: 1,
                                  ),
                                ],
                              ),
                              constraints: const BoxConstraints(
                                minWidth: 16,
                                minHeight: 16,
                              ),
                              child: Text(
                                unreadCount > 9 ? '9+' : unreadCount.toString(),
                                style: const TextStyle(
                                  fontSize: 10,
                                  fontWeight: FontWeight.bold,
                                  color: Colors.white,
                                ),
                                textAlign: TextAlign.center,
                              ),
                            ),
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

  Widget _buildWelcomeCard(user) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(AppConstants.radiusXl),
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 10, sigmaY: 10),
        child: Container(
          padding: const EdgeInsets.all(AppConstants.spacingLg),
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topLeft,
              end: Alignment.bottomRight,
              colors: [
                AppColors.primary.withValues(alpha: 0.3),
                AppColors.secondary.withValues(alpha: 0.2),
              ],
            ),
            borderRadius: BorderRadius.circular(AppConstants.radiusXl),
            border: Border.all(
              color: Colors.white.withValues(alpha: 0.15),
              width: 1,
            ),
            boxShadow: [
              BoxShadow(
                color: AppColors.primary.withValues(alpha: 0.25),
                blurRadius: 30,
                offset: const Offset(0, 15),
              ),
            ],
          ),
          child: Row(
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'สวัสดี, ${user?.name ?? 'ผู้ใช้'}!',
                      style: const TextStyle(
                        fontSize: 24,
                        fontWeight: FontWeight.bold,
                        color: Colors.white,
                        letterSpacing: -0.5,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                          decoration: BoxDecoration(
                            gradient: LinearGradient(
                              colors: [
                                Colors.white.withValues(alpha: 0.25),
                                Colors.white.withValues(alpha: 0.1),
                              ],
                            ),
                            borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                            border: Border.all(
                              color: Colors.white.withValues(alpha: 0.2),
                            ),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(Iconsax.crown1, size: 14, color: Colors.amber),
                              const SizedBox(width: 6),
                              Text(
                                user?.currentPackage ?? 'Free',
                                style: const TextStyle(
                                  fontSize: 12,
                                  fontWeight: FontWeight.w600,
                                  color: Colors.white,
                                ),
                              ),
                            ],
                          ),
                        ),
                        if (user?.hasActiveRental == true) ...[
                          const SizedBox(width: 12),
                          const Text(
                            'กำลังใช้งาน',
                            style: TextStyle(
                              fontSize: 13,
                              color: Colors.white70,
                            ),
                          ),
                        ],
                      ],
                    ),
                  ],
                ),
              ),
              // Avatar with glow
              GestureDetector(
                onTap: () => context.push(AppRoutes.settings),
                child: Container(
                  width: 64,
                  height: 64,
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      colors: [
                        Colors.white.withValues(alpha: 0.3),
                        Colors.white.withValues(alpha: 0.1),
                      ],
                    ),
                    borderRadius: BorderRadius.circular(18),
                    border: Border.all(
                      color: Colors.white.withValues(alpha: 0.3),
                      width: 2,
                    ),
                    boxShadow: [
                      BoxShadow(
                        color: AppColors.primary.withValues(alpha: 0.3),
                        blurRadius: 20,
                      ),
                    ],
                  ),
                  child: user?.avatarUrl != null
                      ? ClipRRect(
                          borderRadius: BorderRadius.circular(16),
                          child: Image.network(user!.avatarUrl!, fit: BoxFit.cover),
                        )
                      : Center(
                          child: Text(
                            user?.initials ?? 'U',
                            style: const TextStyle(
                              fontSize: 24,
                              fontWeight: FontWeight.bold,
                              color: Colors.white,
                            ),
                          ),
                        ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildStatsGrid(DashboardState state) {
    if (state.isLoading) {
      return const ShimmerStatsGrid(count: 4);
    }

    final summary = state.summary;
    final totalPosts = summary?.totalPosts ?? 0;
    final scheduledPosts = summary?.scheduledPosts ?? 0;
    final viralPosts = summary?.viralPosts ?? 0;
    final engagementRate = summary?.engagementRate ?? 0.0;

    return GridView.count(
      crossAxisCount: 2,
      shrinkWrap: true,
      physics: const NeverScrollableScrollPhysics(),
      mainAxisSpacing: AppConstants.spacingMd,
      crossAxisSpacing: AppConstants.spacingMd,
      childAspectRatio: 1.4,
      children: [
        StatsCard(
          title: 'โพสต์ทั้งหมด',
          value: totalPosts.toString(),
          icon: Iconsax.document_text,
          color: AppColors.info,
          trend: summary?.postsThisWeek != null ? '+${summary!.postsThisWeek}' : null,
          trendUp: true,
          onTap: () => context.go(AppRoutes.posts),
        ),
        StatsCard(
          title: 'รอโพสต์',
          value: scheduledPosts.toString(),
          icon: Iconsax.calendar,
          color: AppColors.warning,
          onTap: () => context.go(AppRoutes.posts),
        ),
        StatsCard(
          title: 'ไวรัล',
          value: viralPosts.toString(),
          icon: Iconsax.flash,
          color: AppColors.viral,
          trend: viralPosts > 0 ? '+$viralPosts' : null,
          trendUp: true,
        ),
        StatsCard(
          title: 'Engagement',
          value: '${engagementRate.toStringAsFixed(1)}%',
          icon: Iconsax.tick_circle,
          color: AppColors.success,
        ),
      ],
    );
  }

  Widget _buildUsageSection(DashboardState state) {
    // Use mock data or real data
    final postsUsed = state.summary?.publishedPosts ?? 0;
    const postsTotal = 1000; // From subscription
    final aiUsed = state.summary?.totalPosts ?? 0;
    const aiTotal = 2000; // From subscription

    return ClipRRect(
      borderRadius: BorderRadius.circular(AppConstants.radiusLg),
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 10, sigmaY: 10),
        child: Container(
          padding: const EdgeInsets.all(AppConstants.spacingLg),
          decoration: BoxDecoration(
            gradient: AppColors.glassGradient,
            borderRadius: BorderRadius.circular(AppConstants.radiusLg),
            border: Border.all(color: AppColors.border.withValues(alpha: 0.5)),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Row(
                    children: [
                      Icon(Iconsax.chart_2, color: AppColors.primary, size: 20),
                      SizedBox(width: 10),
                      Text(
                        'โควต้าการใช้งาน',
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                      ),
                    ],
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
                    decoration: BoxDecoration(
                      gradient: AppColors.primaryGradient,
                      borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                    ),
                    child: InkWell(
                      onTap: () => context.push(AppRoutes.subscription),
                      child: const Text(
                        'อัพเกรด',
                        style: TextStyle(
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                          color: Colors.white,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: AppConstants.spacingLg),
              _buildUsageBar(
                label: 'โพสต์',
                used: postsUsed,
                total: postsTotal,
                color: AppColors.primary,
              ),
              const SizedBox(height: AppConstants.spacingMd),
              _buildUsageBar(
                label: 'AI Generation',
                used: aiUsed,
                total: aiTotal,
                color: AppColors.secondary,
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildUsageBar({
    required String label,
    required int used,
    required int total,
    required Color color,
  }) {
    final percentage = total > 0 ? (used / total) : 0.0;
    final isWarning = percentage > 0.8;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(
              label,
              style: const TextStyle(
                fontSize: 14,
                fontWeight: FontWeight.w500,
                color: AppColors.textSecondary,
              ),
            ),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
              decoration: BoxDecoration(
                color: (isWarning ? AppColors.warning : color).withValues(alpha: 0.15),
                borderRadius: BorderRadius.circular(AppConstants.radiusFull),
              ),
              child: Text(
                '$used / $total',
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w600,
                  color: isWarning ? AppColors.warning : color,
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 10),
        Stack(
          children: [
            Container(
              height: 8,
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: BorderRadius.circular(AppConstants.radiusFull),
              ),
            ),
            FractionallySizedBox(
              widthFactor: percentage.clamp(0.0, 1.0),
              child: Container(
                height: 8,
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    colors: isWarning
                        ? [AppColors.warning, AppColors.error]
                        : [color, color.withValues(alpha: 0.7)],
                  ),
                  borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                  boxShadow: [
                    BoxShadow(
                      color: (isWarning ? AppColors.warning : color).withValues(alpha: 0.4),
                      blurRadius: 8,
                      offset: const Offset(0, 2),
                    ),
                  ],
                ),
              ),
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildQuickActions() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Row(
          children: [
            Icon(Iconsax.flash_1, color: AppColors.accent, size: 20),
            SizedBox(width: 10),
            Text(
              'เริ่มต้นใช้งาน',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
              ),
            ),
          ],
        ),
        const SizedBox(height: AppConstants.spacingMd),
        Row(
          children: [
            Expanded(
              child: _buildActionCard(
                icon: Iconsax.add_square,
                label: 'สร้างโพสต์',
                color: AppColors.primary,
                onTap: () => context.push(AppRoutes.createPost),
              ),
            ),
            const SizedBox(width: AppConstants.spacingMd),
            Expanded(
              child: _buildActionCard(
                icon: Iconsax.magic_star,
                label: 'AI ช่วยเขียน',
                color: AppColors.accent,
                onTap: () => context.push(AppRoutes.createPost),
              ),
            ),
          ],
        ),
        const SizedBox(height: AppConstants.spacingMd),
        Row(
          children: [
            Expanded(
              child: _buildActionCard(
                icon: Iconsax.flag,
                label: 'สร้างแคมเปญ',
                color: AppColors.warning,
                onTap: () => context.push('${AppRoutes.campaigns}/create'),
              ),
            ),
            const SizedBox(width: AppConstants.spacingMd),
            Expanded(
              child: _buildActionCard(
                icon: Iconsax.chart_2,
                label: 'ดูสถิติ',
                color: AppColors.success,
                onTap: () => context.go(AppRoutes.analytics),
              ),
            ),
          ],
        ),
      ],
    );
  }

  Widget _buildActionCard({
    required IconData icon,
    required String label,
    required Color color,
    required VoidCallback onTap,
  }) {
    return ClipRRect(
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 5, sigmaY: 5),
        child: Material(
          color: Colors.transparent,
          child: InkWell(
            onTap: onTap,
            borderRadius: BorderRadius.circular(AppConstants.radiusMd),
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
                borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                border: Border.all(color: color.withValues(alpha: 0.25)),
              ),
              child: Row(
                children: [
                  Container(
                    width: 44,
                    height: 44,
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        begin: Alignment.topLeft,
                        end: Alignment.bottomRight,
                        colors: [color, color.withValues(alpha: 0.7)],
                      ),
                      borderRadius: BorderRadius.circular(12),
                      boxShadow: [
                        BoxShadow(
                          color: color.withValues(alpha: 0.4),
                          blurRadius: 10,
                          offset: const Offset(0, 4),
                        ),
                      ],
                    ),
                    child: Icon(icon, color: Colors.white, size: 22),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      label,
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                  ),
                  Icon(Iconsax.arrow_right_3, color: color, size: 18),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildRecentPosts(PostsState state) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            const Row(
              children: [
                Icon(Iconsax.document_text, color: AppColors.info, size: 20),
                SizedBox(width: 10),
                Text(
                  'โพสต์ล่าสุด',
                  style: TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                ),
              ],
            ),
            TextButton(
              onPressed: () => context.go(AppRoutes.posts),
              child: const Text('ดูทั้งหมด'),
            ),
          ],
        ),
        const SizedBox(height: AppConstants.spacingSm),
        if (state.isLoading) ...[
          const ShimmerCard(),
          const SizedBox(height: AppConstants.spacingSm),
          const ShimmerCard(),
          const SizedBox(height: AppConstants.spacingSm),
          const ShimmerCard(),
        ] else if (state.posts.isEmpty) ...[
          _buildEmptyPosts(),
        ] else ...[
          ...state.posts.take(3).map((post) => Padding(
                padding: const EdgeInsets.only(bottom: AppConstants.spacingSm),
                child: _buildPostItem(
                  title: post.excerpt.isNotEmpty ? post.excerpt : 'ไม่มีเนื้อหา',
                  platform: post.platform,
                  status: post.status,
                  timeAgo: _formatTimeAgo(post.createdAt),
                  likes: post.metrics?.likes ?? 0,
                  comments: post.metrics?.comments ?? 0,
                ),
              )),
        ],
      ],
    );
  }

  Widget _buildEmptyPosts() {
    return ClipRRect(
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 5, sigmaY: 5),
        child: Container(
          padding: const EdgeInsets.all(AppConstants.spacingLg),
          decoration: BoxDecoration(
            gradient: AppColors.glassGradient,
            borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            border: Border.all(color: AppColors.border.withValues(alpha: 0.5)),
          ),
          child: Column(
            children: [
              const Icon(
                Iconsax.document,
                color: AppColors.textMuted,
                size: 48,
              ),
              const SizedBox(height: 12),
              const Text(
                'ยังไม่มีโพสต์',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 8),
              const Text(
                'เริ่มสร้างโพสต์แรกของคุณ',
                style: TextStyle(
                  fontSize: 14,
                  color: AppColors.textMuted,
                ),
              ),
              const SizedBox(height: 16),
              ElevatedButton.icon(
                onPressed: () => context.push(AppRoutes.createPost),
                icon: const Icon(Iconsax.add),
                label: const Text('สร้างโพสต์'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: AppColors.primary,
                  foregroundColor: Colors.white,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  String _formatTimeAgo(DateTime date) {
    final now = DateTime.now();
    final diff = now.difference(date);

    if (diff.inDays > 0) {
      return '${diff.inDays} วันที่แล้ว';
    } else if (diff.inHours > 0) {
      return '${diff.inHours} ชั่วโมงที่แล้ว';
    } else if (diff.inMinutes > 0) {
      return '${diff.inMinutes} นาทีที่แล้ว';
    } else {
      return 'เมื่อสักครู่';
    }
  }

  Widget _buildPostItem({
    required String title,
    required String platform,
    required String status,
    required String timeAgo,
    required int likes,
    required int comments,
  }) {
    final platformConfig = Platforms.all[platform];
    final statusColor = PostStatus.getColor(status);
    final statusLabel = PostStatus.getLabel(status);

    return ClipRRect(
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      child: BackdropFilter(
        filter: ImageFilter.blur(sigmaX: 5, sigmaY: 5),
        child: Container(
          padding: const EdgeInsets.all(AppConstants.spacingMd),
          decoration: BoxDecoration(
            gradient: AppColors.glassGradient,
            borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            border: Border.all(color: AppColors.border.withValues(alpha: 0.5)),
          ),
          child: Row(
            children: [
              Container(
                width: 48,
                height: 48,
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    begin: Alignment.topLeft,
                    end: Alignment.bottomRight,
                    colors: [
                      (platformConfig?.color ?? AppColors.textMuted).withValues(alpha: 0.2),
                      (platformConfig?.color ?? AppColors.textMuted).withValues(alpha: 0.1),
                    ],
                  ),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(
                    color: (platformConfig?.color ?? AppColors.textMuted).withValues(alpha: 0.3),
                  ),
                ),
                child: Icon(
                  _getPlatformIcon(platform),
                  color: platformConfig?.color ?? AppColors.textMuted,
                  size: 24,
                ),
              ),
              const SizedBox(width: 14),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: const TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                    const SizedBox(height: 6),
                    Row(
                      children: [
                        Container(
                          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                          decoration: BoxDecoration(
                            color: statusColor.withValues(alpha: 0.15),
                            borderRadius: BorderRadius.circular(6),
                            border: Border.all(
                              color: statusColor.withValues(alpha: 0.3),
                            ),
                          ),
                          child: Text(
                            statusLabel,
                            style: TextStyle(
                              fontSize: 11,
                              fontWeight: FontWeight.w600,
                              color: statusColor,
                            ),
                          ),
                        ),
                        const SizedBox(width: 10),
                        Text(
                          timeAgo,
                          style: const TextStyle(
                            fontSize: 12,
                            color: AppColors.textMuted,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              if (status == 'published') ...[
                Column(
                  crossAxisAlignment: CrossAxisAlignment.end,
                  children: [
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(Iconsax.heart5, size: 14, color: AppColors.error),
                        const SizedBox(width: 4),
                        Text(
                          likes.toString(),
                          style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w500,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                    const SizedBox(height: 4),
                    Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        const Icon(Iconsax.message, size: 14, color: AppColors.info),
                        const SizedBox(width: 4),
                        Text(
                          comments.toString(),
                          style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w500,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  IconData _getPlatformIcon(String platform) {
    switch (platform) {
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
      default:
        return Icons.public;
    }
  }
}
