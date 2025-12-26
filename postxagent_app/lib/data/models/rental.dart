import 'package:json_annotation/json_annotation.dart';

part 'rental.g.dart';

@JsonSerializable()
class RentalPackage {
  final int id;
  final String name;
  @JsonKey(name: 'name_th')
  final String? nameTh;
  final String? description;
  @JsonKey(name: 'description_th')
  final String? descriptionTh;
  @JsonKey(name: 'duration_type')
  final String durationType;
  @JsonKey(name: 'duration_value')
  final int durationValue;
  final double price;
  @JsonKey(name: 'original_price')
  final double? originalPrice;
  final String currency;
  @JsonKey(name: 'posts_limit')
  final int postsLimit;
  @JsonKey(name: 'brands_limit')
  final int brandsLimit;
  @JsonKey(name: 'platforms_limit')
  final int platformsLimit;
  @JsonKey(name: 'ai_generations_limit')
  final int aiGenerationsLimit;
  @JsonKey(name: 'accounts_per_platform')
  final int? accountsPerPlatform;
  @JsonKey(name: 'scheduled_posts_limit')
  final int? scheduledPostsLimit;
  @JsonKey(name: 'team_members_limit')
  final int? teamMembersLimit;
  final List<String>? features;
  @JsonKey(name: 'included_platforms')
  final List<String>? includedPlatforms;
  @JsonKey(name: 'is_active')
  final bool isActive;
  @JsonKey(name: 'is_featured')
  final bool isFeatured;
  @JsonKey(name: 'is_popular')
  final bool isPopular;
  @JsonKey(name: 'sort_order')
  final int sortOrder;
  @JsonKey(name: 'has_trial')
  final bool hasTrial;
  @JsonKey(name: 'trial_days')
  final int? trialDays;

  const RentalPackage({
    required this.id,
    required this.name,
    this.nameTh,
    this.description,
    this.descriptionTh,
    required this.durationType,
    required this.durationValue,
    required this.price,
    this.originalPrice,
    this.currency = 'THB',
    required this.postsLimit,
    required this.brandsLimit,
    required this.platformsLimit,
    required this.aiGenerationsLimit,
    this.accountsPerPlatform,
    this.scheduledPostsLimit,
    this.teamMembersLimit,
    this.features,
    this.includedPlatforms,
    this.isActive = true,
    this.isFeatured = false,
    this.isPopular = false,
    this.sortOrder = 0,
    this.hasTrial = false,
    this.trialDays,
  });

  factory RentalPackage.fromJson(Map<String, dynamic> json) =>
      _$RentalPackageFromJson(json);
  Map<String, dynamic> toJson() => _$RentalPackageToJson(this);

  String get displayName => nameTh ?? name;
  String get displayDescription => descriptionTh ?? description ?? '';

  bool get isUnlimitedPosts => postsLimit == -1;
  bool get isUnlimitedAI => aiGenerationsLimit == -1;

  String get postsLimitText =>
      isUnlimitedPosts ? 'ไม่จำกัด' : '$postsLimit โพสต์';
  String get aiLimitText =>
      isUnlimitedAI ? 'ไม่จำกัด' : '$aiGenerationsLimit ครั้ง';

  double get discountPercentage {
    if (originalPrice == null || originalPrice == 0) return 0;
    return ((originalPrice! - price) / originalPrice!) * 100;
  }

  String get formattedPrice {
    if (price == 0) return 'ฟรี';
    return '฿${price.toStringAsFixed(0)}';
  }

  String get durationText {
    switch (durationType) {
      case 'hourly':
        return '$durationValue ชั่วโมง';
      case 'daily':
        return '$durationValue วัน';
      case 'weekly':
        return '$durationValue สัปดาห์';
      case 'monthly':
        return '$durationValue เดือน';
      case 'yearly':
        return '$durationValue ปี';
      default:
        return '$durationValue $durationType';
    }
  }
}

@JsonSerializable()
class UserRental {
  final int id;
  @JsonKey(name: 'user_id')
  final int userId;
  @JsonKey(name: 'rental_package_id')
  final int rentalPackageId;
  @JsonKey(name: 'starts_at')
  final DateTime startsAt;
  @JsonKey(name: 'expires_at')
  final DateTime expiresAt;
  @JsonKey(name: 'cancelled_at')
  final DateTime? cancelledAt;
  @JsonKey(name: 'amount_paid')
  final double amountPaid;
  final String currency;
  @JsonKey(name: 'payment_method')
  final String? paymentMethod;
  @JsonKey(name: 'payment_reference')
  final String? paymentReference;
  @JsonKey(name: 'posts_used')
  final int postsUsed;
  @JsonKey(name: 'ai_generations_used')
  final int aiGenerationsUsed;
  @JsonKey(name: 'usage_stats')
  final Map<String, dynamic>? usageStats;
  @JsonKey(name: 'auto_renew')
  final bool autoRenew;
  @JsonKey(name: 'next_renewal_at')
  final DateTime? nextRenewalAt;
  final String status;
  final String? notes;
  @JsonKey(name: 'created_at')
  final DateTime createdAt;
  @JsonKey(name: 'updated_at')
  final DateTime updatedAt;

  // Relations
  @JsonKey(name: 'rental_package')
  final RentalPackage? rentalPackage;

  const UserRental({
    required this.id,
    required this.userId,
    required this.rentalPackageId,
    required this.startsAt,
    required this.expiresAt,
    this.cancelledAt,
    required this.amountPaid,
    this.currency = 'THB',
    this.paymentMethod,
    this.paymentReference,
    this.postsUsed = 0,
    this.aiGenerationsUsed = 0,
    this.usageStats,
    this.autoRenew = false,
    this.nextRenewalAt,
    required this.status,
    this.notes,
    required this.createdAt,
    required this.updatedAt,
    this.rentalPackage,
  });

  factory UserRental.fromJson(Map<String, dynamic> json) =>
      _$UserRentalFromJson(json);
  Map<String, dynamic> toJson() => _$UserRentalToJson(this);

  bool get isActive => status == 'active' && !isExpired;
  bool get isExpired => expiresAt.isBefore(DateTime.now());
  bool get isCancelled => cancelledAt != null;

  int get daysRemaining {
    if (isExpired) return 0;
    return expiresAt.difference(DateTime.now()).inDays;
  }

  double get postsUsagePercentage {
    if (rentalPackage == null) return 0;
    if (rentalPackage!.postsLimit == -1) return 0; // Unlimited
    return (postsUsed / rentalPackage!.postsLimit) * 100;
  }

  double get aiUsagePercentage {
    if (rentalPackage == null) return 0;
    if (rentalPackage!.aiGenerationsLimit == -1) return 0; // Unlimited
    return (aiGenerationsUsed / rentalPackage!.aiGenerationsLimit) * 100;
  }

  int get postsRemaining {
    if (rentalPackage == null) return 0;
    if (rentalPackage!.postsLimit == -1) return -1; // Unlimited
    return rentalPackage!.postsLimit - postsUsed;
  }

  int get aiRemaining {
    if (rentalPackage == null) return 0;
    if (rentalPackage!.aiGenerationsLimit == -1) return -1; // Unlimited
    return rentalPackage!.aiGenerationsLimit - aiGenerationsUsed;
  }

  String get statusLabel {
    switch (status) {
      case 'pending':
        return 'รอชำระเงิน';
      case 'active':
        return isExpired ? 'หมดอายุ' : 'ใช้งานอยู่';
      case 'expired':
        return 'หมดอายุ';
      case 'cancelled':
        return 'ยกเลิกแล้ว';
      case 'suspended':
        return 'ถูกระงับ';
      default:
        return status;
    }
  }
}

@JsonSerializable()
class RentalStatus {
  @JsonKey(name: 'has_active_rental')
  final bool hasActiveRental;
  @JsonKey(name: 'current_rental')
  final UserRental? currentRental;
  @JsonKey(name: 'posts_used')
  final int postsUsed;
  @JsonKey(name: 'posts_limit')
  final int postsLimit;
  @JsonKey(name: 'ai_generations_used')
  final int aiGenerationsUsed;
  @JsonKey(name: 'ai_generations_limit')
  final int aiGenerationsLimit;
  @JsonKey(name: 'brands_used')
  final int brandsUsed;
  @JsonKey(name: 'brands_limit')
  final int brandsLimit;
  @JsonKey(name: 'days_remaining')
  final int daysRemaining;

  const RentalStatus({
    required this.hasActiveRental,
    this.currentRental,
    this.postsUsed = 0,
    this.postsLimit = 0,
    this.aiGenerationsUsed = 0,
    this.aiGenerationsLimit = 0,
    this.brandsUsed = 0,
    this.brandsLimit = 0,
    this.daysRemaining = 0,
  });

  factory RentalStatus.fromJson(Map<String, dynamic> json) =>
      _$RentalStatusFromJson(json);
  Map<String, dynamic> toJson() => _$RentalStatusToJson(this);

  bool get isUnlimitedPosts => postsLimit == -1;
  bool get isUnlimitedAI => aiGenerationsLimit == -1;

  double get postsUsagePercentage {
    if (isUnlimitedPosts) return 0;
    if (postsLimit == 0) return 100;
    return (postsUsed / postsLimit) * 100;
  }

  double get aiUsagePercentage {
    if (isUnlimitedAI) return 0;
    if (aiGenerationsLimit == 0) return 100;
    return (aiGenerationsUsed / aiGenerationsLimit) * 100;
  }

  String get postsRemainingText {
    if (isUnlimitedPosts) return 'ไม่จำกัด';
    final remaining = postsLimit - postsUsed;
    return '$remaining/$postsLimit';
  }

  String get aiRemainingText {
    if (isUnlimitedAI) return 'ไม่จำกัด';
    final remaining = aiGenerationsLimit - aiGenerationsUsed;
    return '$remaining/$aiGenerationsLimit';
  }
}

/// Duration Types
class DurationTypes {
  static const Map<String, String> all = {
    'hourly': 'ชั่วโมง',
    'daily': 'วัน',
    'weekly': 'สัปดาห์',
    'monthly': 'เดือน',
    'yearly': 'ปี',
  };
}

/// Payment Methods
class PaymentMethods {
  static const Map<String, String> all = {
    'promptpay': 'พร้อมเพย์',
    'bank_transfer': 'โอนเงิน',
    'credit_card': 'บัตรเครดิต',
    'debit_card': 'บัตรเดบิต',
    'truemoney': 'TrueMoney Wallet',
    'linepay': 'LINE Pay',
    'shopeepay': 'ShopeePay',
  };
}
