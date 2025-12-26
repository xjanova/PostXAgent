import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../../core/constants/app_constants.dart';
import '../../../data/models/social_account.dart';
import '../../../providers/social_account_provider.dart';

class AccountsScreen extends ConsumerStatefulWidget {
  const AccountsScreen({super.key});

  @override
  ConsumerState<AccountsScreen> createState() => _AccountsScreenState();
}

class _AccountsScreenState extends ConsumerState<AccountsScreen> {
  @override
  void initState() {
    super.initState();
    // Load accounts on init
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(socialAccountsNotifierProvider.notifier).loadAccounts();
    });
  }

  @override
  Widget build(BuildContext context) {
    final accountsState = ref.watch(socialAccountsNotifierProvider);
    final accounts = accountsState.accounts;
    final filteredAccounts = accountsState.platformFilter == null
        ? accounts
        : accounts.where((a) => a.platform == accountsState.platformFilter).toList();
    final connectedCount = accounts.where((a) => a.isActive).length;
    final disconnectedCount = accounts.where((a) => !a.isActive).length;

    return Scaffold(
      appBar: AppBar(
        title: const Text('บัญชีโซเชียล'),
        actions: [
          IconButton(
            icon: const Icon(Iconsax.refresh),
            onPressed: () => _syncAllAccounts(),
            tooltip: 'ซิงค์ทั้งหมด',
          ),
        ],
      ),
      body: accountsState.isLoading
          ? const Center(child: CircularProgressIndicator())
          : RefreshIndicator(
              onRefresh: () => ref.read(socialAccountsNotifierProvider.notifier).loadAccounts(),
              child: Column(
                children: [
                  // Summary & Filter
                  Container(
                    padding: const EdgeInsets.all(AppConstants.spacingMd),
                    decoration: const BoxDecoration(
                      color: AppColors.backgroundSecondary,
                      border: Border(bottom: BorderSide(color: AppColors.border)),
                    ),
                    child: Column(
                      children: [
                        // Summary
                        Row(
                          children: [
                            _buildSummaryChip(
                              'ทั้งหมด',
                              accounts.length.toString(),
                              AppColors.primary,
                            ),
                            const SizedBox(width: 8),
                            _buildSummaryChip(
                              'เชื่อมต่อแล้ว',
                              connectedCount.toString(),
                              AppColors.success,
                            ),
                            const SizedBox(width: 8),
                            _buildSummaryChip(
                              'ขาดการเชื่อมต่อ',
                              disconnectedCount.toString(),
                              AppColors.error,
                            ),
                          ],
                        ),
                        const SizedBox(height: AppConstants.spacingMd),

                        // Platform Filter
                        SingleChildScrollView(
                          scrollDirection: Axis.horizontal,
                          child: Row(
                            children: [
                              _buildPlatformFilter(null, 'ทั้งหมด'),
                              _buildPlatformFilter('facebook', 'Facebook'),
                              _buildPlatformFilter('instagram', 'Instagram'),
                              _buildPlatformFilter('tiktok', 'TikTok'),
                              _buildPlatformFilter('twitter', 'X'),
                              _buildPlatformFilter('line', 'LINE'),
                              _buildPlatformFilter('youtube', 'YouTube'),
                            ],
                          ),
                        ),
                      ],
                    ),
                  ),

                  // Error message
                  if (accountsState.error != null)
                    Container(
                      padding: const EdgeInsets.all(AppConstants.spacingMd),
                      margin: const EdgeInsets.all(AppConstants.spacingMd),
                      decoration: BoxDecoration(
                        color: AppColors.error.withValues(alpha: 0.1),
                        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                      ),
                      child: Row(
                        children: [
                          const Icon(Iconsax.warning_2, color: AppColors.error, size: 20),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              accountsState.error!,
                              style: const TextStyle(color: AppColors.error),
                            ),
                          ),
                        ],
                      ),
                    ),

                  // Accounts List
                  Expanded(
                    child: filteredAccounts.isEmpty
                        ? _buildEmptyState()
                        : ListView.builder(
                            padding: const EdgeInsets.all(AppConstants.spacingMd),
                            itemCount: filteredAccounts.length,
                            itemBuilder: (context, index) {
                              return _buildAccountCard(filteredAccounts[index]);
                            },
                          ),
                  ),
                ],
              ),
            ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => _showConnectAccountSheet(context),
        icon: const Icon(Iconsax.add),
        label: const Text('เชื่อมต่อบัญชี'),
      ),
    );
  }

  Widget _buildEmptyState() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            Iconsax.link_21,
            size: 64,
            color: AppColors.textMuted.withValues(alpha: 0.5),
          ),
          const SizedBox(height: 16),
          const Text(
            'ไม่มีบัญชีที่เชื่อมต่อ',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w600,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: 8),
          const Text(
            'กดปุ่มด้านล่างเพื่อเชื่อมต่อบัญชีใหม่',
            style: TextStyle(
              fontSize: 14,
              color: AppColors.textMuted,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildSummaryChip(String label, String count, Color color) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(AppConstants.radiusFull),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            count,
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.bold,
              color: color,
            ),
          ),
          const SizedBox(width: 4),
          Text(
            label,
            style: TextStyle(
              fontSize: 12,
              color: color,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPlatformFilter(String? platform, String label) {
    final accountsState = ref.watch(socialAccountsNotifierProvider);
    final isSelected = accountsState.platformFilter == platform;
    final config = platform != null ? Platforms.all[platform] : null;
    final color = config?.color ?? AppColors.primary;

    return Padding(
      padding: const EdgeInsets.only(right: 8),
      child: ChoiceChip(
        label: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (platform != null) ...[
              Icon(
                _getPlatformIcon(platform),
                size: 16,
                color: isSelected ? color : AppColors.textMuted,
              ),
              const SizedBox(width: 6),
            ],
            Text(label),
          ],
        ),
        selected: isSelected,
        onSelected: (selected) {
          ref.read(socialAccountsNotifierProvider.notifier).setplatformFilter(platform);
        },
      ),
    );
  }

  Widget _buildAccountCard(SocialAccount account) {
    final platform = account.platform;
    final isConnected = account.isActive;
    final config = Platforms.all[platform];
    final color = config?.color ?? AppColors.textMuted;

    Color statusColor;
    String statusLabel;
    IconData statusIcon;

    if (account.isActive && !account.needsRefresh) {
      statusColor = AppColors.success;
      statusLabel = 'ใช้งานปกติ';
      statusIcon = Iconsax.tick_circle;
    } else if (account.needsRefresh) {
      statusColor = AppColors.warning;
      statusLabel = 'ต้องตรวจสอบ';
      statusIcon = Iconsax.warning_2;
    } else {
      statusColor = AppColors.error;
      statusLabel = 'ขาดการเชื่อมต่อ';
      statusIcon = Iconsax.close_circle;
    }

    return Container(
      margin: const EdgeInsets.only(bottom: AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(
          color: !isConnected ? AppColors.error.withValues(alpha: 0.3) : AppColors.border,
        ),
      ),
      child: InkWell(
        onTap: () => _showAccountDetails(account),
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        child: Padding(
          padding: const EdgeInsets.all(AppConstants.spacingMd),
          child: Column(
            children: [
              Row(
                children: [
                  // Platform Icon
                  Container(
                    width: 52,
                    height: 52,
                    decoration: BoxDecoration(
                      color: color.withValues(alpha: 0.15),
                      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                    ),
                    child: Icon(_getPlatformIcon(platform), color: color, size: 26),
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
                                account.name,
                                style: const TextStyle(
                                  fontSize: 15,
                                  fontWeight: FontWeight.w600,
                                  color: AppColors.textPrimary,
                                ),
                              ),
                            ),
                            Container(
                              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                              decoration: BoxDecoration(
                                color: statusColor.withValues(alpha: 0.15),
                                borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                              ),
                              child: Row(
                                mainAxisSize: MainAxisSize.min,
                                children: [
                                  Icon(statusIcon, size: 12, color: statusColor),
                                  const SizedBox(width: 4),
                                  Text(
                                    statusLabel,
                                    style: TextStyle(
                                      fontSize: 10,
                                      fontWeight: FontWeight.w500,
                                      color: statusColor,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 2),
                        Text(
                          '@${account.platformUsername ?? account.platformUserId ?? ''}',
                          style: const TextStyle(
                            fontSize: 13,
                            color: AppColors.textMuted,
                          ),
                        ),
                      ],
                    ),
                  ),
                  PopupMenuButton<String>(
                    icon: const Icon(Iconsax.more, color: AppColors.textMuted, size: 20),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                    ),
                    color: AppColors.card,
                    onSelected: (value) {
                      switch (value) {
                        case 'sync':
                          _syncAccount(account);
                          break;
                        case 'reconnect':
                          _reconnectAccount(account);
                          break;
                        case 'disconnect':
                          _showDisconnectConfirm(account);
                          break;
                      }
                    },
                    itemBuilder: (context) => [
                      const PopupMenuItem(
                        value: 'sync',
                        child: Row(
                          children: [
                            Icon(Iconsax.refresh, size: 18, color: AppColors.textSecondary),
                            SizedBox(width: 10),
                            Text('ซิงค์ข้อมูล'),
                          ],
                        ),
                      ),
                      if (!isConnected)
                        const PopupMenuItem(
                          value: 'reconnect',
                          child: Row(
                            children: [
                              Icon(Iconsax.link_2, size: 18, color: AppColors.success),
                              SizedBox(width: 10),
                              Text('เชื่อมต่อใหม่'),
                            ],
                          ),
                        ),
                      const PopupMenuItem(
                        value: 'disconnect',
                        child: Row(
                          children: [
                            Icon(Iconsax.link_21, size: 18, color: AppColors.error),
                            SizedBox(width: 10),
                            Text('ยกเลิกการเชื่อมต่อ', style: TextStyle(color: AppColors.error)),
                          ],
                        ),
                      ),
                    ],
                  ),
                ],
              ),
              const SizedBox(height: AppConstants.spacingMd),
              const Divider(color: AppColors.border, height: 1),
              const SizedBox(height: AppConstants.spacingMd),
              Row(
                children: [
                  Expanded(
                    child: _buildAccountStat(
                      'ผู้ติดตาม',
                      _formatNumber(account.followersCount ?? 0),
                      Iconsax.people,
                    ),
                  ),
                  Expanded(
                    child: _buildAccountStat(
                      'โพสต์',
                      '${account.postsCount ?? 0}',
                      Iconsax.document_text,
                    ),
                  ),
                  Expanded(
                    child: _buildAccountStat(
                      'อัพเดท',
                      '${account.updatedAt.hour}:${account.updatedAt.minute.toString().padLeft(2, '0')}',
                      Iconsax.refresh,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildAccountStat(String label, String value, IconData icon) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Icon(icon, size: 14, color: AppColors.textMuted),
        const SizedBox(width: 6),
        Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              value,
              style: const TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
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

  Future<void> _syncAllAccounts() async {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('กำลังซิงค์ข้อมูลทั้งหมด...'),
        backgroundColor: AppColors.info,
      ),
    );
    await ref.read(socialAccountsNotifierProvider.notifier).loadAccounts();
  }

  Future<void> _syncAccount(SocialAccount account) async {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('กำลังซิงค์ ${account.name}...'),
        backgroundColor: AppColors.info,
      ),
    );
    await ref.read(socialAccountsNotifierProvider.notifier).syncAccount(account.id);
  }

  Future<void> _reconnectAccount(SocialAccount account) async {
    // Start OAuth flow
    final oauthUrl = await ref.read(socialAccountsNotifierProvider.notifier)
        .connectAccount(account.platform, account.brandId);
    if (oauthUrl != null) {
      final uri = Uri.parse(oauthUrl);
      if (await canLaunchUrl(uri)) {
        await launchUrl(uri, mode: LaunchMode.externalApplication);
      }
    }
  }

  void _showAccountDetails(SocialAccount account) {
    context.push('/accounts/${account.id}');
  }

  void _showDisconnectConfirm(SocialAccount account) {
    showDialog(
      context: context,
      builder: (dialogContext) => AlertDialog(
        backgroundColor: AppColors.card,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        ),
        title: const Text('ยกเลิกการเชื่อมต่อ'),
        content: Text(
          'คุณต้องการยกเลิกการเชื่อมต่อ ${account.name} หรือไม่?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('ยกเลิก'),
          ),
          ElevatedButton(
            onPressed: () async {
              Navigator.pop(dialogContext);
              final success = await ref
                  .read(socialAccountsNotifierProvider.notifier)
                  .disconnectAccount(account.id);
              if (mounted && success) {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text('ยกเลิกการเชื่อมต่อสำเร็จ'),
                    backgroundColor: AppColors.success,
                  ),
                );
              }
            },
            style: ElevatedButton.styleFrom(
              backgroundColor: AppColors.error,
            ),
            child: const Text('ยกเลิกการเชื่อมต่อ'),
          ),
        ],
      ),
    );
  }

  void _showConnectAccountSheet(BuildContext context) {
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
              'เชื่อมต่อบัญชีโซเชียล',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppConstants.spacingSm),
            const Text(
              'เลือกแพลตฟอร์มที่ต้องการเชื่อมต่อ',
              style: TextStyle(
                fontSize: 13,
                color: AppColors.textSecondary,
              ),
            ),
            const SizedBox(height: AppConstants.spacingMd),
            GridView.count(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              crossAxisCount: 3,
              mainAxisSpacing: 12,
              crossAxisSpacing: 12,
              children: Platforms.all.entries.map((entry) {
                final config = entry.value;
                return InkWell(
                  onTap: () async {
                    Navigator.pop(context);
                    // Start OAuth flow
                    final oauthUrl = await ref
                        .read(socialAccountsNotifierProvider.notifier)
                        .connectAccount(entry.key, null);
                    if (oauthUrl != null) {
                      final uri = Uri.parse(oauthUrl);
                      if (await canLaunchUrl(uri)) {
                        await launchUrl(uri, mode: LaunchMode.externalApplication);
                      }
                    }
                  },
                  borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                  child: Container(
                    decoration: BoxDecoration(
                      color: config.color.withValues(alpha: 0.1),
                      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                      border: Border.all(color: config.color.withValues(alpha: 0.3)),
                    ),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(
                          _getPlatformIcon(entry.key),
                          color: config.color,
                          size: 28,
                        ),
                        const SizedBox(height: 6),
                        Text(
                          config.name,
                          style: TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w500,
                            color: config.color,
                          ),
                        ),
                      ],
                    ),
                  ),
                );
              }).toList(),
            ),
            SizedBox(height: MediaQuery.of(context).padding.bottom + AppConstants.spacingMd),
          ],
        ),
      ),
    );
  }
}
