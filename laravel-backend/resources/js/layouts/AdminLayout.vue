<template>
    <div class="min-h-screen bg-gray-900">
        <!-- Admin Sidebar -->
        <aside
            :class="[
                'fixed inset-y-0 left-0 z-50 w-64 bg-gray-800 transform transition-transform duration-300 ease-in-out lg:translate-x-0',
                sidebarOpen ? 'translate-x-0' : '-translate-x-full'
            ]"
        >
            <!-- Logo -->
            <div class="flex items-center justify-center h-16 bg-red-900">
                <span class="text-xl font-bold text-white">Admin Panel</span>
            </div>

            <!-- Admin Info -->
            <div class="px-4 py-3 bg-gray-700/50 border-b border-gray-700">
                <div class="flex items-center">
                    <div class="w-10 h-10 rounded-full bg-red-600 flex items-center justify-center">
                        <ShieldCheckIcon class="w-6 h-6 text-white" />
                    </div>
                    <div class="ml-3">
                        <p class="text-sm font-medium text-white">{{ authStore.user?.name }}</p>
                        <p class="text-xs text-red-400">Administrator</p>
                    </div>
                </div>
            </div>

            <!-- Navigation -->
            <nav class="mt-4 px-2 space-y-1">
                <router-link
                    v-for="item in menuItems"
                    :key="item.name"
                    :to="item.to"
                    :class="[
                        'group flex items-center px-3 py-2 text-sm font-medium rounded-lg transition-colors',
                        isActive(item.to)
                            ? 'bg-red-600 text-white'
                            : 'text-gray-300 hover:bg-gray-700 hover:text-white'
                    ]"
                >
                    <component :is="item.icon" class="w-5 h-5 mr-3" />
                    {{ item.label }}
                    <span
                        v-if="item.badge"
                        class="ml-auto px-2 py-0.5 text-xs rounded-full bg-yellow-500 text-gray-900"
                    >
                        {{ item.badge }}
                    </span>
                </router-link>

                <!-- Back to User Panel -->
                <div class="pt-4 mt-4 border-t border-gray-700">
                    <router-link
                        to="/"
                        class="group flex items-center px-3 py-2 text-sm font-medium rounded-lg text-gray-300 hover:bg-gray-700 hover:text-white"
                    >
                        <ArrowLeftIcon class="w-5 h-5 mr-3" />
                        กลับหน้าผู้ใช้
                    </router-link>
                </div>
            </nav>
        </aside>

        <!-- Mobile sidebar overlay -->
        <div
            v-if="sidebarOpen"
            class="fixed inset-0 z-40 bg-black/50 lg:hidden"
            @click="sidebarOpen = false"
        ></div>

        <!-- Main content -->
        <div class="lg:pl-64">
            <!-- Top Navigation -->
            <header class="sticky top-0 z-30 flex items-center h-16 px-4 bg-gray-800 border-b border-red-900">
                <button
                    class="lg:hidden p-2 text-gray-400 hover:text-white"
                    @click="sidebarOpen = true"
                >
                    <Bars3Icon class="w-6 h-6" />
                </button>

                <div class="flex-1">
                    <h1 class="text-lg font-semibold text-white">{{ pageTitle }}</h1>
                </div>

                <!-- Quick Stats -->
                <div class="hidden md:flex items-center space-x-6 text-sm">
                    <div class="text-center">
                        <p class="text-gray-400">รอยืนยัน</p>
                        <p class="text-yellow-400 font-bold">{{ stats.pendingPayments }}</p>
                    </div>
                    <div class="text-center">
                        <p class="text-gray-400">รายได้วันนี้</p>
                        <p class="text-green-400 font-bold">{{ formatCurrency(stats.revenueToday) }}</p>
                    </div>
                    <div class="text-center">
                        <p class="text-gray-400">ผู้ใช้ Active</p>
                        <p class="text-blue-400 font-bold">{{ stats.activeUsers }}</p>
                    </div>
                </div>

                <!-- User Menu -->
                <button
                    class="ml-4 p-2 text-gray-400 hover:text-white"
                    @click="handleLogout"
                >
                    <ArrowRightOnRectangleIcon class="w-6 h-6" />
                </button>
            </header>

            <!-- Page Content -->
            <main class="p-6">
                <router-view />
            </main>
        </div>
    </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { adminApi } from '../services/api'
import {
    Bars3Icon,
    ArrowLeftIcon,
    ArrowRightOnRectangleIcon,
    ShieldCheckIcon,
    HomeIcon,
    UsersIcon,
    BanknotesIcon,
    CreditCardIcon,
    TicketIcon,
    ServerIcon,
    ChartBarIcon,
    DocumentChartBarIcon,
} from '@heroicons/vue/24/outline'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const sidebarOpen = ref(false)
const stats = ref({
    pendingPayments: 0,
    revenueToday: 0,
    activeUsers: 0,
})

const menuItems = computed(() => [
    { name: 'admin-dashboard', label: 'ภาพรวม', to: '/admin', icon: HomeIcon },
    { name: 'admin-users', label: 'จัดการผู้ใช้', to: '/admin/users', icon: UsersIcon },
    { name: 'admin-payments', label: 'การชำระเงิน', to: '/admin/payments', icon: BanknotesIcon, badge: stats.value.pendingPayments || null },
    { name: 'admin-rentals', label: 'การเช่าแพ็กเกจ', to: '/admin/rentals', icon: CreditCardIcon },
    { name: 'admin-promo-codes', label: 'โค้ดส่วนลด', to: '/admin/promo-codes', icon: TicketIcon },
    { name: 'admin-reports', label: 'รายงาน', to: '/admin/reports', icon: DocumentChartBarIcon },
    { name: 'admin-system', label: 'ตั้งค่าระบบ', to: '/admin/system', icon: ServerIcon },
])

const pageTitle = computed(() => {
    const item = menuItems.value.find(m => isActive(m.to))
    return item?.label || 'Admin'
})

const isActive = (to) => {
    if (to === '/admin') return route.path === '/admin'
    return route.path.startsWith(to)
}

const formatCurrency = (amount) => {
    return new Intl.NumberFormat('th-TH', {
        style: 'currency',
        currency: 'THB',
        minimumFractionDigits: 0
    }).format(amount || 0)
}

const handleLogout = () => {
    authStore.logout()
    router.push({ name: 'login' })
}

const fetchStats = async () => {
    try {
        const response = await adminApi.stats()
        if (response.data.success) {
            const data = response.data.data
            stats.value.pendingPayments = data.payments?.pending || 0
            stats.value.revenueToday = data.revenue?.today || 0
            stats.value.activeUsers = data.rentals?.active || 0
        }
    } catch (error) {
        console.error('Failed to fetch stats:', error)
    }
}

onMounted(() => {
    fetchStats()
})
</script>
