import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../data/models/user.dart';
import '../data/services/api_service.dart';
import '../core/constants/app_constants.dart';

/// Auth State
class AuthState {
  final User? user;
  final String? token;
  final bool isLoading;
  final String? error;

  const AuthState({
    this.user,
    this.token,
    this.isLoading = false,
    this.error,
  });

  bool get isAuthenticated => user != null && token != null;

  AuthState copyWith({
    User? user,
    String? token,
    bool? isLoading,
    String? error,
  }) {
    return AuthState(
      user: user ?? this.user,
      token: token ?? this.token,
      isLoading: isLoading ?? this.isLoading,
      error: error,
    );
  }

  AuthState clearError() {
    return AuthState(
      user: user,
      token: token,
      isLoading: isLoading,
      error: null,
    );
  }
}

/// Auth Notifier
class AuthNotifier extends StateNotifier<AuthState> {
  final ApiService _apiService;
  final FlutterSecureStorage _storage;

  AuthNotifier(this._apiService, this._storage) : super(const AuthState()) {
    _loadSavedAuth();
  }

  /// Load saved authentication from secure storage
  Future<void> _loadSavedAuth() async {
    state = state.copyWith(isLoading: true);
    try {
      final token = await _storage.read(key: AppConstants.tokenKey);
      if (token != null) {
        _apiService.setAuthToken(token);
        await _fetchCurrentUser();
      }
    } catch (e) {
      // Silent fail - user will need to login
    } finally {
      state = state.copyWith(isLoading: false);
    }
  }

  /// Fetch current user profile
  Future<void> _fetchCurrentUser() async {
    try {
      final response = await _apiService.get('/auth/me');
      if (response.data != null && response.data['success'] == true) {
        final userData = response.data['data'];
        final user = User.fromJson(userData);
        final token = await _storage.read(key: AppConstants.tokenKey);
        state = state.copyWith(user: user, token: token);
      }
    } catch (e) {
      // Token might be invalid, clear it
      await logout();
    }
  }

  /// Login with email and password
  Future<bool> login(String email, String password, {bool rememberMe = false}) async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final response = await _apiService.post(
        '/auth/login',
        data: {
          'email': email,
          'password': password,
          'remember_me': rememberMe,
        },
      );

      if (response.data != null && response.data['success'] == true) {
        final authData = AuthData.fromJson(response.data['data']);

        // Save token
        await _storage.write(key: AppConstants.tokenKey, value: authData.token);
        _apiService.setAuthToken(authData.token);

        state = state.copyWith(
          user: authData.user,
          token: authData.token,
          isLoading: false,
        );
        return true;
      } else {
        state = state.copyWith(
          isLoading: false,
          error: response.data?['message'] ?? 'เข้าสู่ระบบไม่สำเร็จ',
        );
        return false;
      }
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
      return false;
    }
  }

  /// Register new user
  Future<bool> register({
    required String name,
    required String email,
    required String password,
    required String passwordConfirmation,
    String? phone,
    String? companyName,
  }) async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final response = await _apiService.post(
        '/auth/register',
        data: {
          'name': name,
          'email': email,
          'password': password,
          'password_confirmation': passwordConfirmation,
          if (phone != null) 'phone': phone,
          if (companyName != null) 'company_name': companyName,
        },
      );

      if (response.data != null && response.data['success'] == true) {
        final authData = AuthData.fromJson(response.data['data']);

        // Save token
        await _storage.write(key: AppConstants.tokenKey, value: authData.token);
        _apiService.setAuthToken(authData.token);

        state = state.copyWith(
          user: authData.user,
          token: authData.token,
          isLoading: false,
        );
        return true;
      } else {
        state = state.copyWith(
          isLoading: false,
          error: response.data?['message'] ?? 'ลงทะเบียนไม่สำเร็จ',
        );
        return false;
      }
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
      return false;
    }
  }

  /// Logout
  Future<void> logout() async {
    try {
      await _apiService.post('/auth/logout');
    } catch (e) {
      // Ignore logout errors
    } finally {
      await _storage.delete(key: AppConstants.tokenKey);
      _apiService.clearAuthToken();
      state = const AuthState();
    }
  }

  /// Update user profile
  Future<bool> updateProfile({
    String? name,
    String? phone,
    String? companyName,
  }) async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final response = await _apiService.put(
        '/auth/profile',
        data: {
          if (name != null) 'name': name,
          if (phone != null) 'phone': phone,
          if (companyName != null) 'company_name': companyName,
        },
      );

      if (response.data != null && response.data['success'] == true) {
        final user = User.fromJson(response.data['data']);
        state = state.copyWith(user: user, isLoading: false);
        return true;
      } else {
        state = state.copyWith(
          isLoading: false,
          error: response.data?['message'] ?? 'อัพเดทข้อมูลไม่สำเร็จ',
        );
        return false;
      }
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
      return false;
    }
  }

  /// Update password
  Future<bool> updatePassword({
    required String currentPassword,
    required String newPassword,
    required String confirmPassword,
  }) async {
    state = state.copyWith(isLoading: true, error: null);
    try {
      final response = await _apiService.put(
        '/auth/password',
        data: {
          'current_password': currentPassword,
          'password': newPassword,
          'password_confirmation': confirmPassword,
        },
      );

      if (response.data != null && response.data['success'] == true) {
        state = state.copyWith(isLoading: false);
        return true;
      } else {
        state = state.copyWith(
          isLoading: false,
          error: response.data?['message'] ?? 'เปลี่ยนรหัสผ่านไม่สำเร็จ',
        );
        return false;
      }
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
      return false;
    }
  }

  /// Clear error
  void clearError() {
    state = state.clearError();
  }

  /// Refresh user data
  Future<void> refreshUser() async {
    await _fetchCurrentUser();
  }

  /// DEV ONLY: Bypass login for testing
  /// รหัส: dev1234
  Future<bool> devLogin(String code) async {
    if (code != 'dev1234') {
      state = state.copyWith(error: 'รหัส bypass ไม่ถูกต้อง');
      return false;
    }

    state = state.copyWith(isLoading: true, error: null);

    // Create mock user for testing
    final mockUser = User(
      id: 1,
      name: 'ผู้ใช้ทดสอบ',
      email: 'test@postxagent.com',
      phone: '0812345678',
      companyName: 'PostXAgent Test',
      language: 'th',
      isActive: true,
      createdAt: DateTime.now(),
      updatedAt: DateTime.now(),
      hasActiveRental: true,
      currentPackage: 'premium',
    );

    const mockToken = 'dev-test-token-bypass-1234';

    await _storage.write(key: AppConstants.tokenKey, value: mockToken);

    state = state.copyWith(
      user: mockUser,
      token: mockToken,
      isLoading: false,
    );

    return true;
  }
}

/// Providers
final secureStorageProvider = Provider<FlutterSecureStorage>((ref) {
  return const FlutterSecureStorage(
    aOptions: AndroidOptions(encryptedSharedPreferences: true),
    iOptions: IOSOptions(accessibility: KeychainAccessibility.first_unlock),
  );
});

final apiServiceProvider = Provider<ApiService>((ref) {
  return ApiService();
});

final authNotifierProvider = StateNotifierProvider<AuthNotifier, AuthState>((ref) {
  final apiService = ref.watch(apiServiceProvider);
  final storage = ref.watch(secureStorageProvider);
  return AuthNotifier(apiService, storage);
});

final authStateProvider = Provider<AuthState>((ref) {
  return ref.watch(authNotifierProvider);
});

final currentUserProvider = Provider<User?>((ref) {
  return ref.watch(authNotifierProvider).user;
});

final isAuthenticatedProvider = Provider<bool>((ref) {
  return ref.watch(authNotifierProvider).isAuthenticated;
});
