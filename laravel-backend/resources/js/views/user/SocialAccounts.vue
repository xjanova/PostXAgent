<template>
    <div class="space-y-6">
        <h1 class="text-2xl font-bold text-white">บัญชีโซเชียลมีเดีย</h1>

        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <div v-for="platform in platforms" :key="platform.id"
                class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <div class="flex items-center justify-between">
                    <div class="flex items-center">
                        <div class="w-10 h-10 rounded-lg flex items-center justify-center" :style="{ backgroundColor: platform.color + '20' }">
                            <span class="text-lg" :style="{ color: platform.color }">{{ platform.icon }}</span>
                        </div>
                        <div class="ml-3">
                            <h3 class="text-white font-medium">{{ platform.name }}</h3>
                            <p v-if="platform.connected" class="text-green-400 text-sm">เชื่อมต่อแล้ว</p>
                            <p v-else class="text-gray-400 text-sm">ยังไม่เชื่อมต่อ</p>
                        </div>
                    </div>
                    <button v-if="platform.connected" class="px-3 py-1 text-red-400 text-sm hover:text-red-300">ยกเลิก</button>
                    <button v-else class="px-3 py-1 bg-indigo-600 hover:bg-indigo-700 text-white text-sm rounded-lg">เชื่อมต่อ</button>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref } from 'vue'

const platforms = ref([
    { id: 'facebook', name: 'Facebook', icon: 'f', color: '#1877f2', connected: false },
    { id: 'instagram', name: 'Instagram', icon: 'ig', color: '#e4405f', connected: false },
    { id: 'tiktok', name: 'TikTok', icon: 'tk', color: '#000', connected: false },
    { id: 'twitter', name: 'Twitter/X', icon: 'X', color: '#1da1f2', connected: false },
    { id: 'youtube', name: 'YouTube', icon: 'yt', color: '#ff0000', connected: false },
    { id: 'line', name: 'LINE', icon: 'L', color: '#00b900', connected: false },
    { id: 'linkedin', name: 'LinkedIn', icon: 'in', color: '#0077b5', connected: false },
    { id: 'threads', name: 'Threads', icon: '@', color: '#000', connected: false },
    { id: 'pinterest', name: 'Pinterest', icon: 'P', color: '#e60023', connected: false },
])
</script>
