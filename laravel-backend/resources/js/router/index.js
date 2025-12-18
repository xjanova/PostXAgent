import { createRouter, createWebHistory } from 'vue-router'
import { useAuthStore } from '../stores/auth'

// Layouts
import MainLayout from '../layouts/MainLayout.vue'
import AuthLayout from '../layouts/AuthLayout.vue'
import AdminLayout from '../layouts/AdminLayout.vue'

// Auth Views
import Login from '../views/auth/Login.vue'
import Register from '../views/auth/Register.vue'
import ForgotPassword from '../views/auth/ForgotPassword.vue'

// User Views
import Dashboard from '../views/user/Dashboard.vue'
import Settings from '../views/user/Settings.vue'
import Subscription from '../views/user/Subscription.vue'
import Brands from '../views/user/Brands.vue'
import Campaigns from '../views/user/Campaigns.vue'
import Posts from '../views/user/Posts.vue'
import SocialAccounts from '../views/user/SocialAccounts.vue'
import Analytics from '../views/user/Analytics.vue'
import WebLearning from '../views/user/WebLearning.vue'
import AITools from '../views/user/AITools.vue'

// Admin Views
import AdminDashboard from '../views/admin/Dashboard.vue'
import AdminUsers from '../views/admin/Users.vue'
import AdminPayments from '../views/admin/Payments.vue'
import AdminRentals from '../views/admin/Rentals.vue'
import AdminPromoCodes from '../views/admin/PromoCodes.vue'
import AdminSystem from '../views/admin/System.vue'

// Error Pages
import NotFound from '../views/NotFound.vue'

const routes = [
    // Auth Routes (ไม่ต้อง login)
    {
        path: '/auth',
        component: AuthLayout,
        meta: { guest: true },
        children: [
            { path: 'login', name: 'login', component: Login },
            { path: 'register', name: 'register', component: Register },
            { path: 'forgot-password', name: 'forgot-password', component: ForgotPassword },
        ]
    },

    // Main App Routes (ต้อง login)
    {
        path: '/',
        component: MainLayout,
        meta: { requiresAuth: true },
        children: [
            { path: '', name: 'dashboard', component: Dashboard },
            { path: 'settings', name: 'settings', component: Settings },
            { path: 'subscription', name: 'subscription', component: Subscription },
            { path: 'brands', name: 'brands', component: Brands },
            { path: 'campaigns', name: 'campaigns', component: Campaigns },
            { path: 'posts', name: 'posts', component: Posts },
            { path: 'social-accounts', name: 'social-accounts', component: SocialAccounts },
            { path: 'analytics', name: 'analytics', component: Analytics },
            { path: 'web-learning', name: 'web-learning', component: WebLearning, meta: { feature: 'web_learning' } },
            { path: 'ai-tools', name: 'ai-tools', component: AITools, meta: { feature: 'ai_tools' } },
        ]
    },

    // Admin Routes (ต้องเป็น admin)
    {
        path: '/admin',
        component: AdminLayout,
        meta: { requiresAuth: true, requiresAdmin: true },
        children: [
            { path: '', name: 'admin-dashboard', component: AdminDashboard },
            { path: 'users', name: 'admin-users', component: AdminUsers },
            { path: 'payments', name: 'admin-payments', component: AdminPayments },
            { path: 'rentals', name: 'admin-rentals', component: AdminRentals },
            { path: 'promo-codes', name: 'admin-promo-codes', component: AdminPromoCodes },
            { path: 'system', name: 'admin-system', component: AdminSystem },
        ]
    },

    // 404 Page
    { path: '/:pathMatch(.*)*', name: 'not-found', component: NotFound }
]

const router = createRouter({
    history: createWebHistory(),
    routes
})

// Navigation Guards
router.beforeEach(async (to, from, next) => {
    const authStore = useAuthStore()

    // ตรวจสอบ token เมื่อเริ่มต้น
    if (!authStore.initialized) {
        await authStore.initAuth()
    }

    const requiresAuth = to.matched.some(record => record.meta.requiresAuth)
    const requiresAdmin = to.matched.some(record => record.meta.requiresAdmin)
    const isGuest = to.matched.some(record => record.meta.guest)
    const requiredFeature = to.meta.feature

    // ถ้าเป็น guest route และ login แล้ว -> ไป dashboard
    if (isGuest && authStore.isAuthenticated) {
        return next({ name: 'dashboard' })
    }

    // ถ้าต้อง login แต่ยังไม่ login -> ไป login
    if (requiresAuth && !authStore.isAuthenticated) {
        return next({ name: 'login', query: { redirect: to.fullPath } })
    }

    // ถ้าต้องเป็น admin แต่ไม่ใช่ -> ไป dashboard
    if (requiresAdmin && !authStore.isAdmin) {
        return next({ name: 'dashboard' })
    }

    // ตรวจสอบ feature access ตาม package
    if (requiredFeature && !authStore.canAccess(requiredFeature)) {
        return next({ name: 'subscription', query: { upgrade: requiredFeature } })
    }

    next()
})

export default router
