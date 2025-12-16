<template>
    <div class="space-y-6">
        <div class="flex items-center justify-between">
            <h1 class="text-2xl font-bold text-white">โพสต์</h1>
            <button class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg flex items-center">
                <PlusIcon class="w-5 h-5 mr-2" />
                สร้างโพสต์
            </button>
        </div>

        <div class="bg-gray-800 rounded-xl border border-gray-700 overflow-hidden">
            <div class="p-4 border-b border-gray-700">
                <div class="flex flex-wrap gap-2">
                    <button v-for="filter in filters" :key="filter.value"
                        :class="['px-3 py-1 rounded-full text-sm', activeFilter === filter.value ? 'bg-indigo-600 text-white' : 'bg-gray-700 text-gray-300']"
                        @click="activeFilter = filter.value">
                        {{ filter.label }}
                    </button>
                </div>
            </div>

            <div v-if="posts.length === 0" class="p-12 text-center">
                <DocumentTextIcon class="w-12 h-12 text-gray-500 mx-auto mb-4" />
                <p class="text-gray-400">ยังไม่มีโพสต์</p>
            </div>

            <div v-else class="divide-y divide-gray-700">
                <div v-for="post in posts" :key="post.id" class="p-4 hover:bg-gray-700/50">
                    <div class="flex items-start">
                        <div class="flex-1">
                            <p class="text-white">{{ post.content?.substring(0, 100) }}...</p>
                            <div class="flex items-center mt-2 space-x-4 text-sm text-gray-400">
                                <span>{{ post.platform }}</span>
                                <span>{{ post.created_at }}</span>
                            </div>
                        </div>
                        <span :class="['px-2 py-1 rounded text-xs', getStatusClass(post.status)]">
                            {{ getStatusLabel(post.status) }}
                        </span>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { postApi } from '../../services/api'
import { PlusIcon, DocumentTextIcon } from '@heroicons/vue/24/outline'

const posts = ref([])
const activeFilter = ref('all')
const filters = [
    { value: 'all', label: 'ทั้งหมด' },
    { value: 'published', label: 'เผยแพร่แล้ว' },
    { value: 'scheduled', label: 'ตั้งเวลา' },
    { value: 'draft', label: 'แบบร่าง' },
]

const getStatusClass = (status) => {
    const classes = {
        published: 'bg-green-500/20 text-green-400',
        scheduled: 'bg-blue-500/20 text-blue-400',
        draft: 'bg-gray-500/20 text-gray-400',
        failed: 'bg-red-500/20 text-red-400',
    }
    return classes[status] || classes.draft
}

const getStatusLabel = (status) => {
    const labels = { published: 'เผยแพร่แล้ว', scheduled: 'ตั้งเวลา', draft: 'แบบร่าง', failed: 'ล้มเหลว' }
    return labels[status] || status
}

onMounted(async () => {
    try {
        const response = await postApi.list()
        posts.value = response.data.data || []
    } catch (error) {
        console.error('Failed to load posts:', error)
    }
})
</script>
