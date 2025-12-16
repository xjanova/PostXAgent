<template>
    <div class="space-y-6">
        <h1 class="text-2xl font-bold text-white">Admin Dashboard</h1>

        <!-- Revenue Stats -->
        <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <p class="text-gray-400 text-sm">รายได้วันนี้</p>
                <p class="text-2xl font-bold text-green-400 mt-1">{{ formatCurrency(stats.revenueToday) }}</p>
                <p class="text-green-400 text-sm mt-2">{{ stats.paymentsToday }} รายการ</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <p class="text-gray-400 text-sm">รายได้เดือนนี้</p>
                <p class="text-2xl font-bold text-white mt-1">{{ formatCurrency(stats.revenueMonth) }}</p>
                <p class="text-gray-400 text-sm mt-2">{{ stats.paymentsMonth }} รายการ</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <p class="text-gray-400 text-sm">ผู้ใช้ Active</p>
                <p class="text-2xl font-bold text-white mt-1">{{ stats.activeRentals }}</p>
                <p class="text-gray-400 text-sm mt-2">มีแพ็กเกจใช้งานอยู่</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <p class="text-gray-400 text-sm">รอยืนยัน</p>
                <p class="text-2xl font-bold text-yellow-400 mt-1">{{ stats.pendingPayments }}</p>
                <router-link to="/admin/payments" class="text-indigo-400 text-sm">ดูรายละเอียด &rarr;</router-link>
            </div>
        </div>

        <!-- Quick Actions -->
        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Pending Payments -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <div class="flex items-center justify-between mb-4">
                    <h2 class="text-lg font-semibold text-white">รอยืนยันการชำระเงิน</h2>
                    <router-link to="/admin/payments" class="text-sm text-indigo-400">ดูทั้งหมด</router-link>
                </div>
                <div v-if="pendingPayments.length === 0" class="text-center py-8">
                    <CheckCircleIcon class="w-12 h-12 text-green-400 mx-auto mb-4" />
                    <p class="text-gray-400">ไม่มีรายการรอยืนยัน</p>
                </div>
                <div v-else class="space-y-3">
                    <div v-for="payment in pendingPayments" :key="payment.id" class="flex items-center justify-between p-3 bg-gray-700/50 rounded-lg">
                        <div>
                            <p class="text-white">{{ payment.user?.name }}</p>
                            <p class="text-gray-400 text-sm">{{ payment.package }} · {{ formatCurrency(payment.amount) }}</p>
                        </div>
                        <div class="flex space-x-2">
                            <button @click="verifyPayment(payment)" class="px-3 py-1 bg-green-600 hover:bg-green-700 text-white text-sm rounded-lg">ยืนยัน</button>
                            <button @click="rejectPayment(payment)" class="px-3 py-1 bg-red-600/20 hover:bg-red-600/30 text-red-400 text-sm rounded-lg">ปฏิเสธ</button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Expiring Soon -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <div class="flex items-center justify-between mb-4">
                    <h2 class="text-lg font-semibold text-white">แพ็กเกจใกล้หมดอายุ (7 วัน)</h2>
                </div>
                <div v-if="stats.expiringSoon === 0" class="text-center py-8">
                    <p class="text-gray-400">ไม่มีแพ็กเกจใกล้หมดอายุ</p>
                </div>
                <div v-else>
                    <p class="text-3xl font-bold text-yellow-400">{{ stats.expiringSoon }}</p>
                    <p class="text-gray-400 text-sm">รายการใกล้หมดอายุ</p>
                </div>
            </div>
        </div>

        <!-- Package Distribution -->
        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-4">แพ็กเกจที่ใช้งาน</h2>
            <div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4">
                <div v-for="pkg in packageStats" :key="pkg.id" class="text-center p-4 bg-gray-700/50 rounded-lg">
                    <p class="text-2xl font-bold text-white">{{ pkg.total_rentals || 0 }}</p>
                    <p class="text-gray-400 text-sm">{{ pkg.name }}</p>
                </div>
            </div>
        </div>

        <!-- System Status -->
        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-4">สถานะระบบ</h2>
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div class="flex items-center">
                    <span :class="['w-3 h-3 rounded-full mr-3', systemStatus.database ? 'bg-green-500' : 'bg-red-500']"></span>
                    <span class="text-gray-300">Database</span>
                </div>
                <div class="flex items-center">
                    <span :class="['w-3 h-3 rounded-full mr-3', systemStatus.aiManager ? 'bg-green-500' : 'bg-red-500']"></span>
                    <span class="text-gray-300">AI Manager</span>
                </div>
                <div class="flex items-center">
                    <span :class="['w-3 h-3 rounded-full mr-3', systemStatus.queue ? 'bg-green-500' : 'bg-red-500']"></span>
                    <span class="text-gray-300">Queue Worker</span>
                </div>
                <div class="flex items-center">
                    <span :class="['w-3 h-3 rounded-full mr-3', systemStatus.redis ? 'bg-green-500' : 'bg-red-500']"></span>
                    <span class="text-gray-300">Redis</span>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { adminApi, aiManagerApi } from '../../services/api'
import { CheckCircleIcon } from '@heroicons/vue/24/outline'

const stats = reactive({
    revenueToday: 0,
    revenueMonth: 0,
    paymentsToday: 0,
    paymentsMonth: 0,
    activeRentals: 0,
    pendingPayments: 0,
    expiringSoon: 0,
})

const pendingPayments = ref([])
const packageStats = ref([])
const systemStatus = reactive({
    database: true,
    aiManager: false,
    queue: true,
    redis: true,
})

const formatCurrency = (amount) => {
    return new Intl.NumberFormat('th-TH', { style: 'currency', currency: 'THB', minimumFractionDigits: 0 }).format(amount || 0)
}

const verifyPayment = async (payment) => {
    if (confirm('ยืนยันการชำระเงินนี้?')) {
        try {
            await adminApi.verifyPayment(payment.uuid)
            pendingPayments.value = pendingPayments.value.filter(p => p.id !== payment.id)
            stats.pendingPayments--
        } catch (error) {
            alert('เกิดข้อผิดพลาด')
        }
    }
}

const rejectPayment = async (payment) => {
    const reason = prompt('กรุณาระบุเหตุผลในการปฏิเสธ:')
    if (reason) {
        try {
            await adminApi.rejectPayment(payment.uuid, reason)
            pendingPayments.value = pendingPayments.value.filter(p => p.id !== payment.id)
            stats.pendingPayments--
        } catch (error) {
            alert('เกิดข้อผิดพลาด')
        }
    }
}

onMounted(async () => {
    try {
        const [statsRes, paymentsRes, aiStatus] = await Promise.all([
            adminApi.stats(),
            adminApi.payments({ status: 'pending', per_page: 5 }),
            aiManagerApi.status().catch(() => ({ data: { is_online: false } })),
        ])

        if (statsRes.data.success) {
            const data = statsRes.data.data
            stats.revenueToday = data.revenue?.today || 0
            stats.revenueMonth = data.revenue?.this_month || 0
            stats.paymentsToday = data.payments?.completed_today || 0
            stats.paymentsMonth = data.payments?.completed_this_month || 0
            stats.activeRentals = data.rentals?.active || 0
            stats.pendingPayments = data.payments?.pending || 0
            stats.expiringSoon = data.rentals?.expiring_soon || 0
            packageStats.value = data.packages || []
        }

        if (paymentsRes.data.success) {
            pendingPayments.value = paymentsRes.data.data
        }

        systemStatus.aiManager = aiStatus.data.is_online || false
    } catch (error) {
        console.error('Failed to load admin data:', error)
    }
})
</script>
