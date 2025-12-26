import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';

import '../../core/constants/app_constants.dart';
import '../../core/router/app_router.dart';
import '../../providers/auth_provider.dart';
import '../../data/models/user.dart';

/// Main Shell with Bottom Navigation and Drawer
class MainShell extends ConsumerStatefulWidget {
  final Widget child;

  const MainShell({super.key, required this.child});

  @override
  ConsumerState<MainShell> createState() => _MainShellState();
}

class _MainShellState extends ConsumerState<MainShell> {
  int _currentIndex = 0;

  final List<_NavItem> _navItems = const [
    _NavItem(
      icon: Iconsax.home_2,
      activeIcon: Iconsax.home_25,
      label: 'หน้าหลัก',
      route: AppRoutes.dashboard,
    ),
    _NavItem(
      icon: Iconsax.document_text,
      activeIcon: Iconsax.document_text1,
      label: 'โพสต์',
      route: AppRoutes.posts,
    ),
    _NavItem(
      icon: Iconsax.chart_2,
      activeIcon: Iconsax.chart_21,
      label: 'สถิติ',
      route: AppRoutes.analytics,
    ),
    _NavItem(
      icon: Iconsax.briefcase,
      activeIcon: Iconsax.briefcase5,
      label: 'แบรนด์',
      route: AppRoutes.brands,
    ),
  ];

  void _onNavTap(int index) {
    if (_currentIndex != index) {
      setState(() => _currentIndex = index);
      context.go(_navItems[index].route);
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = ref.watch(currentUserProvider);
    final location = GoRouterState.of(context).matchedLocation;

    // Update current index based on route
    for (int i = 0; i < _navItems.length; i++) {
      if (location.startsWith(_navItems[i].route)) {
        if (_currentIndex != i) {
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (mounted) setState(() => _currentIndex = i);
          });
        }
        break;
      }
    }

    return Scaffold(
      body: widget.child,
      bottomNavigationBar: _buildBottomNav(),
      drawer: _buildDrawer(user),
      floatingActionButton: _buildFAB(),
      floatingActionButtonLocation: FloatingActionButtonLocation.centerDocked,
    );
  }

  Widget _buildBottomNav() {
    return Container(
      decoration: BoxDecoration(
        color: AppColors.backgroundSecondary,
        boxShadow: [
          BoxShadow(
            color: Colors.black.withValues(alpha:0.2),
            blurRadius: 10,
            offset: const Offset(0, -2),
          ),
        ],
      ),
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 8),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceAround,
            children: [
              for (int i = 0; i < _navItems.length; i++) ...[
                if (i == 2) const SizedBox(width: 56), // Space for FAB
                _buildNavItem(i),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildNavItem(int index) {
    final item = _navItems[index];
    final isSelected = _currentIndex == index;

    return InkWell(
      onTap: () => _onNavTap(index),
      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        decoration: BoxDecoration(
          color: isSelected ? AppColors.primary.withValues(alpha:0.15) : Colors.transparent,
          borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              isSelected ? item.activeIcon : item.icon,
              color: isSelected ? AppColors.primary : AppColors.textMuted,
              size: 24,
            ),
            const SizedBox(height: 4),
            Text(
              item.label,
              style: TextStyle(
                fontSize: 11,
                fontWeight: isSelected ? FontWeight.w600 : FontWeight.w400,
                color: isSelected ? AppColors.primary : AppColors.textMuted,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildFAB() {
    return FloatingActionButton(
      onPressed: () => context.push(AppRoutes.createPost),
      backgroundColor: AppColors.primary,
      elevation: 4,
      child: const Icon(Icons.add, size: 28),
    );
  }

  Widget _buildDrawer(User? user) {
    return Drawer(
      backgroundColor: AppColors.backgroundSecondary,
      child: SafeArea(
        child: Column(
          children: [
            // User Header
            _buildDrawerHeader(user),
            const Divider(color: AppColors.divider),

            // Menu Items
            Expanded(
              child: ListView(
                padding: EdgeInsets.zero,
                children: [
                  _buildDrawerItem(
                    icon: Iconsax.home_2,
                    label: 'หน้าหลัก',
                    onTap: () {
                      Navigator.pop(context);
                      context.go(AppRoutes.dashboard);
                    },
                  ),
                  _buildDrawerItem(
                    icon: Iconsax.document_text,
                    label: 'โพสต์ทั้งหมด',
                    onTap: () {
                      Navigator.pop(context);
                      context.go(AppRoutes.posts);
                    },
                  ),
                  _buildDrawerItem(
                    icon: Iconsax.briefcase,
                    label: 'แบรนด์',
                    onTap: () {
                      Navigator.pop(context);
                      context.go(AppRoutes.brands);
                    },
                  ),
                  _buildDrawerItem(
                    icon: Iconsax.flag,
                    label: 'แคมเปญ',
                    onTap: () {
                      Navigator.pop(context);
                      context.go(AppRoutes.campaigns);
                    },
                  ),
                  _buildDrawerItem(
                    icon: Iconsax.chart_2,
                    label: 'สถิติ',
                    onTap: () {
                      Navigator.pop(context);
                      context.go(AppRoutes.analytics);
                    },
                  ),

                  const Divider(color: AppColors.divider, height: 32),

                  _buildDrawerItem(
                    icon: Iconsax.link,
                    label: 'บัญชีที่เชื่อมต่อ',
                    onTap: () {
                      Navigator.pop(context);
                      context.push(AppRoutes.accounts);
                    },
                  ),
                  _buildDrawerItem(
                    icon: Iconsax.card,
                    label: 'แพ็กเกจของฉัน',
                    onTap: () {
                      Navigator.pop(context);
                      context.push(AppRoutes.subscription);
                    },
                  ),
                  _buildDrawerItem(
                    icon: Iconsax.notification,
                    label: 'การแจ้งเตือน',
                    badge: '3',
                    onTap: () {
                      Navigator.pop(context);
                      context.push(AppRoutes.notifications);
                    },
                  ),

                  const Divider(color: AppColors.divider, height: 32),

                  _buildDrawerItem(
                    icon: Iconsax.setting_2,
                    label: 'ตั้งค่า',
                    onTap: () {
                      Navigator.pop(context);
                      context.push(AppRoutes.settings);
                    },
                  ),
                ],
              ),
            ),

            // Logout
            const Divider(color: AppColors.divider),
            _buildDrawerItem(
              icon: Iconsax.logout,
              label: 'ออกจากระบบ',
              color: AppColors.error,
              onTap: () async {
                Navigator.pop(context);
                await ref.read(authNotifierProvider.notifier).logout();
              },
            ),
            const SizedBox(height: 8),
          ],
        ),
      ),
    );
  }

  Widget _buildDrawerHeader(User? user) {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      child: Row(
        children: [
          // Avatar
          Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              gradient: AppColors.primaryGradient,
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            ),
            child: Center(
              child: user?.avatarUrl != null
                  ? ClipRRect(
                      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                      child: Image.network(
                        user!.avatarUrl!,
                        width: 56,
                        height: 56,
                        fit: BoxFit.cover,
                      ),
                    )
                  : Text(
                      user?.initials ?? 'U',
                      style: const TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                        color: Colors.white,
                      ),
                    ),
            ),
          ),
          const SizedBox(width: AppConstants.spacingMd),

          // User Info
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  user?.name ?? 'ผู้ใช้งาน',
                  style: const TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textPrimary,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 2),
                Text(
                  user?.email ?? '',
                  style: const TextStyle(
                    fontSize: 13,
                    color: AppColors.textSecondary,
                  ),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                if (user?.currentPackage != null) ...[
                  const SizedBox(height: 4),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                    decoration: BoxDecoration(
                      color: AppColors.primary.withValues(alpha:0.2),
                      borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                    ),
                    child: Text(
                      user!.currentPackage!,
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w500,
                        color: AppColors.primary,
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDrawerItem({
    required IconData icon,
    required String label,
    String? badge,
    Color? color,
    required VoidCallback onTap,
  }) {
    return ListTile(
      leading: Icon(icon, color: color ?? AppColors.textSecondary, size: 22),
      title: Text(
        label,
        style: TextStyle(
          fontSize: 15,
          color: color ?? AppColors.textPrimary,
        ),
      ),
      trailing: badge != null
          ? Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
              decoration: BoxDecoration(
                color: AppColors.error,
                borderRadius: BorderRadius.circular(AppConstants.radiusFull),
              ),
              child: Text(
                badge,
                style: const TextStyle(
                  fontSize: 11,
                  fontWeight: FontWeight.w600,
                  color: Colors.white,
                ),
              ),
            )
          : null,
      onTap: onTap,
    );
  }
}

class _NavItem {
  final IconData icon;
  final IconData activeIcon;
  final String label;
  final String route;

  const _NavItem({
    required this.icon,
    required this.activeIcon,
    required this.label,
    required this.route,
  });
}
