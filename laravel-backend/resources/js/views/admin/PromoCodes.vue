<template>
    <div class="space-y-6">
        <div class="flex items-center justify-between">
            <h1 class="text-2xl font-bold text-white">จัดการโค้ดส่วนลด</h1>
            <button @click="showCreateModal = true" class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg transition-colors">
                + สร้างโค้ดใหม่
            </button>
        </div>

        <!-- Stats Cards -->
        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">โค้ดทั้งหมด</p>
                <p class="text-2xl font-bold text-white">{{ stats.total }}</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">ใช้งานได้</p>
                <p class="text-2xl font-bold text-green-400">{{ stats.active }}</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">ถูกใช้ไปแล้ว</p>
                <p class="text-2xl font-bold text-blue-400">{{ stats.totalUsed }}</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-4 border border-gray-700">
                <p class="text-gray-400 text-sm">ส่วนลดรวม</p>
                <p class="text-2xl font-bold text-purple-400">{{ formatCurrency(stats.totalDiscount) }}</p>
            </div>
        </div>

        <!-- Promo Codes Table -->
        <div class="bg-gray-800 rounded-xl border border-gray-700 overflow-hidden">
            <table class="w-full">
                <thead class="bg-gray-700/50">
                    <tr>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">โค้ด</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">ส่วนลด</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">ใช้ได้กับ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">การใช้งาน</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">สถานะ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">หมดอายุ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">จัดการ</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-gray-700">
                    <tr v-for="promo in promoCodes" :key="promo.id" class="hover:bg-gray-700/50">
                        <td class="px-4 py-3">
                            <div class="flex items-center space-x-2">
                                <span class="font-mono text-white bg-gray-700 px-2 py-1 rounded">{{ promo.code }}</span>
                                <button @click="copyCode(promo.code)" class="text-gray-400 hover:text-white">
                                    <ClipboardIcon class="w-4 h-4" />
                                </button>
                            </div>
                        </td>
                        <td class="px-4 py-3">
                            <span v-if="promo.discount_type === 'percentage'" class="text-green-400 font-semibold">
                                {{ promo.discount_value }}%
                            </span>
                            <span v-else class="text-green-400 font-semibold">
                                {{ formatCurrency(promo.discount_value) }}
                            </span>
                        </td>
                        <td class="px-4 py-3 text-gray-400 text-sm">
                            <span v-if="promo.applicable_packages?.length">
                                {{ promo.applicable_packages.map(p => p.name).join(', ') }}
                            </span>
                            <span v-else>ทุกแพ็กเกจ</span>
                        </td>
                        <td class="px-4 py-3">
                            <span class="text-white">{{ promo.used_count || 0 }}</span>
                            <span class="text-gray-500">/{{ promo.max_uses || '∞' }}</span>
                        </td>
                        <td class="px-4 py-3">
                            <span :class="promo.is_active ? 'bg-green-500/20 text-green-400' : 'bg-red-500/20 text-red-400'" class="px-2 py-1 text-xs rounded-full">
                                {{ promo.is_active ? 'ใช้งานได้' : 'ปิดใช้งาน' }}
                            </span>
                        </td>
                        <td class="px-4 py-3 text-gray-400 text-sm">
                            {{ promo.expires_at ? formatDate(promo.expires_at) : 'ไม่มี' }}
                        </td>
                        <td class="px-4 py-3">
                            <div class="flex space-x-2">
                                <button class="text-gray-400 hover:text-white" @click="editPromo(promo)">แก้ไข</button>
                                <button v-if="promo.is_active" class="text-red-400 hover:text-red-300" @click="deactivatePromo(promo)">ปิด</button>
                                <button v-else class="text-green-400 hover:text-green-300" @click="activatePromo(promo)">เปิด</button>
                            </div>
                        </td>
                    </tr>
                </tbody>
            </table>

            <div v-if="promoCodes.length === 0" class="p-12 text-center">
                <p class="text-gray-400">ไม่มีโค้ดส่วนลด</p>
            </div>
        </div>

        <!-- Create/Edit Modal -->
        <div v-if="showCreateModal || showEditModal" class="fixed inset-0 z-50 flex items-center justify-center bg-black/70" @click.self="closeModal">
            <div class="bg-gray-800 rounded-xl p-6 w-full max-w-lg border border-gray-700">
                <div class="flex justify-between items-center mb-6">
                    <h2 class="text-xl font-bold text-white">{{ showEditModal ? 'แก้ไขโค้ดส่วนลด' : 'สร้างโค้ดส่วนลดใหม่' }}</h2>
                    <button @click="closeModal" class="text-gray-400 hover:text-white">&times;</button>
                </div>

                <form @submit.prevent="savePromo" class="space-y-4">
                    <div>
                        <label class="block text-sm text-gray-400 mb-1">โค้ด</label>
                        <div class="flex space-x-2">
                            <input v-model="promoForm.code" type="text" required :disabled="showEditModal"
                                class="flex-1 px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white uppercase"
                                placeholder="เช่น NEWYEAR2024" />
                            <button v-if="!showEditModal" type="button" @click="generateCode" class="px-4 py-2 bg-gray-600 hover:bg-gray-500 text-white rounded-lg">
                                สุ่ม
                            </button>
                        </div>
                    </div>

                    <div class="grid grid-cols-2 gap-4">
                        <div>
                            <label class="block text-sm text-gray-400 mb-1">ประเภทส่วนลด</label>
                            <select v-model="promoForm.discount_type" class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white">
                                <option value="percentage">เปอร์เซ็นต์ (%)</option>
                                <option value="fixed">จำนวนเงิน (฿)</option>
                            </select>
                        </div>
                        <div>
                            <label class="block text-sm text-gray-400 mb-1">มูลค่าส่วนลด</label>
                            <input v-model.number="promoForm.discount_value" type="number" min="1" required
                                class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white"
                                :placeholder="promoForm.discount_type === 'percentage' ? 'เช่น 20' : 'เช่น 100'" />
                        </div>
                    </div>

                    <div v-if="promoForm.discount_type === 'percentage'">
                        <label class="block text-sm text-gray-400 mb-1">ส่วนลดสูงสุด (฿)</label>
                        <input v-model.number="promoForm.max_discount" type="number" min="0"
                            class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white"
                            placeholder="เว้นว่างถ้าไม่จำกัด" />
                    </div>

                    <div class="grid grid-cols-2 gap-4">
                        <div>
                            <label class="block text-sm text-gray-400 mb-1">ยอดขั้นต่ำ (฿)</label>
                            <input v-model.number="promoForm.min_purchase" type="number" min="0"
                                class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white"
                                placeholder="เว้นว่างถ้าไม่มีขั้นต่ำ" />
                        </div>
                        <div>
                            <label class="block text-sm text-gray-400 mb-1">จำนวนครั้งที่ใช้ได้</label>
                            <input v-model.number="promoForm.max_uses" type="number" min="1"
                                class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white"
                                placeholder="เว้นว่างถ้าไม่จำกัด" />
                        </div>
                    </div>

                    <div>
                        <label class="block text-sm text-gray-400 mb-1">ใช้ได้กับแพ็กเกจ</label>
                        <div class="space-y-2">
                            <label v-for="pkg in packages" :key="pkg.id" class="flex items-center space-x-2 cursor-pointer">
                                <input type="checkbox" v-model="promoForm.applicable_package_ids" :value="pkg.id" class="rounded bg-gray-700 border-gray-600 text-indigo-600" />
                                <span class="text-white">{{ pkg.name }}</span>
                                <span class="text-gray-500 text-sm">{{ formatCurrency(pkg.price) }}</span>
                            </label>
                        </div>
                        <p class="text-gray-500 text-xs mt-1">ไม่เลือก = ใช้ได้กับทุกแพ็กเกจ</p>
                    </div>

                    <div class="grid grid-cols-2 gap-4">
                        <div>
                            <label class="block text-sm text-gray-400 mb-1">เริ่มใช้งาน</label>
                            <input v-model="promoForm.starts_at" type="datetime-local"
                                class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white" />
                        </div>
                        <div>
                            <label class="block text-sm text-gray-400 mb-1">หมดอายุ</label>
                            <input v-model="promoForm.expires_at" type="datetime-local"
                                class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white" />
                        </div>
                    </div>

                    <div>
                        <label class="block text-sm text-gray-400 mb-1">รายละเอียด</label>
                        <textarea v-model="promoForm.description" rows="2"
                            class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white"
                            placeholder="คำอธิบายเพิ่มเติม (ไม่บังคับ)"></textarea>
                    </div>

                    <div class="flex items-center space-x-2">
                        <input type="checkbox" v-model="promoForm.is_active" id="is_active" class="rounded bg-gray-700 border-gray-600 text-indigo-600" />
                        <label for="is_active" class="text-white">เปิดใช้งานทันที</label>
                    </div>

                    <div class="flex space-x-3">
                        <button type="button" @click="closeModal" class="flex-1 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors">
                            ยกเลิก
                        </button>
                        <button type="submit" :disabled="saving" class="flex-1 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg transition-colors disabled:opacity-50">
                            {{ saving ? 'กำลังบันทึก...' : 'บันทึก' }}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { adminApi, rentalApi } from '../../services/api'
import { ClipboardIcon } from '@heroicons/vue/24/outline'

const promoCodes = ref([])
const packages = ref([])
const showCreateModal = ref(false)
const showEditModal = ref(false)
const saving = ref(false)
const stats = ref({
    total: 0,
    active: 0,
    totalUsed: 0,
    totalDiscount: 0,
})

const promoForm = reactive({
    id: null,
    code: '',
    discount_type: 'percentage',
    discount_value: 10,
    max_discount: null,
    min_purchase: null,
    max_uses: null,
    applicable_package_ids: [],
    starts_at: '',
    expires_at: '',
    description: '',
    is_active: true,
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

const fetchPromoCodes = async () => {
    try {
        const response = await adminApi.promoCodes()
        if (response.data.success) {
            promoCodes.value = response.data.data
            calculateStats()
        }
    } catch (error) {
        console.error('Failed to fetch promo codes:', error)
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

const calculateStats = () => {
    stats.value = {
        total: promoCodes.value.length,
        active: promoCodes.value.filter(p => p.is_active).length,
        totalUsed: promoCodes.value.reduce((sum, p) => sum + (p.used_count || 0), 0),
        totalDiscount: promoCodes.value.reduce((sum, p) => sum + (p.total_discount_given || 0), 0),
    }
}

const generateCode = () => {
    const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789'
    let code = ''
    for (let i = 0; i < 8; i++) {
        code += chars.charAt(Math.floor(Math.random() * chars.length))
    }
    promoForm.code = code
}

const copyCode = (code) => {
    navigator.clipboard.writeText(code)
    alert('คัดลอกโค้ดแล้ว: ' + code)
}

const resetForm = () => {
    promoForm.id = null
    promoForm.code = ''
    promoForm.discount_type = 'percentage'
    promoForm.discount_value = 10
    promoForm.max_discount = null
    promoForm.min_purchase = null
    promoForm.max_uses = null
    promoForm.applicable_package_ids = []
    promoForm.starts_at = ''
    promoForm.expires_at = ''
    promoForm.description = ''
    promoForm.is_active = true
}

const closeModal = () => {
    showCreateModal.value = false
    showEditModal.value = false
    resetForm()
}

const editPromo = (promo) => {
    promoForm.id = promo.id
    promoForm.code = promo.code
    promoForm.discount_type = promo.discount_type
    promoForm.discount_value = promo.discount_value
    promoForm.max_discount = promo.max_discount
    promoForm.min_purchase = promo.min_purchase
    promoForm.max_uses = promo.max_uses
    promoForm.applicable_package_ids = promo.applicable_packages?.map(p => p.id) || []
    promoForm.starts_at = promo.starts_at ? new Date(promo.starts_at).toISOString().slice(0, 16) : ''
    promoForm.expires_at = promo.expires_at ? new Date(promo.expires_at).toISOString().slice(0, 16) : ''
    promoForm.description = promo.description || ''
    promoForm.is_active = promo.is_active
    showEditModal.value = true
}

const savePromo = async () => {
    saving.value = true
    try {
        const data = {
            code: promoForm.code.toUpperCase(),
            discount_type: promoForm.discount_type,
            discount_value: promoForm.discount_value,
            max_discount: promoForm.max_discount,
            min_purchase: promoForm.min_purchase,
            max_uses: promoForm.max_uses,
            applicable_package_ids: promoForm.applicable_package_ids,
            starts_at: promoForm.starts_at || null,
            expires_at: promoForm.expires_at || null,
            description: promoForm.description,
            is_active: promoForm.is_active,
        }

        if (promoForm.id) {
            await adminApi.updatePromoCode(promoForm.id, data)
        } else {
            await adminApi.createPromoCode(data)
        }

        closeModal()
        fetchPromoCodes()
    } catch (error) {
        console.error('Failed to save promo code:', error)
        alert('เกิดข้อผิดพลาด: ' + (error.response?.data?.message || error.message))
    } finally {
        saving.value = false
    }
}

const deactivatePromo = async (promo) => {
    if (confirm(`ปิดใช้งานโค้ด ${promo.code}?`)) {
        try {
            await adminApi.updatePromoCode(promo.id, { is_active: false })
            fetchPromoCodes()
        } catch (error) {
            console.error('Failed to deactivate promo:', error)
        }
    }
}

const activatePromo = async (promo) => {
    try {
        await adminApi.updatePromoCode(promo.id, { is_active: true })
        fetchPromoCodes()
    } catch (error) {
        console.error('Failed to activate promo:', error)
    }
}

onMounted(() => {
    fetchPromoCodes()
    fetchPackages()
})
</script>
