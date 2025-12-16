<template>
    <div class="space-y-6">
        <div class="flex items-center justify-between">
            <h1 class="text-2xl font-bold text-white">จัดการแบรนด์</h1>
            <button class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg" @click="showModal = true">
                + เพิ่มแบรนด์
            </button>
        </div>

        <div v-if="brands.length === 0" class="bg-gray-800 rounded-xl p-12 text-center border border-gray-700">
            <BuildingStorefrontIcon class="w-12 h-12 text-gray-500 mx-auto mb-4" />
            <p class="text-gray-400">ยังไม่มีแบรนด์</p>
            <button class="mt-4 text-indigo-400 hover:text-indigo-300" @click="showModal = true">
                สร้างแบรนด์แรก
            </button>
        </div>

        <div v-else class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <div v-for="brand in brands" :key="brand.id" class="bg-gray-800 rounded-xl p-5 border border-gray-700">
                <div class="flex items-start justify-between">
                    <div class="flex items-center">
                        <div class="w-12 h-12 rounded-lg bg-indigo-600/20 flex items-center justify-center mr-3">
                            <span class="text-xl font-bold text-indigo-400">{{ brand.name[0] }}</span>
                        </div>
                        <div>
                            <h3 class="text-white font-medium">{{ brand.name }}</h3>
                            <p class="text-gray-400 text-sm">{{ brand.description }}</p>
                        </div>
                    </div>
                </div>
                <div class="mt-4 flex justify-end space-x-2">
                    <button class="px-3 py-1 text-gray-400 hover:text-white text-sm">แก้ไข</button>
                    <button class="px-3 py-1 text-red-400 hover:text-red-300 text-sm">ลบ</button>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { brandApi } from '../../services/api'
import { BuildingStorefrontIcon } from '@heroicons/vue/24/outline'

const brands = ref([])
const showModal = ref(false)

onMounted(async () => {
    try {
        const response = await brandApi.list()
        brands.value = response.data.data || []
    } catch (error) {
        console.error('Failed to load brands:', error)
    }
})
</script>
