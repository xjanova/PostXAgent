<template>
    <div>
        <h2 class="text-2xl font-bold text-white text-center mb-6">เข้าสู่ระบบ</h2>

        <form @submit.prevent="handleSubmit" class="space-y-5">
            <!-- Email -->
            <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">อีเมล</label>
                <input
                    v-model="form.email"
                    type="email"
                    class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
                    placeholder="your@email.com"
                    required
                />
                <p v-if="errors.email" class="mt-1 text-sm text-red-400">{{ errors.email }}</p>
            </div>

            <!-- Password -->
            <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">รหัสผ่าน</label>
                <div class="relative">
                    <input
                        v-model="form.password"
                        :type="showPassword ? 'text' : 'password'"
                        class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
                        placeholder="********"
                        required
                    />
                    <button
                        type="button"
                        class="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400 hover:text-white"
                        @click="showPassword = !showPassword"
                    >
                        <EyeIcon v-if="!showPassword" class="w-5 h-5" />
                        <EyeSlashIcon v-else class="w-5 h-5" />
                    </button>
                </div>
                <p v-if="errors.password" class="mt-1 text-sm text-red-400">{{ errors.password }}</p>
            </div>

            <!-- Remember & Forgot -->
            <div class="flex items-center justify-between">
                <label class="flex items-center">
                    <input
                        v-model="form.remember"
                        type="checkbox"
                        class="w-4 h-4 rounded bg-gray-700 border-gray-600 text-indigo-600 focus:ring-indigo-500"
                    />
                    <span class="ml-2 text-sm text-gray-400">จดจำฉัน</span>
                </label>
                <router-link
                    to="/auth/forgot-password"
                    class="text-sm text-indigo-400 hover:text-indigo-300"
                >
                    ลืมรหัสผ่าน?
                </router-link>
            </div>

            <!-- Error Message -->
            <div v-if="errorMessage" class="p-3 bg-red-500/20 border border-red-500/50 rounded-lg">
                <p class="text-sm text-red-400">{{ errorMessage }}</p>
            </div>

            <!-- Submit -->
            <button
                type="submit"
                :disabled="loading"
                class="w-full py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-medium rounded-lg transition-colors disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center"
            >
                <svg v-if="loading" class="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                {{ loading ? 'กำลังเข้าสู่ระบบ...' : 'เข้าสู่ระบบ' }}
            </button>
        </form>

        <!-- Divider -->
        <div class="relative my-6">
            <div class="absolute inset-0 flex items-center">
                <div class="w-full border-t border-gray-700"></div>
            </div>
            <div class="relative flex justify-center text-sm">
                <span class="px-2 bg-gray-800 text-gray-500">หรือ</span>
            </div>
        </div>

        <!-- Social Login -->
        <div class="space-y-3">
            <button
                type="button"
                class="w-full py-3 px-4 bg-gray-700 hover:bg-gray-600 text-white font-medium rounded-lg transition-colors flex items-center justify-center"
            >
                <svg class="w-5 h-5 mr-2" viewBox="0 0 24 24">
                    <path fill="currentColor" d="M12.545,10.239v3.821h5.445c-0.712,2.315-2.647,3.972-5.445,3.972c-3.332,0-6.033-2.701-6.033-6.032s2.701-6.032,6.033-6.032c1.498,0,2.866,0.549,3.921,1.453l2.814-2.814C17.503,2.988,15.139,2,12.545,2C7.021,2,2.543,6.477,2.543,12s4.478,10,10.002,10c8.396,0,10.249-7.85,9.426-11.748L12.545,10.239z"/>
                </svg>
                เข้าสู่ระบบด้วย Google
            </button>
        </div>

        <!-- Register Link -->
        <p class="mt-6 text-center text-gray-400">
            ยังไม่มีบัญชี?
            <router-link to="/auth/register" class="text-indigo-400 hover:text-indigo-300 font-medium">
                สมัครสมาชิก
            </router-link>
        </p>
    </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { EyeIcon, EyeSlashIcon } from '@heroicons/vue/24/outline'

const router = useRouter()
const route = useRoute()
const authStore = useAuthStore()

const form = reactive({
    email: '',
    password: '',
    remember: false,
})

const errors = reactive({
    email: '',
    password: '',
})

const loading = ref(false)
const showPassword = ref(false)
const errorMessage = ref('')

const handleSubmit = async () => {
    // Reset errors
    errors.email = ''
    errors.password = ''
    errorMessage.value = ''

    // Basic validation
    if (!form.email) {
        errors.email = 'กรุณากรอกอีเมล'
        return
    }
    if (!form.password) {
        errors.password = 'กรุณากรอกรหัสผ่าน'
        return
    }

    loading.value = true

    const result = await authStore.login({
        email: form.email,
        password: form.password,
        remember: form.remember,
    })

    loading.value = false

    if (result.success) {
        const redirect = route.query.redirect || '/'
        router.push(redirect)
    } else {
        errorMessage.value = result.message
    }
}
</script>
