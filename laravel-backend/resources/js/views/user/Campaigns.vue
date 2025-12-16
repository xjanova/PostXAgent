<template>
    <div class="space-y-6">
        <div class="flex items-center justify-between">
            <h1 class="text-2xl font-bold text-white">แคมเปญ</h1>
            <button class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg">
                + สร้างแคมเปญ
            </button>
        </div>

        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <p class="text-gray-400 text-center py-8">ยังไม่มีแคมเปญ</p>
        </div>
    </div>
</template>

<script setup>
import { ref } from 'vue'
const campaigns = ref([])
</script>
