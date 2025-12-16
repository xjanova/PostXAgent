<template>
    <div class="space-y-6">
        <div class="flex items-center justify-between">
            <h1 class="text-2xl font-bold text-white">รายงาน</h1>
            <div class="flex space-x-3">
                <select v-model="dateRange" class="px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white">
                    <option value="7d">7 วันล่าสุด</option>
                    <option value="30d">30 วันล่าสุด</option>
                    <option value="90d">90 วันล่าสุด</option>
                    <option value="1y">1 ปี</option>
                    <option value="all">ทั้งหมด</option>
                </select>
                <button @click="exportReport" class="px-4 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg transition-colors">
                    <ArrowDownTrayIcon class="w-5 h-5 inline mr-1" />
                    ส่งออก Excel
                </button>
            </div>
        </div>

        <!-- Summary Stats -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div class="bg-gradient-to-r from-green-600 to-green-700 rounded-xl p-5">
                <p class="text-green-200 text-sm">รายได้รวม</p>
                <p class="text-3xl font-bold text-white mt-1">{{ formatCurrency(summaryStats.totalRevenue) }}</p>
                <p class="text-green-200 text-sm mt-2">
                    <span :class="summaryStats.revenueGrowth >= 0 ? 'text-green-300' : 'text-red-300'">
                        {{ summaryStats.revenueGrowth >= 0 ? '+' : '' }}{{ summaryStats.revenueGrowth }}%
                    </span>
                    จากช่วงก่อนหน้า
                </p>
            </div>
            <div class="bg-gradient-to-r from-blue-600 to-blue-700 rounded-xl p-5">
                <p class="text-blue-200 text-sm">ผู้ใช้ใหม่</p>
                <p class="text-3xl font-bold text-white mt-1">{{ summaryStats.newUsers }}</p>
                <p class="text-blue-200 text-sm mt-2">
                    <span :class="summaryStats.userGrowth >= 0 ? 'text-green-300' : 'text-red-300'">
                        {{ summaryStats.userGrowth >= 0 ? '+' : '' }}{{ summaryStats.userGrowth }}%
                    </span>
                    จากช่วงก่อนหน้า
                </p>
            </div>
            <div class="bg-gradient-to-r from-purple-600 to-purple-700 rounded-xl p-5">
                <p class="text-purple-200 text-sm">การเช่าใหม่</p>
                <p class="text-3xl font-bold text-white mt-1">{{ summaryStats.newRentals }}</p>
                <p class="text-purple-200 text-sm mt-2">
                    <span :class="summaryStats.rentalGrowth >= 0 ? 'text-green-300' : 'text-red-300'">
                        {{ summaryStats.rentalGrowth >= 0 ? '+' : '' }}{{ summaryStats.rentalGrowth }}%
                    </span>
                    จากช่วงก่อนหน้า
                </p>
            </div>
            <div class="bg-gradient-to-r from-orange-600 to-orange-700 rounded-xl p-5">
                <p class="text-orange-200 text-sm">โพสต์ทั้งหมด</p>
                <p class="text-3xl font-bold text-white mt-1">{{ summaryStats.totalPosts }}</p>
                <p class="text-orange-200 text-sm mt-2">
                    <span :class="summaryStats.postGrowth >= 0 ? 'text-green-300' : 'text-red-300'">
                        {{ summaryStats.postGrowth >= 0 ? '+' : '' }}{{ summaryStats.postGrowth }}%
                    </span>
                    จากช่วงก่อนหน้า
                </p>
            </div>
        </div>

        <!-- Charts Row -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Revenue Chart -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <h2 class="text-lg font-semibold text-white mb-4">รายได้ตามวัน</h2>
                <div class="h-64 flex items-end space-x-2">
                    <div v-for="(day, index) in revenueData" :key="index" class="flex-1 flex flex-col items-center">
                        <div class="w-full bg-green-500 rounded-t" :style="{ height: getChartHeight(day.amount, maxRevenue) + '%' }"></div>
                        <span class="text-gray-500 text-xs mt-2">{{ day.label }}</span>
                    </div>
                </div>
            </div>

            <!-- Users Chart -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <h2 class="text-lg font-semibold text-white mb-4">ผู้ใช้ใหม่ตามวัน</h2>
                <div class="h-64 flex items-end space-x-2">
                    <div v-for="(day, index) in usersData" :key="index" class="flex-1 flex flex-col items-center">
                        <div class="w-full bg-blue-500 rounded-t" :style="{ height: getChartHeight(day.count, maxUsers) + '%' }"></div>
                        <span class="text-gray-500 text-xs mt-2">{{ day.label }}</span>
                    </div>
                </div>
            </div>
        </div>

        <!-- Package Distribution -->
        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <h2 class="text-lg font-semibold text-white mb-4">การกระจายแพ็กเกจ</h2>
                <div class="space-y-3">
                    <div v-for="pkg in packageDistribution" :key="pkg.name" class="flex items-center">
                        <div class="w-24 text-gray-400 text-sm">{{ pkg.name }}</div>
                        <div class="flex-1 h-4 bg-gray-700 rounded-full mx-3 overflow-hidden">
                            <div class="h-full rounded-full" :class="pkg.color" :style="{ width: pkg.percentage + '%' }"></div>
                        </div>
                        <div class="w-16 text-right text-white text-sm">{{ pkg.count }}</div>
                        <div class="w-12 text-right text-gray-400 text-sm">{{ pkg.percentage }}%</div>
                    </div>
                </div>
            </div>

            <!-- Payment Methods -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <h2 class="text-lg font-semibold text-white mb-4">ช่องทางชำระเงิน</h2>
                <div class="space-y-3">
                    <div v-for="method in paymentMethods" :key="method.name" class="flex items-center">
                        <div class="w-24 text-gray-400 text-sm">{{ method.name }}</div>
                        <div class="flex-1 h-4 bg-gray-700 rounded-full mx-3 overflow-hidden">
                            <div class="h-full rounded-full" :class="method.color" :style="{ width: method.percentage + '%' }"></div>
                        </div>
                        <div class="w-16 text-right text-white text-sm">{{ formatCurrency(method.amount) }}</div>
                        <div class="w-12 text-right text-gray-400 text-sm">{{ method.percentage }}%</div>
                    </div>
                </div>
            </div>

            <!-- Platform Usage -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <h2 class="text-lg font-semibold text-white mb-4">การใช้งานแพลตฟอร์ม</h2>
                <div class="space-y-3">
                    <div v-for="platform in platformUsage" :key="platform.name" class="flex items-center">
                        <div class="w-24 text-gray-400 text-sm">{{ platform.name }}</div>
                        <div class="flex-1 h-4 bg-gray-700 rounded-full mx-3 overflow-hidden">
                            <div class="h-full rounded-full" :class="platform.color" :style="{ width: platform.percentage + '%' }"></div>
                        </div>
                        <div class="w-16 text-right text-white text-sm">{{ platform.posts }}</div>
                        <div class="w-12 text-right text-gray-400 text-sm">{{ platform.percentage }}%</div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Top Users Table -->
        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-4">ผู้ใช้ที่ใช้งานมากที่สุด</h2>
            <table class="w-full">
                <thead class="bg-gray-700/50">
                    <tr>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">#</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">ผู้ใช้</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">แพ็กเกจ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">โพสต์</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">AI Generations</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">ยอดชำระ</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-gray-700">
                    <tr v-for="(user, index) in topUsers" :key="user.id" class="hover:bg-gray-700/50">
                        <td class="px-4 py-3 text-gray-400">{{ index + 1 }}</td>
                        <td class="px-4 py-3">
                            <div class="flex items-center">
                                <div class="w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center mr-2">
                                    <span class="text-white text-xs">{{ user.name[0] }}</span>
                                </div>
                                <div>
                                    <p class="text-white text-sm">{{ user.name }}</p>
                                    <p class="text-gray-500 text-xs">{{ user.email }}</p>
                                </div>
                            </div>
                        </td>
                        <td class="px-4 py-3">
                            <span class="px-2 py-1 bg-indigo-500/20 text-indigo-400 text-xs rounded-full">{{ user.package }}</span>
                        </td>
                        <td class="px-4 py-3 text-white">{{ user.posts }}</td>
                        <td class="px-4 py-3 text-white">{{ user.ai_generations }}</td>
                        <td class="px-4 py-3 text-green-400 font-semibold">{{ formatCurrency(user.total_paid) }}</td>
                    </tr>
                </tbody>
            </table>
        </div>

        <!-- Recent Activity -->
        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-4">กิจกรรมล่าสุด</h2>
            <div class="space-y-3">
                <div v-for="activity in recentActivity" :key="activity.id" class="flex items-center justify-between py-2 border-b border-gray-700 last:border-0">
                    <div class="flex items-center">
                        <div :class="['w-10 h-10 rounded-full flex items-center justify-center mr-3', getActivityColor(activity.type)]">
                            <component :is="getActivityIcon(activity.type)" class="w-5 h-5 text-white" />
                        </div>
                        <div>
                            <p class="text-white text-sm">{{ activity.description }}</p>
                            <p class="text-gray-500 text-xs">{{ activity.user }}</p>
                        </div>
                    </div>
                    <span class="text-gray-400 text-sm">{{ formatRelativeTime(activity.created_at) }}</span>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { adminApi } from '../../services/api'
import {
    ArrowDownTrayIcon,
    UserPlusIcon,
    CreditCardIcon,
    DocumentTextIcon,
    SparklesIcon,
} from '@heroicons/vue/24/outline'

const dateRange = ref('30d')

const summaryStats = ref({
    totalRevenue: 0,
    revenueGrowth: 0,
    newUsers: 0,
    userGrowth: 0,
    newRentals: 0,
    rentalGrowth: 0,
    totalPosts: 0,
    postGrowth: 0,
})

const revenueData = ref([])
const usersData = ref([])
const packageDistribution = ref([])
const paymentMethods = ref([])
const platformUsage = ref([])
const topUsers = ref([])
const recentActivity = ref([])

const maxRevenue = computed(() => Math.max(...revenueData.value.map(d => d.amount), 1))
const maxUsers = computed(() => Math.max(...usersData.value.map(d => d.count), 1))

const formatCurrency = (amount) => {
    if (amount >= 1000000) {
        return '฿' + (amount / 1000000).toFixed(1) + 'M'
    }
    if (amount >= 1000) {
        return '฿' + (amount / 1000).toFixed(1) + 'K'
    }
    return new Intl.NumberFormat('th-TH', {
        style: 'currency',
        currency: 'THB',
        minimumFractionDigits: 0
    }).format(amount || 0)
}

const formatRelativeTime = (date) => {
    const now = new Date()
    const diff = now - new Date(date)
    const minutes = Math.floor(diff / 60000)
    const hours = Math.floor(diff / 3600000)
    const days = Math.floor(diff / 86400000)

    if (minutes < 1) return 'เมื่อสักครู่'
    if (minutes < 60) return `${minutes} นาทีที่แล้ว`
    if (hours < 24) return `${hours} ชั่วโมงที่แล้ว`
    return `${days} วันที่แล้ว`
}

const getChartHeight = (value, max) => {
    return Math.max(5, (value / max) * 100)
}

const getActivityColor = (type) => {
    const colors = {
        new_user: 'bg-blue-600',
        payment: 'bg-green-600',
        post: 'bg-purple-600',
        ai: 'bg-orange-600',
    }
    return colors[type] || 'bg-gray-600'
}

const getActivityIcon = (type) => {
    const icons = {
        new_user: UserPlusIcon,
        payment: CreditCardIcon,
        post: DocumentTextIcon,
        ai: SparklesIcon,
    }
    return icons[type] || DocumentTextIcon
}

const fetchReportData = async () => {
    try {
        const response = await adminApi.reports({ range: dateRange.value })
        if (response.data.success) {
            const data = response.data.data
            summaryStats.value = data.summary || summaryStats.value
            revenueData.value = data.revenue_chart || []
            usersData.value = data.users_chart || []
            packageDistribution.value = data.package_distribution || []
            paymentMethods.value = data.payment_methods || []
            platformUsage.value = data.platform_usage || []
            topUsers.value = data.top_users || []
            recentActivity.value = data.recent_activity || []
        }
    } catch (error) {
        console.error('Failed to fetch report data:', error)
        // Load mock data for demo
        loadMockData()
    }
}

const loadMockData = () => {
    summaryStats.value = {
        totalRevenue: 1250000,
        revenueGrowth: 12.5,
        newUsers: 156,
        userGrowth: 8.3,
        newRentals: 89,
        rentalGrowth: 15.2,
        totalPosts: 4521,
        postGrowth: 22.1,
    }

    revenueData.value = Array.from({ length: 7 }, (_, i) => ({
        label: ['อา', 'จ', 'อ', 'พ', 'พฤ', 'ศ', 'ส'][i],
        amount: Math.floor(Math.random() * 50000) + 10000,
    }))

    usersData.value = Array.from({ length: 7 }, (_, i) => ({
        label: ['อา', 'จ', 'อ', 'พ', 'พฤ', 'ศ', 'ส'][i],
        count: Math.floor(Math.random() * 30) + 5,
    }))

    packageDistribution.value = [
        { name: 'Trial', count: 234, percentage: 35, color: 'bg-gray-500' },
        { name: 'Monthly', count: 189, percentage: 28, color: 'bg-blue-500' },
        { name: 'Quarterly', count: 156, percentage: 23, color: 'bg-purple-500' },
        { name: 'Yearly', count: 94, percentage: 14, color: 'bg-green-500' },
    ]

    paymentMethods.value = [
        { name: 'PromptPay', amount: 650000, percentage: 52, color: 'bg-blue-500' },
        { name: 'โอนเงิน', amount: 375000, percentage: 30, color: 'bg-green-500' },
        { name: 'บัตรเครดิต', amount: 225000, percentage: 18, color: 'bg-purple-500' },
    ]

    platformUsage.value = [
        { name: 'Facebook', posts: 1823, percentage: 40, color: 'bg-blue-600' },
        { name: 'Instagram', posts: 1245, percentage: 28, color: 'bg-pink-500' },
        { name: 'TikTok', posts: 892, percentage: 20, color: 'bg-gray-800' },
        { name: 'Twitter', posts: 561, percentage: 12, color: 'bg-sky-500' },
    ]

    topUsers.value = [
        { id: 1, name: 'สมชาย ใจดี', email: 'somchai@example.com', package: 'Yearly', posts: 521, ai_generations: 234, total_paid: 12990 },
        { id: 2, name: 'สมหญิง รักดี', email: 'somying@example.com', package: 'Quarterly', posts: 423, ai_generations: 189, total_paid: 8970 },
        { id: 3, name: 'ธนกร มั่งมี', email: 'thanakorn@example.com', package: 'Yearly', posts: 398, ai_generations: 156, total_paid: 12990 },
        { id: 4, name: 'วิภา สวยงาม', email: 'wipa@example.com', package: 'Monthly', posts: 345, ai_generations: 134, total_paid: 5970 },
        { id: 5, name: 'อนันต์ สุขใจ', email: 'anan@example.com', package: 'Quarterly', posts: 312, ai_generations: 98, total_paid: 8970 },
    ]

    recentActivity.value = [
        { id: 1, type: 'new_user', description: 'ผู้ใช้ใหม่ลงทะเบียน', user: 'test@example.com', created_at: new Date(Date.now() - 300000) },
        { id: 2, type: 'payment', description: 'ชำระเงินแพ็กเกจ Monthly', user: 'somchai@example.com', created_at: new Date(Date.now() - 1800000) },
        { id: 3, type: 'post', description: 'โพสต์ใหม่ไปยัง Facebook', user: 'somying@example.com', created_at: new Date(Date.now() - 3600000) },
        { id: 4, type: 'ai', description: 'สร้างเนื้อหาด้วย AI', user: 'thanakorn@example.com', created_at: new Date(Date.now() - 7200000) },
        { id: 5, type: 'payment', description: 'ชำระเงินแพ็กเกจ Yearly', user: 'wipa@example.com', created_at: new Date(Date.now() - 14400000) },
    ]
}

const exportReport = async () => {
    try {
        const response = await adminApi.exportReport({ range: dateRange.value })
        const blob = new Blob([response.data], { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' })
        const url = window.URL.createObjectURL(blob)
        const a = document.createElement('a')
        a.href = url
        a.download = `report_${dateRange.value}_${new Date().toISOString().split('T')[0]}.xlsx`
        a.click()
        window.URL.revokeObjectURL(url)
    } catch (error) {
        console.error('Failed to export report:', error)
        alert('ไม่สามารถส่งออกรายงานได้')
    }
}

watch(dateRange, () => {
    fetchReportData()
})

onMounted(() => {
    fetchReportData()
})
</script>
