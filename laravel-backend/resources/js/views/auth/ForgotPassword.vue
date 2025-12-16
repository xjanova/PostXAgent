<template>
    <div>
        <h2 class="text-2xl font-bold text-white text-center mb-2">ลืมรหัสผ่าน</h2>
        <p class="text-gray-400 text-center mb-6">กรอกอีเมลเพื่อรับลิงก์รีเซ็ตรหัสผ่าน</p>

        <!-- Success Message -->
        <div v-if="sent" class="p-4 bg-green-500/20 border border-green-500/50 rounded-lg mb-6">
            <p class="text-sm text-green-400">
                ส่งลิงก์รีเซ็ตรหัสผ่านไปยังอีเมลของคุณแล้ว กรุณาตรวจสอบกล่องจดหมาย
            </p>
        </div>

        <form v-if="!sent" @submit.prevent="handleSubmit" class="space-y-5">
            <!-- Email -->
            <div>
                <label class="block text-sm font-medium text-gray-300 mb-1">อีเมล</label>
                <input
                    v-model="email"
                    type="email"
                    class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    placeholder="your@email.com"
                    required
                />
            </div>

            <!-- Error Message -->
            <div v-if="errorMessage" class="p-3 bg-red-500/20 border border-red-500/50 rounded-lg">
                <p class="text-sm text-red-400">{{ errorMessage }}</p>
            </div>

            <!-- Submit -->
            <button
                type="submit"
                :disabled="loading"
                class="w-full py-3 px-4 bg-indigo-600 hover:bg-indigo-700 text-white font-medium rounded-lg transition-colors disabled:opacity-50 flex items-center justify-center"
            >
                <svg v-if="loading" class="animate-spin -ml-1 mr-3 h-5 w-5 text-white" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                    <circle class="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" stroke-width="4"></circle>
                    <path class="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
                </svg>
                {{ loading ? 'กำลังส่ง...' : 'ส่งลิงก์รีเซ็ตรหัสผ่าน' }}
            </button>
        </form>

        <!-- Back to Login -->
        <p class="mt-6 text-center">
            <router-link to="/auth/login" class="text-indigo-400 hover:text-indigo-300 font-medium">
                &larr; กลับไปหน้าเข้าสู่ระบบ
            </router-link>
        </p>
    </div>
</template>

<script setup>
import { ref } from 'vue'
import { authApi } from '../../services/api'

const email = ref('')
const loading = ref(false)
const sent = ref(false)
const errorMessage = ref('')

const handleSubmit = async () => {
    errorMessage.value = ''
    loading.value = true

    try {
        await authApi.forgotPassword(email.value)
        sent.value = true
    } catch (error) {
        errorMessage.value = error.response?.data?.message || 'เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง'
    } finally {
        loading.value = false
    }
}
</script>
