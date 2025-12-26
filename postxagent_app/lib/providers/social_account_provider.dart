import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/models/social_account.dart';
import '../data/repositories/social_account_repository.dart';
import 'auth_provider.dart';

/// Social Account Repository Provider
final socialAccountRepositoryProvider = Provider<SocialAccountRepository>((ref) {
  final apiService = ref.watch(apiServiceProvider);
  return SocialAccountRepository(apiService);
});

/// Social Accounts State
class SocialAccountsState {
  final List<SocialAccount> accounts;
  final bool isLoading;
  final String? error;
  final String? platformFilter;
  final int? brandFilter;

  const SocialAccountsState({
    this.accounts = const [],
    this.isLoading = false,
    this.error,
    this.platformFilter,
    this.brandFilter,
  });

  SocialAccountsState copyWith({
    List<SocialAccount>? accounts,
    bool? isLoading,
    String? error,
    String? platformFilter,
    int? brandFilter,
  }) {
    return SocialAccountsState(
      accounts: accounts ?? this.accounts,
      isLoading: isLoading ?? this.isLoading,
      error: error,
      platformFilter: platformFilter ?? this.platformFilter,
      brandFilter: brandFilter ?? this.brandFilter,
    );
  }

  /// Get accounts by platform
  List<SocialAccount> getByPlatform(String platform) {
    return accounts.where((a) => a.platform == platform).toList();
  }

  /// Get connected accounts count
  int get connectedCount =>
      accounts.where((a) => a.isActive).length;

  /// Get accounts grouped by platform
  Map<String, List<SocialAccount>> get byPlatform {
    final map = <String, List<SocialAccount>>{};
    for (final account in accounts) {
      map.putIfAbsent(account.platform, () => []).add(account);
    }
    return map;
  }
}

/// Social Accounts Notifier
class SocialAccountsNotifier extends StateNotifier<SocialAccountsState> {
  final SocialAccountRepository _repository;

  SocialAccountsNotifier(this._repository) : super(const SocialAccountsState());

  /// Load accounts
  Future<void> loadAccounts() async {
    state = state.copyWith(isLoading: true, error: null);

    final result = await _repository.getSocialAccounts(
      brandId: state.brandFilter,
      platform: state.platformFilter,
    );

    if (result.isSuccess) {
      state = state.copyWith(
        accounts: result.data!,
        isLoading: false,
      );
    } else {
      state = state.copyWith(
        isLoading: false,
        error: result.error,
      );
    }
  }

  /// Filter by platform
  Future<void> setplatformFilter(String? platform) async {
    state = state.copyWith(platformFilter: platform);
    await loadAccounts();
  }

  /// Filter by brand
  Future<void> setBrandFilter(int? brandId) async {
    state = state.copyWith(brandFilter: brandId);
    await loadAccounts();
  }

  /// Connect account (returns OAuth URL)
  Future<String?> connectAccount(String platform, int? brandId) async {
    final result = await _repository.connectAccount(platform, brandId);
    if (result.isSuccess) {
      return result.data;
    }
    state = state.copyWith(error: result.error);
    return null;
  }

  /// Disconnect account
  Future<bool> disconnectAccount(int id) async {
    final result = await _repository.disconnectAccount(id);
    if (result.isSuccess) {
      state = state.copyWith(
        accounts: state.accounts.where((a) => a.id != id).toList(),
      );
      return true;
    }
    state = state.copyWith(error: result.error);
    return false;
  }

  /// Refresh account token
  Future<bool> refreshAccountToken(int id) async {
    final result = await _repository.refreshAccountToken(id);
    if (result.isSuccess) {
      _updateAccountInList(result.data!);
      return true;
    }
    state = state.copyWith(error: result.error);
    return false;
  }

  /// Sync account data
  Future<bool> syncAccount(int id) async {
    final result = await _repository.syncAccount(id);
    if (result.isSuccess) {
      _updateAccountInList(result.data!);
      return true;
    }
    state = state.copyWith(error: result.error);
    return false;
  }

  void _updateAccountInList(SocialAccount account) {
    state = state.copyWith(
      accounts: state.accounts.map((a) {
        if (a.id == account.id) return account;
        return a;
      }).toList(),
    );
  }

  void clearError() {
    state = state.copyWith(error: null);
  }
}

/// Social Accounts Notifier Provider
final socialAccountsNotifierProvider =
    StateNotifierProvider<SocialAccountsNotifier, SocialAccountsState>((ref) {
  final repository = ref.watch(socialAccountRepositoryProvider);
  return SocialAccountsNotifier(repository);
});

/// Platform connection status
class PlatformConnectionStatus {
  final String platform;
  final bool isConnected;
  final int accountCount;
  final SocialAccount? primaryAccount;

  const PlatformConnectionStatus({
    required this.platform,
    required this.isConnected,
    required this.accountCount,
    this.primaryAccount,
  });
}

/// Platform Connection Status Provider
final platformConnectionStatusProvider =
    Provider.family<PlatformConnectionStatus, String>((ref, platform) {
  final state = ref.watch(socialAccountsNotifierProvider);
  final accounts = state.getByPlatform(platform);
  final activeAccounts = accounts.where((a) => a.isActive).toList();

  return PlatformConnectionStatus(
    platform: platform,
    isConnected: activeAccounts.isNotEmpty,
    accountCount: activeAccounts.length,
    primaryAccount: activeAccounts.isNotEmpty ? activeAccounts.first : null,
  );
});
