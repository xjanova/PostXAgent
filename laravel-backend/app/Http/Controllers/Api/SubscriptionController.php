<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use App\Models\User;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Laravel\Cashier\Exceptions\IncompletePayment;

class SubscriptionController extends Controller
{
    /**
     * Get available subscription plans
     */
    public function plans(): JsonResponse
    {
        $plans = [
            [
                'id' => 'starter',
                'name' => 'Starter',
                'price_monthly' => 990,
                'price_yearly' => 9900,
                'currency' => 'THB',
                'features' => [
                    'posts_per_month' => 100,
                    'brands' => 3,
                    'platforms' => 5,
                    'ai_generations' => 500,
                    'analytics' => 'basic',
                    'support' => 'email',
                ],
                'stripe_price_monthly' => 'price_starter_monthly',
                'stripe_price_yearly' => 'price_starter_yearly',
            ],
            [
                'id' => 'professional',
                'name' => 'Professional',
                'price_monthly' => 2490,
                'price_yearly' => 24900,
                'currency' => 'THB',
                'features' => [
                    'posts_per_month' => 500,
                    'brands' => 10,
                    'platforms' => 9,
                    'ai_generations' => 2000,
                    'analytics' => 'advanced',
                    'support' => 'priority',
                    'api_access' => true,
                ],
                'stripe_price_monthly' => 'price_professional_monthly',
                'stripe_price_yearly' => 'price_professional_yearly',
                'popular' => true,
            ],
            [
                'id' => 'enterprise',
                'name' => 'Enterprise',
                'price_monthly' => 7990,
                'price_yearly' => 79900,
                'currency' => 'THB',
                'features' => [
                    'posts_per_month' => -1, // Unlimited
                    'brands' => -1,
                    'platforms' => 9,
                    'ai_generations' => -1,
                    'analytics' => 'advanced',
                    'support' => 'dedicated',
                    'api_access' => true,
                    'custom_integration' => true,
                    'white_label' => true,
                ],
                'stripe_price_monthly' => 'price_enterprise_monthly',
                'stripe_price_yearly' => 'price_enterprise_yearly',
            ],
        ];

        return response()->json(['plans' => $plans]);
    }

    /**
     * Get current subscription status
     */
    public function status(Request $request): JsonResponse
    {
        $user = $request->user();

        if (!$user->subscribed('default')) {
            return response()->json([
                'subscribed' => false,
                'plan' => 'free',
                'usage' => $user->getUsageQuota(),
            ]);
        }

        $subscription = $user->subscription('default');

        return response()->json([
            'subscribed' => true,
            'plan' => $this->getPlanNameFromPrice($subscription->stripe_price),
            'status' => $subscription->stripe_status,
            'current_period_end' => $subscription->ends_at ?? $subscription->asStripeSubscription()->current_period_end,
            'cancel_at_period_end' => $subscription->onGracePeriod(),
            'usage' => $user->getUsageQuota(),
        ]);
    }

    /**
     * Create subscription checkout session
     */
    public function checkout(Request $request): JsonResponse
    {
        $request->validate([
            'price_id' => 'required|string',
        ]);

        $user = $request->user();

        try {
            $checkout = $user->newSubscription('default', $request->price_id)
                ->checkout([
                    'success_url' => config('app.frontend_url') . '/subscription/success?session_id={CHECKOUT_SESSION_ID}',
                    'cancel_url' => config('app.frontend_url') . '/subscription/cancel',
                    'locale' => 'th',
                ]);

            return response()->json([
                'checkout_url' => $checkout->url,
            ]);
        } catch (\Exception $e) {
            return response()->json([
                'error' => 'Failed to create checkout session',
                'message' => $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Create customer portal session
     */
    public function portal(Request $request): JsonResponse
    {
        $user = $request->user();

        $portal = $user->billingPortalUrl(
            config('app.frontend_url') . '/dashboard'
        );

        return response()->json([
            'portal_url' => $portal,
        ]);
    }

    /**
     * Change subscription plan
     */
    public function changePlan(Request $request): JsonResponse
    {
        $request->validate([
            'price_id' => 'required|string',
        ]);

        $user = $request->user();

        if (!$user->subscribed('default')) {
            return response()->json([
                'error' => 'No active subscription',
            ], 400);
        }

        try {
            $user->subscription('default')->swap($request->price_id);

            return response()->json([
                'success' => true,
                'message' => 'Subscription updated successfully',
            ]);
        } catch (\Exception $e) {
            return response()->json([
                'error' => 'Failed to change subscription',
                'message' => $e->getMessage(),
            ], 500);
        }
    }

    /**
     * Cancel subscription
     */
    public function cancel(Request $request): JsonResponse
    {
        $user = $request->user();

        if (!$user->subscribed('default')) {
            return response()->json([
                'error' => 'No active subscription',
            ], 400);
        }

        $user->subscription('default')->cancel();

        return response()->json([
            'success' => true,
            'message' => 'Subscription will be cancelled at the end of the billing period',
        ]);
    }

    /**
     * Resume cancelled subscription
     */
    public function resume(Request $request): JsonResponse
    {
        $user = $request->user();

        if (!$user->subscription('default')?->onGracePeriod()) {
            return response()->json([
                'error' => 'Subscription cannot be resumed',
            ], 400);
        }

        $user->subscription('default')->resume();

        return response()->json([
            'success' => true,
            'message' => 'Subscription resumed successfully',
        ]);
    }

    /**
     * Get invoice history
     */
    public function invoices(Request $request): JsonResponse
    {
        $user = $request->user();

        $invoices = $user->invoices()->map(function ($invoice) {
            return [
                'id' => $invoice->id,
                'date' => $invoice->date()->toIso8601String(),
                'total' => $invoice->total(),
                'status' => $invoice->status,
                'pdf_url' => $invoice->invoicePdfUrl(),
            ];
        });

        return response()->json(['invoices' => $invoices]);
    }

    private function getPlanNameFromPrice(string $priceId): string
    {
        return match(true) {
            str_contains($priceId, 'starter') => 'starter',
            str_contains($priceId, 'professional') => 'professional',
            str_contains($priceId, 'enterprise') => 'enterprise',
            default => 'unknown',
        };
    }
}
