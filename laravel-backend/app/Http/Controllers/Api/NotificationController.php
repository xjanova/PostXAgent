<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;

class NotificationController extends Controller
{
    /**
     * Get all notifications for the authenticated user
     */
    public function index(Request $request): JsonResponse
    {
        $notifications = $request->user()
            ->notifications()
            ->when($request->unread_only, fn($q) => $q->whereNull('read_at'))
            ->orderBy('created_at', 'desc')
            ->paginate($request->per_page ?? 20);

        return response()->json([
            'success' => true,
            'data' => $notifications,
            'unread_count' => $request->user()->unreadNotifications()->count(),
        ]);
    }

    /**
     * Get unread notifications count
     */
    public function unreadCount(Request $request): JsonResponse
    {
        return response()->json([
            'success' => true,
            'unread_count' => $request->user()->unreadNotifications()->count(),
        ]);
    }

    /**
     * Mark a notification as read
     */
    public function markAsRead(Request $request, string $id): JsonResponse
    {
        $notification = $request->user()
            ->notifications()
            ->findOrFail($id);

        $notification->markAsRead();

        return response()->json([
            'success' => true,
            'message' => 'Notification marked as read',
        ]);
    }

    /**
     * Mark all notifications as read
     */
    public function markAllAsRead(Request $request): JsonResponse
    {
        $request->user()->unreadNotifications->markAsRead();

        return response()->json([
            'success' => true,
            'message' => 'All notifications marked as read',
        ]);
    }

    /**
     * Delete a notification
     */
    public function destroy(Request $request, string $id): JsonResponse
    {
        $notification = $request->user()
            ->notifications()
            ->findOrFail($id);

        $notification->delete();

        return response()->json([
            'success' => true,
            'message' => 'Notification deleted',
        ]);
    }

    /**
     * Delete all notifications
     */
    public function destroyAll(Request $request): JsonResponse
    {
        $request->user()->notifications()->delete();

        return response()->json([
            'success' => true,
            'message' => 'All notifications deleted',
        ]);
    }

    /**
     * Get notification preferences
     */
    public function preferences(Request $request): JsonResponse
    {
        $user = $request->user();
        $preferences = $user->notification_preferences ?? $this->getDefaultPreferences();

        return response()->json([
            'success' => true,
            'data' => $preferences,
        ]);
    }

    /**
     * Update notification preferences
     */
    public function updatePreferences(Request $request): JsonResponse
    {
        $validated = $request->validate([
            'email_post_published' => 'boolean',
            'email_post_failed' => 'boolean',
            'email_rental_expiring' => 'boolean',
            'email_payment_received' => 'boolean',
            'email_campaign_completed' => 'boolean',
            'email_account_warning' => 'boolean',
        ]);

        $user = $request->user();
        $user->notification_preferences = array_merge(
            $user->notification_preferences ?? $this->getDefaultPreferences(),
            $validated
        );
        $user->save();

        return response()->json([
            'success' => true,
            'message' => 'Notification preferences updated',
            'data' => $user->notification_preferences,
        ]);
    }

    /**
     * Get default notification preferences
     */
    private function getDefaultPreferences(): array
    {
        return [
            'email_post_published' => false,
            'email_post_failed' => true,
            'email_rental_expiring' => true,
            'email_payment_received' => true,
            'email_campaign_completed' => true,
            'email_account_warning' => true,
        ];
    }
}
