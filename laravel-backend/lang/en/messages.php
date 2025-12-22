<?php

return [
    // General
    'success' => 'Success',
    'error' => 'An error occurred',
    'created' => 'Created successfully',
    'updated' => 'Updated successfully',
    'deleted' => 'Deleted successfully',
    'not_found' => 'Not found',
    'unauthorized' => 'Unauthorized access',
    'forbidden' => 'Access forbidden',
    'validation_failed' => 'Validation failed',

    // Auth
    'auth' => [
        'login_success' => 'Login successful',
        'logout_success' => 'Logout successful',
        'register_success' => 'Registration successful',
        'invalid_credentials' => 'Invalid email or password',
        'account_disabled' => 'Account is disabled',
        'password_reset_sent' => 'Password reset link sent to email',
        'password_reset_success' => 'Password changed successfully',
        'token_expired' => 'Token has expired',
        'token_invalid' => 'Invalid token',
    ],

    // Brands
    'brands' => [
        'created' => 'Brand created successfully',
        'updated' => 'Brand updated successfully',
        'deleted' => 'Brand deleted successfully',
        'not_found' => 'Brand not found',
        'limit_reached' => 'Maximum brand limit reached',
    ],

    // Posts
    'posts' => [
        'created' => 'Post created successfully',
        'updated' => 'Post updated successfully',
        'deleted' => 'Post deleted successfully',
        'scheduled' => 'Post scheduled successfully',
        'published' => 'Post published successfully',
        'publish_failed' => 'Failed to publish post',
        'not_found' => 'Post not found',
        'limit_reached' => 'Monthly post limit reached',
    ],

    // Campaigns
    'campaigns' => [
        'created' => 'Campaign created successfully',
        'updated' => 'Campaign updated successfully',
        'deleted' => 'Campaign deleted successfully',
        'started' => 'Campaign started successfully',
        'paused' => 'Campaign paused successfully',
        'stopped' => 'Campaign stopped successfully',
        'not_found' => 'Campaign not found',
    ],

    // Social Accounts
    'social_accounts' => [
        'connected' => 'Account connected successfully',
        'disconnected' => 'Account disconnected successfully',
        'refreshed' => 'Token refreshed successfully',
        'connection_failed' => 'Failed to connect account',
        'token_expired' => 'Token expired. Please reconnect',
        'not_found' => 'Social account not found',
    ],

    // AI Content
    'ai' => [
        'content_generated' => 'Content generated successfully',
        'image_generated' => 'Image generated successfully',
        'generation_failed' => 'Generation failed',
        'limit_reached' => 'AI generation limit reached',
        'provider_unavailable' => 'AI provider unavailable',
    ],

    // Rentals
    'rentals' => [
        'created' => 'Package subscription created',
        'activated' => 'Package activated successfully',
        'cancelled' => 'Package cancelled successfully',
        'renewed' => 'Package renewed successfully',
        'expired' => 'Package has expired',
        'no_active_rental' => 'No active package subscription',
        'payment_pending' => 'Payment pending',
        'payment_confirmed' => 'Payment confirmed',
        'payment_rejected' => 'Payment rejected',
    ],

    // Workflows
    'workflows' => [
        'created' => 'Workflow created successfully',
        'updated' => 'Workflow updated successfully',
        'deleted' => 'Workflow deleted successfully',
        'executed' => 'Workflow executed successfully',
        'execution_failed' => 'Workflow execution failed',
        'not_found' => 'Workflow not found',
    ],

    // Comments
    'comments' => [
        'replied' => 'Comment replied successfully',
        'reply_failed' => 'Failed to reply to comment',
        'analyzed' => 'Comment analyzed successfully',
        'skipped' => 'Comment skipped',
    ],

    // Roles
    'roles' => [
        'created' => 'Role created successfully',
        'updated' => 'Role updated successfully',
        'deleted' => 'Role deleted successfully',
        'assigned' => 'Role assigned successfully',
        'removed' => 'Role removed from user',
        'protected' => 'This role cannot be modified',
    ],

    // Payments
    'payments' => [
        'approved' => 'Payment approved successfully',
        'rejected' => 'Payment rejected successfully',
        'refunded' => 'Refund processed successfully',
        'slip_uploaded' => 'Payment slip uploaded successfully',
    ],

    // AI Manager
    'ai_manager' => [
        'connected' => 'AI Manager connected',
        'disconnected' => 'AI Manager is not responding',
        'started' => 'AI Manager started successfully',
        'stopped' => 'AI Manager stopped successfully',
    ],
];
