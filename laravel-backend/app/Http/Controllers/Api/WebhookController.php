<?php

namespace App\Http\Controllers\Api;

use App\Http\Controllers\Controller;
use Illuminate\Http\Request;
use Illuminate\Http\JsonResponse;
use Illuminate\Support\Facades\Log;

class WebhookController extends Controller
{
    /**
     * Handle Stripe webhooks
     */
    public function handleStripe(Request $request): JsonResponse
    {
        $payload = $request->getContent();
        $sigHeader = $request->header('Stripe-Signature');
        $endpointSecret = config('services.stripe.webhook_secret');

        try {
            // Verify webhook signature if secret is configured
            if ($endpointSecret) {
                $event = \Stripe\Webhook::constructEvent(
                    $payload,
                    $sigHeader,
                    $endpointSecret
                );
            } else {
                $event = json_decode($payload, false);
            }
        } catch (\UnexpectedValueException $e) {
            Log::error('Stripe webhook: Invalid payload', ['error' => $e->getMessage()]);
            return response()->json(['error' => 'Invalid payload'], 400);
        } catch (\Stripe\Exception\SignatureVerificationException $e) {
            Log::error('Stripe webhook: Invalid signature', ['error' => $e->getMessage()]);
            return response()->json(['error' => 'Invalid signature'], 400);
        }

        // Handle the event
        switch ($event->type ?? $event['type'] ?? '') {
            case 'checkout.session.completed':
                $this->handleCheckoutCompleted($event->data->object ?? $event['data']['object']);
                break;

            case 'customer.subscription.created':
            case 'customer.subscription.updated':
                $this->handleSubscriptionUpdated($event->data->object ?? $event['data']['object']);
                break;

            case 'customer.subscription.deleted':
                $this->handleSubscriptionCanceled($event->data->object ?? $event['data']['object']);
                break;

            case 'invoice.paid':
                $this->handleInvoicePaid($event->data->object ?? $event['data']['object']);
                break;

            case 'invoice.payment_failed':
                $this->handlePaymentFailed($event->data->object ?? $event['data']['object']);
                break;

            default:
                Log::info('Stripe webhook: Unhandled event type', ['type' => $event->type ?? 'unknown']);
        }

        return response()->json(['received' => true]);
    }

    /**
     * Handle checkout completed
     */
    private function handleCheckoutCompleted($session): void
    {
        Log::info('Stripe checkout completed', [
            'session_id' => $session->id ?? $session['id'] ?? null,
            'customer' => $session->customer ?? $session['customer'] ?? null,
        ]);

        // Implement checkout completion logic
        // - Activate subscription
        // - Send confirmation email
    }

    /**
     * Handle subscription updated
     */
    private function handleSubscriptionUpdated($subscription): void
    {
        Log::info('Stripe subscription updated', [
            'subscription_id' => $subscription->id ?? $subscription['id'] ?? null,
            'status' => $subscription->status ?? $subscription['status'] ?? null,
        ]);

        // Implement subscription update logic
        // - Update user's subscription status
        // - Update features/quotas
    }

    /**
     * Handle subscription canceled
     */
    private function handleSubscriptionCanceled($subscription): void
    {
        Log::info('Stripe subscription canceled', [
            'subscription_id' => $subscription->id ?? $subscription['id'] ?? null,
        ]);

        // Implement subscription cancellation logic
        // - Revoke access
        // - Send notification
    }

    /**
     * Handle invoice paid
     */
    private function handleInvoicePaid($invoice): void
    {
        Log::info('Stripe invoice paid', [
            'invoice_id' => $invoice->id ?? $invoice['id'] ?? null,
            'amount' => $invoice->amount_paid ?? $invoice['amount_paid'] ?? null,
        ]);

        // Implement invoice payment logic
        // - Record payment
        // - Generate receipt
    }

    /**
     * Handle payment failed
     */
    private function handlePaymentFailed($invoice): void
    {
        Log::warning('Stripe payment failed', [
            'invoice_id' => $invoice->id ?? $invoice['id'] ?? null,
            'customer' => $invoice->customer ?? $invoice['customer'] ?? null,
        ]);

        // Implement payment failure logic
        // - Send notification to user
        // - Retry logic
    }
}
