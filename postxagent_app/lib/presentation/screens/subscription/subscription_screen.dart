import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:iconsax/iconsax.dart';

import '../../../core/constants/app_constants.dart';
import '../../widgets/common/custom_button.dart';

class SubscriptionScreen extends ConsumerStatefulWidget {
  const SubscriptionScreen({super.key});

  @override
  ConsumerState<SubscriptionScreen> createState() => _SubscriptionScreenState();
}

class _SubscriptionScreenState extends ConsumerState<SubscriptionScreen> {
  int _selectedPlanIndex = 1; // Pro plan

  // Sample current subscription
  final Map<String, dynamic> _currentPlan = {
    'name': 'Pro',
    'price': 990,
    'billing_cycle': 'monthly',
    'start_date': '2024-12-01',
    'end_date': '2025-01-01',
    'is_trial': false,
    'days_remaining': 10,
    'usage': {
      'posts_used': 145,
      'posts_limit': 500,
      'ai_generations_used': 89,
      'ai_generations_limit': 200,
      'brands_used': 3,
      'brands_limit': 5,
      'accounts_used': 8,
      'accounts_limit': 15,
    },
  };

  final List<Map<String, dynamic>> _plans = [
    {
      'name': 'Starter',
      'price': 0,
      'price_yearly': 0,
      'description': 'เริ่มต้นใช้งาน',
      'features': [
        '50 โพสต์/เดือน',
        '20 AI Generations/เดือน',
        '1 แบรนด์',
        '3 บัญชีโซเชียล',
        'แพลตฟอร์มพื้นฐาน',
      ],
      'is_popular': false,
    },
    {
      'name': 'Pro',
      'price': 990,
      'price_yearly': 9900,
      'description': 'สำหรับธุรกิจที่กำลังเติบโต',
      'features': [
        '500 โพสต์/เดือน',
        '200 AI Generations/เดือน',
        '5 แบรนด์',
        '15 บัญชีโซเชียล',
        'ทุกแพลตฟอร์ม',
        'Priority Support',
        'Analytics Dashboard',
      ],
      'is_popular': true,
    },
    {
      'name': 'Enterprise',
      'price': 2990,
      'price_yearly': 29900,
      'description': 'สำหรับองค์กรขนาดใหญ่',
      'features': [
        'โพสต์ไม่จำกัด',
        'AI Generations ไม่จำกัด',
        'แบรนด์ไม่จำกัด',
        'บัญชีโซเชียลไม่จำกัด',
        'ทุกแพลตฟอร์ม',
        '24/7 Priority Support',
        'Advanced Analytics',
        'Custom Integration',
        'Dedicated Account Manager',
      ],
      'is_popular': false,
    },
  ];

  @override
  Widget build(BuildContext context) {
    final usage = _currentPlan['usage'] as Map<String, dynamic>;
    final daysRemaining = _currentPlan['days_remaining'] as int;

    return Scaffold(
      appBar: AppBar(
        title: const Text('แพ็กเกจและการเช่า'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(AppConstants.spacingMd),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Current Plan Card
            Container(
              padding: const EdgeInsets.all(AppConstants.spacingMd),
              decoration: BoxDecoration(
                gradient: AppColors.primaryGradient,
                borderRadius: BorderRadius.circular(AppConstants.radiusLg),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: Colors.white.withValues(alpha:0.2),
                          borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                        ),
                        child: const Text(
                          'แพ็กเกจปัจจุบัน',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w500,
                            color: Colors.white,
                          ),
                        ),
                      ),
                      const Spacer(),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: daysRemaining <= 7
                              ? AppColors.warning
                              : Colors.white.withValues(alpha:0.2),
                          borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                        ),
                        child: Text(
                          'เหลืออีก $daysRemaining วัน',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w600,
                            color: daysRemaining <= 7 ? Colors.black : Colors.white,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: AppConstants.spacingMd),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text(
                        _currentPlan['name'],
                        style: const TextStyle(
                          fontSize: 28,
                          fontWeight: FontWeight.bold,
                          color: Colors.white,
                        ),
                      ),
                      const SizedBox(width: 8),
                      Padding(
                        padding: const EdgeInsets.only(bottom: 4),
                        child: Text(
                          '฿${_currentPlan['price']}/เดือน',
                          style: TextStyle(
                            fontSize: 14,
                            color: Colors.white.withValues(alpha:0.8),
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'หมดอายุ: ${_currentPlan['end_date']}',
                    style: TextStyle(
                      fontSize: 13,
                      color: Colors.white.withValues(alpha:0.8),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppConstants.spacingMd),

            // Usage Stats
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
                  const Text(
                    'การใช้งานเดือนนี้',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: AppConstants.spacingMd),
                  _buildUsageBar(
                    'โพสต์',
                    usage['posts_used'],
                    usage['posts_limit'],
                    AppColors.primary,
                  ),
                  const SizedBox(height: 12),
                  _buildUsageBar(
                    'AI Generations',
                    usage['ai_generations_used'],
                    usage['ai_generations_limit'],
                    AppColors.secondary,
                  ),
                  const SizedBox(height: 12),
                  _buildUsageBar(
                    'แบรนด์',
                    usage['brands_used'],
                    usage['brands_limit'],
                    AppColors.success,
                  ),
                  const SizedBox(height: 12),
                  _buildUsageBar(
                    'บัญชีโซเชียล',
                    usage['accounts_used'],
                    usage['accounts_limit'],
                    AppColors.info,
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppConstants.spacingLg),

            // Plans Section
            const Text(
              'เปลี่ยนแพ็กเกจ',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.w600,
                color: AppColors.textPrimary,
              ),
            ),
            const SizedBox(height: AppConstants.spacingMd),

            // Plans List
            ...List.generate(_plans.length, (index) {
              return _buildPlanCard(_plans[index], index);
            }),
            const SizedBox(height: AppConstants.spacingLg),

            // Payment History
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
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      const Text(
                        'ประวัติการชำระเงิน',
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w600,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      TextButton(
                        onPressed: () {},
                        child: const Text('ดูทั้งหมด'),
                      ),
                    ],
                  ),
                  const SizedBox(height: AppConstants.spacingSm),
                  _buildPaymentItem('1 ธ.ค. 2024', 'Pro - รายเดือน', 990, 'success'),
                  _buildPaymentItem('1 พ.ย. 2024', 'Pro - รายเดือน', 990, 'success'),
                  _buildPaymentItem('1 ต.ค. 2024', 'Starter - รายเดือน', 0, 'success'),
                ],
              ),
            ),
            const SizedBox(height: AppConstants.spacingXl),
          ],
        ),
      ),
    );
  }

  Widget _buildUsageBar(String label, int used, int limit, Color color) {
    final progress = limit > 0 ? used / limit : 0.0;
    final isWarning = progress > 0.8;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(
              label,
              style: const TextStyle(
                fontSize: 13,
                color: AppColors.textSecondary,
              ),
            ),
            Text(
              '$used / $limit',
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w500,
                color: isWarning ? AppColors.warning : AppColors.textPrimary,
              ),
            ),
          ],
        ),
        const SizedBox(height: 6),
        ClipRRect(
          borderRadius: BorderRadius.circular(AppConstants.radiusFull),
          child: LinearProgressIndicator(
            value: progress.clamp(0.0, 1.0),
            backgroundColor: AppColors.surface,
            valueColor: AlwaysStoppedAnimation(isWarning ? AppColors.warning : color),
            minHeight: 8,
          ),
        ),
      ],
    );
  }

  Widget _buildPlanCard(Map<String, dynamic> plan, int index) {
    final isSelected = _selectedPlanIndex == index;
    final isPopular = plan['is_popular'] as bool;
    final isCurrent = plan['name'] == _currentPlan['name'];

    return Container(
      margin: const EdgeInsets.only(bottom: AppConstants.spacingMd),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        border: Border.all(
          color: isSelected
              ? AppColors.primary
              : isPopular
                  ? AppColors.secondary.withValues(alpha:0.5)
                  : AppColors.border,
          width: isSelected ? 2 : 1,
        ),
      ),
      child: InkWell(
        onTap: () {
          setState(() => _selectedPlanIndex = index);
        },
        borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        child: Padding(
          padding: const EdgeInsets.all(AppConstants.spacingMd),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Checkbox(
                    value: _selectedPlanIndex == index,
                    onChanged: (value) {
                      if (value == true) {
                        setState(() => _selectedPlanIndex = index);
                      }
                    },
                    shape: const CircleBorder(),
                    activeColor: AppColors.primary,
                  ),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Text(
                              plan['name'],
                              style: const TextStyle(
                                fontSize: 18,
                                fontWeight: FontWeight.bold,
                                color: AppColors.textPrimary,
                              ),
                            ),
                            if (isPopular) ...[
                              const SizedBox(width: 8),
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                                decoration: BoxDecoration(
                                  gradient: AppColors.primaryGradient,
                                  borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                                ),
                                child: const Text(
                                  'ยอดนิยม',
                                  style: TextStyle(
                                    fontSize: 10,
                                    fontWeight: FontWeight.w600,
                                    color: Colors.white,
                                  ),
                                ),
                              ),
                            ],
                            if (isCurrent) ...[
                              const SizedBox(width: 8),
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                                decoration: BoxDecoration(
                                  color: AppColors.success.withValues(alpha:0.15),
                                  borderRadius: BorderRadius.circular(AppConstants.radiusFull),
                                ),
                                child: const Text(
                                  'ปัจจุบัน',
                                  style: TextStyle(
                                    fontSize: 10,
                                    fontWeight: FontWeight.w600,
                                    color: AppColors.success,
                                  ),
                                ),
                              ),
                            ],
                          ],
                        ),
                        Text(
                          plan['description'],
                          style: const TextStyle(
                            fontSize: 13,
                            color: AppColors.textMuted,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          const Text(
                            '฿',
                            style: TextStyle(
                              fontSize: 14,
                              fontWeight: FontWeight.w600,
                              color: AppColors.primary,
                            ),
                          ),
                          Text(
                            '${plan['price']}',
                            style: const TextStyle(
                              fontSize: 24,
                              fontWeight: FontWeight.bold,
                              color: AppColors.primary,
                            ),
                          ),
                        ],
                      ),
                      const Text(
                        '/เดือน',
                        style: TextStyle(
                          fontSize: 11,
                          color: AppColors.textMuted,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
              const SizedBox(height: AppConstants.spacingMd),
              const Divider(color: AppColors.border, height: 1),
              const SizedBox(height: AppConstants.spacingMd),
              Wrap(
                spacing: 8,
                runSpacing: 6,
                children: (plan['features'] as List<String>).map((feature) {
                  return Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      const Icon(Iconsax.tick_circle, size: 14, color: AppColors.success),
                      const SizedBox(width: 4),
                      Text(
                        feature,
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  );
                }).toList(),
              ),
              if (isSelected && !isCurrent) ...[
                const SizedBox(height: AppConstants.spacingMd),
                SizedBox(
                  width: double.infinity,
                  child: CustomButton(
                    onPressed: () => _showUpgradeConfirm(plan),
                    label: plan['price'] > _currentPlan['price'] ? 'อัพเกรด' : 'เปลี่ยนแพ็กเกจ',
                    icon: plan['price'] > _currentPlan['price'] ? Iconsax.arrow_up_3 : Iconsax.arrow_swap_horizontal,
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildPaymentItem(String date, String description, int amount, String status) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        children: [
          Container(
            width: 40,
            height: 40,
            decoration: BoxDecoration(
              color: AppColors.success.withValues(alpha:0.15),
              borderRadius: BorderRadius.circular(AppConstants.radiusMd),
            ),
            child: const Icon(Iconsax.receipt_1, color: AppColors.success, size: 20),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  description,
                  style: const TextStyle(
                    fontSize: 14,
                    color: AppColors.textPrimary,
                  ),
                ),
                Text(
                  date,
                  style: const TextStyle(
                    fontSize: 12,
                    color: AppColors.textMuted,
                  ),
                ),
              ],
            ),
          ),
          Text(
            amount > 0 ? '฿$amount' : 'ฟรี',
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color: AppColors.textPrimary,
            ),
          ),
        ],
      ),
    );
  }

  void _showUpgradeConfirm(Map<String, dynamic> plan) {
    showDialog(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: AppColors.card,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppConstants.radiusLg),
        ),
        title: Text(
          plan['price'] > _currentPlan['price'] ? 'ยืนยันการอัพเกรด' : 'ยืนยันการเปลี่ยนแพ็กเกจ',
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('คุณต้องการเปลี่ยนเป็นแพ็กเกจ ${plan['name']} หรือไม่?'),
            const SizedBox(height: 12),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.surface,
                borderRadius: BorderRadius.circular(AppConstants.radiusMd),
              ),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text('ราคา'),
                  Text(
                    '฿${plan['price']}/เดือน',
                    style: const TextStyle(fontWeight: FontWeight.w600),
                  ),
                ],
              ),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('ยกเลิก'),
          ),
          ElevatedButton(
            onPressed: () {
              Navigator.pop(context);
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(
                  content: Text('เปลี่ยนแพ็กเกจสำเร็จ!'),
                  backgroundColor: AppColors.success,
                ),
              );
            },
            child: const Text('ยืนยัน'),
          ),
        ],
      ),
    );
  }
}
