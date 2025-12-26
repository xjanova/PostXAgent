import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/models/notification.dart' as app;
import '../data/services/api_service.dart';
import 'auth_provider.dart';

/// Notifications State
class NotificationsState {
  final List<app.AppNotification> notifications;
  final bool isLoading;
  final String? error;
  final int unreadCount;

  const NotificationsState({
    this.notifications = const [],
    this.isLoading = false,
    this.error,
    this.unreadCount = 0,
  });

  NotificationsState copyWith({
    List<app.AppNotification>? notifications,
    bool? isLoading,
    String? error,
    int? unreadCount,
  }) {
    return NotificationsState(
      notifications: notifications ?? this.notifications,
      isLoading: isLoading ?? this.isLoading,
      error: error,
      unreadCount: unreadCount ?? this.unreadCount,
    );
  }
}

/// Notifications Notifier
class NotificationsNotifier extends StateNotifier<NotificationsState> {
  final ApiService _apiService;

  NotificationsNotifier(this._apiService) : super(const NotificationsState());

  /// Load notifications
  Future<void> loadNotifications() async {
    state = state.copyWith(isLoading: true, error: null);

    try {
      final response = await _apiService.get(ApiEndpoints.notifications);

      if (response.data['success'] == true) {
        final items = (response.data['data'] as List?)
                ?.map((item) =>
                    app.AppNotification.fromJson(item as Map<String, dynamic>))
                .toList() ??
            [];

        final unreadCount = items.where((n) => n.isUnread).length;

        state = state.copyWith(
          notifications: items,
          isLoading: false,
          unreadCount: unreadCount,
        );
      } else {
        state = state.copyWith(
          isLoading: false,
          error: response.data['message'] ?? 'ไม่สามารถโหลดการแจ้งเตือนได้',
        );
      }
    } catch (e) {
      state = state.copyWith(
        isLoading: false,
        error: e.toString(),
      );
    }
  }

  /// Mark notification as read
  Future<bool> markAsRead(String id) async {
    try {
      final response = await _apiService.post(
        '${ApiEndpoints.notifications}/$id/read',
      );

      if (response.data['success'] == true) {
        state = state.copyWith(
          notifications: state.notifications.map((n) {
            if (n.id == id) {
              return app.AppNotification(
                id: n.id,
                type: n.type,
                title: n.title,
                body: n.body,
                data: n.data,
                readAt: DateTime.now(),
                createdAt: n.createdAt,
              );
            }
            return n;
          }).toList(),
          unreadCount: state.unreadCount > 0 ? state.unreadCount - 1 : 0,
        );
        return true;
      }
      return false;
    } catch (e) {
      return false;
    }
  }

  /// Mark all as read
  Future<bool> markAllAsRead() async {
    try {
      final response = await _apiService.post(
        '${ApiEndpoints.notifications}/read-all',
      );

      if (response.data['success'] == true) {
        final now = DateTime.now();
        state = state.copyWith(
          notifications: state.notifications.map((n) {
            return app.AppNotification(
              id: n.id,
              type: n.type,
              title: n.title,
              body: n.body,
              data: n.data,
              readAt: now,
              createdAt: n.createdAt,
            );
          }).toList(),
          unreadCount: 0,
        );
        return true;
      }
      return false;
    } catch (e) {
      return false;
    }
  }

  /// Delete notification
  Future<bool> deleteNotification(String id) async {
    try {
      final response = await _apiService.delete(
        '${ApiEndpoints.notifications}/$id',
      );

      if (response.data['success'] == true) {
        final notification = state.notifications.firstWhere((n) => n.id == id);
        state = state.copyWith(
          notifications: state.notifications.where((n) => n.id != id).toList(),
          unreadCount:
              notification.isUnread && state.unreadCount > 0
                  ? state.unreadCount - 1
                  : state.unreadCount,
        );
        return true;
      }
      return false;
    } catch (e) {
      return false;
    }
  }

  /// Clear all notifications
  Future<bool> clearAll() async {
    try {
      final response = await _apiService.delete(
        '${ApiEndpoints.notifications}/clear',
      );

      if (response.data['success'] == true) {
        state = state.copyWith(
          notifications: [],
          unreadCount: 0,
        );
        return true;
      }
      return false;
    } catch (e) {
      return false;
    }
  }

  /// Add notification (from push/websocket)
  void addNotification(app.AppNotification notification) {
    state = state.copyWith(
      notifications: [notification, ...state.notifications],
      unreadCount: state.unreadCount + 1,
    );
  }
}

/// Notifications Provider
final notificationsNotifierProvider =
    StateNotifierProvider<NotificationsNotifier, NotificationsState>((ref) {
  final apiService = ref.watch(apiServiceProvider);
  return NotificationsNotifier(apiService);
});

/// Unread Count Provider
final unreadNotificationsCountProvider = Provider<int>((ref) {
  return ref.watch(notificationsNotifierProvider).unreadCount;
});
