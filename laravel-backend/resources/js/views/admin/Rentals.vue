<template>
    <div class="space-y-6">
        <div class="flex items-center justify-between">
            <h1 class="text-2xl font-bold text-white">จัดการการเช่าแพ็กเกจ</h1>
            <div class="flex space-x-3">
                <select v-model="statusFilter" class="px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white">
                    <option value="">ทั้งหมด</option>
                    <option value="active">กำลังใช้งาน</option>
                    <option value="pending">รอชำระเงิน</option>
                    <option value="expired">หมดอายุ</option>
                    <option value="cancelled">ยกเลิก</option>
                </select>
                <select v-model="packageFilter" class="px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white">
                    <option value="">ทุกแพ็กเกจ</option>
                    <option v-for="pkg in packages" :key="pkg.id" :value="pkg.id">{{ pkg.name }}</option>
                </select>
            </div>
        </div>

        <!-- Stats Cards -->
        <div class="grid grid-cols-1 md:grid-cols-5 gap-4">
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">กำลังใช้งาน</p>
                <p class="text-2xl font-bold text-green-400">{{ stats.active }}</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">รอชำระเงิน</p>
                <p class="text-2xl font-bold text-yellow-400">{{ stats.pending }}</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">หมดอายุเร็วๆ นี้</p>
                <p class="text-2xl font-bold text-orange-400">{{ stats.expiringSoon }}</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">หมดอายุแล้ว</p>
                <p class="text-2xl font-bold text-red-400">{{ stats.expired }}</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">รวมทั้งหมด</p>
                <p class="text-2xl font-bold text-white">{{ stats.total }}</p>
            </div>
        </div>

        <!-- Rentals Table -->
        <div class="bg-gray-800 rounded-xl border border-gray-700 overflow-hidden">
            <table class="w-full">
                <thead class="bg-gray-700/50">
                    <tr>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">ผู้ใช้</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">แพ็กเกจ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">ราคา</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">สถานะ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">เริ่มใช้งาน</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">หมดอายุ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">การใช้งาน</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">จัดการ</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-gray-700">
                    <tr v-for="rental in rentals" :key="rental.id" class="hover:bg-gray-700/50">
                        <td class="px-4 py-3">
                            <div class="flex items-center">
                                <div class="w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center mr-2">
                                    <span class="text-white text-xs">{{ rental.user?.name?.[0] || '?' }}</span>
                                </div>
                                <div>
                                    <p class="text-white text-sm">{{ rental.user?.name }}</p>
                                    <p class="text-gray-500 text-xs">{{ rental.user?.email }}</p>
                                </div>
                            </div>
                        </td>
                        <td class="px-4 py-3">
                            <span class="px-2 py-1 bg-indigo-500/20 text-indigo-400 text-xs rounded-full">
                                {{ rental.package?.name }}
                            </span>
                        </td>
                        <td class="px-4 py-3 text-white font-semibold">{{ formatCurrency(rental.price_paid) }}</td>
                        <td class="px-4 py-3">
                            <span :class="getStatusClass(rental.status)">
                                {{ getStatusLabel(rental.status) }}
                            </span>
                        </td>
                        <td class="px-4 py-3 text-gray-400 text-sm">{{ formatDate(rental.starts_at) }}</td>
                        <td class="px-4 py-3 text-sm">
                            <span :class="isExpiringSoon(rental.expires_at) ? 'text-orange-400' : 'text-gray-400'">
                                {{ formatDate(rental.expires_at) }}
                            </span>
                        </td>
                        <td class="px-4 py-3">
                            <div class="text-xs text-gray-400">
                                <p>โพสต์: {{ rental.posts_used || 0 }}/{{ rental.package?.limits?.posts_per_month || '∞' }}</p>
                                <p>AI: {{ rental.ai_used || 0 }}/{{ rental.package?.limits?.ai_generations || '∞' }}</p>
                            </div>
                        </td>
                        <td class="px-4 py-3">
                            <div class="flex space-x-2">
                                <button class="text-gray-400 hover:text-white" @click="viewRental(rental)">ดู</button>
                                <button v-if="rental.status === 'active'" class="text-orange-400 hover:text-orange-300" @click="extendRental(rental)">ต่ออายุ</button>
                                <button v-if="rental.status === 'active'" class="text-red-400 hover:text-red-300" @click="cancelRental(rental)">ยกเลิก</button>
                            </div>
                        </td>
                    </tr>
                </tbody>
            </table>

            <div v-if="rentals.length === 0" class="p-12 text-center">
                <p class="text-gray-400">ไม่พบรายการเช่า</p>
            </div>
        </div>

        <!-- Pagination -->
        <div class="flex items-center justify-between">
            <p class="text-gray-400 text-sm">แสดง {{ rentals.length }} จาก {{ totalRentals }} รายการ</p>
            <div class="flex space-x-2">
                <button :disabled="currentPage === 1" class="px-3 py-1 bg-gray-700 text-gray-300 rounded disabled:opacity-50" @click="prevPage">&larr; ก่อนหน้า</button>
                <button :disabled="currentPage >= totalPages" class="px-3 py-1 bg-gray-700 text-gray-300 rounded disabled:opacity-50" @click="nextPage">ถัดไป &rarr;</button>
            </div>
        </div>

        <!-- Rental Detail Modal -->
        <div v-if="selectedRental" class="fixed inset-0 z-50 flex items-center justify-center bg-black/70" @click.self="selectedRental = null">
            <div class="bg-gray-800 rounded-xl p-6 w-full max-w-2xl border border-gray-700">
                <div class="flex justify-between items-center mb-6">
                    <h2 class="text-xl font-bold text-white">รายละเอียดการเช่า</h2>
                    <button @click="selectedRental = null" class="text-gray-400 hover:text-white">&times;</button>
                </div>

                <div class="space-y-4">
                    <div class="grid grid-cols-2 gap-4">
                        <div>
                            <p class="text-gray-400 text-sm">ผู้เช่า</p>
                            <p class="text-white">{{ selectedRental.user?.name }}</p>
                            <p class="text-gray-500 text-sm">{{ selectedRental.user?.email }}</p>
                        </div>
                        <div>
                            <p class="text-gray-400 text-sm">แพ็กเกจ</p>
                            <p class="text-white">{{ selectedRental.package?.name }}</p>
                            <p class="text-gray-500 text-sm">{{ selectedRental.package?.duration_days }} วัน</p>
                        </div>
                    </div>

                    <div class="grid grid-cols-3 gap-4">
                        <div>
                            <p class="text-gray-400 text-sm">ราคาที่ชำระ</p>
                            <p class="text-white font-bold">{{ formatCurrency(selectedRental.price_paid) }}</p>
                        </div>
                        <div v-if="selectedRental.discount_amount">
                            <p class="text-gray-400 text-sm">ส่วนลด</p>
                            <p class="text-green-400">-{{ formatCurrency(selectedRental.discount_amount) }}</p>
                        </div>
                        <div v-if="selectedRental.promo_code">
                            <p class="text-gray-400 text-sm">โค้ดส่วนลด</p>
                            <p class="text-purple-400 font-mono">{{ selectedRental.promo_code }}</p>
                        </div>
                    </div>

                    <div class="grid grid-cols-2 gap-4">
                        <div>
                            <p class="text-gray-400 text-sm">เริ่มใช้งาน</p>
                            <p class="text-white">{{ formatDate(selectedRental.starts_at) }}</p>
                        </div>
                        <div>
                            <p class="text-gray-400 text-sm">หมดอายุ</p>
                            <p :class="isExpiringSoon(selectedRental.expires_at) ? 'text-orange-400' : 'text-white'">
                                {{ formatDate(selectedRental.expires_at) }}
                            </p>
                        </div>
                    </div>

                    <!-- Usage Stats -->
                    <div class="bg-gray-700/50 rounded-lg p-4">
                        <h3 class="text-white font-semibold mb-3">การใช้งาน</h3>
                        <div class="grid grid-cols-2 gap-4">
                            <div>
                                <p class="text-gray-400 text-sm">โพสต์</p>
                                <div class="flex items-center">
                                    <div class="flex-1 h-2 bg-gray-600 rounded-full mr-2">
                                        <div class="h-2 bg-blue-500 rounded-full" :style="{ width: getUsagePercent(selectedRental.posts_used, selectedRental.package?.limits?.posts_per_month) + '%' }"></div>
                                    </div>
                                    <span class="text-white text-sm">{{ selectedRental.posts_used || 0 }}/{{ selectedRental.package?.limits?.posts_per_month || '∞' }}</span>
                                </div>
                            </div>
                            <div>
                                <p class="text-gray-400 text-sm">AI Generations</p>
                                <div class="flex items-center">
                                    <div class="flex-1 h-2 bg-gray-600 rounded-full mr-2">
                                        <div class="h-2 bg-purple-500 rounded-full" :style="{ width: getUsagePercent(selectedRental.ai_used, selectedRental.package?.limits?.ai_generations) + '%' }"></div>
                                    </div>
                                    <span class="text-white text-sm">{{ selectedRental.ai_used || 0 }}/{{ selectedRental.package?.limits?.ai_generations || '∞' }}</span>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Payment History -->
                    <div v-if="selectedRental.payments?.length">
                        <h3 class="text-white font-semibold mb-3">ประวัติการชำระเงิน</h3>
                        <div class="space-y-2">
                            <div v-for="payment in selectedRental.payments" :key="payment.id" class="flex justify-between items-center bg-gray-700/50 rounded-lg p-3">
                                <div>
                                    <p class="text-white text-sm">{{ payment.payment_reference }}</p>
                                    <p class="text-gray-400 text-xs">{{ formatDate(payment.created_at) }}</p>
                                </div>
                                <div class="text-right">
                                    <p class="text-white font-semibold">{{ formatCurrency(payment.amount) }}</p>
                                    <span :class="getStatusClass(payment.status)" class="text-xs">{{ getStatusLabel(payment.status) }}</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue'
import { adminApi, rentalApi } from '../../services/api'

const rentals = ref([])
const packages = ref([])
const statusFilter = ref('')
const packageFilter = ref('')
const currentPage = ref(1)
const totalRentals = ref(0)
const totalPages = ref(1)
const selectedRental = ref(null)
const stats = ref({
    active: 0,
    pending: 0,
    expiringSoon: 0,
    expired: 0,
    total: 0,
})

const formatCurrency = (amount) => {
    return new Intl.NumberFormat('th-TH', {
        style: 'currency',
        currency: 'THB',
        minimumFractionDigits: 0
    }).format(amount || 0)
}

const formatDate = (date) => {
    if (!date) return '-'
    return new Date(date).toLocaleDateString('th-TH', {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
    })
}

const isExpiringSoon = (date) => {
    if (!date) return false
    const expiryDate = new Date(date)
    const now = new Date()
    const daysUntilExpiry = Math.ceil((expiryDate - now) / (1000 * 60 * 60 * 24))
    return daysUntilExpiry > 0 && daysUntilExpiry <= 7
}

const getStatusClass = (status) => {
    const classes = {
        active: 'px-2 py-1 text-xs rounded-full bg-green-500/20 text-green-400',
        pending: 'px-2 py-1 text-xs rounded-full bg-yellow-500/20 text-yellow-400',
        expired: 'px-2 py-1 text-xs rounded-full bg-red-500/20 text-red-400',
        cancelled: 'px-2 py-1 text-xs rounded-full bg-gray-500/20 text-gray-400',
        completed: 'px-2 py-1 text-xs rounded-full bg-blue-500/20 text-blue-400',
    }
    return classes[status] || classes.pending
}

const getStatusLabel = (status) => {
    const labels = {
        active: 'กำลังใช้งาน',
        pending: 'รอชำระเงิน',
        expired: 'หมดอายุ',
        cancelled: 'ยกเลิก',
        completed: 'เสร็จสิ้น',
    }
    return labels[status] || status
}

const getUsagePercent = (used, limit) => {
    if (!limit || limit === -1) return 0
    return Math.min(100, (used / limit) * 100)
}

const fetchRentals = async () => {
    try {
        const response = await adminApi.rentals({
            status: statusFilter.value,
            package_id: packageFilter.value,
            page: currentPage.value,
            per_page: 20,
        })
        if (response.data.success) {
            rentals.value = response.data.data
            totalRentals.value = response.data.meta?.total || 0
            totalPages.value = response.data.meta?.last_page || 1
        }
    } catch (error) {
        console.error('Failed to fetch rentals:', error)
    }
}

const fetchPackages = async () => {
    try {
        const response = await rentalApi.packages()
        if (response.data.success) {
            packages.value = response.data.data
        }
    } catch (error) {
        console.error('Failed to fetch packages:', error)
    }
}

const fetchStats = async () => {
    try {
        const response = await adminApi.stats()
        if (response.data.success) {
            const data = response.data.data
            stats.value = {
                active: data.rentals?.active || 0,
                pending: data.rentals?.pending || 0,
                expiringSoon: data.rentals?.expiring_soon || 0,
                expired: data.rentals?.expired || 0,
                total: data.rentals?.total || 0,
            }
        }
    } catch (error) {
        console.error('Failed to fetch stats:', error)
    }
}

const viewRental = (rental) => {
    selectedRental.value = rental
}

const extendRental = async (rental) => {
    if (confirm(`ต่ออายุแพ็กเกจ ${rental.package?.name} สำหรับ ${rental.user?.name}?`)) {
        try {
            await adminApi.extendRental(rental.id)
            fetchRentals()
            fetchStats()
        } catch (error) {
            console.error('Failed to extend rental:', error)
        }
    }
}

const cancelRental = async (rental) => {
    if (confirm(`ยกเลิกการเช่าของ ${rental.user?.name}? การกระทำนี้ไม่สามารถย้อนกลับได้`)) {
        try {
            await adminApi.cancelRental(rental.id)
            fetchRentals()
            fetchStats()
        } catch (error) {
            console.error('Failed to cancel rental:', error)
        }
    }
}

const prevPage = () => { if (currentPage.value > 1) { currentPage.value--; fetchRentals() } }
const nextPage = () => { if (currentPage.value < totalPages.value) { currentPage.value++; fetchRentals() } }

watch([statusFilter, packageFilter], () => {
    currentPage.value = 1
    fetchRentals()
})

onMounted(() => {
    fetchRentals()
    fetchPackages()
    fetchStats()
})
</script>
