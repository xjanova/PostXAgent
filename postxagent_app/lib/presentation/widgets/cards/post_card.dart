import 'package:flutter/material.dart';
import 'package:iconsax/iconsax.dart';
import 'package:cached_network_image/cached_network_image.dart';
import 'package:timeago/timeago.dart' as timeago;

import '../../../core/constants/app_constants.dart';
import '../../../data/models/post.dart';

class PostCard extends StatelessWidget {
  final Post post;
  final VoidCallback? onTap;
  final VoidCallback? onEdit;
  final VoidCallback? onDelete;
  final VoidCallback? onPublish;

  const PostCard({
    super.key,
    required this.post,
    this.onTap,
    this.onEdit,
    this.onDelete,
    this.onPublish,
  });

  @override
  Widget build(BuildContext context) {
    final platformConfig = Platforms.all[post.platform];
    final statusColor = PostStatus.getColor(post.status);
    final statusLabel = PostStatus.getLabel(post.status);

    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(AppConstants.radiusLg),
      child: Container(
        decoration: BoxDecoration(
          color: AppColors.card,
          borderRadius: BorderRadius.circular(AppConstants.radiusLg),
          border: Border.all(color: AppColors.border),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Header
            Padding(
              padding: const EdgeInsets.all(AppConstants.spacingMd),
              child: Row(
                children: [
                  // Brand/Platform
                  Container(
                    width: 44,
                    height: 44,
                    decoration: BoxDecoration(
                      color: (platformConfig?.color ?? AppColors.textMuted)
                          .withValues(alpha:0.15),
                      borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                    ),
                    child: post.brand?.logoUrl != null
                        ? ClipRRect(
                            borderRadius:
                                BorderRadius.circular(AppConstants.radiusSm),
                            child: CachedNetworkImage(
                              imageUrl: post.brand!.logoUrl!,
                              fit: BoxFit.cover,
                            ),
                          )
                        : Icon(
                            _getPlatformIcon(post.platform),
                            color: platformConfig?.color ?? AppColors.textMuted,
                            size: 22,
                          ),
                  ),
                  const SizedBox(width: 12),

                  // Info
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            if (post.brand != null) ...[
                              Text(
                                post.brand!.name,
                                style: const TextStyle(
                                  fontSize: 14,
                                  fontWeight: FontWeight.w600,
                                  color: AppColors.textPrimary,
                                ),
                              ),
                              const SizedBox(width: 6),
                            ],
                            Container(
                              padding: const EdgeInsets.symmetric(
                                  horizontal: 6, vertical: 2),
                              decoration: BoxDecoration(
                                color: (platformConfig?.color ??
                                        AppColors.textMuted)
                                    .withValues(alpha:0.15),
                                borderRadius: BorderRadius.circular(4),
                              ),
                              child: Text(
                                platformConfig?.name ?? post.platform,
                                style: TextStyle(
                                  fontSize: 10,
                                  fontWeight: FontWeight.w500,
                                  color: platformConfig?.color ??
                                      AppColors.textMuted,
                                ),
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: 4),
                        Text(
                          _getTimeText(),
                          style: const TextStyle(
                            fontSize: 12,
                            color: AppColors.textMuted,
                          ),
                        ),
                      ],
                    ),
                  ),

                  // Status Badge
                  Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: statusColor.withValues(alpha:0.15),
                      borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          PostStatus.getIcon(post.status),
                          size: 12,
                          color: statusColor,
                        ),
                        const SizedBox(width: 4),
                        Text(
                          statusLabel,
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w500,
                            color: statusColor,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),

            // Content
            if (post.contentText != null && post.contentText!.isNotEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: AppConstants.spacingMd,
                ),
                child: Text(
                  post.contentText!,
                  style: const TextStyle(
                    fontSize: 14,
                    color: AppColors.textPrimary,
                    height: 1.4,
                  ),
                  maxLines: 3,
                  overflow: TextOverflow.ellipsis,
                ),
              ),

            // Media Preview
            if (post.firstMediaUrl != null) ...[
              const SizedBox(height: AppConstants.spacingSm),
              AspectRatio(
                aspectRatio: 16 / 9,
                child: CachedNetworkImage(
                  imageUrl: post.firstMediaUrl!,
                  fit: BoxFit.cover,
                  placeholder: (context, url) => Container(
                    color: AppColors.surface,
                    child: const Center(
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  ),
                  errorWidget: (context, url, error) => Container(
                    color: AppColors.surface,
                    child: const Center(
                      child: Icon(Iconsax.gallery_slash, color: AppColors.textMuted),
                    ),
                  ),
                ),
              ),
            ],

            // Metrics or Actions
            Padding(
              padding: const EdgeInsets.all(AppConstants.spacingMd),
              child: post.status == 'published'
                  ? _buildMetrics()
                  : _buildActions(),
            ),

            // Viral Badge
            if (post.isViral) ...[
              Container(
                width: double.infinity,
                padding: const EdgeInsets.symmetric(vertical: 8),
                decoration: BoxDecoration(
                  gradient: LinearGradient(
                    colors: [
                      AppColors.viral.withValues(alpha:0.1),
                      AppColors.trending.withValues(alpha:0.1),
                    ],
                  ),
                  borderRadius: const BorderRadius.only(
                    bottomLeft: Radius.circular(AppConstants.radiusLg),
                    bottomRight: Radius.circular(AppConstants.radiusLg),
                  ),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    const Icon(Iconsax.flash_1, size: 16, color: AppColors.viral),
                    const SizedBox(width: 6),
                    const Text(
                      'Viral!',
                      style: TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: AppColors.viral,
                      ),
                    ),
                    if (post.viralScore != null) ...[
                      const SizedBox(width: 8),
                      Text(
                        'Score: ${post.viralScore}',
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ],
                ),
              ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildMetrics() {
    return Row(
      children: [
        _metricItem(Iconsax.heart, post.metrics?.likes ?? 0, AppColors.error),
        const SizedBox(width: 16),
        _metricItem(Iconsax.message, post.metrics?.comments ?? 0, AppColors.info),
        const SizedBox(width: 16),
        _metricItem(Iconsax.share, post.metrics?.shares ?? 0, AppColors.success),
        if (post.metrics?.views != null && post.metrics!.views! > 0) ...[
          const SizedBox(width: 16),
          _metricItem(Iconsax.eye, post.metrics!.views!, AppColors.textSecondary),
        ],
        const Spacer(),
        if (post.metrics?.engagementRate != null)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
            decoration: BoxDecoration(
              color: AppColors.surface,
              borderRadius: BorderRadius.circular(AppConstants.radiusFull),
            ),
            child: Text(
              '${post.metrics!.engagementRate!.toStringAsFixed(1)}% engagement',
              style: const TextStyle(
                fontSize: 11,
                color: AppColors.textSecondary,
              ),
            ),
          ),
      ],
    );
  }

  Widget _metricItem(IconData icon, int value, Color color) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(icon, size: 16, color: color),
        const SizedBox(width: 4),
        Text(
          _formatNumber(value),
          style: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w500,
            color: AppColors.textSecondary,
          ),
        ),
      ],
    );
  }

  Widget _buildActions() {
    return Row(
      children: [
        if (post.aiGenerated)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
            decoration: BoxDecoration(
              color: AppColors.secondary.withValues(alpha:0.15),
              borderRadius: BorderRadius.circular(4),
            ),
            child: const Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                Icon(Iconsax.magic_star, size: 12, color: AppColors.secondary),
                SizedBox(width: 4),
                Text(
                  'AI',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.w500,
                    color: AppColors.secondary,
                  ),
                ),
              ],
            ),
          ),
        const Spacer(),
        if (onEdit != null)
          IconButton(
            icon: const Icon(Iconsax.edit_2, size: 18),
            onPressed: onEdit,
            color: AppColors.textSecondary,
            padding: EdgeInsets.zero,
            constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
          ),
        if (post.status == 'draft' || post.status == 'scheduled')
          if (onPublish != null)
            IconButton(
              icon: const Icon(Iconsax.send_1, size: 18),
              onPressed: onPublish,
              color: AppColors.primary,
              padding: EdgeInsets.zero,
              constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
            ),
        if (onDelete != null)
          IconButton(
            icon: const Icon(Iconsax.trash, size: 18),
            onPressed: onDelete,
            color: AppColors.error,
            padding: EdgeInsets.zero,
            constraints: const BoxConstraints(minWidth: 36, minHeight: 36),
          ),
      ],
    );
  }

  String _getTimeText() {
    if (post.publishedAt != null) {
      return timeago.format(post.publishedAt!, locale: 'th');
    }
    if (post.scheduledAt != null) {
      return 'กำหนดโพสต์ ${_formatDateTime(post.scheduledAt!)}';
    }
    return timeago.format(post.createdAt, locale: 'th');
  }

  String _formatDateTime(DateTime date) {
    return '${date.day}/${date.month} ${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';
  }

  String _formatNumber(int number) {
    if (number >= 1000000) {
      return '${(number / 1000000).toStringAsFixed(1)}M';
    } else if (number >= 1000) {
      return '${(number / 1000).toStringAsFixed(1)}K';
    }
    return number.toString();
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
