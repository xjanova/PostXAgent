import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';

import '../../../core/constants/app_constants.dart';
import '../../../data/models/post.dart';
import '../../../providers/brand_provider.dart';
import '../../../providers/post_provider.dart';
import '../../widgets/common/custom_button.dart';

class CreatePostScreen extends ConsumerStatefulWidget {
  final int? brandId;
  final int? postId; // null = create, not null = edit

  const CreatePostScreen({super.key, this.brandId, this.postId});

  @override
  ConsumerState<CreatePostScreen> createState() => _CreatePostScreenState();
}

class _CreatePostScreenState extends ConsumerState<CreatePostScreen> {
  final _contentController = TextEditingController();
  final _hashtagsController = TextEditingController();

  int? _selectedBrandId;
  final List<String> _selectedPlatforms = ['facebook'];
  bool _isScheduled = false;
  DateTime? _scheduledDate;
  TimeOfDay? _scheduledTime;
  final List<String> _mediaFiles = [];

  bool get isEditing => widget.postId != null;

  @override
  void initState() {
    super.initState();
    _selectedBrandId = widget.brandId;
    // Load brands for dropdown
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(brandsNotifierProvider.notifier).loadBrands();
      if (isEditing) {
        _loadPost();
      }
    });
  }

  Future<void> _loadPost() async {
    await ref.read(postDetailNotifierProvider.notifier).loadPost(widget.postId!);
    final post = ref.read(postDetailNotifierProvider).post;
    if (post != null) {
      _contentController.text = post.contentText ?? '';
      _hashtagsController.text = post.hashtags?.map((h) => '#$h').join(' ') ?? '';
      setState(() {
        _selectedBrandId = post.brandId;
        _selectedPlatforms.clear();
        _selectedPlatforms.add(post.platform);
        if (post.scheduledAt != null) {
          _isScheduled = true;
          _scheduledDate = post.scheduledAt;
          _scheduledTime = TimeOfDay.fromDateTime(post.scheduledAt!);
        }
        if (post.mediaUrls != null) {
          _mediaFiles.addAll(post.mediaUrls!);
        }
      });
    }
  }

  @override
  void dispose() {
    _contentController.dispose();
    _hashtagsController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final postDetailState = ref.watch(postDetailNotifierProvider);

    // Update content if AI generated
    ref.listen<CreatePostState>(createPostNotifierProvider, (prev, next) {
      if (next.generatedContent != null && prev?.generatedContent != next.generatedContent) {
        _contentController.text = next.generatedContent!;
        if (next.generatedHashtags != null) {
          _hashtagsController.text = next.generatedHashtags!.map((h) => '#$h').join(' ');
        }
      }
    });

    return Scaffold(
      appBar: AppBar(
        title: Text(isEditing ? 'แก้ไขโพสต์' : 'สร้างโพสต์'),
        actions: [
          TextButton.icon(
            onPressed: _saveDraft,
            icon: const Icon(Iconsax.document, size: 18),
            label: const Text('บันทึกแบบร่าง'),
          ),
        ],
      ),
      body: postDetailState.isLoading && isEditing
          ? const Center(child: CircularProgressIndicator())
          : Column(
        children: [
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppConstants.spacingMd),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Brand Selector
                  _buildBrandSelector(),
                  const SizedBox(height: AppConstants.spacingMd),

                  // Platform Selector
                  _buildPlatformSelector(),
                  const SizedBox(height: AppConstants.spacingMd),

                  // AI Generate Button
                  _buildAIGenerateSection(),
                  const SizedBox(height: AppConstants.spacingMd),

                  // Content Input
                  _buildContentInput(),
                  const SizedBox(height: AppConstants.spacingMd),

                  // Media Upload
                  _buildMediaUpload(),
                  const SizedBox(height: AppConstants.spacingMd),

                  // Hashtags
                  _buildHashtagsInput(),
                  const SizedBox(height: AppConstants.spacingMd),

                  // Schedule Toggle
                  _buildScheduleSection(),
                  const SizedBox(height: AppConstants.spacingLg),
                ],
              ),
            ),
          ),

          // Bottom Action Bar
          _buildBottomActions(),
        ],
      ),
    );
  }

  Widget _buildBrandSelector() {
    final brandsState = ref.watch(brandsNotifierProvider);

    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'แบรนด์',
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w500,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingSm),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: AppConstants.spacingMd),
            decoration: BoxDecoration(
              color: AppColors.inputBackground,
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
              border: Border.all(color: AppColors.border),
            ),
            child: DropdownButtonHideUnderline(
              child: DropdownButton<int>(
                value: _selectedBrandId,
                isExpanded: true,
                hint: const Text(
                  'เลือกแบรนด์ (ไม่บังคับ)',
                  style: TextStyle(color: AppColors.textMuted),
                ),
                dropdownColor: AppColors.card,
                icon: const Icon(Iconsax.arrow_down_1, color: AppColors.textMuted),
                items: brandsState.brands.map((brand) {
                  return DropdownMenuItem<int>(
                    value: brand.id,
                    child: Row(
                      children: [
                        Container(
                          width: 32,
                          height: 32,
                          decoration: BoxDecoration(
                            gradient: AppColors.primaryGradient,
                            borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                          ),
                          child: Center(
                            child: Text(
                              brand.name.substring(0, 1),
                              style: const TextStyle(
                                fontWeight: FontWeight.bold,
                                color: Colors.white,
                              ),
                            ),
                          ),
                        ),
                        const SizedBox(width: 12),
                        Text(brand.name),
                      ],
                    ),
                  );
                }).toList(),
                onChanged: (value) {
                  setState(() => _selectedBrandId = value);
                },
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildPlatformSelector() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
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
                'แพลตฟอร์ม',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textSecondary,
                ),
              ),
              Text(
                '${_selectedPlatforms.length} เลือกแล้ว',
                style: const TextStyle(
                  fontSize: 12,
                  color: AppColors.primary,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spacingMd),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: Platforms.all.entries.map((entry) {
              final isSelected = _selectedPlatforms.contains(entry.key);
              return _buildPlatformChip(
                entry.key,
                entry.value.name,
                entry.value.color,
                isSelected,
              );
            }).toList(),
          ),
        ],
      ),
    );
  }

  Widget _buildPlatformChip(String key, String name, Color color, bool isSelected) {
    return InkWell(
      onTap: () {
        setState(() {
          if (isSelected) {
            _selectedPlatforms.remove(key);
          } else {
            _selectedPlatforms.add(key);
          }
        });
      },
      borderRadius: BorderRadius.circular(AppConstants.radiusFull),
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        decoration: BoxDecoration(
          color: isSelected ? color.withValues(alpha:0.15) : AppColors.surface,
          borderRadius: BorderRadius.circular(AppConstants.radiusFull),
          border: Border.all(
            color: isSelected ? color : AppColors.border,
            width: isSelected ? 2 : 1,
          ),
        ),
        child: Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              _getPlatformIcon(key),
              size: 16,
              color: isSelected ? color : AppColors.textMuted,
            ),
            const SizedBox(width: 6),
            Text(
              name,
              style: TextStyle(
                fontSize: 13,
                fontWeight: isSelected ? FontWeight.w600 : FontWeight.w400,
                color: isSelected ? color : AppColors.textSecondary,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildAIGenerateSection() {
    final createState = ref.watch(createPostNotifierProvider);
    final isGenerating = createState.isGenerating;

    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [
            AppColors.secondary.withValues(alpha:0.15),
            AppColors.primary.withValues(alpha:0.15),
          ],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.secondary.withValues(alpha:0.3)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.all(8),
                decoration: BoxDecoration(
                  color: AppColors.secondary.withValues(alpha:0.2),
                  borderRadius: BorderRadius.circular(AppConstants.radiusSm),
                ),
                child: const Icon(
                  Iconsax.magic_star,
                  color: AppColors.secondary,
                  size: 20,
                ),
              ),
              const SizedBox(width: 12),
              const Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'AI Content Generator',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    Text(
                      'สร้างเนื้อหาอัตโนมัติด้วย AI',
                      style: TextStyle(
                        fontSize: 12,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spacingMd),
          SizedBox(
            width: double.infinity,
            child: ElevatedButton.icon(
              onPressed: isGenerating ? null : _showAIGenerator,
              style: ElevatedButton.styleFrom(
                backgroundColor: AppColors.secondary,
                padding: const EdgeInsets.symmetric(vertical: 12),
              ),
              icon: isGenerating
                  ? const SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(
                        strokeWidth: 2,
                        color: Colors.white,
                      ),
                    )
                  : const Icon(Iconsax.flash_1),
              label: Text(isGenerating ? 'กำลังสร้าง...' : 'สร้างเนื้อหาด้วย AI'),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildContentInput() {
    final charCount = _contentController.text.length;
    final maxChars = _getMinMaxChars();

    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
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
                'เนื้อหาโพสต์',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textSecondary,
                ),
              ),
              Text(
                '$charCount / $maxChars',
                style: TextStyle(
                  fontSize: 12,
                  color: charCount > maxChars ? AppColors.error : AppColors.textMuted,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spacingSm),
          TextField(
            controller: _contentController,
            maxLines: 8,
            onChanged: (value) => setState(() {}),
            decoration: InputDecoration(
              hintText: 'เขียนเนื้อหาโพสต์ของคุณที่นี่...',
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                borderSide: BorderSide.none,
              ),
              filled: true,
              fillColor: AppColors.inputBackground,
            ),
          ),
          const SizedBox(height: AppConstants.spacingSm),
          Row(
            children: [
              _buildContentToolButton(Iconsax.emoji_happy, 'อีโมจิ'),
              _buildContentToolButton(Iconsax.link, 'ลิงก์'),
              _buildContentToolButton(Iconsax.location, 'สถานที่'),
              _buildContentToolButton(Iconsax.user_tag, 'แท็ก'),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildContentToolButton(IconData icon, String tooltip) {
    return Tooltip(
      message: tooltip,
      child: IconButton(
        icon: Icon(icon, size: 20, color: AppColors.textMuted),
        onPressed: () {
          // TODO: Implement tool action
        },
      ),
    );
  }

  Widget _buildMediaUpload() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'สื่อ',
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w500,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          if (_mediaFiles.isEmpty)
            InkWell(
              onTap: _pickMedia,
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
              child: Container(
                height: 120,
                width: double.infinity,
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                  border: Border.all(
                    color: AppColors.border,
                    style: BorderStyle.solid,
                  ),
                ),
                child: const Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Iconsax.gallery_add, size: 32, color: AppColors.textMuted),
                    SizedBox(height: 8),
                    Text(
                      'แตะเพื่อเพิ่มรูปภาพหรือวิดีโอ',
                      style: TextStyle(
                        fontSize: 13,
                        color: AppColors.textMuted,
                      ),
                    ),
                    SizedBox(height: 4),
                    Text(
                      'รองรับ JPG, PNG, MP4 (สูงสุด 10 ไฟล์)',
                      style: TextStyle(
                        fontSize: 11,
                        color: AppColors.textDisabled,
                      ),
                    ),
                  ],
                ),
              ),
            )
          else
            SizedBox(
              height: 100,
              child: ListView.builder(
                scrollDirection: Axis.horizontal,
                itemCount: _mediaFiles.length + 1,
                itemBuilder: (context, index) {
                  if (index == _mediaFiles.length) {
                    return InkWell(
                      onTap: _pickMedia,
                      child: Container(
                        width: 100,
                        margin: const EdgeInsets.only(right: 8),
                        decoration: BoxDecoration(
                          color: AppColors.surface,
                          borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                          border: Border.all(color: AppColors.border),
                        ),
                        child: const Icon(
                          Iconsax.add,
                          color: AppColors.textMuted,
                        ),
                      ),
                    );
                  }
                  return Container(
                    width: 100,
                    margin: const EdgeInsets.only(right: 8),
                    decoration: BoxDecoration(
                      color: AppColors.surface,
                      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                    ),
                    // TODO: Show media preview
                  );
                },
              ),
            ),
          const SizedBox(height: AppConstants.spacingSm),
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _pickMedia,
                  icon: const Icon(Iconsax.gallery, size: 18),
                  label: const Text('รูปภาพ'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _pickMedia,
                  icon: const Icon(Iconsax.video, size: 18),
                  label: const Text('วิดีโอ'),
                ),
              ),
              const SizedBox(width: 8),
              Expanded(
                child: OutlinedButton.icon(
                  onPressed: _generateAIImage,
                  icon: const Icon(Iconsax.magic_star, size: 18),
                  label: const Text('AI รูป'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildHashtagsInput() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
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
                'แฮชแท็ก',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textSecondary,
                ),
              ),
              TextButton.icon(
                onPressed: _generateHashtags,
                icon: const Icon(Iconsax.magic_star, size: 16),
                label: const Text('สร้างอัตโนมัติ'),
                style: TextButton.styleFrom(
                  foregroundColor: AppColors.secondary,
                  padding: EdgeInsets.zero,
                  minimumSize: Size.zero,
                  tapTargetSize: MaterialTapTargetSize.shrinkWrap,
                ),
              ),
            ],
          ),
          const SizedBox(height: AppConstants.spacingSm),
          TextField(
            controller: _hashtagsController,
            decoration: InputDecoration(
              hintText: '#โปรโมชั่น #ลดราคา #ออนไลน์',
              prefixIcon: const Icon(Iconsax.hashtag, size: 20),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                borderSide: BorderSide.none,
              ),
              filled: true,
              fillColor: AppColors.inputBackground,
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildScheduleSection() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Row(
                children: [
                  Icon(Iconsax.calendar_1, size: 20, color: AppColors.textSecondary),
                  SizedBox(width: 8),
                  Text(
                    'ตั้งเวลาโพสต์',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w500,
                      color: AppColors.textPrimary,
                    ),
                  ),
                ],
              ),
              Switch(
                value: _isScheduled,
                onChanged: (value) {
                  setState(() => _isScheduled = value);
                  if (value && _scheduledDate == null) {
                    _pickScheduleDateTime();
                  }
                },
              ),
            ],
          ),
          if (_isScheduled) ...[
            const SizedBox(height: AppConstants.spacingMd),
            InkWell(
              onTap: _pickScheduleDateTime,
              child: Container(
                padding: const EdgeInsets.all(AppConstants.spacingMd),
                decoration: BoxDecoration(
                  color: AppColors.inputBackground,
                  borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                  border: Border.all(color: AppColors.border),
                ),
                child: Row(
                  children: [
                    const Icon(Iconsax.clock, color: AppColors.primary, size: 20),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Text(
                        _scheduledDate != null && _scheduledTime != null
                            ? _formatScheduleDateTime()
                            : 'เลือกวันและเวลา',
                        style: TextStyle(
                          fontSize: 14,
                          color: _scheduledDate != null
                              ? AppColors.textPrimary
                              : AppColors.textMuted,
                        ),
                      ),
                    ),
                    const Icon(Iconsax.arrow_right_3, color: AppColors.textMuted, size: 18),
                  ],
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildBottomActions() {
    final createState = ref.watch(createPostNotifierProvider);
    final isLoading = createState.isLoading;

    return Container(
      padding: EdgeInsets.only(
        left: AppConstants.spacingMd,
        right: AppConstants.spacingMd,
        bottom: MediaQuery.of(context).padding.bottom + AppConstants.spacingMd,
        top: AppConstants.spacingSm,
      ),
      decoration: const BoxDecoration(
        color: AppColors.backgroundSecondary,
        border: Border(top: BorderSide(color: AppColors.border)),
      ),
      child: Row(
        children: [
          Expanded(
            child: CustomButton(
              onPressed: _previewPost,
              label: 'ดูตัวอย่าง',
              icon: Iconsax.eye,
              isOutlined: true,
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            flex: 2,
            child: CustomButton(
              onPressed: isLoading || _selectedPlatforms.isEmpty ? null : _publishPost,
              label: _isScheduled ? 'ตั้งเวลาโพสต์' : 'โพสต์เลย',
              icon: _isScheduled ? Iconsax.calendar_tick : Iconsax.send_1,
              isLoading: isLoading,
            ),
          ),
        ],
      ),
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
      case 'threads':
        return Iconsax.text;
      case 'linkedin':
        return Iconsax.briefcase;
      case 'pinterest':
        return Iconsax.gallery;
      default:
        return Iconsax.global;
    }
  }

  int _getMinMaxChars() {
    if (_selectedPlatforms.isEmpty) return 2200;
    return _selectedPlatforms
        .map((p) => Platforms.all[p]?.maxCharacters ?? 2200)
        .reduce((a, b) => a < b ? a : b);
  }

  String _formatScheduleDateTime() {
    if (_scheduledDate == null || _scheduledTime == null) return '';
    final date = _scheduledDate!;
    final time = _scheduledTime!;
    return '${date.day}/${date.month}/${date.year} เวลา ${time.hour.toString().padLeft(2, '0')}:${time.minute.toString().padLeft(2, '0')}';
  }

  void _showAIGenerator() {
    showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => _AIGeneratorSheet(
        onGenerate: (content) {
          setState(() {
            _contentController.text = content;
          });
        },
      ),
    );
  }

  void _pickMedia() {
    // TODO: Implement image/video picker
  }

  void _generateAIImage() {
    // TODO: Implement AI image generation
  }

  void _generateHashtags() {
    // TODO: Generate hashtags with AI
    setState(() {
      _hashtagsController.text = '#โปรโมชั่น #ลดราคา #ออนไลน์ #ช้อปสนุก #ส่งฟรี';
    });
  }

  void _pickScheduleDateTime() async {
    final date = await showDatePicker(
      context: context,
      initialDate: _scheduledDate ?? DateTime.now().add(const Duration(hours: 1)),
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );
    if (date != null && mounted) {
      final time = await showTimePicker(
        context: context,
        initialTime: _scheduledTime ?? TimeOfDay.now(),
      );
      if (time != null) {
        setState(() {
          _scheduledDate = date;
          _scheduledTime = time;
        });
      }
    }
  }

  void _saveDraft() {
    // TODO: Save as draft
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('บันทึกแบบร่างแล้ว'),
        backgroundColor: AppColors.success,
      ),
    );
  }

  void _previewPost() {
    // TODO: Show preview
  }

  Future<void> _publishPost() async {
    if (_contentController.text.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('กรุณากรอกเนื้อหาโพสต์'),
          backgroundColor: AppColors.error,
        ),
      );
      return;
    }

    // Parse hashtags
    final hashtags = _hashtagsController.text
        .split(RegExp(r'[,\s]+'))
        .map((h) => h.replaceAll('#', '').trim())
        .where((h) => h.isNotEmpty)
        .toList();

    // Build scheduled datetime
    DateTime? scheduledAt;
    if (_isScheduled && _scheduledDate != null && _scheduledTime != null) {
      scheduledAt = DateTime(
        _scheduledDate!.year,
        _scheduledDate!.month,
        _scheduledDate!.day,
        _scheduledTime!.hour,
        _scheduledTime!.minute,
      );
    }

    final request = CreatePostRequest(
      brandId: _selectedBrandId,
      contentText: _contentController.text.trim(),
      contentType: _mediaFiles.isEmpty ? 'text' : 'image',
      mediaUrls: _mediaFiles.isEmpty ? null : _mediaFiles,
      hashtags: hashtags.isEmpty ? null : hashtags,
      platforms: _selectedPlatforms,
      scheduledAt: scheduledAt,
      publishNow: !_isScheduled,
    );

    final result = await ref
        .read(createPostNotifierProvider.notifier)
        .createPost(request);

    if (result != null) {
      // Refresh posts list
      ref.read(postsNotifierProvider.notifier).loadPosts(refresh: true);

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(_isScheduled ? 'ตั้งเวลาโพสต์สำเร็จ!' : 'โพสต์สำเร็จ!'),
            backgroundColor: AppColors.success,
          ),
        );
        context.pop();
      }
    } else {
      final error = ref.read(createPostNotifierProvider).error;
      if (mounted && error != null) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(error),
            backgroundColor: AppColors.error,
          ),
        );
      }
    }
  }
}

// AI Generator Bottom Sheet
class _AIGeneratorSheet extends StatefulWidget {
  final Function(String) onGenerate;

  const _AIGeneratorSheet({required this.onGenerate});

  @override
  State<_AIGeneratorSheet> createState() => _AIGeneratorSheetState();
}

class _AIGeneratorSheetState extends State<_AIGeneratorSheet> {
  final _promptController = TextEditingController();
  String _selectedTone = 'casual';
  String _selectedLength = 'medium';
  bool _isGenerating = false;
  String? _generatedContent;

  final List<Map<String, String>> _tones = [
    {'key': 'casual', 'label': 'เป็นกันเอง'},
    {'key': 'professional', 'label': 'มืออาชีพ'},
    {'key': 'funny', 'label': 'ตลกขำขัน'},
    {'key': 'urgent', 'label': 'เร่งด่วน'},
    {'key': 'inspiring', 'label': 'สร้างแรงบันดาลใจ'},
  ];

  final List<Map<String, String>> _lengths = [
    {'key': 'short', 'label': 'สั้น'},
    {'key': 'medium', 'label': 'ปานกลาง'},
    {'key': 'long', 'label': 'ยาว'},
  ];

  @override
  void dispose() {
    _promptController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Container(
      height: MediaQuery.of(context).size.height * 0.85,
      decoration: const BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppConstants.radiusXl),
        ),
      ),
      child: Column(
        children: [
          // Handle
          Container(
            margin: const EdgeInsets.only(top: 12),
            width: 40,
            height: 4,
            decoration: BoxDecoration(
              color: AppColors.border,
              borderRadius: BorderRadius.circular(2),
            ),
          ),

          // Header
          Padding(
            padding: const EdgeInsets.all(AppConstants.spacingMd),
            child: Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    gradient: AppColors.primaryGradient,
                    borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                  ),
                  child: const Icon(
                    Iconsax.magic_star,
                    color: Colors.white,
                    size: 24,
                  ),
                ),
                const SizedBox(width: 12),
                const Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'AI Content Generator',
                        style: TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.bold,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      Text(
                        'สร้างเนื้อหาด้วย AI อัจฉริยะ',
                        style: TextStyle(
                          fontSize: 13,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
                IconButton(
                  icon: const Icon(Iconsax.close_circle, color: AppColors.textMuted),
                  onPressed: () => Navigator.pop(context),
                ),
              ],
            ),
          ),

          const Divider(color: AppColors.border, height: 1),

          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppConstants.spacingMd),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Prompt Input
                  const Text(
                    'บอก AI ว่าคุณต้องการเนื้อหาแบบไหน',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w500,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: AppConstants.spacingSm),
                  TextField(
                    controller: _promptController,
                    maxLines: 4,
                    decoration: InputDecoration(
                      hintText: 'เช่น: โปรโมทสินค้าลดราคา 50% ช่วงปีใหม่ เน้นความคุ้มค่า',
                      border: OutlineInputBorder(
                        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                        borderSide: BorderSide.none,
                      ),
                      filled: true,
                      fillColor: AppColors.inputBackground,
                    ),
                  ),
                  const SizedBox(height: AppConstants.spacingMd),

                  // Tone Selection
                  const Text(
                    'โทนเสียง',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w500,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: AppConstants.spacingSm),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: _tones.map((tone) {
                      final isSelected = _selectedTone == tone['key'];
                      return ChoiceChip(
                        label: Text(tone['label']!),
                        selected: isSelected,
                        onSelected: (selected) {
                          setState(() => _selectedTone = tone['key']!);
                        },
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: AppConstants.spacingMd),

                  // Length Selection
                  const Text(
                    'ความยาว',
                    style: TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w500,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: AppConstants.spacingSm),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: _lengths.map((length) {
                      final isSelected = _selectedLength == length['key'];
                      return ChoiceChip(
                        label: Text(length['label']!),
                        selected: isSelected,
                        onSelected: (selected) {
                          setState(() => _selectedLength = length['key']!);
                        },
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: AppConstants.spacingLg),

                  // Generate Button
                  SizedBox(
                    width: double.infinity,
                    child: ElevatedButton.icon(
                      onPressed: _isGenerating ? null : _generate,
                      style: ElevatedButton.styleFrom(
                        backgroundColor: AppColors.primary,
                        padding: const EdgeInsets.symmetric(vertical: 14),
                      ),
                      icon: _isGenerating
                          ? const SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(
                                strokeWidth: 2,
                                color: Colors.white,
                              ),
                            )
                          : const Icon(Iconsax.magic_star),
                      label: Text(_isGenerating ? 'กำลังสร้าง...' : 'สร้างเนื้อหา'),
                    ),
                  ),

                  // Generated Content
                  if (_generatedContent != null) ...[
                    const SizedBox(height: AppConstants.spacingLg),
                    Container(
                      padding: const EdgeInsets.all(AppConstants.spacingMd),
                      decoration: BoxDecoration(
                        color: AppColors.success.withValues(alpha:0.1),
                        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
                        border: Border.all(color: AppColors.success.withValues(alpha:0.3)),
                      ),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              const Icon(
                                Iconsax.tick_circle,
                                color: AppColors.success,
                                size: 20,
                              ),
                              const SizedBox(width: 8),
                              const Text(
                                'เนื้อหาที่สร้างขึ้น',
                                style: TextStyle(
                                  fontSize: 14,
                                  fontWeight: FontWeight.w600,
                                  color: AppColors.success,
                                ),
                              ),
                              const Spacer(),
                              IconButton(
                                icon: const Icon(Iconsax.refresh, size: 20),
                                color: AppColors.textMuted,
                                onPressed: _generate,
                                tooltip: 'สร้างใหม่',
                              ),
                            ],
                          ),
                          const SizedBox(height: AppConstants.spacingSm),
                          Text(
                            _generatedContent!,
                            style: const TextStyle(
                              fontSize: 14,
                              color: AppColors.textPrimary,
                              height: 1.6,
                            ),
                          ),
                          const SizedBox(height: AppConstants.spacingMd),
                          SizedBox(
                            width: double.infinity,
                            child: ElevatedButton.icon(
                              onPressed: () {
                                widget.onGenerate(_generatedContent!);
                                Navigator.pop(context);
                              },
                              style: ElevatedButton.styleFrom(
                                backgroundColor: AppColors.success,
                              ),
                              icon: const Icon(Iconsax.tick_circle),
                              label: const Text('ใช้เนื้อหานี้'),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  void _generate() async {
    if (_promptController.text.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('กรุณากรอกคำอธิบายสิ่งที่ต้องการ'),
          backgroundColor: AppColors.error,
        ),
      );
      return;
    }

    setState(() {
      _isGenerating = true;
      _generatedContent = null;
    });

    try {
      // TODO: Call AI API
      await Future.delayed(const Duration(seconds: 2));

      setState(() {
        _generatedContent = '''โปรโมชั่นสุดพิเศษต้อนรับปีใหม่! 🎉

ลดราคาสินค้าทุกชิ้นสูงสุด 50% เฉพาะช่วงวันที่ 25-31 ธันวาคม 2024 นี้เท่านั้น!

✨ สิทธิพิเศษสำหรับลูกค้า:
• ลด 50% ทุกชิ้น
• ส่งฟรีทั่วไทย
• แลกพอยต์ได้ 2 เท่า

อย่าพลาด! สั่งซื้อเลยวันนี้ 🛒

#โปรโมชั่น #ลดราคา #ปีใหม่2025 #ช้อปสนุก''';
      });
    } catch (e) {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('เกิดข้อผิดพลาด: $e'),
            backgroundColor: AppColors.error,
          ),
        );
      }
    } finally {
      if (mounted) {
        setState(() => _isGenerating = false);
      }
    }
  }
}
