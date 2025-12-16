<template>
    <div class="min-h-screen bg-gray-900">
        <!-- Sidebar -->
        <aside
            :class="[
                'fixed inset-y-0 left-0 z-50 w-64 bg-gray-800 transform transition-transform duration-300 ease-in-out lg:translate-x-0',
                sidebarOpen ? 'translate-x-0' : '-translate-x-full'
            ]"
        >
            <!-- Logo -->
            <div class="flex items-center justify-center h-16 bg-gray-900">
                <span class="text-xl font-bold text-white">PostXAgent</span>
            </div>

            <!-- User Package Info -->
            <div class="px-4 py-3 bg-gray-700/50 border-b border-gray-700">
                <div class="flex items-center">
                    <div class="w-10 h-10 rounded-full bg-indigo-600 flex items-center justify-center">
                        <span class="text-white font-medium">{{ userInitials }}</span>
                    </div>
                    <div class="ml-3">
                        <p class="text-sm font-medium text-white">{{ authStore.user?.name }}</p>
                        <p class="text-xs text-indigo-400">{{ authStore.currentPackage }}</p>
                    </div>
                </div>
                <div v-if="authStore.hasActiveRental" class="mt-2">
                    <div class="flex justify-between text-xs text-gray-400">
                        <span>เหลือ {{ authStore.daysRemaining }} วัน</span>
                        <span>{{ usagePercent }}% ใช้งาน</span>
                    </div>
                    <div class="mt-1 h-1.5 bg-gray-600 rounded-full overflow-hidden">
                        <div
                            class="h-full bg-indigo-500 rounded-full transition-all"
                            :style="{ width: usagePercent + '%' }"
                        ></div>
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
                            ? 'bg-indigo-600 text-white'
                            : 'text-gray-300 hover:bg-gray-700 hover:text-white',
                        item.disabled ? 'opacity-50 cursor-not-allowed pointer-events-none' : ''
                    ]"
                >
                    <component :is="item.icon" class="w-5 h-5 mr-3" />
                    {{ item.label }}
                    <span
                        v-if="item.badge"
                        class="ml-auto px-2 py-0.5 text-xs rounded-full bg-indigo-500"
                    >
                        {{ item.badge }}
                    </span>
                    <LockClosedIcon v-if="item.locked" class="w-4 h-4 ml-auto text-gray-500" />
                </router-link>

                <!-- Admin Section -->
                <div v-if="authStore.isAdmin" class="pt-4 mt-4 border-t border-gray-700">
                    <p class="px-3 text-xs font-semibold text-gray-500 uppercase">Admin</p>
                    <router-link
                        v-for="item in adminMenuItems"
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
            <header class="sticky top-0 z-30 flex items-center h-16 px-4 bg-gray-800 border-b border-gray-700">
                <button
                    class="lg:hidden p-2 text-gray-400 hover:text-white"
                    @click="sidebarOpen = true"
                >
                    <Bars3Icon class="w-6 h-6" />
                </button>

                <div class="flex-1"></div>

                <!-- Quick Actions -->
                <div class="flex items-center space-x-4">
                    <!-- AI Manager Status -->
                    <div class="flex items-center">
                        <span
                            :class="[
                                'w-2.5 h-2.5 rounded-full mr-2',
                                aiStatus === 'online' ? 'bg-green-500' : 'bg-red-500'
                            ]"
                        ></span>
                        <span class="text-sm text-gray-400">AI Manager</span>
                    </div>

                    <!-- Notifications -->
                    <button class="p-2 text-gray-400 hover:text-white relative">
                        <BellIcon class="w-6 h-6" />
                        <span v-if="notificationCount" class="absolute top-0 right-0 w-4 h-4 text-xs bg-red-500 rounded-full flex items-center justify-center text-white">
                            {{ notificationCount }}
                        </span>
                    </button>

                    <!-- User Menu -->
                    <div class="relative" ref="userMenuRef">
                        <button
                            class="flex items-center p-1 rounded-lg hover:bg-gray-700"
                            @click="userMenuOpen = !userMenuOpen"
                        >
                            <div class="w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center">
                                <span class="text-white text-sm font-medium">{{ userInitials }}</span>
                            </div>
                            <ChevronDownIcon class="w-4 h-4 ml-1 text-gray-400" />
                        </button>

                        <transition
                            enter-active-class="transition ease-out duration-100"
                            enter-from-class="transform opacity-0 scale-95"
                            enter-to-class="transform opacity-100 scale-100"
                            leave-active-class="transition ease-in duration-75"
                            leave-from-class="transform opacity-100 scale-100"
                            leave-to-class="transform opacity-0 scale-95"
                        >
                            <div
                                v-if="userMenuOpen"
                                class="absolute right-0 mt-2 w-48 py-1 bg-gray-800 rounded-lg shadow-lg border border-gray-700"
                            >
                                <router-link
                                    to="/settings"
                                    class="block px-4 py-2 text-sm text-gray-300 hover:bg-gray-700"
                                    @click="userMenuOpen = false"
                                >
                                    ตั้งค่าบัญชี
                                </router-link>
                                <router-link
                                    to="/subscription"
                                    class="block px-4 py-2 text-sm text-gray-300 hover:bg-gray-700"
                                    @click="userMenuOpen = false"
                                >
                                    แพ็กเกจของฉัน
                                </router-link>
                                <hr class="my-1 border-gray-700" />
                                <button
                                    class="w-full text-left px-4 py-2 text-sm text-red-400 hover:bg-gray-700"
                                    @click="handleLogout"
                                >
                                    ออกจากระบบ
                                </button>
                            </div>
                        </transition>
                    </div>
                </div>
            </header>

            <!-- Page Content -->
            <main class="p-6">
                <router-view />
            </main>
        </div>
    </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { aiManagerApi } from '../services/api'
import {
    Bars3Icon,
    BellIcon,
    ChevronDownIcon,
    HomeIcon,
    Cog6ToothIcon,
    CreditCardIcon,
    BuildingStorefrontIcon,
    MegaphoneIcon,
    DocumentTextIcon,
    ShareIcon,
    ChartBarIcon,
    AcademicCapIcon,
    SparklesIcon,
    UsersIcon,
    BanknotesIcon,
    TicketIcon,
    ServerIcon,
    LockClosedIcon,
} from '@heroicons/vue/24/outline'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const sidebarOpen = ref(false)
const userMenuOpen = ref(false)
const userMenuRef = ref(null)
const aiStatus = ref('offline')
const notificationCount = ref(0)

const userInitials = computed(() => {
    const name = authStore.user?.name || ''
    return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
})

const usagePercent = computed(() => {
    const limits = authStore.usageLimits
    const remaining = authStore.usageRemaining
    if (!limits.posts || limits.posts === -1) return 0
    const used = limits.posts - (remaining.posts || 0)
    return Math.min(100, Math.round((used / limits.posts) * 100))
})

const menuItems = computed(() => [
    { name: 'dashboard', label: 'แดชบอร์ด', to: '/', icon: HomeIcon },
    { name: 'brands', label: 'แบรนด์', to: '/brands', icon: BuildingStorefrontIcon },
    { name: 'campaigns', label: 'แคมเปญ', to: '/campaigns', icon: MegaphoneIcon },
    { name: 'posts', label: 'โพสต์', to: '/posts', icon: DocumentTextIcon, badge: authStore.usageRemaining.posts },
    { name: 'social-accounts', label: 'บัญชีโซเชียล', to: '/social-accounts', icon: ShareIcon },
    { name: 'analytics', label: 'วิเคราะห์', to: '/analytics', icon: ChartBarIcon, disabled: !authStore.canAccess('analytics'), locked: !authStore.canAccess('analytics') },
    { name: 'web-learning', label: 'เครื่องมือเรียนรู้', to: '/web-learning', icon: AcademicCapIcon, disabled: !authStore.canAccess('web_learning'), locked: !authStore.canAccess('web_learning') },
    { name: 'ai-tools', label: 'AI Tools', to: '/ai-tools', icon: SparklesIcon, disabled: !authStore.canAccess('ai_tools'), locked: !authStore.canAccess('ai_tools') },
    { name: 'subscription', label: 'แพ็กเกจ', to: '/subscription', icon: CreditCardIcon },
    { name: 'settings', label: 'ตั้งค่า', to: '/settings', icon: Cog6ToothIcon },
])

const adminMenuItems = [
    { name: 'admin-dashboard', label: 'ภาพรวม', to: '/admin', icon: ChartBarIcon },
    { name: 'admin-users', label: 'ผู้ใช้', to: '/admin/users', icon: UsersIcon },
    { name: 'admin-payments', label: 'การชำระเงิน', to: '/admin/payments', icon: BanknotesIcon },
    { name: 'admin-rentals', label: 'การเช่า', to: '/admin/rentals', icon: CreditCardIcon },
    { name: 'admin-promo-codes', label: 'โค้ดส่วนลด', to: '/admin/promo-codes', icon: TicketIcon },
    { name: 'admin-system', label: 'ระบบ', to: '/admin/system', icon: ServerIcon },
]

const isActive = (to) => {
    if (to === '/') return route.path === '/'
    return route.path.startsWith(to)
}

const handleLogout = () => {
    authStore.logout()
    router.push({ name: 'login' })
}

const checkAIStatus = async () => {
    try {
        const response = await aiManagerApi.status()
        aiStatus.value = response.data.is_online ? 'online' : 'offline'
    } catch {
        aiStatus.value = 'offline'
    }
}

// Close user menu on click outside
const handleClickOutside = (event) => {
    if (userMenuRef.value && !userMenuRef.value.contains(event.target)) {
        userMenuOpen.value = false
    }
}

let statusInterval = null

onMounted(() => {
    checkAIStatus()
    statusInterval = setInterval(checkAIStatus, 30000)
    document.addEventListener('click', handleClickOutside)
})

onUnmounted(() => {
    if (statusInterval) clearInterval(statusInterval)
    document.removeEventListener('click', handleClickOutside)
})
</script>
