import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../presentation/screens/auth/login_screen.dart';
import '../../presentation/screens/auth/register_screen.dart';
import '../../presentation/screens/dashboard/dashboard_screen.dart';
import '../../presentation/screens/posts/posts_screen.dart';
import '../../presentation/screens/posts/post_detail_screen.dart';
import '../../presentation/screens/posts/create_post_screen.dart';
import '../../presentation/screens/brands/brands_screen.dart';
import '../../presentation/screens/brands/brand_detail_screen.dart';
import '../../presentation/screens/brands/create_brand_screen.dart';
import '../../presentation/screens/campaigns/campaigns_screen.dart';
import '../../presentation/screens/campaigns/campaign_detail_screen.dart';
import '../../presentation/screens/campaigns/create_campaign_screen.dart';
import '../../presentation/screens/analytics/analytics_screen.dart';
import '../../presentation/screens/accounts/accounts_screen.dart';
import '../../presentation/screens/subscription/subscription_screen.dart';
import '../../presentation/screens/notifications/notifications_screen.dart';
import '../../presentation/screens/settings/settings_screen.dart';
import '../../presentation/screens/main_shell.dart';
import '../../providers/auth_provider.dart';

/// Route names
class AppRoutes {
  AppRoutes._();

  // Auth
  static const String login = '/login';
  static const String register = '/register';

  // Main
  static const String dashboard = '/';
  static const String posts = '/posts';
  static const String postDetail = '/posts/:id';
  static const String createPost = '/posts/create';
  static const String editPost = '/posts/:id/edit';

  static const String brands = '/brands';
  static const String brandDetail = '/brands/:id';
  static const String createBrand = '/brands/create';

  static const String campaigns = '/campaigns';
  static const String campaignDetail = '/campaigns/:id';
  static const String createCampaign = '/campaigns/create';

  static const String analytics = '/analytics';
  static const String accounts = '/accounts';
  static const String subscription = '/subscription';
  static const String notifications = '/notifications';
  static const String settings = '/settings';
}

/// Navigation Keys
final _rootNavigatorKey = GlobalKey<NavigatorState>();
final _shellNavigatorKey = GlobalKey<NavigatorState>();

/// App Router Provider
final appRouterProvider = Provider<GoRouter>((ref) {
  final authState = ref.watch(authStateProvider);

  return GoRouter(
    navigatorKey: _rootNavigatorKey,
    initialLocation: AppRoutes.dashboard,
    debugLogDiagnostics: true,
    redirect: (context, state) {
      final isLoggedIn = authState.isAuthenticated;
      final isLoggingIn = state.matchedLocation == AppRoutes.login;
      final isRegistering = state.matchedLocation == AppRoutes.register;

      // If not logged in and not on auth pages, redirect to login
      if (!isLoggedIn && !isLoggingIn && !isRegistering) {
        return AppRoutes.login;
      }

      // If logged in and on auth pages, redirect to dashboard
      if (isLoggedIn && (isLoggingIn || isRegistering)) {
        return AppRoutes.dashboard;
      }

      return null;
    },
    routes: [
      // Auth Routes
      GoRoute(
        path: AppRoutes.login,
        name: 'login',
        builder: (context, state) => const LoginScreen(),
      ),
      GoRoute(
        path: AppRoutes.register,
        name: 'register',
        builder: (context, state) => const RegisterScreen(),
      ),

      // Main Shell with Bottom Navigation
      ShellRoute(
        navigatorKey: _shellNavigatorKey,
        builder: (context, state, child) => MainShell(child: child),
        routes: [
          // Dashboard
          GoRoute(
            path: AppRoutes.dashboard,
            name: 'dashboard',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: DashboardScreen(),
            ),
          ),

          // Posts
          GoRoute(
            path: AppRoutes.posts,
            name: 'posts',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: PostsScreen(),
            ),
            routes: [
              GoRoute(
                path: 'create',
                name: 'createPost',
                parentNavigatorKey: _rootNavigatorKey,
                builder: (context, state) => const CreatePostScreen(),
              ),
              GoRoute(
                path: ':id',
                name: 'postDetail',
                parentNavigatorKey: _rootNavigatorKey,
                builder: (context, state) {
                  final id = state.pathParameters['id']!;
                  return PostDetailScreen(postId: id);
                },
                routes: [
                  GoRoute(
                    path: 'edit',
                    name: 'editPost',
                    builder: (context, state) {
                      final id = int.tryParse(state.pathParameters['id'] ?? '');
                      return CreatePostScreen(postId: id);
                    },
                  ),
                ],
              ),
            ],
          ),

          // Analytics
          GoRoute(
            path: AppRoutes.analytics,
            name: 'analytics',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: AnalyticsScreen(),
            ),
          ),

          // Brands
          GoRoute(
            path: AppRoutes.brands,
            name: 'brands',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: BrandsScreen(),
            ),
            routes: [
              GoRoute(
                path: 'create',
                name: 'createBrand',
                parentNavigatorKey: _rootNavigatorKey,
                builder: (context, state) => const CreateBrandScreen(),
              ),
              GoRoute(
                path: ':id',
                name: 'brandDetail',
                parentNavigatorKey: _rootNavigatorKey,
                builder: (context, state) {
                  final id = state.pathParameters['id']!;
                  return BrandDetailScreen(brandId: id);
                },
              ),
            ],
          ),

          // Campaigns
          GoRoute(
            path: AppRoutes.campaigns,
            name: 'campaigns',
            pageBuilder: (context, state) => const NoTransitionPage(
              child: CampaignsScreen(),
            ),
            routes: [
              GoRoute(
                path: 'create',
                name: 'createCampaign',
                parentNavigatorKey: _rootNavigatorKey,
                builder: (context, state) => const CreateCampaignScreen(),
              ),
              GoRoute(
                path: ':id',
                name: 'campaignDetail',
                parentNavigatorKey: _rootNavigatorKey,
                builder: (context, state) {
                  final id = state.pathParameters['id']!;
                  return CampaignDetailScreen(campaignId: id);
                },
              ),
            ],
          ),
        ],
      ),

      // Full Screen Routes (outside shell)
      GoRoute(
        path: AppRoutes.accounts,
        name: 'accounts',
        builder: (context, state) => const AccountsScreen(),
      ),
      GoRoute(
        path: AppRoutes.subscription,
        name: 'subscription',
        builder: (context, state) => const SubscriptionScreen(),
      ),
      GoRoute(
        path: AppRoutes.notifications,
        name: 'notifications',
        builder: (context, state) => const NotificationsScreen(),
      ),
      GoRoute(
        path: AppRoutes.settings,
        name: 'settings',
        builder: (context, state) => const SettingsScreen(),
      ),
    ],
    errorBuilder: (context, state) => Scaffold(
      body: Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(
              Icons.error_outline,
              size: 64,
              color: Colors.red,
            ),
            const SizedBox(height: 16),
            Text(
              'ไม่พบหน้าที่ต้องการ',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 8),
            Text(
              state.error?.message ?? 'เกิดข้อผิดพลาด',
              style: Theme.of(context).textTheme.bodyMedium,
            ),
            const SizedBox(height: 24),
            ElevatedButton(
              onPressed: () => context.go(AppRoutes.dashboard),
              child: const Text('กลับหน้าหลัก'),
            ),
          ],
        ),
      ),
    ),
  );
});
