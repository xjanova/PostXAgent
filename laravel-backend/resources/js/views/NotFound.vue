<template>
    <div class="min-h-screen bg-gray-900 flex items-center justify-center px-4">
        <div class="text-center">
            <h1 class="text-9xl font-bold text-indigo-600">404</h1>
            <div class="mt-4">
                <h2 class="text-3xl font-semibold text-white mb-2">ไม่พบหน้านี้</h2>
                <p class="text-gray-400 mb-8">
                    ขออภัย หน้าที่คุณกำลังค้นหาไม่มีอยู่หรือถูกย้ายไปแล้ว
                </p>
            </div>

            <div class="flex flex-col sm:flex-row items-center justify-center gap-4">
                <router-link
                    to="/"
                    class="px-6 py-3 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg font-medium transition-colors flex items-center gap-2"
                >
                    <HomeIcon class="w-5 h-5" />
                    กลับหน้าหลัก
                </router-link>
                <button
                    @click="$router.back()"
                    class="px-6 py-3 bg-gray-700 hover:bg-gray-600 text-white rounded-lg font-medium transition-colors flex items-center gap-2"
                >
                    <ArrowLeftIcon class="w-5 h-5" />
                    ย้อนกลับ
                </button>
            </div>

            <div class="mt-12 text-gray-500 text-sm">
                <p>หากคุณคิดว่านี่เป็นข้อผิดพลาด กรุณาติดต่อผู้ดูแลระบบ</p>
            </div>
        </div>
    </div>
</template>

<script setup>
import { HomeIcon, ArrowLeftIcon } from '@heroicons/vue/24/outline'
</script>
