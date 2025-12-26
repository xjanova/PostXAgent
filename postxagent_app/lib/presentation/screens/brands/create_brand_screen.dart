import 'dart:io';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';
import 'package:image_picker/image_picker.dart';

import '../../../core/constants/app_constants.dart';
import '../../../data/models/brand.dart';
import '../../../providers/brand_provider.dart';
import '../../widgets/common/custom_text_field.dart';
import '../../widgets/common/custom_button.dart';

class CreateBrandScreen extends ConsumerStatefulWidget {
  final int? brandId; // null = create, not null = edit

  const CreateBrandScreen({super.key, this.brandId});

  @override
  ConsumerState<CreateBrandScreen> createState() => _CreateBrandScreenState();
}

class _CreateBrandScreenState extends ConsumerState<CreateBrandScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _descriptionController = TextEditingController();
  final _websiteController = TextEditingController();
  final _keywordsController = TextEditingController();

  File? _logoFile;
  String? _selectedIndustry;
  String _selectedTone = 'friendly';
  bool _isActive = true;

  bool get isEditing => widget.brandId != null;

  @override
  void initState() {
    super.initState();
    if (isEditing) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        _loadBrand();
      });
    }
  }

  Future<void> _loadBrand() async {
    await ref.read(brandDetailNotifierProvider.notifier).loadBrand(widget.brandId!);
    final brand = ref.read(brandDetailNotifierProvider).brand;
    if (brand != null) {
      _nameController.text = brand.name;
      _descriptionController.text = brand.description ?? '';
      _websiteController.text = brand.websiteUrl ?? '';
      _keywordsController.text = brand.keywords?.join(', ') ?? '';
      setState(() {
        _selectedIndustry = brand.industry;
        _selectedTone = brand.tone ?? 'friendly';
        _isActive = brand.isActive;
      });
    }
  }

  @override
  void dispose() {
    _nameController.dispose();
    _descriptionController.dispose();
    _websiteController.dispose();
    _keywordsController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final detailState = ref.watch(brandDetailNotifierProvider);

    return Scaffold(
      appBar: AppBar(
        title: Text(isEditing ? 'แก้ไขแบรนด์' : 'สร้างแบรนด์ใหม่'),
        actions: [
          if (isEditing)
            IconButton(
              icon: Icon(
                _isActive ? Iconsax.eye : Iconsax.eye_slash,
                color: _isActive ? AppColors.success : AppColors.textMuted,
              ),
              onPressed: () {
                setState(() => _isActive = !_isActive);
              },
              tooltip: _isActive ? 'ใช้งานอยู่' : 'ไม่ใช้งาน',
            ),
        ],
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
                    // Logo Upload
                    Center(
                      child: GestureDetector(
                        onTap: _pickLogo,
                        child: Container(
                          width: 120,
                          height: 120,
                          decoration: BoxDecoration(
                            color: AppColors.surface,
                            borderRadius: BorderRadius.circular(AppConstants.radiusLg),
                            border: Border.all(
                              color: AppColors.border,
                              width: 2,
                            ),
                            image: _logoFile != null
                                ? DecorationImage(
                                    image: FileImage(_logoFile!),
                                    fit: BoxFit.cover,
                                  )
                                : null,
                          ),
                          child: _logoFile == null
                              ? const Column(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    Icon(
                                      Iconsax.gallery_add,
                                      size: 40,
                                      color: AppColors.textMuted,
                                    ),
                                    SizedBox(height: 8),
                                    Text(
                                      'อัพโหลดโลโก้',
                                      style: TextStyle(
                                        fontSize: 12,
                                        color: AppColors.textMuted,
                                      ),
                                    ),
                                  ],
                                )
                              : null,
                        ),
                      ),
                    ),
                    const SizedBox(height: AppConstants.spacingLg),

                    // Brand Name
                    const Text(
                      'ชื่อแบรนด์ *',
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w500,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: AppConstants.spacingSm),
                    CustomTextField(
                      controller: _nameController,
                      hint: 'เช่น ร้านขายของออนไลน์',
                      prefixIcon: Iconsax.shop,
                      validator: (value) {
                        if (value == null || value.isEmpty) {
                          return 'กรุณากรอกชื่อแบรนด์';
                        }
                        if (value.length < 2) {
                          return 'ชื่อแบรนด์ต้องมีอย่างน้อย 2 ตัวอักษร';
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: AppConstants.spacingMd),

                    // Industry
                    const Text(
                      'ประเภทธุรกิจ',
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
                          value: _selectedIndustry,
                          isExpanded: true,
                          hint: const Text(
                            'เลือกประเภทธุรกิจ',
                            style: TextStyle(color: AppColors.textMuted),
                          ),
                          dropdownColor: AppColors.card,
                          icon: const Icon(Iconsax.arrow_down_1, color: AppColors.textMuted),
                          items: BrandIndustries.all.entries.map((entry) {
                            return DropdownMenuItem(
                              value: entry.key,
                              child: Text(entry.value),
                            );
                          }).toList(),
                          onChanged: (value) {
                            setState(() {
                              _selectedIndustry = value;
                            });
                          },
                        ),
                      ),
                    ),
                    const SizedBox(height: AppConstants.spacingMd),

                    // Description
                    const Text(
                      'รายละเอียดแบรนด์',
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w500,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: AppConstants.spacingSm),
                    CustomTextField(
                      controller: _descriptionController,
                      hint: 'อธิบายเกี่ยวกับแบรนด์ของคุณ...',
                      maxLines: 4,
                    ),
                    const SizedBox(height: AppConstants.spacingMd),

                    // Website
                    const Text(
                      'เว็บไซต์',
                      style: TextStyle(
                        fontSize: 14,
                        fontWeight: FontWeight.w500,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: AppConstants.spacingSm),
                    CustomTextField(
                      controller: _websiteController,
                      hint: 'https://example.com',
                      prefixIcon: Iconsax.global,
                      keyboardType: TextInputType.url,
                      validator: (value) {
                        if (value != null && value.isNotEmpty) {
                          final uri = Uri.tryParse(value);
                          if (uri == null || !uri.hasScheme) {
                            return 'กรุณากรอก URL ที่ถูกต้อง';
                          }
                        }
                        return null;
                      },
                    ),
                    const SizedBox(height: AppConstants.spacingLg),

                    // AI Content Settings Section
                    Container(
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
                            children: [
                              Container(
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: AppColors.secondary.withValues(alpha: 0.15),
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
                                      'AI Content Settings',
                                      style: TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w600,
                                        color: AppColors.textPrimary,
                                      ),
                                    ),
                                    Text(
                                      'ตั้งค่าสำหรับการสร้างเนื้อหาด้วย AI',
                                      style: TextStyle(
                                        fontSize: 12,
                                        color: AppColors.textMuted,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ],
                          ),
                          const SizedBox(height: AppConstants.spacingMd),
                          const Text(
                            'โทนเสียงแบรนด์',
                            style: TextStyle(
                              fontSize: 14,
                              fontWeight: FontWeight.w500,
                              color: AppColors.textSecondary,
                            ),
                          ),
                          const SizedBox(height: AppConstants.spacingSm),
                          Wrap(
                            spacing: 8,
                            runSpacing: 8,
                            children: BrandTones.all.entries.map((entry) {
                              return _buildToneChip(entry.value, entry.key);
                            }).toList(),
                          ),
                          const SizedBox(height: AppConstants.spacingMd),
                          const Text(
                            'คำหลักของแบรนด์',
                            style: TextStyle(
                              fontSize: 14,
                              fontWeight: FontWeight.w500,
                              color: AppColors.textSecondary,
                            ),
                          ),
                          const SizedBox(height: AppConstants.spacingSm),
                          CustomTextField(
                            controller: _keywordsController,
                            hint: 'เพิ่มคำหลัก คั่นด้วย comma',
                            prefixIcon: Iconsax.tag,
                          ),
                        ],
                      ),
                    ),
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
                            onPressed: detailState.isSaving ? null : _saveBrand,
                            label: isEditing ? 'บันทึก' : 'สร้างแบรนด์',
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

  Widget _buildToneChip(String label, String value) {
    final isSelected = _selectedTone == value;
    return CustomChip(
      label: label,
      isSelected: isSelected,
      onTap: () {
        setState(() => _selectedTone = value);
      },
    );
  }

  Future<void> _pickLogo() async {
    final picker = ImagePicker();
    final image = await picker.pickImage(
      source: ImageSource.gallery,
      maxWidth: 512,
      maxHeight: 512,
    );

    if (image != null) {
      setState(() {
        _logoFile = File(image.path);
      });
    }
  }

  Future<void> _saveBrand() async {
    if (!_formKey.currentState!.validate()) return;

    // Parse keywords
    final keywords = _keywordsController.text
        .split(',')
        .map((e) => e.trim())
        .where((e) => e.isNotEmpty)
        .toList();

    final request = BrandRequest(
      name: _nameController.text.trim(),
      description: _descriptionController.text.trim().isEmpty
          ? null
          : _descriptionController.text.trim(),
      industry: _selectedIndustry,
      websiteUrl: _websiteController.text.trim().isEmpty
          ? null
          : _websiteController.text.trim(),
      tone: _selectedTone,
      keywords: keywords.isEmpty ? null : keywords,
    );

    Brand? result;

    if (isEditing) {
      result = await ref
          .read(brandDetailNotifierProvider.notifier)
          .updateBrand(widget.brandId!, request);
    } else {
      result = await ref
          .read(brandDetailNotifierProvider.notifier)
          .createBrand(request);
    }

    if (result != null) {
      // Upload logo if selected
      if (_logoFile != null) {
        await ref
            .read(brandDetailNotifierProvider.notifier)
            .uploadLogo(result.id, _logoFile!);
      }

      // Refresh brands list
      ref.read(brandsNotifierProvider.notifier).loadBrands(refresh: true);

      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(isEditing ? 'บันทึกแบรนด์สำเร็จ!' : 'สร้างแบรนด์สำเร็จ!'),
            backgroundColor: AppColors.success,
          ),
        );
        context.pop();
      }
    }
  }
}
