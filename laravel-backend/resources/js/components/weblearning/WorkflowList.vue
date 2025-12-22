<template>
    <div class="space-y-4">
        <!-- Header -->
        <div class="flex items-center justify-between">
            <div>
                <h2 class="text-xl font-semibold text-white">Learned Workflows</h2>
                <p class="text-sm text-gray-400">จัดการ workflow ที่ระบบเรียนรู้ไว้</p>
            </div>
            <div class="flex items-center gap-3">
                <button
                    @click="$emit('teach-new')"
                    class="flex items-center gap-2 px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg transition-colors"
                >
                    <AcademicCapIcon class="w-5 h-5" />
                    สอน Workflow ใหม่
                </button>
                <button
                    @click="$emit('ai-generate')"
                    class="flex items-center gap-2 px-4 py-2 bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white rounded-lg transition-colors"
                >
                    <SparklesIcon class="w-5 h-5" />
                    สร้างด้วย AI
                </button>
            </div>
        </div>

        <!-- Filters -->
        <div class="flex items-center gap-4 p-4 bg-gray-800 rounded-lg border border-gray-700">
            <div class="flex-1">
                <div class="relative">
                    <MagnifyingGlassIcon class="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-gray-400" />
                    <input
                        v-model="searchQuery"
                        type="text"
                        placeholder="ค้นหา workflow..."
                        class="w-full pl-10 pr-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white placeholder-gray-500 focus:ring-2 focus:ring-purple-500 focus:border-transparent"
                    />
                </div>
            </div>
            <select
                v-model="filterPlatform"
                class="px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
            >
                <option value="">ทุก Platform</option>
                <option v-for="platform in platforms" :key="platform.value" :value="platform.value">
                    {{ platform.label }}
                </option>
            </select>
            <select
                v-model="filterStatus"
                class="px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
            >
                <option value="">ทุกสถานะ</option>
                <option value="active">Active</option>
                <option value="learning">Learning</option>
                <option value="disabled">Disabled</option>
            </select>
        </div>

        <!-- Loading -->
        <div v-if="loading" class="flex items-center justify-center py-12">
            <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-purple-500"></div>
        </div>

        <!-- Workflow Grid -->
        <div v-else-if="filteredWorkflows.length > 0" class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            <div
                v-for="workflow in filteredWorkflows"
                :key="workflow.id"
                class="bg-gray-800 rounded-lg border border-gray-700 hover:border-purple-500/50 transition-colors overflow-hidden"
            >
                <!-- Workflow Header -->
                <div class="p-4 border-b border-gray-700">
                    <div class="flex items-start justify-between">
                        <div class="flex items-center gap-3">
                            <div :class="['w-10 h-10 rounded-lg flex items-center justify-center', platformColor(workflow.platform)]">
                                <component :is="platformIcon(workflow.platform)" class="w-6 h-6 text-white" />
                            </div>
                            <div>
                                <h3 class="font-medium text-white">{{ workflow.name }}</h3>
                                <p class="text-sm text-gray-400">{{ workflow.workflow_type }}</p>
                            </div>
                        </div>
                        <span :class="['px-2 py-1 text-xs rounded-full', statusClass(workflow.status)]">
                            {{ statusLabel(workflow.status) }}
                        </span>
                    </div>
                </div>

                <!-- Workflow Stats -->
                <div class="p-4 space-y-3">
                    <!-- Success Rate -->
                    <div>
                        <div class="flex items-center justify-between text-sm mb-1">
                            <span class="text-gray-400">Success Rate</span>
                            <span :class="successRateColor(workflow.success_rate)">
                                {{ workflow.success_rate.toFixed(1) }}%
                            </span>
                        </div>
                        <div class="h-2 bg-gray-700 rounded-full overflow-hidden">
                            <div
                                :class="['h-full rounded-full transition-all', successRateBg(workflow.success_rate)]"
                                :style="{ width: `${workflow.success_rate}%` }"
                            ></div>
                        </div>
                    </div>

                    <!-- Stats Grid -->
                    <div class="grid grid-cols-3 gap-2 text-center">
                        <div class="p-2 bg-gray-900 rounded">
                            <div class="text-lg font-semibold text-white">{{ workflow.success_count }}</div>
                            <div class="text-xs text-gray-400">สำเร็จ</div>
                        </div>
                        <div class="p-2 bg-gray-900 rounded">
                            <div class="text-lg font-semibold text-white">{{ workflow.failure_count }}</div>
                            <div class="text-xs text-gray-400">ล้มเหลว</div>
                        </div>
                        <div class="p-2 bg-gray-900 rounded">
                            <div class="text-lg font-semibold text-white">{{ workflow.steps_count || 0 }}</div>
                            <div class="text-xs text-gray-400">ขั้นตอน</div>
                        </div>
                    </div>

                    <!-- Source Badge -->
                    <div class="flex items-center gap-2">
                        <span :class="['px-2 py-1 text-xs rounded', sourceClass(workflow.source)]">
                            {{ sourceLabel(workflow.source) }}
                        </span>
                        <span class="text-xs text-gray-500">v{{ workflow.version }}</span>
                        <span v-if="workflow.avg_execution_time" class="text-xs text-gray-500">
                            ~{{ workflow.avg_execution_time.toFixed(1) }}s
                        </span>
                    </div>
                </div>

                <!-- Workflow Actions -->
                <div class="px-4 py-3 bg-gray-900/50 border-t border-gray-700 flex items-center justify-between">
                    <div class="flex items-center gap-2">
                        <button
                            @click="$emit('test', workflow)"
                            class="p-2 text-gray-400 hover:text-white hover:bg-gray-700 rounded-lg transition-colors"
                            title="ทดสอบ"
                        >
                            <PlayIcon class="w-5 h-5" />
                        </button>
                        <button
                            @click="$emit('edit', workflow)"
                            class="p-2 text-gray-400 hover:text-white hover:bg-gray-700 rounded-lg transition-colors"
                            title="แก้ไข"
                        >
                            <PencilIcon class="w-5 h-5" />
                        </button>
                        <button
                            @click="$emit('optimize', workflow)"
                            class="p-2 text-gray-400 hover:text-purple-400 hover:bg-gray-700 rounded-lg transition-colors"
                            title="Optimize ด้วย AI"
                        >
                            <SparklesIcon class="w-5 h-5" />
                        </button>
                    </div>
                    <div class="flex items-center gap-2">
                        <button
                            @click="$emit('clone', workflow)"
                            class="p-2 text-gray-400 hover:text-blue-400 hover:bg-gray-700 rounded-lg transition-colors"
                            title="คัดลอก"
                        >
                            <DocumentDuplicateIcon class="w-5 h-5" />
                        </button>
                        <button
                            @click="$emit('delete', workflow)"
                            class="p-2 text-gray-400 hover:text-red-400 hover:bg-gray-700 rounded-lg transition-colors"
                            title="ลบ"
                        >
                            <TrashIcon class="w-5 h-5" />
                        </button>
                    </div>
                </div>
            </div>
        </div>

        <!-- Empty State -->
        <div v-else class="text-center py-12 bg-gray-800 rounded-lg border border-gray-700">
            <CpuChipIcon class="w-16 h-16 mx-auto text-gray-600 mb-4" />
            <h3 class="text-lg font-medium text-white mb-2">ยังไม่มี Workflow</h3>
            <p class="text-gray-400 mb-6">เริ่มสอนระบบด้วยการสาธิต หรือให้ AI สร้างให้อัตโนมัติ</p>
            <div class="flex items-center justify-center gap-4">
                <button
                    @click="$emit('teach-new')"
                    class="flex items-center gap-2 px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg transition-colors"
                >
                    <AcademicCapIcon class="w-5 h-5" />
                    สอน Workflow ใหม่
                </button>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import {
    MagnifyingGlassIcon,
    AcademicCapIcon,
    SparklesIcon,
    PlayIcon,
    PencilIcon,
    DocumentDuplicateIcon,
    TrashIcon,
    CpuChipIcon,
    GlobeAltIcon
} from '@heroicons/vue/24/outline'

const props = defineProps({
    workflows: {
        type: Array,
        default: () => []
    },
    loading: {
        type: Boolean,
        default: false
    }
})

defineEmits(['teach-new', 'ai-generate', 'test', 'edit', 'optimize', 'clone', 'delete'])

const searchQuery = ref('')
const filterPlatform = ref('')
const filterStatus = ref('')

const platforms = [
    { value: 'facebook', label: 'Facebook' },
    { value: 'instagram', label: 'Instagram' },
    { value: 'tiktok', label: 'TikTok' },
    { value: 'twitter', label: 'Twitter/X' },
    { value: 'line', label: 'LINE' },
    { value: 'youtube', label: 'YouTube' },
    { value: 'threads', label: 'Threads' },
    { value: 'linkedin', label: 'LinkedIn' },
    { value: 'pinterest', label: 'Pinterest' }
]

const filteredWorkflows = computed(() => {
    return props.workflows.filter(w => {
        if (searchQuery.value && !w.name.toLowerCase().includes(searchQuery.value.toLowerCase())) {
            return false
        }
        if (filterPlatform.value && w.platform !== filterPlatform.value) {
            return false
        }
        if (filterStatus.value && w.status !== filterStatus.value) {
            return false
        }
        return true
    })
})

const platformColor = (platform) => {
    const colors = {
        facebook: 'bg-blue-600',
        instagram: 'bg-gradient-to-br from-purple-600 to-pink-500',
        tiktok: 'bg-black',
        twitter: 'bg-sky-500',
        line: 'bg-green-500',
        youtube: 'bg-red-600',
        threads: 'bg-gray-900',
        linkedin: 'bg-blue-700',
        pinterest: 'bg-red-500'
    }
    return colors[platform] || 'bg-gray-600'
}

const platformIcon = () => GlobeAltIcon

const statusClass = (status) => {
    const classes = {
        active: 'bg-green-500/20 text-green-400',
        learning: 'bg-yellow-500/20 text-yellow-400',
        disabled: 'bg-gray-500/20 text-gray-400',
        deprecated: 'bg-red-500/20 text-red-400'
    }
    return classes[status] || classes.disabled
}

const statusLabel = (status) => {
    const labels = {
        active: 'Active',
        learning: 'Learning',
        disabled: 'Disabled',
        deprecated: 'Deprecated'
    }
    return labels[status] || status
}

const sourceClass = (source) => {
    const classes = {
        manual: 'bg-blue-500/20 text-blue-400',
        ai_observed: 'bg-purple-500/20 text-purple-400',
        ai_generated: 'bg-gradient-to-r from-blue-500/20 to-purple-500/20 text-purple-400',
        imported: 'bg-gray-500/20 text-gray-400'
    }
    return classes[source] || classes.manual
}

const sourceLabel = (source) => {
    const labels = {
        manual: 'Manual',
        ai_observed: 'AI Observed',
        ai_generated: 'AI Generated',
        imported: 'Imported'
    }
    return labels[source] || source
}

const successRateColor = (rate) => {
    if (rate >= 90) return 'text-green-400'
    if (rate >= 70) return 'text-yellow-400'
    return 'text-red-400'
}

const successRateBg = (rate) => {
    if (rate >= 90) return 'bg-green-500'
    if (rate >= 70) return 'bg-yellow-500'
    return 'bg-red-500'
}
</script>
