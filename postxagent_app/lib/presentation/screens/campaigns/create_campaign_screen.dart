import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';

import '../../../core/constants/app_constants.dart';
import '../../../data/models/campaign.dart';
import '../../../providers/brand_provider.dart';
import '../../../providers/campaign_provider.dart';
import '../../widgets/common/custom_button.dart';
import '../../widgets/common/custom_text_field.dart';

class CreateCampaignScreen extends ConsumerStatefulWidget {
  final int? campaignId; // null = create, not null = edit

  const CreateCampaignScreen({super.key, this.campaignId});

  @override
  ConsumerState<CreateCampaignScreen> createState() => _CreateCampaignScreenState();
}

class _CreateCampaignScreenState extends ConsumerState<CreateCampaignScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _budgetController = TextEditingController();

  int? _selectedBrandId;
  String? _selectedType;
  String? _selectedGoal;
  final List<String> _selectedPlatforms = ['facebook', 'instagram'];
  DateTime? _startDate;
  DateTime? _endDate;

  bool get isEditing => widget.campaignId != null;

  @override
  void initState() {
    super.initState();
    // Load brands for dropdown
    WidgetsBinding.instance.addPostFrameCallback((_) {
      ref.read(brandsNotifierProvider.notifier).loadBrands();
      if (isEditing) {
        _loadCampaign();
      }
    });
  }

  Future<void> _loadCampaign() async {
    await ref.read(campaignDetailNotifierProvider.notifier).loadCampaign(widget.campaignId!);
    final campaign = ref.read(campaignDetailNotifierProvider).campaign;
    if (campaign != null) {
      _nameController.text = campaign.name;
      _descriptionController.text = campaign.description ?? '';
      _budgetController.text = campaign.budget?.toString() ?? '';
      setState(() {
        _selectedBrandId = campaign.brandId;
        _selectedType = campaign.type;
        _selectedGoal = campaign.goal;
        _selectedPlatforms.clear();
        if (campaign.targetPlatforms != null) {
          _selectedPlatforms.addAll(campaign.targetPlatforms!);
        }
        _startDate = campaign.startDate;
        _endDate = campaign.endDate;
      });
    }
  }

  @override
  void dispose() {
    _nameController.dispose();
    _descriptionController.dispose();
    _budgetController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final brandsState = ref.watch(brandsNotifierProvider);
    final detailState = ref.watch(campaignDetailNotifierProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(isEditing ? 'แก้ไขแคมเปญ' : 'สร้างแคมเปญใหม่'),
      ),
      body: detailState.isLoading && isEditing
          ? const Center(child: CircularProgressIndicator())
          : Form(
        key: _formKey,
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(AppConstants.spacingMd),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              // Campaign Name
              const Text(
                'ชื่อแคมเปญ *',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: AppConstants.spacingSm),
              CustomTextField(
                controller: _nameController,
                hint: 'เช่น โปรโมชั่นปีใหม่ 2025',
                prefixIcon: Iconsax.chart_2,
                validator: (value) {
                  if (value == null || value.isEmpty) {
                    return 'กรุณากรอกชื่อแคมเปญ';
                  }
                  return null;
                },
              ),
              const SizedBox(height: AppConstants.spacingMd),

              // Brand
              const Text(
                'แบรนด์',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textPrimary,
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
                        child: Text(brand.name),
                      );
                    }).toList(),
                    onChanged: (value) {
                      setState(() => _selectedBrandId = value);
                    },
                  ),
                ),
              ),
              const SizedBox(height: AppConstants.spacingMd),

              // Campaign Type
              const Text(
                'ประเภทแคมเปญ',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textPrimary,
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
                  child: DropdownButton<String>(
                    value: _selectedType,
                    isExpanded: true,
                    hint: const Text(
                      'เลือกประเภทแคมเปญ',
                      style: TextStyle(color: AppColors.textMuted),
                    ),
                    dropdownColor: AppColors.card,
                    icon: const Icon(Iconsax.arrow_down_1, color: AppColors.textMuted),
                    items: CampaignTypes.all.entries.map((entry) {
                      return DropdownMenuItem<String>(
                        value: entry.key,
                        child: Text(entry.value),
                      );
                    }).toList(),
                    onChanged: (value) {
                      setState(() => _selectedType = value);
                    },
                  ),
                ),
              ),
              const SizedBox(height: AppConstants.spacingMd),

              // Campaign Goal
              const Text(
                'เป้าหมาย',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textPrimary,
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
                  child: DropdownButton<String>(
                    value: _selectedGoal,
                    isExpanded: true,
                    hint: const Text(
                      'เลือกเป้าหมาย',
                      style: TextStyle(color: AppColors.textMuted),
                    ),
                    dropdownColor: AppColors.card,
                    icon: const Icon(Iconsax.arrow_down_1, color: AppColors.textMuted),
                    items: CampaignGoals.all.entries.map((entry) {
                      return DropdownMenuItem<String>(
                        value: entry.key,
                        child: Text(entry.value),
                      );
                    }).toList(),
                    onChanged: (value) {
                      setState(() => _selectedGoal = value);
                    },
                  ),
                ),
              ),
              const SizedBox(height: AppConstants.spacingMd),

              // Description
              const Text(
                'รายละเอียด',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: AppConstants.spacingSm),
              CustomTextField(
                controller: _descriptionController,
                hint: 'อธิบายเกี่ยวกับแคมเปญ...',
                maxLines: 4,
              ),
              const SizedBox(height: AppConstants.spacingMd),

              // Platform Selector
              _buildPlatformSelector(),
              const SizedBox(height: AppConstants.spacingMd),

              // Date Range
              _buildDateRangeSelector(),
              const SizedBox(height: AppConstants.spacingLg),

              // AI Content Settings
              _buildAISettingsCard(),
              const SizedBox(height: AppConstants.spacingXl),

              // Error message
              if (detailState.error != null)
                Container(
                  padding: const EdgeInsets.all(AppConstants.spacingMd),
                  margin: const EdgeInsets.only(bottom: AppConstants.spacingMd),
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
                          detailState.error!,
                          style: const TextStyle(color: AppColors.error),
                        ),
                      ),
                    ],
                  ),
                ),

              // Submit Buttons
              Row(
                children: [
                  Expanded(
                    child: CustomButton(
                      onPressed: () => context.pop(),
                      label: 'ยกเลิก',
                      isOutlined: true,
                    ),
                  ),
                  const SizedBox(width: AppConstants.spacingMd),
                  Expanded(
                    flex: 2,
                    child: CustomButton(
                      onPressed: detailState.isSaving ? null : _saveCampaign,
                      label: isEditing ? 'บันทึก' : 'สร้างแคมเปญ',
                      icon: isEditing ? Iconsax.tick_circle : Iconsax.add,
                      isLoading: detailState.isSaving,
                    ),
                  ),
                ],
              ),
              SizedBox(height: MediaQuery.of(context).padding.bottom + AppConstants.spacingMd),
            ],
          ),
        ),
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
                  color: AppColors.textPrimary,
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

  Widget _buildDateRangeSelector() {
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
            'ช่วงเวลาแคมเปญ',
            style: TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w500,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          Row(
            children: [
              Expanded(
                child: InkWell(
                  onTap: () => _pickDate(true),
                  child: Container(
                    padding: const EdgeInsets.all(AppConstants.spacingMd),
                    decoration: BoxDecoration(
                      color: AppColors.inputBackground,
                      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                      border: Border.all(color: AppColors.border),
                    ),
                    child: Row(
                      children: [
                        const Icon(Iconsax.calendar_1, size: 18, color: AppColors.textMuted),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            _startDate != null
                                ? '${_startDate!.day}/${_startDate!.month}/${_startDate!.year}'
                                : 'วันเริ่มต้น',
                            style: TextStyle(
                              fontSize: 13,
                              color: _startDate != null
                                  ? AppColors.textPrimary
                                  : AppColors.textMuted,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 8),
                child: Icon(Iconsax.arrow_right_3, size: 16, color: AppColors.textMuted),
              ),
              Expanded(
                child: InkWell(
                  onTap: () => _pickDate(false),
                  child: Container(
                    padding: const EdgeInsets.all(AppConstants.spacingMd),
                    decoration: BoxDecoration(
                      color: AppColors.inputBackground,
                      borderRadius: BorderRadius.circular(AppConstants.radiusMd),
                      border: Border.all(color: AppColors.border),
                    ),
                    child: Row(
                      children: [
                        const Icon(Iconsax.calendar_1, size: 18, color: AppColors.textMuted),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            _endDate != null
                                ? '${_endDate!.day}/${_endDate!.month}/${_endDate!.year}'
                                : 'วันสิ้นสุด',
                            style: TextStyle(
                              fontSize: 13,
                              color: _endDate != null
                                  ? AppColors.textPrimary
                                  : AppColors.textMuted,
                            ),
                          ),
                        ),
                      ],
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

  Widget _buildAISettingsCard() {
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
                      'AI Content Generation',
                      style: TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    Text(
                      'สร้างเนื้อหาอัตโนมัติตามตารางที่กำหนด',
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
          _buildSettingRow('โพสต์อัตโนมัติ', 'สร้างและโพสต์อัตโนมัติตามตารางเวลา'),
          _buildSettingRow('AI Hashtags', 'สร้าง hashtag อัตโนมัติ'),
          _buildSettingRow('Best Time to Post', 'โพสต์ในช่วงเวลาที่ดีที่สุด'),
        ],
      ),
    );
  }

  Widget _buildSettingRow(String title, String subtitle) {
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Row(
        children: [
          const Icon(Iconsax.tick_circle, size: 16, color: AppColors.success),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w500,
                    color: AppColors.textPrimary,
                  ),
                ),
                Text(
                  subtitle,
                  style: const TextStyle(
                    fontSize: 11,
                    color: AppColors.textMuted,
                  ),
                ),
              ],
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

  void _pickDate(bool isStart) async {
    final initialDate = isStart
        ? (_startDate ?? DateTime.now())
        : (_endDate ?? DateTime.now().add(const Duration(days: 7)));

    final date = await showDatePicker(
      context: context,
      initialDate: initialDate,
      firstDate: isStart ? DateTime.now() : (_startDate ?? DateTime.now()),
      lastDate: DateTime.now().add(const Duration(days: 365)),
    );

    if (date != null) {
      setState(() {
        if (isStart) {
          _startDate = date;
          if (_endDate != null && _endDate!.isBefore(date)) {
            _endDate = null;
          }
        } else {
          _endDate = date;
        }
      });
    }
  }

  Future<void> _saveCampaign() async {
    if (!_formKey.currentState!.validate()) return;

    if (_selectedPlatforms.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('กรุณาเลือกแพลตฟอร์มอย่างน้อย 1 รายการ'),
          backgroundColor: AppColors.error,
        ),
      );
      return;
    }

    // Parse budget
    double? budget;
    if (_budgetController.text.isNotEmpty) {
      budget = double.tryParse(_budgetController.text);
    }

    final request = CreateCampaignRequest(
      name: _nameController.text.trim(),
      brandId: _selectedBrandId,
      description: _descriptionController.text.trim().isEmpty
          ? null
          : _descriptionController.text.trim(),
      type: _selectedType,
      goal: _selectedGoal,
      targetPlatforms: _selectedPlatforms.isEmpty ? null : _selectedPlatforms,
      budget: budget,
      startDate: _startDate,
      endDate: _endDate,
    );

    Campaign? result;

    if (isEditing) {
      result = await ref
          .read(campaignDetailNotifierProvider.notifier)
          .updateCampaign(widget.campaignId!, request);
    } else {
      result = await ref
          .read(campaignDetailNotifierProvider.notifier)
          .createCampaign(request);
    }

    if (result != null) {
      // Refresh campaigns list
      ref.read(campaignsNotifierProvider.notifier).loadCampaigns(refresh: true);

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(isEditing ? 'บันทึกแคมเปญสำเร็จ!' : 'สร้างแคมเปญสำเร็จ!'),
            backgroundColor: AppColors.success,
          ),
        );
        context.pop();
      }
    }
  }
}
