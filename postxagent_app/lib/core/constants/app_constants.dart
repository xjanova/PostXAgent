import 'package:flutter/material.dart';

/// App Constants
class AppConstants {
  AppConstants._();

  static const String appName = 'PostXAgent';
  static const String appTagline = 'AI-Powered Social Media Marketing';
  static const String appVersion = '1.0.0';

  // API Configuration
  static const String baseUrl = 'http://localhost:8000/api/v1';
  static const Duration apiTimeout = Duration(seconds: 30);

  // Storage Keys
  static const String tokenKey = 'auth_token';
  static const String refreshTokenKey = 'refresh_token';
  static const String userKey = 'user_data';
  static const String themeKey = 'theme_mode';
  static const String languageKey = 'language';
  static const String onboardingKey = 'onboarding_complete';

  // Pagination
  static const int defaultPageSize = 20;

  // Animation Durations
  static const Duration shortAnimation = Duration(milliseconds: 200);
  static const Duration mediumAnimation = Duration(milliseconds: 350);
  static const Duration longAnimation = Duration(milliseconds: 500);

  // Spacing
  static const double spacingXs = 4.0;
  static const double spacingSm = 8.0;
  static const double spacingMd = 16.0;
  static const double spacingLg = 24.0;
  static const double spacingXl = 32.0;
  static const double spacingXxl = 48.0;

  // Border Radius
  static const double radiusSm = 8.0;
  static const double radiusMd = 12.0;
  static const double radiusLg = 16.0;
  static const double radiusXl = 24.0;
  static const double radiusFull = 100.0;

  // Card Elevation
  static const double elevationSm = 2.0;
  static const double elevationMd = 4.0;
  static const double elevationLg = 8.0;
}

/// App Colors - Premium Dark Theme with Modern Vibrant Colors
class AppColors {
  AppColors._();

  // Primary Colors - Vibrant Purple/Blue
  static const Color primary = Color(0xFF8B5CF6);      // Vibrant Purple
  static const Color primaryDark = Color(0xFF7C3AED);
  static const Color primaryLight = Color(0xFFA78BFA);
  static const Color secondary = Color(0xFF06B6D4);    // Cyan
  static const Color accent = Color(0xFFEC4899);       // Pink

  // Additional Accent Colors
  static const Color tertiary = Color(0xFF10B981);     // Emerald
  static const Color gold = Color(0xFFF59E0B);         // Amber/Gold

  // Background Colors - Deep Dark
  static const Color background = Color(0xFF0A0A0F);   // Near black
  static const Color backgroundSecondary = Color(0xFF12121A);
  static const Color card = Color(0xFF18181F);
  static const Color cardElevated = Color(0xFF1F1F2A);
  static const Color surface = Color(0xFF1A1A24);
  static const Color inputBackground = Color(0xFF252530);

  // Glass Effect Colors
  static const Color glass = Color(0xFF1E1E2E);
  static const Color glassBorder = Color(0xFF2A2A3C);

  // Text Colors
  static const Color textPrimary = Color(0xFFFAFAFA);
  static const Color textSecondary = Color(0xFFA1A1AA);
  static const Color textMuted = Color(0xFF71717A);
  static const Color textDisabled = Color(0xFF52525B);

  // Status Colors - More Vibrant
  static const Color success = Color(0xFF22C55E);      // Green 500
  static const Color successLight = Color(0xFF4ADE80);
  static const Color warning = Color(0xFFF97316);      // Orange 500
  static const Color warningLight = Color(0xFFFB923C);
  static const Color error = Color(0xFFEF4444);        // Red 500
  static const Color errorLight = Color(0xFFF87171);
  static const Color info = Color(0xFF3B82F6);         // Blue 500
  static const Color infoLight = Color(0xFF60A5FA);

  // Platform Colors
  static const Color facebook = Color(0xFF1877F2);
  static const Color instagram = Color(0xFFE4405F);
  static const Color instagramGradientStart = Color(0xFFF58529);
  static const Color instagramGradientMiddle = Color(0xFFDD2A7B);
  static const Color instagramGradientEnd = Color(0xFF8134AF);
  static const Color tiktok = Color(0xFF000000);
  static const Color tiktokAccent = Color(0xFFEE1D52);
  static const Color twitter = Color(0xFF1DA1F2);
  static const Color line = Color(0xFF00B900);
  static const Color youtube = Color(0xFFFF0000);
  static const Color threads = Color(0xFF000000);
  static const Color linkedin = Color(0xFF0A66C2);
  static const Color pinterest = Color(0xFFE60023);

  // Gradients - More Dynamic
  static const LinearGradient primaryGradient = LinearGradient(
    colors: [Color(0xFF8B5CF6), Color(0xFF06B6D4)],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  static const LinearGradient accentGradient = LinearGradient(
    colors: [Color(0xFFEC4899), Color(0xFFF97316)],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  static const LinearGradient successGradient = LinearGradient(
    colors: [Color(0xFF10B981), Color(0xFF06B6D4)],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  static const LinearGradient goldGradient = LinearGradient(
    colors: [Color(0xFFF59E0B), Color(0xFFEF4444)],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  static const LinearGradient cardGradient = LinearGradient(
    colors: [card, cardElevated],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  static const LinearGradient glassGradient = LinearGradient(
    colors: [
      Color(0x20FFFFFF),
      Color(0x08FFFFFF),
    ],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  static const LinearGradient instagramGradient = LinearGradient(
    colors: [instagramGradientStart, instagramGradientMiddle, instagramGradientEnd],
    begin: Alignment.topLeft,
    end: Alignment.bottomRight,
  );

  // Neon/Glow Colors
  static const Color neonPurple = Color(0xFFBF5AF2);
  static const Color neonBlue = Color(0xFF0EA5E9);
  static const Color neonGreen = Color(0xFF22D3EE);
  static const Color neonPink = Color(0xFFF472B6);

  // Viral/Trending
  static const Color viral = Color(0xFFFF6B35);
  static const Color trending = Color(0xFFFFD93D);

  // Border
  static const Color border = Color(0xFF27272A);
  static const Color borderLight = Color(0xFF3F3F46);
  static const Color divider = Color(0xFF1F1F24);

  // Shimmer Colors
  static const Color shimmerBase = Color(0xFF1F1F2A);
  static const Color shimmerHighlight = Color(0xFF2A2A3C);
}

/// Platform Configuration
class PlatformConfig {
  final String name;
  final String nameTh;
  final String icon;
  final Color color;
  final int maxCharacters;
  final bool supportsImage;
  final bool supportsVideo;
  final bool supportsCarousel;
  final bool supportsStory;
  final bool supportsReel;

  const PlatformConfig({
    required this.name,
    required this.nameTh,
    required this.icon,
    required this.color,
    required this.maxCharacters,
    this.supportsImage = true,
    this.supportsVideo = true,
    this.supportsCarousel = false,
    this.supportsStory = false,
    this.supportsReel = false,
  });
}

class Platforms {
  Platforms._();

  static const Map<String, PlatformConfig> all = {
    'facebook': PlatformConfig(
      name: 'Facebook',
      nameTh: 'เฟซบุ๊ก',
      icon: 'facebook',
      color: AppColors.facebook,
      maxCharacters: 63206,
      supportsCarousel: true,
      supportsStory: true,
      supportsReel: true,
    ),
    'instagram': PlatformConfig(
      name: 'Instagram',
      nameTh: 'อินสตาแกรม',
      icon: 'instagram',
      color: AppColors.instagram,
      maxCharacters: 2200,
      supportsCarousel: true,
      supportsStory: true,
      supportsReel: true,
    ),
    'tiktok': PlatformConfig(
      name: 'TikTok',
      nameTh: 'ติ๊กต็อก',
      icon: 'tiktok',
      color: AppColors.tiktok,
      maxCharacters: 2200,
      supportsImage: false,
    ),
    'twitter': PlatformConfig(
      name: 'X (Twitter)',
      nameTh: 'เอ็กซ์ (ทวิตเตอร์)',
      icon: 'twitter',
      color: AppColors.twitter,
      maxCharacters: 280,
      supportsCarousel: true,
    ),
    'line': PlatformConfig(
      name: 'LINE',
      nameTh: 'ไลน์',
      icon: 'line',
      color: AppColors.line,
      maxCharacters: 10000,
    ),
    'youtube': PlatformConfig(
      name: 'YouTube',
      nameTh: 'ยูทูบ',
      icon: 'youtube',
      color: AppColors.youtube,
      maxCharacters: 5000,
      supportsImage: false,
    ),
    'threads': PlatformConfig(
      name: 'Threads',
      nameTh: 'เธรดส์',
      icon: 'threads',
      color: AppColors.threads,
      maxCharacters: 500,
      supportsCarousel: true,
    ),
    'linkedin': PlatformConfig(
      name: 'LinkedIn',
      nameTh: 'ลิงก์อิน',
      icon: 'linkedin',
      color: AppColors.linkedin,
      maxCharacters: 3000,
      supportsCarousel: true,
    ),
    'pinterest': PlatformConfig(
      name: 'Pinterest',
      nameTh: 'พินเทอเรสต์',
      icon: 'pinterest',
      color: AppColors.pinterest,
      maxCharacters: 500,
    ),
  };
}

/// Post Status Configuration
class PostStatus {
  static const String draft = 'draft';
  static const String pending = 'pending';
  static const String scheduled = 'scheduled';
  static const String publishing = 'publishing';
  static const String published = 'published';
  static const String failed = 'failed';
  static const String deleted = 'deleted';

  static Color getColor(String status) {
    switch (status) {
      case draft:
        return AppColors.textMuted;
      case pending:
        return AppColors.warning;
      case scheduled:
        return AppColors.info;
      case publishing:
        return AppColors.primary;
      case published:
        return AppColors.success;
      case failed:
        return AppColors.error;
      case deleted:
        return AppColors.textDisabled;
      default:
        return AppColors.textMuted;
    }
  }

  static String getLabel(String status) {
    switch (status) {
      case draft:
        return 'แบบร่าง';
      case pending:
        return 'รอดำเนินการ';
      case scheduled:
        return 'ตั้งเวลาแล้ว';
      case publishing:
        return 'กำลังโพสต์';
      case published:
        return 'โพสต์แล้ว';
      case failed:
        return 'ล้มเหลว';
      case deleted:
        return 'ถูกลบ';
      default:
        return status;
    }
  }

  static IconData getIcon(String status) {
    switch (status) {
      case draft:
        return Icons.edit_outlined;
      case pending:
        return Icons.hourglass_empty;
      case scheduled:
        return Icons.schedule;
      case publishing:
        return Icons.cloud_upload_outlined;
      case published:
        return Icons.check_circle_outline;
      case failed:
        return Icons.error_outline;
      case deleted:
        return Icons.delete_outline;
      default:
        return Icons.help_outline;
    }
  }
}

/// Campaign Status Configuration
class CampaignStatus {
  static const String draft = 'draft';
  static const String scheduled = 'scheduled';
  static const String active = 'active';
  static const String paused = 'paused';
  static const String completed = 'completed';
  static const String cancelled = 'cancelled';

  static Color getColor(String status) {
    switch (status) {
      case draft:
        return AppColors.textMuted;
      case scheduled:
        return AppColors.info;
      case active:
        return AppColors.success;
      case paused:
        return AppColors.warning;
      case completed:
        return AppColors.primary;
      case cancelled:
        return AppColors.error;
      default:
        return AppColors.textMuted;
    }
  }

  static String getLabel(String status) {
    switch (status) {
      case draft:
        return 'แบบร่าง';
      case scheduled:
        return 'ตั้งเวลาแล้ว';
      case active:
        return 'กำลังทำงาน';
      case paused:
        return 'หยุดชั่วคราว';
      case completed:
        return 'เสร็จสิ้น';
      case cancelled:
        return 'ยกเลิก';
      default:
        return status;
    }
  }
}
