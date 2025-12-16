<template>
    <div class="space-y-6">
        <!-- Header -->
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between">
            <div>
                <h1 class="text-2xl font-bold text-white">แดชบอร์ด</h1>
                <p class="text-gray-400 mt-1">ยินดีต้อนรับ {{ authStore.user?.name }}</p>
            </div>
            <div class="mt-4 sm:mt-0 flex space-x-3">
                <router-link
                    to="/posts"
                    class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg transition-colors flex items-center"
                >
                    <PlusIcon class="w-5 h-5 mr-2" />
                    สร้างโพสต์
                </router-link>
            </div>
        </div>

        <!-- Package Warning -->
        <div v-if="!authStore.hasActiveRental" class="p-4 bg-yellow-500/20 border border-yellow-500/50 rounded-lg">
            <div class="flex items-start">
                <ExclamationTriangleIcon class="w-6 h-6 text-yellow-400 mr-3 flex-shrink-0" />
                <div>
                    <h3 class="font-medium text-yellow-400">ไม่มีแพ็กเกจที่ใช้งานอยู่</h3>
                    <p class="text-sm text-yellow-400/80 mt-1">
                        คุณยังไม่มีแพ็กเกจที่ใช้งาน กรุณาเลือกแพ็กเกจเพื่อเริ่มใช้งาน
                    </p>
                    <router-link
                        to="/subscription"
                        class="inline-flex items-center mt-2 text-sm text-yellow-400 hover:text-yellow-300"
                    >
                        ดูแพ็กเกจทั้งหมด &rarr;
                    </router-link>
                </div>
            </div>
        </div>

        <!-- Stats Cards -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-gray-400 text-sm">โพสต์ทั้งหมด</p>
                        <p class="text-2xl font-bold text-white mt-1">{{ stats.totalPosts }}</p>
                    </div>
                    <div class="w-12 h-12 rounded-lg bg-indigo-600/20 flex items-center justify-center">
                        <DocumentTextIcon class="w-6 h-6 text-indigo-400" />
                    </div>
                </div>
                <div class="mt-3 flex items-center text-sm">
                    <span class="text-green-400">+{{ stats.postsThisWeek }}</span>
                    <span class="text-gray-500 ml-1">สัปดาห์นี้</span>
                </div>
            </div>

            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-gray-400 text-sm">การมีส่วนร่วม</p>
                        <p class="text-2xl font-bold text-white mt-1">{{ formatNumber(stats.totalEngagement) }}</p>
                    </div>
                    <div class="w-12 h-12 rounded-lg bg-green-600/20 flex items-center justify-center">
                        <HeartIcon class="w-6 h-6 text-green-400" />
                    </div>
                </div>
                <div class="mt-3 flex items-center text-sm">
                    <span :class="stats.engagementChange >= 0 ? 'text-green-400' : 'text-red-400'">
                        {{ stats.engagementChange >= 0 ? '+' : '' }}{{ stats.engagementChange }}%
                    </span>
                    <span class="text-gray-500 ml-1">จากสัปดาห์ก่อน</span>
                </div>
            </div>

            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-gray-400 text-sm">แคมเปญ Active</p>
                        <p class="text-2xl font-bold text-white mt-1">{{ stats.activeCampaigns }}</p>
                    </div>
                    <div class="w-12 h-12 rounded-lg bg-purple-600/20 flex items-center justify-center">
                        <MegaphoneIcon class="w-6 h-6 text-purple-400" />
                    </div>
                </div>
                <div class="mt-3 flex items-center text-sm">
                    <span class="text-gray-500">จาก {{ stats.totalCampaigns }} แคมเปญ</span>
                </div>
            </div>

            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-gray-400 text-sm">บัญชีโซเชียล</p>
                        <p class="text-2xl font-bold text-white mt-1">{{ stats.connectedAccounts }}</p>
                    </div>
                    <div class="w-12 h-12 rounded-lg bg-blue-600/20 flex items-center justify-center">
                        <ShareIcon class="w-6 h-6 text-blue-400" />
                    </div>
                </div>
                <div class="mt-3 flex items-center text-sm">
                    <span class="text-gray-500">จาก 9 แพลตฟอร์ม</span>
                </div>
            </div>
        </div>

        <!-- Usage & Quick Actions -->
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <!-- Usage Stats -->
            <div class="lg:col-span-2 bg-gray-800 rounded-xl p-6 border border-gray-700">
                <h2 class="text-lg font-semibold text-white mb-4">การใช้งานแพ็กเกจ</h2>

                <div v-if="authStore.hasActiveRental" class="space-y-4">
                    <!-- Posts Usage -->
                    <div>
                        <div class="flex justify-between text-sm mb-1">
                            <span class="text-gray-400">โพสต์</span>
                            <span class="text-white">
                                {{ usageData.posts.used }} / {{ usageData.posts.limit === -1 ? 'ไม่จำกัด' : usageData.posts.limit }}
                            </span>
                        </div>
                        <div class="h-2 bg-gray-700 rounded-full overflow-hidden">
                            <div
                                class="h-full bg-indigo-500 rounded-full transition-all"
                                :style="{ width: getUsagePercent(usageData.posts) + '%' }"
                            ></div>
                        </div>
                    </div>

                    <!-- AI Generations Usage -->
                    <div>
                        <div class="flex justify-between text-sm mb-1">
                            <span class="text-gray-400">AI Generations</span>
                            <span class="text-white">
                                {{ usageData.aiGenerations.used }} / {{ usageData.aiGenerations.limit === -1 ? 'ไม่จำกัด' : usageData.aiGenerations.limit }}
                            </span>
                        </div>
                        <div class="h-2 bg-gray-700 rounded-full overflow-hidden">
                            <div
                                class="h-full bg-purple-500 rounded-full transition-all"
                                :style="{ width: getUsagePercent(usageData.aiGenerations) + '%' }"
                            ></div>
                        </div>
                    </div>

                    <!-- Package Info -->
                    <div class="mt-6 p-4 bg-gray-700/50 rounded-lg">
                        <div class="flex items-center justify-between">
                            <div>
                                <p class="text-sm text-gray-400">แพ็กเกจปัจจุบัน</p>
                                <p class="text-lg font-semibold text-white">{{ authStore.currentPackage }}</p>
                            </div>
                            <div class="text-right">
                                <p class="text-sm text-gray-400">เหลืออีก</p>
                                <p class="text-lg font-semibold text-indigo-400">{{ authStore.daysRemaining }} วัน</p>
                            </div>
                        </div>
                    </div>
                </div>

                <div v-else class="text-center py-8">
                    <p class="text-gray-400">ยังไม่มีข้อมูลการใช้งาน</p>
                    <router-link to="/subscription" class="text-indigo-400 hover:text-indigo-300 text-sm">
                        เลือกแพ็กเกจ
                    </router-link>
                </div>
            </div>

            <!-- Quick Actions -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <h2 class="text-lg font-semibold text-white mb-4">ทางลัด</h2>
                <div class="space-y-3">
                    <router-link
                        to="/posts"
                        class="flex items-center p-3 bg-gray-700/50 hover:bg-gray-700 rounded-lg transition-colors"
                    >
                        <div class="w-10 h-10 rounded-lg bg-indigo-600/20 flex items-center justify-center mr-3">
                            <PlusIcon class="w-5 h-5 text-indigo-400" />
                        </div>
                        <div>
                            <p class="text-white font-medium">สร้างโพสต์ใหม่</p>
                            <p class="text-gray-400 text-sm">โพสต์ลงหลายแพลตฟอร์ม</p>
                        </div>
                    </router-link>

                    <router-link
                        to="/ai-tools"
                        class="flex items-center p-3 bg-gray-700/50 hover:bg-gray-700 rounded-lg transition-colors"
                        :class="{ 'opacity-50 pointer-events-none': !authStore.canAccess('ai_tools') }"
                    >
                        <div class="w-10 h-10 rounded-lg bg-purple-600/20 flex items-center justify-center mr-3">
                            <SparklesIcon class="w-5 h-5 text-purple-400" />
                        </div>
                        <div>
                            <p class="text-white font-medium">AI Content Generator</p>
                            <p class="text-gray-400 text-sm">สร้างเนื้อหาด้วย AI</p>
                        </div>
                        <LockClosedIcon v-if="!authStore.canAccess('ai_tools')" class="w-4 h-4 text-gray-500 ml-auto" />
                    </router-link>

                    <router-link
                        to="/social-accounts"
                        class="flex items-center p-3 bg-gray-700/50 hover:bg-gray-700 rounded-lg transition-colors"
                    >
                        <div class="w-10 h-10 rounded-lg bg-blue-600/20 flex items-center justify-center mr-3">
                            <ShareIcon class="w-5 h-5 text-blue-400" />
                        </div>
                        <div>
                            <p class="text-white font-medium">เชื่อมต่อบัญชี</p>
                            <p class="text-gray-400 text-sm">เพิ่มบัญชีโซเชียลมีเดีย</p>
                        </div>
                    </router-link>

                    <router-link
                        to="/web-learning"
                        class="flex items-center p-3 bg-gray-700/50 hover:bg-gray-700 rounded-lg transition-colors"
                        :class="{ 'opacity-50 pointer-events-none': !authStore.canAccess('web_learning') }"
                    >
                        <div class="w-10 h-10 rounded-lg bg-green-600/20 flex items-center justify-center mr-3">
                            <AcademicCapIcon class="w-5 h-5 text-green-400" />
                        </div>
                        <div>
                            <p class="text-white font-medium">เครื่องมือเรียนรู้</p>
                            <p class="text-gray-400 text-sm">บันทึก workflow อัตโนมัติ</p>
                        </div>
                        <LockClosedIcon v-if="!authStore.canAccess('web_learning')" class="w-4 h-4 text-gray-500 ml-auto" />
                    </router-link>
                </div>
            </div>
        </div>

        <!-- Recent Posts -->
        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <div class="flex items-center justify-between mb-4">
                <h2 class="text-lg font-semibold text-white">โพสต์ล่าสุด</h2>
                <router-link to="/posts" class="text-sm text-indigo-400 hover:text-indigo-300">
                    ดูทั้งหมด &rarr;
                </router-link>
            </div>

            <div v-if="recentPosts.length > 0" class="space-y-3">
                <div
                    v-for="post in recentPosts"
                    :key="post.id"
                    class="flex items-center p-3 bg-gray-700/50 rounded-lg"
                >
                    <div class="w-10 h-10 rounded-lg bg-gray-600 flex items-center justify-center mr-3">
                        <component :is="getPlatformIcon(post.platform)" class="w-5 h-5 text-gray-300" />
                    </div>
                    <div class="flex-1 min-w-0">
                        <p class="text-white text-sm truncate">{{ post.content }}</p>
                        <p class="text-gray-400 text-xs mt-1">{{ post.created_at }}</p>
                    </div>
                    <span
                        :class="[
                            'px-2 py-1 text-xs rounded-full',
                            post.status === 'published' ? 'bg-green-500/20 text-green-400' :
                            post.status === 'scheduled' ? 'bg-blue-500/20 text-blue-400' :
                            'bg-gray-500/20 text-gray-400'
                        ]"
                    >
                        {{ getStatusLabel(post.status) }}
                    </span>
                </div>
            </div>

            <div v-else class="text-center py-8">
                <p class="text-gray-400">ยังไม่มีโพสต์</p>
                <router-link to="/posts" class="text-indigo-400 hover:text-indigo-300 text-sm">
                    สร้างโพสต์แรกของคุณ
                </router-link>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { analyticsApi, postApi } from '../../services/api'
import {
    PlusIcon,
    DocumentTextIcon,
    HeartIcon,
    MegaphoneIcon,
    ShareIcon,
    SparklesIcon,
    AcademicCapIcon,
    ExclamationTriangleIcon,
    LockClosedIcon,
} from '@heroicons/vue/24/outline'

const authStore = useAuthStore()

const stats = reactive({
    totalPosts: 0,
    postsThisWeek: 0,
    totalEngagement: 0,
    engagementChange: 0,
    activeCampaigns: 0,
    totalCampaigns: 0,
    connectedAccounts: 0,
})

const usageData = reactive({
    posts: { used: 0, limit: 0 },
    aiGenerations: { used: 0, limit: 0 },
})

const recentPosts = ref([])

const formatNumber = (num) => {
    if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M'
    if (num >= 1000) return (num / 1000).toFixed(1) + 'K'
    return num.toString()
}

const getUsagePercent = (usage) => {
    if (usage.limit === -1) return 0
    if (usage.limit === 0) return 100
    return Math.min(100, Math.round((usage.used / usage.limit) * 100))
}

const getStatusLabel = (status) => {
    const labels = {
        published: 'เผยแพร่แล้ว',
        scheduled: 'ตั้งเวลา',
        draft: 'แบบร่าง',
        failed: 'ล้มเหลว',
    }
    return labels[status] || status
}

const getPlatformIcon = (platform) => {
    // Return appropriate icon based on platform
    return ShareIcon
}

const fetchDashboardData = async () => {
    try {
        // Fetch analytics overview
        const analyticsResponse = await analyticsApi.overview()
        if (analyticsResponse.data.success) {
            const data = analyticsResponse.data.data
            stats.totalPosts = data.total_posts || 0
            stats.postsThisWeek = data.posts_this_week || 0
            stats.totalEngagement = data.total_engagement || 0
            stats.engagementChange = data.engagement_change || 0
            stats.activeCampaigns = data.active_campaigns || 0
            stats.totalCampaigns = data.total_campaigns || 0
            stats.connectedAccounts = data.connected_accounts || 0
        }

        // Fetch usage from rental status
        if (authStore.rental) {
            usageData.posts.used = authStore.usageLimits.posts - (authStore.usageRemaining.posts || 0)
            usageData.posts.limit = authStore.usageLimits.posts
            usageData.aiGenerations.used = authStore.usageLimits.ai_generations - (authStore.usageRemaining.ai_generations || 0)
            usageData.aiGenerations.limit = authStore.usageLimits.ai_generations
        }

        // Fetch recent posts
        const postsResponse = await postApi.list()
        if (postsResponse.data.success) {
            recentPosts.value = postsResponse.data.data.slice(0, 5)
        }
    } catch (error) {
        console.error('Failed to fetch dashboard data:', error)
    }
}

onMounted(() => {
    fetchDashboardData()
})
</script>
