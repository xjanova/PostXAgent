<template>
    <div class="space-y-6">
        <h1 class="text-2xl font-bold text-white">วิเคราะห์ผลงาน</h1>

        <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <p class="text-gray-400 text-sm">การเข้าถึง</p>
                <p class="text-2xl font-bold text-white mt-1">{{ formatNumber(stats.reach) }}</p>
                <p class="text-green-400 text-sm mt-2">+12.5%</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <p class="text-gray-400 text-sm">การมีส่วนร่วม</p>
                <p class="text-2xl font-bold text-white mt-1">{{ formatNumber(stats.engagement) }}</p>
                <p class="text-green-400 text-sm mt-2">+8.3%</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <p class="text-gray-400 text-sm">คลิก</p>
                <p class="text-2xl font-bold text-white mt-1">{{ formatNumber(stats.clicks) }}</p>
                <p class="text-red-400 text-sm mt-2">-2.1%</p>
            </div>
            <div class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <p class="text-gray-400 text-sm">ผู้ติดตามใหม่</p>
                <p class="text-2xl font-bold text-white mt-1">{{ stats.newFollowers }}</p>
                <p class="text-green-400 text-sm mt-2">+15.7%</p>
            </div>
        </div>

        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-4">ประสิทธิภาพรายแพลตฟอร์ม</h2>
            <p class="text-gray-400 text-center py-8">กราฟจะแสดงที่นี่</p>
        </div>
    </div>
</template>

<script setup>
import { reactive } from 'vue'

const stats = reactive({
    reach: 125000,
    engagement: 8500,
    clicks: 2340,
    newFollowers: 156,
})

const formatNumber = (num) => {
    if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M'
    if (num >= 1000) return (num / 1000).toFixed(1) + 'K'
    return num.toString()
}
</script>
