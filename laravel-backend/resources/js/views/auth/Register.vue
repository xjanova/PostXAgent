<template>
    <div>
        <h2 class="text-2xl font-bold text-white text-center mb-6">สมัครสมาชิก</h2>

        <form @submit.prevent="handleSubmit" class="space-y-4">
            <!-- Name -->
            <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">ชื่อ-นามสกุล</label>
                <input
                    v-model="form.name"
                    type="text"
                    class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    placeholder="ชื่อของคุณ"
                    required
                />
                <p v-if="errors.name" class="mt-1 text-sm text-red-400">{{ errors.name }}</p>
            </div>

            <!-- Email -->
            <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">อีเมล</label>
                <input
                    v-model="form.email"
                    type="email"
                    class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    placeholder="your@email.com"
                    required
                />
                <p v-if="errors.email" class="mt-1 text-sm text-red-400">{{ errors.email }}</p>
            </div>

            <!-- Phone (Optional) -->
            <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">เบอร์โทรศัพท์ (ไม่บังคับ)</label>
                <input
                    v-model="form.phone"
                    type="tel"
                    class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    placeholder="08X-XXX-XXXX"
                />
            </div>

            <!-- Password -->
            <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">รหัสผ่าน</label>
                <input
                    v-model="form.password"
                    type="password"
                    class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    placeholder="อย่างน้อย 8 ตัวอักษร"
                    required
                />
                <p v-if="errors.password" class="mt-1 text-sm text-red-400">{{ errors.password }}</p>
            </div>

            <!-- Confirm Password -->
            <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">ยืนยันรหัสผ่าน</label>
                <input
                    v-model="form.password_confirmation"
                    type="password"
                    class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    placeholder="ยืนยันรหัสผ่าน"
                    required
                />
            </div>

            <!-- Terms -->
            <div class="flex items-start">
                <input
                    v-model="form.terms"
                    type="checkbox"
                    class="w-4 h-4 mt-1 rounded bg-gray-700 border-gray-600 text-indigo-600 focus:ring-indigo-500"
                    required
                />
                <label class="ml-2 text-sm text-gray-400">
                    ฉันยอมรับ
                    <a href="#" class="text-indigo-400 hover:text-indigo-300">ข้อกำหนดการใช้งาน</a>
                    และ
                    <a href="#" class="text-indigo-400 hover:text-indigo-300">นโยบายความเป็นส่วนตัว</a>
                </label>
            </div>

            <!-- Error Message -->
            <div v-if="errorMessage" class="p-3 bg-red-500/20 border border-red-500/50 rounded-lg">
                <p class="text-sm text-red-400">{{ errorMessage }}</p>
            </div>

            <!-- Submit -->
            <button
                type="submit"
                :disabled="loading || !form.terms"
                class="w-full py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-medium rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center"
            >
                <svg v-if="loading" class="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                {{ loading ? 'กำลังสมัครสมาชิก...' : 'สมัครสมาชิก' }}
            </button>
        </form>

        <!-- Login Link -->
        <p class="mt-6 text-center text-gray-400">
            มีบัญชีอยู่แล้ว?
            <router-link to="/auth/login" class="text-indigo-400 hover:text-indigo-300 font-medium">
                เข้าสู่ระบบ
            </router-link>
        </p>
    </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const form = reactive({
    name: '',
    email: '',
    phone: '',
    password: '',
    password_confirmation: '',
    terms: false,
})

const errors = reactive({
    name: '',
    email: '',
    password: '',
})

const loading = ref(false)
const errorMessage = ref('')

const handleSubmit = async () => {
    // Reset errors
    Object.keys(errors).forEach(key => errors[key] = '')
    errorMessage.value = ''

    // Validation
    if (form.password !== form.password_confirmation) {
        errors.password = 'รหัสผ่านไม่ตรงกัน'
        return
    }

    if (form.password.length < 8) {
        errors.password = 'รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร'
        return
    }

    loading.value = true

    const result = await authStore.register(form)

    loading.value = false

    if (result.success) {
        router.push('/')
    } else {
        if (result.errors) {
            Object.keys(result.errors).forEach(key => {
                if (errors[key] !== undefined) {
                    errors[key] = result.errors[key][0]
                }
            })
        }
        errorMessage.value = result.message
    }
}
</script>
