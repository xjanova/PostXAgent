import 'dart:async';
import 'dart:ui';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';
import 'package:iconsax/iconsax.dart';
import 'package:shimmer/shimmer.dart';
import 'package:cached_network_image/cached_network_image.dart';

import '../../../core/constants/app_constants.dart';
import '../../../data/models/brand.dart';
import '../../../providers/brand_provider.dart';

class BrandsScreen extends ConsumerStatefulWidget {
  const BrandsScreen({super.key});

  @override
  ConsumerState<BrandsScreen> createState() => _BrandsScreenState();
}

class _BrandsScreenState extends ConsumerState<BrandsScreen> {
  final _searchController = TextEditingController();
  final _scrollController = ScrollController();
  Timer? _searchDebounce;
  bool? _activeFilter;

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_onScroll);
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _loadBrands();
    });
  }

  @override
  void dispose() {
    _searchController.dispose();
    _scrollController.dispose();
    _searchDebounce?.cancel();
    super.dispose();
  }

  void _onScroll() {
    if (_scrollController.position.pixels >=
        _scrollController.position.maxScrollExtent - 200) {
      ref.read(brandsNotifierProvider.notifier).loadMore();
    }
  }

  Future<void> _loadBrands() async {
    await ref.read(brandsNotifierProvider.notifier).loadBrands(refresh: true);
  }

  void _onSearchChanged(String value) {
    _searchDebounce?.cancel();
    _searchDebounce = Timer(const Duration(milliseconds: 500), () {
      ref.read(brandsNotifierProvider.notifier).search(value);
    });
  }

  @override
  Widget build(BuildContext context) {
    final brandsState = ref.watch(brandsNotifierProvider);

    return Scaffold(
      appBar: AppBar(
        title: const Text('แบรนด์'),
        actions: [
          IconButton(
            icon: const Icon(Iconsax.filter),
            onPressed: () {
              _showFilterSheet(context);
            },
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _loadBrands,
        color: AppColors.primary,
        backgroundColor: AppColors.card,
        child: Column(
          children: [
            // Search bar
            Padding(
              padding: const EdgeInsets.all(AppConstants.spacingMd),
              child: TextField(
                controller: _searchController,
                decoration: InputDecoration(
                  hintText: 'ค้นหาแบรนด์...',
                  prefixIcon: const Icon(Iconsax.search_normal, size: 20),
                  suffixIcon: _searchController.text.isNotEmpty
                      ? IconButton(
                          icon: const Icon(Iconsax.close_circle, size: 20),
                          onPressed: () {
                            _searchController.clear();
                            ref.read(brandsNotifierProvider.notifier).search('');
                          },
                        )
                      : null,
                ),
                onChanged: _onSearchChanged,
              ),
            ),

            // Stats summary
            Padding(
              padding:
                  const EdgeInsets.symmetric(horizontal: AppConstants.spacingMd),
              child: Row(
                children: [
                  _buildSummaryChip(
                    'ทั้งหมด',
                    brandsState.brands.length.toString(),
                    AppColors.primary,
                  ),
                  const SizedBox(width: 8),
                  _buildSummaryChip(
                    'ใช้งาน',
                    brandsState.activeCount.toString(),
                    AppColors.success,
                  ),
                  const SizedBox(width: 8),
                  _buildSummaryChip(
                    'ไม่ใช้งาน',
                    brandsState.inactiveCount.toString(),
                    AppColors.textMuted,
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppConstants.spacingMd),

            // Brands list
            Expanded(
              child: _buildBody(brandsState),
            ),
          ],
        ),
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () {
          context.push('/brands/create');
        },
        icon: const Icon(Iconsax.add),
        label: const Text('สร้างแบรนด์'),
      ),
    );
  }

  Widget _buildBody(BrandsState state) {
    if (state.isLoading && state.brands.isEmpty) {
      return _buildShimmerList();
    }

    if (state.error != null && state.brands.isEmpty) {
      return _buildErrorState(state.error!);
    }

    if (state.brands.isEmpty) {
      return _buildEmptyState();
    }

    return ListView.builder(
      controller: _scrollController,
      padding: const EdgeInsets.symmetric(horizontal: AppConstants.spacingMd),
      itemCount: state.brands.length + (state.isLoadingMore ? 1 : 0),
      itemBuilder: (context, index) {
        if (index >= state.brands.length) {
          return _buildLoadingMoreIndicator();
        }
        return _buildBrandCard(state.brands[index]);
      },
    );
  }

  Widget _buildShimmerList() {
    return Shimmer.fromColors(
      baseColor: AppColors.shimmerBase,
      highlightColor: AppColors.shimmerHighlight,
      child: ListView.builder(
        padding: const EdgeInsets.symmetric(horizontal: AppConstants.spacingMd),
        itemCount: 4,
        itemBuilder: (context, index) {
          return Container(
            height: 140,
            margin: const EdgeInsets.only(bottom: AppConstants.spacingMd),
            decoration: BoxDecoration(
              color: AppColors.card,
              borderRadius: BorderRadius.circular(AppConstants.radiusLg),
            ),
          );
        },
      ),
    );
  }

  Widget _buildLoadingMoreIndicator() {
    return Container(
      padding: const EdgeInsets.all(AppConstants.spacingMd),
      alignment: Alignment.center,
      child: const SizedBox(
        width: 24,
        height: 24,
        child: CircularProgressIndicator(
          strokeWidth: 2,
          color: AppColors.primary,
        ),
      ),
    );
  }

  Widget _buildEmptyState() {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Container(
            width: 80,
            height: 80,
            decoration: BoxDecoration(
              color: AppColors.secondary.withValues(alpha: 0.1),
              borderRadius: BorderRadius.circular(40),
            ),
            child: const Icon(
              Iconsax.building_4,
              size: 40,
              color: AppColors.secondary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingMd),
          const Text(
            'ยังไม่มีแบรนด์',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingSm),
          const Text(
            'สร้างแบรนด์แรกของคุณเพื่อเริ่มต้นใช้งาน',
            style: TextStyle(
              fontSize: 14,
              color: AppColors.textSecondary,
            ),
          ),
          const SizedBox(height: AppConstants.spacingLg),
          ElevatedButton.icon(
            onPressed: () => context.push('/brands/create'),
            icon: const Icon(Iconsax.add),
            label: const Text('สร้างแบรนด์'),
          ),
        ],
      ),
    );
  }

  Widget _buildErrorState(String error) {
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(
            Iconsax.warning_2,
            size: 64,
            color: AppColors.error,
          ),
          const SizedBox(height: AppConstants.spacingMd),
          Text(
            error,
            style: const TextStyle(
              fontSize: 14,
              color: AppColors.textSecondary,
            ),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: AppConstants.spacingLg),
          ElevatedButton.icon(
            onPressed: _loadBrands,
            icon: const Icon(Iconsax.refresh),
            label: const Text('ลองใหม่'),
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

  Widget _buildBrandCard(Brand brand) {
    return Container(
      margin: const EdgeInsets.only(bottom: AppConstants.spacingMd),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        child: BackdropFilter(
          filter: ImageFilter.blur(sigmaX: 5, sigmaY: 5),
          child: Container(
            decoration: BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  AppColors.card,
                  AppColors.card.withValues(alpha: 0.9),
                ],
              ),
              borderRadius: BorderRadius.circular(AppConstants.radiusLg),
              border: Border.all(color: AppColors.border),
            ),
            child: InkWell(
              onTap: () {
                context.push('/brands/${brand.id}');
              },
              borderRadius: BorderRadius.circular(AppConstants.radiusLg),
              child: Padding(
                padding: const EdgeInsets.all(AppConstants.spacingMd),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      children: [
                        // Logo
                        _buildBrandLogo(brand),
                        const SizedBox(width: 12),
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Row(
                                children: [
                                  Expanded(
                                    child: Text(
                                      brand.name,
                                      style: const TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w600,
                                        color: AppColors.textPrimary,
                                      ),
                                    ),
                                  ),
                                  _buildStatusBadge(brand.isActive),
                                ],
                              ),
                              const SizedBox(height: 4),
                              if (brand.description != null)
                                Text(
                                  brand.description!,
                                  style: const TextStyle(
                                    fontSize: 13,
                                    color: AppColors.textSecondary,
                                  ),
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                ),
                              if (brand.industry != null)
                                Padding(
                                  padding: const EdgeInsets.only(top: 4),
                                  child: Text(
                                    BrandIndustries.all[brand.industry] ??
                                        brand.industry!,
                                    style: const TextStyle(
                                      fontSize: 12,
                                      color: AppColors.textMuted,
                                    ),
                                  ),
                                ),
                            ],
                          ),
                        ),
                        const Icon(
                          Iconsax.arrow_right_3,
                          color: AppColors.textMuted,
                          size: 20,
                        ),
                      ],
                    ),
                    const SizedBox(height: AppConstants.spacingMd),
                    const Divider(color: AppColors.border, height: 1),
                    const SizedBox(height: AppConstants.spacingMd),
                    Row(
                      children: [
                        _buildStatItem(
                          Iconsax.people,
                          '${brand.socialAccountsCount ?? 0} บัญชี',
                          AppColors.info,
                        ),
                        const SizedBox(width: AppConstants.spacingMd),
                        _buildStatItem(
                          Iconsax.document_text,
                          '${brand.postsCount ?? 0} โพสต์',
                          AppColors.success,
                        ),
                        const SizedBox(width: AppConstants.spacingMd),
                        _buildStatItem(
                          Iconsax.chart_2,
                          '${brand.campaignsCount ?? 0} แคมเปญ',
                          AppColors.secondary,
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

  Widget _buildBrandLogo(Brand brand) {
    if (brand.logoUrl != null && brand.logoUrl!.isNotEmpty) {
      return ClipRRect(
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        child: CachedNetworkImage(
          imageUrl: brand.logoUrl!,
          width: 56,
          height: 56,
          fit: BoxFit.cover,
          placeholder: (context, url) => Container(
            width: 56,
            height: 56,
            decoration: BoxDecoration(
              color: AppColors.shimmerBase,
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            ),
          ),
          errorWidget: (context, url, error) => _buildDefaultLogo(brand),
        ),
      );
    }
    return _buildDefaultLogo(brand);
  }

  Widget _buildDefaultLogo(Brand brand) {
    // Parse brand color or use default
    Color logoColor = AppColors.primary;
    if (brand.brandColors != null && brand.brandColors!.isNotEmpty) {
      try {
        final colorHex = brand.brandColors!.first.replaceAll('#', '');
        logoColor = Color(int.parse('FF$colorHex', radix: 16));
      } catch (_) {}
    }

    return Container(
      width: 56,
      height: 56,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            logoColor,
            logoColor.withValues(alpha: 0.7),
          ],
        ),
        borderRadius: BorderRadius.circular(AppConstants.radiusMd),
        boxShadow: [
          BoxShadow(
            color: logoColor.withValues(alpha: 0.3),
            blurRadius: 8,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      child: Center(
        child: Text(
          brand.name.isNotEmpty ? brand.name.substring(0, 1).toUpperCase() : 'B',
          style: const TextStyle(
            fontSize: 24,
            fontWeight: FontWeight.bold,
            color: Colors.white,
          ),
        ),
      ),
    );
  }

  Widget _buildStatusBadge(bool isActive) {
    return Container(
      padding: const EdgeInsets.symmetric(
        horizontal: 8,
        vertical: 2,
      ),
      decoration: BoxDecoration(
        color: isActive
            ? AppColors.success.withValues(alpha: 0.15)
            : AppColors.textMuted.withValues(alpha: 0.15),
        borderRadius: BorderRadius.circular(AppConstants.radiusFull),
      ),
      child: Text(
        isActive ? 'ใช้งาน' : 'ไม่ใช้งาน',
        style: TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.w500,
          color: isActive ? AppColors.success : AppColors.textMuted,
        ),
      ),
    );
  }

  Widget _buildStatItem(IconData icon, String label, Color color) {
    return Row(
      children: [
        Icon(icon, size: 16, color: color),
        const SizedBox(width: 4),
        Text(
          label,
          style: const TextStyle(
            fontSize: 12,
            color: AppColors.textSecondary,
          ),
        ),
      ],
    );
  }

  void _showFilterSheet(BuildContext context) {
    showModalBottomSheet(
      context: context,
      backgroundColor: AppColors.card,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(
          top: Radius.circular(AppConstants.radiusLg),
        ),
      ),
      builder: (context) => StatefulBuilder(
        builder: (context, setModalState) => Padding(
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
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text(
                    'กรองแบรนด์',
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  TextButton(
                    onPressed: () {
                      setModalState(() => _activeFilter = null);
                      ref
                          .read(brandsNotifierProvider.notifier)
                          .setActiveFilter(null);
                      Navigator.pop(context);
                    },
                    child: const Text('ล้างตัวกรอง'),
                  ),
                ],
              ),
              const SizedBox(height: AppConstants.spacingMd),
              const Text(
                'สถานะ',
                style: TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w500,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: AppConstants.spacingSm),
              Wrap(
                spacing: 8,
                children: [
                  ChoiceChip(
                    label: const Text('ทั้งหมด'),
                    selected: _activeFilter == null,
                    onSelected: (selected) {
                      if (selected) {
                        setModalState(() => _activeFilter = null);
                      }
                    },
                  ),
                  ChoiceChip(
                    label: const Text('ใช้งาน'),
                    selected: _activeFilter == true,
                    onSelected: (selected) {
                      if (selected) {
                        setModalState(() => _activeFilter = true);
                      }
                    },
                  ),
                  ChoiceChip(
                    label: const Text('ไม่ใช้งาน'),
                    selected: _activeFilter == false,
                    onSelected: (selected) {
                      if (selected) {
                        setModalState(() => _activeFilter = false);
                      }
                    },
                  ),
                ],
              ),
              const SizedBox(height: AppConstants.spacingLg),
              SizedBox(
                width: double.infinity,
                child: ElevatedButton(
                  onPressed: () {
                    ref
                        .read(brandsNotifierProvider.notifier)
                        .setActiveFilter(_activeFilter);
                    Navigator.pop(context);
                  },
                  child: const Text('ใช้ตัวกรอง'),
                ),
              ),
              SizedBox(height: MediaQuery.of(context).padding.bottom),
            ],
          ),
        ),
      ),
    );
  }
}
