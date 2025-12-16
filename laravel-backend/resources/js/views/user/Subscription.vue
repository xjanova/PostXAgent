<template>
    <div class="space-y-6">
        <!-- Current Plan -->
        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-4">แพ็กเกจปัจจุบัน</h2>

            <div v-if="authStore.hasActiveRental" class="flex items-start justify-between">
                <div>
                    <h3 class="text-2xl font-bold text-white">{{ authStore.currentPackage }}</h3>
                    <p class="text-gray-400 mt-1">หมดอายุ: {{ formatDate(authStore.rental?.expires_at) }}</p>
                    <p class="text-indigo-400 text-sm mt-2">เหลืออีก {{ authStore.daysRemaining }} วัน</p>
                </div>
                <div class="text-right">
                    <p class="text-gray-400 text-sm">การใช้งาน</p>
                    <p class="text-white">โพสต์: {{ authStore.usageRemaining?.posts || 0 }} เหลือ</p>
                    <p class="text-white">AI: {{ authStore.usageRemaining?.ai_generations || 0 }} เหลือ</p>
                </div>
            </div>

            <div v-else class="text-center py-8">
                <ExclamationTriangleIcon class="w-12 h-12 text-yellow-400 mx-auto mb-4" />
                <p class="text-gray-400">คุณยังไม่มีแพ็กเกจที่ใช้งานอยู่</p>
            </div>
        </div>

        <!-- Upgrade Message -->
        <div v-if="upgradeFeature" class="p-4 bg-indigo-500/20 border border-indigo-500/50 rounded-lg">
            <p class="text-indigo-400">
                อัพเกรดแพ็กเกจเพื่อเข้าถึงฟีเจอร์ <strong>{{ getFeatureLabel(upgradeFeature) }}</strong>
            </p>
        </div>

        <!-- Packages Grid -->
        <div>
            <h2 class="text-lg font-semibold text-white mb-4">เลือกแพ็กเกจ</h2>
            <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                <div
                    v-for="pkg in packages"
                    :key="pkg.id"
                    :class="[
                        'bg-gray-800 rounded-xl p-6 border transition-all cursor-pointer',
                        selectedPackage?.id === pkg.id
                            ? 'border-indigo-500 ring-2 ring-indigo-500/50'
                            : 'border-gray-700 hover:border-gray-600'
                    ]"
                    @click="selectPackage(pkg)"
                >
                    <!-- Popular Badge -->
                    <div v-if="pkg.is_popular" class="flex justify-end -mt-2 -mr-2 mb-2">
                        <span class="px-3 py-1 bg-indigo-600 text-white text-xs font-medium rounded-full">
                            ยอดนิยม
                        </span>
                    </div>

                    <!-- Featured Badge -->
                    <div v-if="pkg.is_featured && !pkg.is_popular" class="flex justify-end -mt-2 -mr-2 mb-2">
                        <span class="px-3 py-1 bg-green-600 text-white text-xs font-medium rounded-full">
                            คุ้มที่สุด
                        </span>
                    </div>

                    <h3 class="text-xl font-bold text-white">{{ pkg.name_th || pkg.name }}</h3>
                    <p class="text-gray-400 text-sm mt-1">{{ pkg.description_th || pkg.description }}</p>

                    <!-- Price -->
                    <div class="mt-4">
                        <div class="flex items-baseline">
                            <span class="text-3xl font-bold text-white">{{ formatPrice(pkg.price) }}</span>
                            <span class="text-gray-400 ml-2">/ {{ pkg.duration_text }}</span>
                        </div>
                        <div v-if="pkg.original_price" class="mt-1">
                            <span class="text-gray-500 line-through text-sm">{{ formatPrice(pkg.original_price) }}</span>
                            <span class="text-green-400 text-sm ml-2">ประหยัด {{ pkg.discount_percentage }}%</span>
                        </div>
                        <p v-if="pkg.price_per_day" class="text-gray-500 text-xs mt-1">
                            {{ formatPrice(pkg.price_per_day) }}/วัน
                        </p>
                    </div>

                    <!-- Features -->
                    <ul class="mt-4 space-y-2">
                        <li class="flex items-center text-sm text-gray-300">
                            <CheckIcon class="w-4 h-4 text-green-400 mr-2" />
                            {{ pkg.limits?.posts === -1 ? 'โพสต์ไม่จำกัด' : `${pkg.limits?.posts} โพสต์` }}
                        </li>
                        <li class="flex items-center text-sm text-gray-300">
                            <CheckIcon class="w-4 h-4 text-green-400 mr-2" />
                            {{ pkg.limits?.ai_generations === -1 ? 'AI ไม่จำกัด' : `${pkg.limits?.ai_generations} AI generations` }}
                        </li>
                        <li class="flex items-center text-sm text-gray-300">
                            <CheckIcon class="w-4 h-4 text-green-400 mr-2" />
                            {{ pkg.limits?.brands === -1 ? 'แบรนด์ไม่จำกัด' : `${pkg.limits?.brands} แบรนด์` }}
                        </li>
                        <li class="flex items-center text-sm text-gray-300">
                            <CheckIcon class="w-4 h-4 text-green-400 mr-2" />
                            {{ pkg.limits?.platforms }} แพลตฟอร์ม
                        </li>
                    </ul>

                    <!-- Select Button -->
                    <button
                        class="w-full mt-6 py-2 rounded-lg font-medium transition-colors"
                        :class="selectedPackage?.id === pkg.id
                            ? 'bg-indigo-600 text-white'
                            : 'bg-gray-700 text-gray-300 hover:bg-gray-600'"
                    >
                        {{ selectedPackage?.id === pkg.id ? 'เลือกแล้ว' : 'เลือกแพ็กเกจนี้' }}
                    </button>
                </div>
            </div>
        </div>

        <!-- Promo Code & Checkout -->
        <div v-if="selectedPackage" class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-4">ชำระเงิน</h2>

            <!-- Promo Code -->
            <div class="mb-6">
                <label class="block text-sm font-medium text-gray-300 mb-2">โค้ดส่วนลด (ถ้ามี)</label>
                <div class="flex gap-2">
                    <input
                        v-model="promoCode"
                        type="text"
                        class="flex-1 px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white uppercase"
                        placeholder="PROMO2024"
                        :disabled="promoApplied"
                    />
                    <button
                        v-if="!promoApplied"
                        class="px-4 py-2 bg-gray-600 hover:bg-gray-500 text-white rounded-lg transition-colors"
                        :disabled="!promoCode || promoLoading"
                        @click="validatePromo"
                    >
                        {{ promoLoading ? '...' : 'ใช้โค้ด' }}
                    </button>
                    <button
                        v-else
                        class="px-4 py-2 bg-red-600/20 text-red-400 rounded-lg"
                        @click="removePromo"
                    >
                        ลบโค้ด
                    </button>
                </div>
                <p v-if="promoMessage" :class="promoApplied ? 'text-green-400' : 'text-red-400'" class="text-sm mt-1">
                    {{ promoMessage }}
                </p>
            </div>

            <!-- Order Summary -->
            <div class="bg-gray-700/50 rounded-lg p-4 mb-6">
                <div class="flex justify-between text-gray-300 mb-2">
                    <span>{{ selectedPackage.name_th || selectedPackage.name }}</span>
                    <span>{{ formatPrice(selectedPackage.price) }}</span>
                </div>
                <div v-if="discountAmount > 0" class="flex justify-between text-green-400 mb-2">
                    <span>ส่วนลด</span>
                    <span>-{{ formatPrice(discountAmount) }}</span>
                </div>
                <hr class="border-gray-600 my-2" />
                <div class="flex justify-between text-white font-bold">
                    <span>รวมทั้งสิ้น</span>
                    <span>{{ formatPrice(totalAmount) }}</span>
                </div>
            </div>

            <!-- Payment Method -->
            <div class="mb-6">
                <label class="block text-sm font-medium text-gray-300 mb-2">เลือกวิธีชำระเงิน</label>
                <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
                    <button
                        v-for="method in paymentMethods"
                        :key="method.id"
                        :class="[
                            'p-4 rounded-lg border text-left transition-colors',
                            selectedMethod === method.id
                                ? 'border-indigo-500 bg-indigo-500/10'
                                : 'border-gray-600 hover:border-gray-500'
                        ]"
                        @click="selectedMethod = method.id"
                    >
                        <p class="text-white font-medium">{{ method.name_th }}</p>
                        <p class="text-gray-400 text-xs mt-1">{{ method.description }}</p>
                    </button>
                </div>
            </div>

            <!-- Checkout Button -->
            <button
                class="w-full py-3 bg-indigo-600 hover:bg-indigo-700 text-white font-medium rounded-lg transition-colors disabled:opacity-50"
                :disabled="!selectedMethod || checkoutLoading"
                @click="checkout"
            >
                {{ checkoutLoading ? 'กำลังดำเนินการ...' : `ชำระเงิน ${formatPrice(totalAmount)}` }}
            </button>
        </div>

        <!-- Payment Modal -->
        <div v-if="showPaymentModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
            <div class="bg-gray-800 rounded-xl p-6 max-w-md w-full mx-4 border border-gray-700">
                <h3 class="text-xl font-bold text-white mb-4">ชำระเงิน</h3>

                <!-- PromptPay QR -->
                <div v-if="paymentInfo.method === 'promptpay'" class="text-center">
                    <p class="text-gray-400 mb-4">สแกน QR Code ด้วยแอปธนาคารของคุณ</p>
                    <div class="bg-white p-4 rounded-lg inline-block">
                        <img :src="'data:image/png;base64,' + paymentInfo.qr_code" alt="PromptPay QR" class="w-48 h-48" />
                    </div>
                    <p class="text-white font-bold mt-4">{{ formatPrice(paymentInfo.amount) }}</p>
                    <p class="text-gray-400 text-sm mt-2">Ref: {{ paymentInfo.reference }}</p>
                </div>

                <!-- Bank Transfer -->
                <div v-if="paymentInfo.method === 'bank_transfer'">
                    <p class="text-gray-400 mb-4">โอนเงินไปยังบัญชีด้านล่าง</p>
                    <div v-for="bank in paymentInfo.bank_accounts" :key="bank.bank_code" class="bg-gray-700 rounded-lg p-4 mb-3">
                        <p class="text-white font-medium">{{ bank.bank_name }}</p>
                        <p class="text-gray-300">{{ bank.account_number }}</p>
                        <p class="text-gray-400 text-sm">{{ bank.account_name }}</p>
                    </div>
                    <p class="text-white font-bold">จำนวน: {{ formatPrice(paymentInfo.amount) }}</p>
                    <p class="text-gray-400 text-sm">Ref: {{ paymentInfo.reference }}</p>

                    <!-- Upload Slip -->
                    <div class="mt-4">
                        <label class="block text-sm font-medium text-gray-300 mb-2">อัพโหลดสลิป</label>
                        <input
                            type="file"
                            accept="image/*"
                            class="w-full text-gray-400"
                            @change="handleSlipUpload"
                        />
                    </div>
                </div>

                <div class="flex gap-3 mt-6">
                    <button
                        class="flex-1 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg"
                        @click="showPaymentModal = false"
                    >
                        ปิด
                    </button>
                    <button
                        v-if="paymentInfo.method === 'bank_transfer'"
                        class="flex-1 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg"
                        :disabled="!slipFile"
                        @click="submitSlip"
                    >
                        ส่งสลิป
                    </button>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { rentalApi } from '../../services/api'
import {
    CheckIcon,
    ExclamationTriangleIcon,
} from '@heroicons/vue/24/outline'

const route = useRoute()
const authStore = useAuthStore()

const packages = ref([])
const paymentMethods = ref([])
const selectedPackage = ref(null)
const selectedMethod = ref('')
const promoCode = ref('')
const promoApplied = ref(false)
const promoMessage = ref('')
const promoLoading = ref(false)
const discountAmount = ref(0)
const checkoutLoading = ref(false)
const showPaymentModal = ref(false)
const paymentInfo = reactive({})
const slipFile = ref(null)

const upgradeFeature = computed(() => route.query.upgrade)

const totalAmount = computed(() => {
    if (!selectedPackage.value) return 0
    return Math.max(0, selectedPackage.value.price - discountAmount.value)
})

const formatPrice = (price) => {
    return new Intl.NumberFormat('th-TH', {
        style: 'currency',
        currency: 'THB',
        minimumFractionDigits: 0
    }).format(price || 0)
}

const formatDate = (date) => {
    if (!date) return '-'
    return new Date(date).toLocaleDateString('th-TH', {
        year: 'numeric',
        month: 'long',
        day: 'numeric'
    })
}

const getFeatureLabel = (feature) => {
    const labels = {
        web_learning: 'เครื่องมือเรียนรู้',
        ai_tools: 'AI Tools',
        analytics: 'วิเคราะห์',
        api_access: 'API Access',
    }
    return labels[feature] || feature
}

const selectPackage = (pkg) => {
    selectedPackage.value = pkg
    discountAmount.value = 0
    promoApplied.value = false
    promoMessage.value = ''
}

const validatePromo = async () => {
    if (!promoCode.value) return

    promoLoading.value = true
    try {
        const response = await rentalApi.validatePromo(promoCode.value, selectedPackage.value?.id)
        if (response.data.success) {
            promoApplied.value = true
            promoMessage.value = response.data.message
            const promo = response.data.data
            if (promo.discount_type === 'percentage') {
                discountAmount.value = Math.min(
                    (selectedPackage.value.price * promo.discount_value / 100),
                    promo.max_discount || Infinity
                )
            } else {
                discountAmount.value = promo.discount_value
            }
        } else {
            promoMessage.value = response.data.message
        }
    } catch (error) {
        promoMessage.value = 'เกิดข้อผิดพลาด'
    }
    promoLoading.value = false
}

const removePromo = () => {
    promoCode.value = ''
    promoApplied.value = false
    promoMessage.value = ''
    discountAmount.value = 0
}

const checkout = async () => {
    if (!selectedPackage.value || !selectedMethod.value) return

    checkoutLoading.value = true
    try {
        const response = await rentalApi.checkout({
            package_id: selectedPackage.value.id,
            payment_method: selectedMethod.value,
            promo_code: promoApplied.value ? promoCode.value : null,
        })

        if (response.data.success) {
            const data = response.data.data
            if (data.requires_payment) {
                Object.assign(paymentInfo, {
                    method: selectedMethod.value,
                    ...data.payment_info,
                    uuid: data.payment.id,
                    reference: data.payment.reference,
                    amount: data.payment.amount,
                })
                showPaymentModal.value = true
            } else {
                // Free trial activated
                alert(response.data.message)
                await authStore.fetchRentalStatus()
            }
        }
    } catch (error) {
        alert(error.response?.data?.message || 'เกิดข้อผิดพลาด')
    }
    checkoutLoading.value = false
}

const handleSlipUpload = (event) => {
    slipFile.value = event.target.files[0]
}

const submitSlip = async () => {
    if (!slipFile.value || !paymentInfo.uuid) return

    try {
        await rentalApi.uploadSlip(paymentInfo.uuid, slipFile.value)
        alert('อัพโหลดสลิปสำเร็จ รอตรวจสอบ')
        showPaymentModal.value = false
    } catch (error) {
        alert(error.response?.data?.message || 'เกิดข้อผิดพลาด')
    }
}

onMounted(async () => {
    try {
        const [packagesRes, methodsRes] = await Promise.all([
            rentalApi.packages(),
            rentalApi.paymentMethods(),
        ])
        packages.value = packagesRes.data.data
        paymentMethods.value = methodsRes.data.data
    } catch (error) {
        console.error('Failed to load data:', error)
    }
})
</script>
