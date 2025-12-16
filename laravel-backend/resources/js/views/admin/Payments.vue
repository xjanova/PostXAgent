<template>
    <div class="space-y-6">
        <div class="flex items-center justify-between">
            <h1 class="text-2xl font-bold text-white">จัดการการชำระเงิน</h1>
            <div class="flex space-x-3">
                <select v-model="statusFilter" class="px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white">
                    <option value="">ทั้งหมด</option>
                    <option value="pending">รอยืนยัน</option>
                    <option value="processing">กำลังตรวจสอบ</option>
                    <option value="completed">เสร็จสิ้น</option>
                    <option value="failed">ล้มเหลว</option>
                    <option value="refunded">คืนเงินแล้ว</option>
                </select>
                <select v-model="methodFilter" class="px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white">
                    <option value="">ทุกช่องทาง</option>
                    <option value="promptpay">PromptPay</option>
                    <option value="bank_transfer">โอนเงิน</option>
                    <option value="credit_card">บัตรเครดิต</option>
                </select>
            </div>
        </div>

        <!-- Stats Cards -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">รอยืนยัน</p>
                <p class="text-2xl font-bold text-yellow-400">{{ stats.pending }}</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">รายได้วันนี้</p>
                <p class="text-2xl font-bold text-green-400">{{ formatCurrency(stats.todayRevenue) }}</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">รายได้เดือนนี้</p>
                <p class="text-2xl font-bold text-blue-400">{{ formatCurrency(stats.monthRevenue) }}</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">รวมทั้งหมด</p>
                <p class="text-2xl font-bold text-white">{{ formatCurrency(stats.totalRevenue) }}</p>
            </div>
        </div>

        <!-- Payments Table -->
        <div class="bg-gray-800 rounded-xl border border-gray-700 overflow-hidden">
            <table class="w-full">
                <thead class="bg-gray-700/50">
                    <tr>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">รหัส</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">ผู้ใช้</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">แพ็กเกจ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">จำนวน</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">ช่องทาง</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">สถานะ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">วันที่</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">จัดการ</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-gray-700">
                    <tr v-for="payment in payments" :key="payment.id" class="hover:bg-gray-700/50">
                        <td class="px-4 py-3 font-mono text-sm text-gray-300">{{ payment.payment_reference }}</td>
                        <td class="px-4 py-3">
                            <div class="flex items-center">
                                <div class="w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center mr-2">
                                    <span class="text-white text-xs">{{ payment.user?.name?.[0] || '?' }}</span>
                                </div>
                                <div>
                                    <p class="text-white text-sm">{{ payment.user?.name }}</p>
                                    <p class="text-gray-500 text-xs">{{ payment.user?.email }}</p>
                                </div>
                            </div>
                        </td>
                        <td class="px-4 py-3 text-gray-300 text-sm">{{ payment.user_rental?.package?.name || '-' }}</td>
                        <td class="px-4 py-3 text-white font-semibold">{{ formatCurrency(payment.amount) }}</td>
                        <td class="px-4 py-3">
                            <span :class="getMethodClass(payment.payment_method)">
                                {{ getMethodLabel(payment.payment_method) }}
                            </span>
                        </td>
                        <td class="px-4 py-3">
                            <span :class="getStatusClass(payment.status)">
                                {{ getStatusLabel(payment.status) }}
                            </span>
                        </td>
                        <td class="px-4 py-3 text-gray-400 text-sm">{{ formatDate(payment.created_at) }}</td>
                        <td class="px-4 py-3">
                            <div class="flex space-x-2">
                                <button class="text-gray-400 hover:text-white" @click="viewPayment(payment)">ดู</button>
                                <button v-if="payment.status === 'pending'" class="text-green-400 hover:text-green-300" @click="verifyPayment(payment)">ยืนยัน</button>
                                <button v-if="payment.status === 'pending'" class="text-red-400 hover:text-red-300" @click="rejectPayment(payment)">ปฏิเสธ</button>
                            </div>
                        </td>
                    </tr>
                </tbody>
            </table>

            <div v-if="payments.length === 0" class="p-12 text-center">
                <p class="text-gray-400">ไม่พบรายการชำระเงิน</p>
            </div>
        </div>

        <!-- Pagination -->
        <div class="flex items-center justify-between">
            <p class="text-gray-400 text-sm">แสดง {{ payments.length }} จาก {{ totalPayments }} รายการ</p>
            <div class="flex space-x-2">
                <button :disabled="currentPage === 1" class="px-3 py-1 bg-gray-700 text-gray-300 rounded disabled:opacity-50" @click="prevPage">&larr; ก่อนหน้า</button>
                <button :disabled="currentPage >= totalPages" class="px-3 py-1 bg-gray-700 text-gray-300 rounded disabled:opacity-50" @click="nextPage">ถัดไป &rarr;</button>
            </div>
        </div>

        <!-- Payment Detail Modal -->
        <div v-if="selectedPayment" class="fixed inset-0 z-50 flex items-center justify-center bg-black/70" @click.self="selectedPayment = null">
            <div class="bg-gray-800 rounded-xl p-6 w-full max-w-2xl border border-gray-700 max-h-[90vh] overflow-y-auto">
                <div class="flex justify-between items-center mb-6">
                    <h2 class="text-xl font-bold text-white">รายละเอียดการชำระเงิน</h2>
                    <button @click="selectedPayment = null" class="text-gray-400 hover:text-white">&times;</button>
                </div>

                <div class="space-y-4">
                    <div class="grid grid-cols-2 gap-4">
                        <div>
                            <p class="text-gray-400 text-sm">รหัสอ้างอิง</p>
                            <p class="text-white font-mono">{{ selectedPayment.payment_reference }}</p>
                        </div>
                        <div>
                            <p class="text-gray-400 text-sm">UUID</p>
                            <p class="text-white font-mono text-sm">{{ selectedPayment.uuid }}</p>
                        </div>
                    </div>

                    <div class="grid grid-cols-2 gap-4">
                        <div>
                            <p class="text-gray-400 text-sm">ผู้ชำระเงิน</p>
                            <p class="text-white">{{ selectedPayment.user?.name }}</p>
                            <p class="text-gray-500 text-sm">{{ selectedPayment.user?.email }}</p>
                        </div>
                        <div>
                            <p class="text-gray-400 text-sm">แพ็กเกจ</p>
                            <p class="text-white">{{ selectedPayment.user_rental?.package?.name || '-' }}</p>
                        </div>
                    </div>

                    <div class="grid grid-cols-3 gap-4">
                        <div>
                            <p class="text-gray-400 text-sm">จำนวนเงิน</p>
                            <p class="text-white text-xl font-bold">{{ formatCurrency(selectedPayment.amount) }}</p>
                        </div>
                        <div>
                            <p class="text-gray-400 text-sm">ค่าธรรมเนียม</p>
                            <p class="text-white">{{ formatCurrency(selectedPayment.fee || 0) }}</p>
                        </div>
                        <div>
                            <p class="text-gray-400 text-sm">สุทธิ</p>
                            <p class="text-green-400 font-bold">{{ formatCurrency(selectedPayment.net_amount) }}</p>
                        </div>
                    </div>

                    <div class="grid grid-cols-2 gap-4">
                        <div>
                            <p class="text-gray-400 text-sm">ช่องทางชำระเงิน</p>
                            <p class="text-white">{{ getMethodLabel(selectedPayment.payment_method) }}</p>
                        </div>
                        <div>
                            <p class="text-gray-400 text-sm">สถานะ</p>
                            <span :class="getStatusClass(selectedPayment.status)">
                                {{ getStatusLabel(selectedPayment.status) }}
                            </span>
                        </div>
                    </div>

                    <!-- Transfer Slip -->
                    <div v-if="selectedPayment.transfer_slip_url">
                        <p class="text-gray-400 text-sm mb-2">สลิปการโอนเงิน</p>
                        <img :src="selectedPayment.transfer_slip_url" alt="Transfer slip" class="max-w-full rounded-lg border border-gray-600" />
                    </div>

                    <!-- PromptPay QR -->
                    <div v-if="selectedPayment.promptpay_qr_url">
                        <p class="text-gray-400 text-sm mb-2">PromptPay QR</p>
                        <img :src="selectedPayment.promptpay_qr_url" alt="PromptPay QR" class="w-48 h-48 rounded-lg border border-gray-600" />
                    </div>

                    <!-- Admin Notes -->
                    <div v-if="selectedPayment.status === 'pending'" class="mt-6">
                        <label class="block text-gray-400 text-sm mb-2">หมายเหตุ (สำหรับ Admin)</label>
                        <textarea v-model="adminNotes" rows="3" class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white" placeholder="หมายเหตุเพิ่มเติม..."></textarea>
                    </div>

                    <!-- Actions -->
                    <div v-if="selectedPayment.status === 'pending'" class="flex space-x-3 mt-6">
                        <button @click="verifyPaymentConfirm" class="flex-1 py-2 bg-green-600 hover:bg-green-700 text-white rounded-lg transition-colors">
                            ยืนยันการชำระเงิน
                        </button>
                        <button @click="rejectPaymentConfirm" class="flex-1 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors">
                            ปฏิเสธ
                        </button>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue'
import { adminApi } from '../../services/api'

const payments = ref([])
const statusFilter = ref('')
const methodFilter = ref('')
const currentPage = ref(1)
const totalPayments = ref(0)
const totalPages = ref(1)
const selectedPayment = ref(null)
const adminNotes = ref('')
const stats = ref({
    pending: 0,
    todayRevenue: 0,
    monthRevenue: 0,
    totalRevenue: 0,
})

const formatCurrency = (amount) => {
    return new Intl.NumberFormat('th-TH', {
        style: 'currency',
        currency: 'THB',
        minimumFractionDigits: 0
    }).format(amount || 0)
}

const formatDate = (date) => new Date(date).toLocaleDateString('th-TH', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
})

const getStatusClass = (status) => {
    const classes = {
        pending: 'px-2 py-1 text-xs rounded-full bg-yellow-500/20 text-yellow-400',
        processing: 'px-2 py-1 text-xs rounded-full bg-blue-500/20 text-blue-400',
        completed: 'px-2 py-1 text-xs rounded-full bg-green-500/20 text-green-400',
        failed: 'px-2 py-1 text-xs rounded-full bg-red-500/20 text-red-400',
        refunded: 'px-2 py-1 text-xs rounded-full bg-purple-500/20 text-purple-400',
        cancelled: 'px-2 py-1 text-xs rounded-full bg-gray-500/20 text-gray-400',
    }
    return classes[status] || classes.pending
}

const getStatusLabel = (status) => {
    const labels = {
        pending: 'รอยืนยัน',
        processing: 'กำลังตรวจสอบ',
        completed: 'เสร็จสิ้น',
        failed: 'ล้มเหลว',
        refunded: 'คืนเงินแล้ว',
        cancelled: 'ยกเลิก',
    }
    return labels[status] || status
}

const getMethodClass = (method) => {
    const classes = {
        promptpay: 'px-2 py-1 text-xs rounded-full bg-blue-500/20 text-blue-400',
        bank_transfer: 'px-2 py-1 text-xs rounded-full bg-green-500/20 text-green-400',
        credit_card: 'px-2 py-1 text-xs rounded-full bg-purple-500/20 text-purple-400',
    }
    return classes[method] || 'px-2 py-1 text-xs rounded-full bg-gray-500/20 text-gray-400'
}

const getMethodLabel = (method) => {
    const labels = {
        promptpay: 'PromptPay',
        bank_transfer: 'โอนเงิน',
        credit_card: 'บัตรเครดิต',
        debit_card: 'บัตรเดบิต',
        truemoney: 'TrueMoney',
        linepay: 'LINE Pay',
        shopeepay: 'ShopeePay',
        manual: 'ชำระด้วยตนเอง',
    }
    return labels[method] || method
}

const fetchPayments = async () => {
    try {
        const response = await adminApi.payments({
            status: statusFilter.value,
            method: methodFilter.value,
            page: currentPage.value,
            per_page: 20,
        })
        if (response.data.success) {
            payments.value = response.data.data
            totalPayments.value = response.data.meta?.total || 0
            totalPages.value = response.data.meta?.last_page || 1
        }
    } catch (error) {
        console.error('Failed to fetch payments:', error)
    }
}

const fetchStats = async () => {
    try {
        const response = await adminApi.stats()
        if (response.data.success) {
            const data = response.data.data
            stats.value = {
                pending: data.payments?.pending || 0,
                todayRevenue: data.revenue?.today || 0,
                monthRevenue: data.revenue?.month || 0,
                totalRevenue: data.revenue?.total || 0,
            }
        }
    } catch (error) {
        console.error('Failed to fetch stats:', error)
    }
}

const viewPayment = (payment) => {
    selectedPayment.value = payment
    adminNotes.value = ''
}

const verifyPayment = (payment) => {
    selectedPayment.value = payment
    adminNotes.value = ''
}

const rejectPayment = (payment) => {
    selectedPayment.value = payment
    adminNotes.value = ''
}

const verifyPaymentConfirm = async () => {
    if (!selectedPayment.value) return
    try {
        await adminApi.verifyPayment(selectedPayment.value.id, { notes: adminNotes.value })
        selectedPayment.value = null
        fetchPayments()
        fetchStats()
    } catch (error) {
        console.error('Failed to verify payment:', error)
    }
}

const rejectPaymentConfirm = async () => {
    if (!selectedPayment.value || !adminNotes.value) {
        alert('กรุณาระบุเหตุผลในการปฏิเสธ')
        return
    }
    try {
        await adminApi.rejectPayment(selectedPayment.value.id, { reason: adminNotes.value })
        selectedPayment.value = null
        fetchPayments()
        fetchStats()
    } catch (error) {
        console.error('Failed to reject payment:', error)
    }
}

const prevPage = () => { if (currentPage.value > 1) { currentPage.value--; fetchPayments() } }
const nextPage = () => { if (currentPage.value < totalPages.value) { currentPage.value++; fetchPayments() } }

watch([statusFilter, methodFilter], () => {
    currentPage.value = 1
    fetchPayments()
})

onMounted(() => {
    fetchPayments()
    fetchStats()
})
</script>
