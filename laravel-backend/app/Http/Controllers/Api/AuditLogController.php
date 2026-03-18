<?php

declare(strict_types=1);

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\User;
use Illuminate\Http\JsonResponse;
use Illuminate\Http\Request;
use Spatie\Activitylog\Models\Activity;

class AuditLogController extends Controller
{
    /**
     * Get audit logs with filtering
     */
    public function index(Request $request): JsonResponse
    {
        $request->validate([
            'log_name' => 'sometimes|string|max:100',
            'causer_id' => 'sometimes|integer',
            'subject_type' => 'sometimes|string|max:255',
            'event' => 'sometimes|string|max:100',
            'from' => 'sometimes|date',
            'to' => 'sometimes|date|after_or_equal:from',
            'per_page' => 'sometimes|integer|min:1|max:100',
        ]);

        $query = Activity::with(['causer', 'subject'])
            ->orderBy('created_at', 'desc');

        // Filter by log name (category)
        if ($request->has('log_name')) {
            $query->where('log_name', $request->input('log_name'));
        }

        // Filter by causer (user who performed the action)
        if ($request->has('causer_id')) {
            $query->where('causer_id', $request->input('causer_id'))
                  ->where('causer_type', 'App\\Models\\User');
        }

        // Filter by subject type (model affected)
        if ($request->has('subject_type')) {
            $query->where('subject_type', $request->input('subject_type'));
        }

        // Filter by event
        if ($request->has('event')) {
            $query->where('event', $request->input('event'));
        }

        // Filter by date range
        if ($request->has('from')) {
            $query->whereDate('created_at', '>=', $request->input('from'));
        }
        if ($request->has('to')) {
            $query->whereDate('created_at', '<=', $request->input('to'));
        }

        $logs = $query->paginate($request->integer('per_page', 50));

        return response()->json([
            'success' => true,
            'data' => $logs->map(function (Activity $log): array {
                /** @var User|null $causer */
                $causer = $log->causer;

                return [
                    'id' => $log->id,
                    'log_name' => $log->log_name,
                    'description' => $log->description,
                    'event' => $log->event,
                    'causer' => $causer !== null ? [
                        'id' => $causer->id,
                        'name' => $causer->name,
                        'email' => $causer->email,
                    ] : null,
                    'subject_type' => class_basename($log->subject_type ?? ''),
                    'subject_id' => $log->subject_id,
                    'properties' => $log->properties,
                    'created_at' => $log->created_at?->toIso8601String(),
                ];
            }),
            'meta' => [
                'current_page' => $logs->currentPage(),
                'last_page' => $logs->lastPage(),
                'per_page' => $logs->perPage(),
                'total' => $logs->total(),
            ],
        ]);
    }

    /**
     * Get a specific audit log entry
     */
    public function show(Activity $activity): JsonResponse
    {
        $activity->load(['causer', 'subject']);

        /** @var User|null $causer */
        $causer = $activity->causer;

        /** @var \Illuminate\Database\Eloquent\Model|null $subject */
        $subject = $activity->subject;

        return response()->json([
            'success' => true,
            'data' => [
                'id' => $activity->id,
                'log_name' => $activity->log_name,
                'description' => $activity->description,
                'event' => $activity->event,
                'causer' => $causer !== null ? [
                    'id' => $causer->id,
                    'name' => $causer->name,
                    'email' => $causer->email,
                ] : null,
                'subject' => $subject !== null ? [
                    'type' => class_basename($activity->subject_type ?? ''),
                    'id' => $activity->subject_id,
                    'data' => $subject->toArray(),
                ] : null,
                'properties' => $activity->properties,
                'created_at' => $activity->created_at?->toIso8601String(),
            ],
        ]);
    }

    /**
     * Get audit log statistics
     */
    public function stats(Request $request): JsonResponse
    {
        $request->validate([
            'days' => 'sometimes|integer|min:1|max:365',
        ]);

        /** @var int $days */
        $days = $request->integer('days', 30);
        $startDate = now()->subDays($days);

        // Get activity by log name
        $byLogName = Activity::where('created_at', '>=', $startDate)
            ->selectRaw('log_name, COUNT(*) as count')
            ->groupBy('log_name')
            ->pluck('count', 'log_name');

        // Get activity by event type
        $byEvent = Activity::where('created_at', '>=', $startDate)
            ->whereNotNull('event')
            ->selectRaw('event, COUNT(*) as count')
            ->groupBy('event')
            ->pluck('count', 'event');

        // Get daily activity
        $dailyActivity = Activity::where('created_at', '>=', $startDate)
            ->selectRaw('DATE(created_at) as date, COUNT(*) as count')
            ->groupBy('date')
            ->orderBy('date')
            ->pluck('count', 'date');

        // Get top causers
        $topCausers = Activity::where('created_at', '>=', $startDate)
            ->whereNotNull('causer_id')
            ->with('causer:id,name,email')
            ->selectRaw('causer_id, causer_type, COUNT(*) as count')
            ->groupBy('causer_id', 'causer_type')
            ->orderByDesc('count')
            ->limit(10)
            ->get()
            ->map(function (Activity $item): array {
                /** @var User|null $causer */
                $causer = $item->causer;
                /** @var int $count */
                $count = $item->getAttribute('count');

                return [
                    'user' => $causer !== null ? [
                        'id' => $causer->id,
                        'name' => $causer->name,
                    ] : null,
                    'count' => $count,
                ];
            });

        $totalActivities = Activity::where('created_at', '>=', $startDate)->count();

        return response()->json([
            'success' => true,
            'data' => [
                'period' => [
                    'from' => $startDate->toDateString(),
                    'to' => now()->toDateString(),
                    'days' => $days,
                ],
                'total_activities' => $totalActivities,
                'by_log_name' => $byLogName,
                'by_event' => $byEvent,
                'daily_activity' => $dailyActivity,
                'top_causers' => $topCausers,
            ],
        ]);
    }

    /**
     * Get user's activity log
     */
    public function userActivity(Request $request, int $userId): JsonResponse
    {
        $request->validate([
            'per_page' => 'sometimes|integer|min:1|max:100',
        ]);

        $logs = Activity::where('causer_id', $userId)
            ->where('causer_type', 'App\\Models\\User')
            ->with('subject')
            ->orderBy('created_at', 'desc')
            ->paginate($request->integer('per_page', 50));

        return response()->json([
            'success' => true,
            'data' => $logs->map(fn (Activity $log): array => [
                'id' => $log->id,
                'description' => $log->description,
                'event' => $log->event,
                'subject_type' => class_basename($log->subject_type ?? ''),
                'subject_id' => $log->subject_id,
                'properties' => $log->properties,
                'created_at' => $log->created_at?->toIso8601String(),
            ]),
            'meta' => [
                'current_page' => $logs->currentPage(),
                'last_page' => $logs->lastPage(),
                'per_page' => $logs->perPage(),
                'total' => $logs->total(),
            ],
        ]);
    }

    /**
     * Get available log names for filtering
     */
    public function logNames(): JsonResponse
    {
        $logNames = Activity::distinct('log_name')
            ->whereNotNull('log_name')
            ->pluck('log_name');

        return response()->json([
            'success' => true,
            'data' => $logNames,
        ]);
    }

    /**
     * Export audit logs as CSV
     */
    public function export(Request $request): \Illuminate\Http\Response
    {
        $request->validate([
            'from' => 'sometimes|date',
            'to' => 'sometimes|date|after_or_equal:from',
        ]);

        $query = Activity::with(['causer'])
            ->orderBy('created_at', 'desc');

        if ($request->has('from')) {
            $query->whereDate('created_at', '>=', $request->input('from'));
        }
        if ($request->has('to')) {
            $query->whereDate('created_at', '<=', $request->input('to'));
        }

        $logs = $query->limit(10000)->get();

        $csv = "ID,Log Name,Description,Event,Causer,Subject Type,Subject ID,Created At\n";

        foreach ($logs as $log) {
            /** @var User|null $causer */
            $causer = $log->causer;
            $causerName = $causer !== null ? $causer->name : 'System';
            $subjectType = class_basename($log->subject_type ?? '');

            $csv .= sprintf(
                "%d,%s,%s,%s,%s,%s,%s,%s\n",
                $log->id,
                $log->log_name ?? '',
                str_replace(',', ';', $log->description ?? ''),
                $log->event ?? '',
                $causerName,
                $subjectType,
                $log->subject_id ?? '',
                $log->created_at?->toDateTimeString() ?? ''
            );
        }

        return response($csv, 200, [
            'Content-Type' => 'text/csv',
            'Content-Disposition' => 'attachment; filename="audit-log-' . now()->format('Y-m-d') . '.csv"',
        ]);
    }
}
