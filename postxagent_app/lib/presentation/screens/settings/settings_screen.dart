import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';

import '../../../core/constants/app_constants.dart';

class SettingsScreen extends ConsumerStatefulWidget {
  const SettingsScreen({super.key});

  @override
  ConsumerState<SettingsScreen> createState() => _SettingsScreenState();
}

class _SettingsScreenState extends ConsumerState<SettingsScreen> {
  // Sample user data
  final Map<String, dynamic> _user = {
    'name': 'สมชาย ใจดี',
    'email': 'somchai@example.com',
    'phone': '0812345678',
    'avatar': null,
  };

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('ตั้งค่า'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppConstants.spacingMd),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Profile Card
            _buildProfileCard(),
            const SizedBox(height: AppConstants.spacingLg),

            // Account Settings
            _buildSectionTitle('บัญชี'),
            _buildSettingsTile(
              icon: Iconsax.user,
              title: 'แก้ไขโปรไฟล์',
              subtitle: 'ชื่อ, อีเมล, รูปภาพ',
              onTap: () => _showEditProfile(context),
            ),
            _buildSettingsTile(
              icon: Iconsax.lock,
              title: 'เปลี่ยนรหัสผ่าน',
              subtitle: 'อัพเดทรหัสผ่านของคุณ',
              onTap: () => _showChangePassword(context),
            ),
            _buildSettingsTile(
              icon: Iconsax.shield_tick,
              title: 'ความปลอดภัย',
              subtitle: 'การยืนยันตัวตนสองขั้นตอน',
              onTap: () {},
            ),
            const SizedBox(height: AppConstants.spacingLg),

            // Preferences
            _buildSectionTitle('การตั้งค่า'),
            _buildSettingsTile(
              icon: Iconsax.notification,
              title: 'การแจ้งเตือน',
              subtitle: 'จัดการการแจ้งเตือน',
              onTap: () => context.push('/notifications'),
            ),
            _buildSettingsTile(
              icon: Iconsax.language_square,
              title: 'ภาษา',
              subtitle: 'ไทย',
              trailing: Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                ),
                child: const Text(
                  'TH',
                  style: TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: AppColors.textSecondary,
                  ),
                ),
              ),
              onTap: () => _showLanguageSelector(context),
            ),
            _buildSettingsTile(
              icon: Iconsax.moon,
              title: 'ธีม',
              subtitle: 'มืด',
              trailing: Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                ),
                child: const Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Icon(Iconsax.moon, size: 14, color: AppColors.textSecondary),
                    SizedBox(width: 4),
                    Text(
                      'มืด',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              onTap: () {},
            ),
            const SizedBox(height: AppConstants.spacingLg),

            // AI Settings
            _buildSectionTitle('AI Settings'),
            _buildSettingsTile(
              icon: Iconsax.magic_star,
              title: 'AI Provider',
              subtitle: 'Google Gemini',
              onTap: () => _showAIProviderSelector(context),
            ),
            _buildSettingsTile(
              icon: Iconsax.text,
              title: 'โทนเสียงเริ่มต้น',
              subtitle: 'เป็นกันเอง',
              onTap: () {},
            ),
            _buildSettingsTile(
              icon: Iconsax.gallery,
              title: 'Image AI Provider',
              subtitle: 'Stable Diffusion',
              onTap: () {},
            ),
            const SizedBox(height: AppConstants.spacingLg),

            // Support
            _buildSectionTitle('ช่วยเหลือ'),
            _buildSettingsTile(
              icon: Iconsax.book,
              title: 'คู่มือการใช้งาน',
              subtitle: 'เรียนรู้วิธีใช้งาน',
              onTap: () {},
            ),
            _buildSettingsTile(
              icon: Iconsax.message_question,
              title: 'ติดต่อเรา',
              subtitle: 'แจ้งปัญหาหรือข้อเสนอแนะ',
              onTap: () => _showContactUs(context),
            ),
            _buildSettingsTile(
              icon: Iconsax.info_circle,
              title: 'เกี่ยวกับ',
              subtitle: 'เวอร์ชัน ${AppConstants.appVersion}',
              onTap: () => _showAbout(context),
            ),
            const SizedBox(height: AppConstants.spacingLg),

            // Logout
            _buildLogoutButton(),
            const SizedBox(height: AppConstants.spacingXl),
          ],
        ),
      ),
    );
  }

  Widget _buildProfileCard() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        gradient: AppColors.primaryGradient,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
      ),
      child: Row(
        children: [
          Container(
            width: 64,
            height: 64,
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            ),
            child: Center(
              child: Text(
                _user['name'].toString().substring(0, 1),
                style: const TextStyle(
                  fontSize: 28,
                  fontWeight: FontWeight.bold,
                  color: AppColors.primary,
                ),
              ),
            ),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  _user['name'],
                  style: const TextStyle(
                    fontSize: 18,
                    fontWeight: FontWeight.w600,
                    color: Colors.white,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  _user['email'],
                  style: TextStyle(
                    fontSize: 13,
                    color: Colors.white.withValues(alpha:0.8),
                  ),
                ),
                const SizedBox(height: 4),
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                  decoration: BoxDecoration(
                    color: Colors.white.withValues(alpha:0.2),
                    borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                  ),
                  child: const Text(
                    'Pro Member',
                    style: TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.w500,
                      color: Colors.white,
                    ),
                  ),
                ),
              ],
            ),
          ),
          IconButton(
            icon: const Icon(Iconsax.edit_2, color: Colors.white),
            onPressed: () => _showEditProfile(context),
          ),
        ],
      ),
    );
  }

  Widget _buildSectionTitle(String title) {
    return Padding(
      padding: const EdgeInsets.only(bottom: AppConstants.spacingSm),
      child: Text(
        title,
        style: const TextStyle(
          fontSize: 14,
          fontWeight: FontWeight.w600,
          color: AppColors.textMuted,
        ),
      ),
    );
  }

  Widget _buildSettingsTile({
    required IconData icon,
    required String title,
    required String subtitle,
    Widget? trailing,
    required VoidCallback onTap,
  }) {
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        border: Border.all(color: AppColors.border),
      ),
      child: ListTile(
        leading: Container(
          width: 40,
          height: 40,
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: BorderRadius.circular(AppConstants.radiusSm),
          ),
          child: Icon(icon, color: AppColors.textSecondary, size: 20),
        ),
        title: Text(
          title,
          style: const TextStyle(
            fontSize: 14,
            fontWeight: FontWeight.w500,
            color: AppColors.textPrimary,
          ),
        ),
        subtitle: Text(
          subtitle,
          style: const TextStyle(
            fontSize: 12,
            color: AppColors.textMuted,
          ),
        ),
        trailing: trailing ?? const Icon(Iconsax.arrow_right_3, size: 18, color: AppColors.textMuted),
        onTap: onTap,
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
      ),
    );
  }

  Widget _buildLogoutButton() {
    return SizedBox(
      width: double.infinity,
      child: OutlinedButton.icon(
        onPressed: () => _showLogoutConfirm(context),
        style: OutlinedButton.styleFrom(
          foregroundColor: AppColors.error,
          side: const BorderSide(color: AppColors.error),
          padding: const EdgeInsets.symmetric(vertical: 14),
        ),
        icon: const Icon(Iconsax.logout),
        label: const Text('ออกจากระบบ'),
      ),
    );
  }

  void _showEditProfile(BuildContext context) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.card,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppConstants.radiusLg),
        ),
      ),
      builder: (context) => Padding(
        padding: EdgeInsets.only(
          bottom: MediaQuery.of(context).viewInsets.bottom,
        ),
        child: DraggableScrollableSheet(
          initialChildSize: 0.6,
          minChildSize: 0.4,
          maxChildSize: 0.9,
          expand: false,
          builder: (context, scrollController) => _EditProfileSheet(
            scrollController: scrollController,
            user: _user,
          ),
        ),
      ),
    );
  }

  void _showChangePassword(BuildContext context) {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: AppColors.card,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppConstants.radiusLg),
        ),
      ),
      builder: (context) => Padding(
        padding: EdgeInsets.only(
          bottom: MediaQuery.of(context).viewInsets.bottom,
        ),
        child: const _ChangePasswordSheet(),
      ),
    );
  }

  void _showLanguageSelector(BuildContext context) {
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
            const Text(
              'เลือกภาษา',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppConstants.spacingMd),
            ListTile(
              leading: const Text('🇹🇭', style: TextStyle(fontSize: 24)),
              title: const Text('ไทย'),
              trailing: const Icon(Iconsax.tick_circle, color: AppColors.success),
              onTap: () => Navigator.pop(context),
            ),
            ListTile(
              leading: const Text('🇺🇸', style: TextStyle(fontSize: 24)),
              title: const Text('English'),
              onTap: () => Navigator.pop(context),
            ),
            SizedBox(height: MediaQuery.of(context).padding.bottom),
          ],
        ),
      ),
    );
  }

  void _showAIProviderSelector(BuildContext context) {
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
            const Text(
              'เลือก AI Provider',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppConstants.spacingMd),
            ListTile(
              leading: Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: AppColors.success.withValues(alpha:0.15),
                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                ),
                child: const Icon(Iconsax.cpu, color: AppColors.success),
              ),
              title: const Text('Ollama (Local)'),
              subtitle: const Text('ฟรี, ทำงานบนเครื่อง'),
              onTap: () => Navigator.pop(context),
            ),
            ListTile(
              leading: Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: AppColors.info.withValues(alpha:0.15),
                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                ),
                child: const Icon(Iconsax.star, color: AppColors.info),
              ),
              title: const Text('Google Gemini'),
              subtitle: const Text('มี Free tier'),
              trailing: const Icon(Iconsax.tick_circle, color: AppColors.success),
              onTap: () => Navigator.pop(context),
            ),
            ListTile(
              leading: Container(
                width: 40,
                height: 40,
                decoration: BoxDecoration(
                  color: AppColors.secondary.withValues(alpha:0.15),
                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                ),
                child: const Icon(Iconsax.cpu_setting, color: AppColors.secondary),
              ),
              title: const Text('OpenAI GPT-4'),
              subtitle: const Text('เสียค่าใช้จ่าย'),
              onTap: () => Navigator.pop(context),
            ),
            SizedBox(height: MediaQuery.of(context).padding.bottom),
          ],
        ),
      ),
    );
  }

  void _showContactUs(BuildContext context) {
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
            const Text(
              'ติดต่อเรา',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppConstants.spacingMd),
            ListTile(
              leading: const Icon(Iconsax.message_text, color: AppColors.line),
              title: const Text('LINE Official'),
              subtitle: const Text('@postxagent'),
              onTap: () {},
            ),
            ListTile(
              leading: const Icon(Icons.facebook, color: AppColors.facebook),
              title: const Text('Facebook'),
              subtitle: const Text('PostXAgent'),
              onTap: () {},
            ),
            ListTile(
              leading: const Icon(Iconsax.sms, color: AppColors.primary),
              title: const Text('อีเมล'),
              subtitle: const Text('support@postxagent.com'),
              onTap: () {},
            ),
            SizedBox(height: MediaQuery.of(context).padding.bottom),
          ],
        ),
      ),
    );
  }

  void _showAbout(BuildContext context) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: AppColors.card,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        ),
        title: const Row(
          children: [
            Icon(Iconsax.cpu_charge, color: AppColors.primary),
            SizedBox(width: 8),
            Text(AppConstants.appName),
          ],
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(AppConstants.appTagline),
            const SizedBox(height: 16),
            _buildAboutRow('เวอร์ชัน', AppConstants.appVersion),
            _buildAboutRow('พัฒนาโดย', 'PostXAgent Team'),
            const SizedBox(height: 16),
            const Text(
              '© 2024 PostXAgent. All rights reserved.',
              style: TextStyle(
                fontSize: 11,
                color: AppColors.textMuted,
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('ปิด'),
          ),
        ],
      ),
    );
  }

  Widget _buildAboutRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label, style: const TextStyle(color: AppColors.textMuted)),
          Text(value, style: const TextStyle(fontWeight: FontWeight.w500)),
        ],
      ),
    );
  }

  void _showLogoutConfirm(BuildContext context) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: AppColors.card,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        ),
        title: const Text('ออกจากระบบ'),
        content: const Text('คุณต้องการออกจากระบบหรือไม่?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('ยกเลิก'),
          ),
          ElevatedButton(
            onPressed: () {
              Navigator.pop(context);
              context.go('/login');
            },
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.error,
            ),
            child: const Text('ออกจากระบบ'),
          ),
        ],
      ),
    );
  }
}

class _EditProfileSheet extends StatelessWidget {
  final ScrollController scrollController;
  final Map<String, dynamic> user;

  const _EditProfileSheet({
    required this.scrollController,
    required this.user,
  });

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Container(
          margin: const EdgeInsets.only(top: 12),
          width: 40,
          height: 4,
          decoration: BoxDecoration(
            color: AppColors.border,
            borderRadius: BorderRadius.circular(2),
          ),
        ),
        const Padding(
          padding: EdgeInsets.all(AppConstants.spacingMd),
          child: Text(
            'แก้ไขโปรไฟล์',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
            ),
          ),
        ),
        const Divider(color: AppColors.border, height: 1),
        Expanded(
          child: ListView(
            controller: scrollController,
            padding: const EdgeInsets.all(AppConstants.spacingMd),
            children: [
              // Avatar
              Center(
                child: Stack(
                  children: [
                    Container(
                      width: 80,
                      height: 80,
                      decoration: BoxDecoration(
                        gradient: AppColors.primaryGradient,
                        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                      ),
                      child: Center(
                        child: Text(
                          user['name'].toString().substring(0, 1),
                          style: const TextStyle(
                            fontSize: 32,
                            fontWeight: FontWeight.bold,
                            color: Colors.white,
                          ),
                        ),
                      ),
                    ),
                    Positioned(
                      bottom: -4,
                      right: -4,
                      child: Container(
                        padding: const EdgeInsets.all(6),
                        decoration: const BoxDecoration(
                          color: AppColors.card,
                          shape: BoxShape.circle,
                        ),
                        child: const Icon(Iconsax.camera, size: 16, color: AppColors.primary),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: AppConstants.spacingLg),
              TextField(
                decoration: const InputDecoration(
                  labelText: 'ชื่อ-นามสกุล',
                  prefixIcon: Icon(Iconsax.user),
                ),
                controller: TextEditingController(text: user['name']),
              ),
              const SizedBox(height: AppConstants.spacingMd),
              TextField(
                decoration: const InputDecoration(
                  labelText: 'อีเมล',
                  prefixIcon: Icon(Iconsax.sms),
                ),
                controller: TextEditingController(text: user['email']),
              ),
              const SizedBox(height: AppConstants.spacingMd),
              TextField(
                decoration: const InputDecoration(
                  labelText: 'เบอร์โทรศัพท์',
                  prefixIcon: Icon(Iconsax.call),
                ),
                controller: TextEditingController(text: user['phone']),
              ),
              const SizedBox(height: AppConstants.spacingLg),
              ElevatedButton(
                onPressed: () => Navigator.pop(context),
                child: const Text('บันทึก'),
              ),
            ],
          ),
        ),
      ],
    );
  }
}

class _ChangePasswordSheet extends StatelessWidget {
  const _ChangePasswordSheet();

  @override
  Widget build(BuildContext context) {
    return Padding(
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
          const Text(
            'เปลี่ยนรหัสผ่าน',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          const TextField(
            obscureText: true,
            decoration: InputDecoration(
              labelText: 'รหัสผ่านปัจจุบัน',
              prefixIcon: Icon(Iconsax.lock),
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          const TextField(
            obscureText: true,
            decoration: InputDecoration(
              labelText: 'รหัสผ่านใหม่',
              prefixIcon: Icon(Iconsax.lock_1),
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          const TextField(
            obscureText: true,
            decoration: InputDecoration(
              labelText: 'ยืนยันรหัสผ่านใหม่',
              prefixIcon: Icon(Iconsax.lock_1),
            ),
          ),
          const SizedBox(height: AppConstants.spacingLg),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('เปลี่ยนรหัสผ่าน'),
            ),
          ),
          SizedBox(height: MediaQuery.of(context).padding.bottom + AppConstants.spacingMd),
        ],
      ),
    );
  }
}
