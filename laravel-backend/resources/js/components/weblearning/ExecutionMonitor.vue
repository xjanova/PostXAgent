<template>
    <div class="space-y-6">
        <!-- Header -->
        <div class="flex items-center justify-between">
            <div>
                <h2 class="text-xl font-semibold text-white">Workflow Executions</h2>
                <p class="text-sm text-gray-400">ติดตามการทำงานของ workflow แบบ real-time</p>
            </div>
            <div class="flex items-center gap-3">
                <button
                    @click="refreshExecutions"
                    :disabled="loading"
                    class="flex items-center gap-2 px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors disabled:opacity-50"
                >
                    <ArrowPathIcon :class="['w-5 h-5', loading && 'animate-spin']" />
                    รีเฟรช
                </button>
            </div>
        </div>

        <!-- Filters -->
        <div class="flex items-center gap-4 p-4 bg-gray-800 rounded-lg border border-gray-700">
            <select
                v-model="filterStatus"
                class="px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
            >
                <option value="">ทุกสถานะ</option>
                <option value="pending">Pending</option>
                <option value="running">Running</option>
                <option value="completed">Completed</option>
                <option value="failed">Failed</option>
                <option value="cancelled">Cancelled</option>
            </select>
            <select
                v-model="filterWorkflow"
                class="px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
            >
                <option value="">ทุก Workflow</option>
                <option v-for="w in workflows" :key="w.id" :value="w.id">{{ w.name }}</option>
            </select>
            <div class="flex-1"></div>
            <div class="flex items-center gap-2 text-gray-400">
                <div class="w-2 h-2 rounded-full bg-green-500 animate-pulse"></div>
                <span class="text-sm">Live updates</span>
            </div>
        </div>

        <!-- Running Executions -->
        <div v-if="runningExecutions.length > 0" class="space-y-4">
            <h3 class="text-lg font-medium text-white flex items-center gap-2">
                <div class="w-2 h-2 rounded-full bg-blue-500 animate-pulse"></div>
                กำลังทำงาน ({{ runningExecutions.length }})
            </h3>
            <div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
                <div
                    v-for="execution in runningExecutions"
                    :key="execution.id"
                    class="bg-gray-800 rounded-lg border border-blue-500/50 overflow-hidden"
                >
                    <!-- Execution Header -->
                    <div class="p-4 border-b border-gray-700">
                        <div class="flex items-center justify-between">
                            <div class="flex items-center gap-3">
                                <div class="w-10 h-10 rounded-lg bg-blue-600/20 flex items-center justify-center">
                                    <PlayCircleIcon class="w-6 h-6 text-blue-400" />
                                </div>
                                <div>
                                    <h4 class="font-medium text-white">{{ execution.workflow?.name }}</h4>
                                    <p class="text-sm text-gray-400">Started {{ formatTime(execution.started_at) }}</p>
                                </div>
                            </div>
                            <button
                                @click="cancelExecution(execution.id)"
                                class="p-2 text-gray-400 hover:text-red-400 hover:bg-gray-700 rounded-lg transition-colors"
                            >
                                <StopIcon class="w-5 h-5" />
                            </button>
                        </div>
                    </div>

                    <!-- Progress -->
                    <div class="p-4">
                        <div class="flex items-center justify-between text-sm mb-2">
                            <span class="text-gray-400">Progress</span>
                            <span class="text-white">{{ execution.current_step || 0 }}/{{ execution.total_steps || 0 }}</span>
                        </div>
                        <div class="h-2 bg-gray-700 rounded-full overflow-hidden">
                            <div
                                class="h-full bg-blue-500 rounded-full transition-all"
                                :style="{ width: `${(execution.current_step / execution.total_steps) * 100}%` }"
                            ></div>
                        </div>

                        <!-- Current Step -->
                        <div v-if="execution.current_step_name" class="mt-3 p-3 bg-gray-900 rounded-lg">
                            <div class="flex items-center gap-2 text-sm">
                                <div class="w-2 h-2 rounded-full bg-blue-500 animate-pulse"></div>
                                <span class="text-gray-400">ขั้นตอนปัจจุบัน:</span>
                                <span class="text-white">{{ execution.current_step_name }}</span>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Recent Executions Table -->
        <div class="bg-gray-800 rounded-lg border border-gray-700 overflow-hidden">
            <div class="p-4 border-b border-gray-700">
                <h3 class="font-medium text-white">ประวัติการทำงาน</h3>
            </div>
            <div class="overflow-x-auto">
                <table class="w-full">
                    <thead class="bg-gray-900">
                        <tr>
                            <th class="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Workflow</th>
                            <th class="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Status</th>
                            <th class="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Started</th>
                            <th class="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Duration</th>
                            <th class="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Account</th>
                            <th class="px-4 py-3 text-left text-xs font-medium text-gray-400 uppercase">Actions</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-gray-700">
                        <tr
                            v-for="execution in filteredExecutions"
                            :key="execution.id"
                            class="hover:bg-gray-700/50 transition-colors"
                        >
                            <td class="px-4 py-3">
                                <div class="flex items-center gap-3">
                                    <div :class="['w-8 h-8 rounded flex items-center justify-center', platformColor(execution.workflow?.platform)]">
                                        <GlobeAltIcon class="w-4 h-4 text-white" />
                                    </div>
                                    <div>
                                        <div class="text-sm font-medium text-white">{{ execution.workflow?.name }}</div>
                                        <div class="text-xs text-gray-400">{{ execution.workflow?.workflow_type }}</div>
                                    </div>
                                </div>
                            </td>
                            <td class="px-4 py-3">
                                <span :class="['px-2 py-1 text-xs rounded-full', statusClass(execution.status)]">
                                    {{ statusLabel(execution.status) }}
                                </span>
                            </td>
                            <td class="px-4 py-3 text-sm text-gray-400">
                                {{ formatTime(execution.started_at) }}
                            </td>
                            <td class="px-4 py-3 text-sm text-gray-400">
                                {{ formatDuration(execution.duration_ms) }}
                            </td>
                            <td class="px-4 py-3 text-sm text-gray-400">
                                {{ execution.social_account?.username || '-' }}
                            </td>
                            <td class="px-4 py-3">
                                <div class="flex items-center gap-2">
                                    <button
                                        @click="viewDetails(execution)"
                                        class="p-1.5 text-gray-400 hover:text-white hover:bg-gray-600 rounded transition-colors"
                                        title="ดูรายละเอียด"
                                    >
                                        <EyeIcon class="w-4 h-4" />
                                    </button>
                                    <button
                                        v-if="execution.status === 'failed'"
                                        @click="retryExecution(execution)"
                                        class="p-1.5 text-gray-400 hover:text-yellow-400 hover:bg-gray-600 rounded transition-colors"
                                        title="ลองใหม่"
                                    >
                                        <ArrowPathIcon class="w-4 h-4" />
                                    </button>
                                </div>
                            </td>
                        </tr>
                    </tbody>
                </table>
            </div>

            <!-- Empty State -->
            <div v-if="filteredExecutions.length === 0" class="p-8 text-center">
                <ClockIcon class="w-12 h-12 mx-auto text-gray-600 mb-3" />
                <p class="text-gray-400">ยังไม่มีประวัติการทำงาน</p>
            </div>

            <!-- Pagination -->
            <div v-if="totalPages > 1" class="p-4 border-t border-gray-700 flex items-center justify-between">
                <p class="text-sm text-gray-400">
                    แสดง {{ (currentPage - 1) * perPage + 1 }}-{{ Math.min(currentPage * perPage, totalItems) }} จาก {{ totalItems }}
                </p>
                <div class="flex items-center gap-2">
                    <button
                        @click="currentPage--"
                        :disabled="currentPage === 1"
                        class="px-3 py-1 bg-gray-700 hover:bg-gray-600 text-white rounded disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        ก่อนหน้า
                    </button>
                    <button
                        @click="currentPage++"
                        :disabled="currentPage === totalPages"
                        class="px-3 py-1 bg-gray-700 hover:bg-gray-600 text-white rounded disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        ถัดไป
                    </button>
                </div>
            </div>
        </div>

        <!-- Execution Details Modal -->
        <Modal v-model="showDetails" title="รายละเอียดการทำงาน" size="full">
            <template v-if="selectedExecution">
                <div class="grid grid-cols-3 gap-6">
                    <!-- Left: Basic Info -->
                    <div class="space-y-4">
                        <div class="p-4 bg-gray-900 rounded-lg">
                            <h4 class="text-sm font-medium text-gray-400 mb-3">ข้อมูลทั่วไป</h4>
                            <dl class="space-y-2">
                                <div class="flex justify-between">
                                    <dt class="text-gray-400">Workflow</dt>
                                    <dd class="text-white">{{ selectedExecution.workflow?.name }}</dd>
                                </div>
                                <div class="flex justify-between">
                                    <dt class="text-gray-400">Status</dt>
                                    <dd>
                                        <span :class="['px-2 py-1 text-xs rounded', statusClass(selectedExecution.status)]">
                                            {{ statusLabel(selectedExecution.status) }}
                                        </span>
                                    </dd>
                                </div>
                                <div class="flex justify-between">
                                    <dt class="text-gray-400">Started</dt>
                                    <dd class="text-white">{{ formatTime(selectedExecution.started_at) }}</dd>
                                </div>
                                <div class="flex justify-between">
                                    <dt class="text-gray-400">Duration</dt>
                                    <dd class="text-white">{{ formatDuration(selectedExecution.duration_ms) }}</dd>
                                </div>
                                <div class="flex justify-between">
                                    <dt class="text-gray-400">Trigger</dt>
                                    <dd class="text-white">{{ selectedExecution.trigger_source || 'manual' }}</dd>
                                </div>
                            </dl>
                        </div>

                        <!-- Error Info -->
                        <div v-if="selectedExecution.error_message" class="p-4 bg-red-500/10 border border-red-500/30 rounded-lg">
                            <h4 class="text-sm font-medium text-red-400 mb-2">Error</h4>
                            <p class="text-sm text-red-300">{{ selectedExecution.error_message }}</p>
                            <p v-if="selectedExecution.error_code" class="text-xs text-red-400 mt-1">
                                Code: {{ selectedExecution.error_code }}
                            </p>
                        </div>
                    </div>

                    <!-- Middle: Step Results -->
                    <div class="space-y-4">
                        <h4 class="text-sm font-medium text-gray-400">ผลแต่ละขั้นตอน</h4>
                        <div class="space-y-2 max-h-96 overflow-y-auto">
                            <div
                                v-for="(step, index) in selectedExecution.step_results || []"
                                :key="index"
                                :class="[
                                    'p-3 rounded-lg border',
                                    step.success
                                        ? 'bg-green-500/10 border-green-500/30'
                                        : 'bg-red-500/10 border-red-500/30'
                                ]"
                            >
                                <div class="flex items-center gap-2 mb-1">
                                    <CheckCircleIcon v-if="step.success" class="w-4 h-4 text-green-400" />
                                    <XCircleIcon v-else class="w-4 h-4 text-red-400" />
                                    <span class="text-sm font-medium" :class="step.success ? 'text-green-400' : 'text-red-400'">
                                        {{ step.name || `Step ${index + 1}` }}
                                    </span>
                                    <span class="text-xs text-gray-500 ml-auto">{{ step.duration_ms }}ms</span>
                                </div>
                                <p v-if="step.error" class="text-xs text-red-300 mt-1">{{ step.error }}</p>
                            </div>
                        </div>
                    </div>

                    <!-- Right: Screenshots -->
                    <div class="space-y-4">
                        <h4 class="text-sm font-medium text-gray-400">Screenshots</h4>
                        <div v-if="selectedExecution.screenshots?.length" class="space-y-2">
                            <div
                                v-for="(screenshot, index) in selectedExecution.screenshots"
                                :key="index"
                                class="relative group"
                            >
                                <img
                                    :src="screenshot"
                                    :alt="`Screenshot ${index + 1}`"
                                    class="w-full rounded-lg border border-gray-700"
                                />
                                <div class="absolute inset-0 bg-black/50 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity rounded-lg">
                                    <button class="p-2 bg-white/20 rounded-lg">
                                        <MagnifyingGlassPlusIcon class="w-6 h-6 text-white" />
                                    </button>
                                </div>
                            </div>
                        </div>
                        <div v-else class="p-8 bg-gray-900 rounded-lg text-center">
                            <CameraIcon class="w-8 h-8 mx-auto text-gray-600 mb-2" />
                            <p class="text-sm text-gray-400">ไม่มี screenshot</p>
                        </div>
                    </div>
                </div>
            </template>
        </Modal>
    </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import Modal from '../ui/Modal.vue'
import {
    ArrowPathIcon,
    PlayCircleIcon,
    StopIcon,
    EyeIcon,
    ClockIcon,
    GlobeAltIcon,
    CheckCircleIcon,
    XCircleIcon,
    CameraIcon,
    MagnifyingGlassPlusIcon
} from '@heroicons/vue/24/outline'

const props = defineProps({
    executions: {
        type: Array,
        default: () => []
    },
    workflows: {
        type: Array,
        default: () => []
    }
})

const emit = defineEmits(['refresh', 'cancel', 'retry', 'view'])

const loading = ref(false)
const filterStatus = ref('')
const filterWorkflow = ref('')
const currentPage = ref(1)
const perPage = 20
const totalItems = ref(0)
const showDetails = ref(false)
const selectedExecution = ref(null)

let refreshInterval = null

const runningExecutions = computed(() => {
    return props.executions.filter(e => e.status === 'running')
})

const filteredExecutions = computed(() => {
    return props.executions.filter(e => {
        if (filterStatus.value && e.status !== filterStatus.value) return false
        if (filterWorkflow.value && e.workflow_id !== filterWorkflow.value) return false
        return true
    })
})

const totalPages = computed(() => Math.ceil(totalItems.value / perPage))

const refreshExecutions = () => {
    loading.value = true
    emit('refresh')
    setTimeout(() => loading.value = false, 500)
}

const cancelExecution = (id) => {
    emit('cancel', id)
}

const retryExecution = (execution) => {
    emit('retry', execution)
}

const viewDetails = (execution) => {
    selectedExecution.value = execution
    showDetails.value = true
}

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

const statusClass = (status) => {
    const classes = {
        pending: 'bg-gray-500/20 text-gray-400',
        running: 'bg-blue-500/20 text-blue-400',
        completed: 'bg-green-500/20 text-green-400',
        failed: 'bg-red-500/20 text-red-400',
        cancelled: 'bg-yellow-500/20 text-yellow-400'
    }
    return classes[status] || classes.pending
}

const statusLabel = (status) => {
    const labels = {
        pending: 'Pending',
        running: 'Running',
        completed: 'Completed',
        failed: 'Failed',
        cancelled: 'Cancelled'
    }
    return labels[status] || status
}

const formatTime = (timestamp) => {
    if (!timestamp) return '-'
    const date = new Date(timestamp)
    return date.toLocaleString('th-TH', {
        day: '2-digit',
        month: '2-digit',
        hour: '2-digit',
        minute: '2-digit'
    })
}

const formatDuration = (ms) => {
    if (!ms) return '-'
    if (ms < 1000) return `${ms}ms`
    if (ms < 60000) return `${(ms / 1000).toFixed(1)}s`
    return `${(ms / 60000).toFixed(1)}m`
}

onMounted(() => {
    // Auto-refresh every 5 seconds
    refreshInterval = setInterval(() => {
        if (runningExecutions.value.length > 0) {
            emit('refresh')
        }
    }, 5000)
})

onUnmounted(() => {
    if (refreshInterval) {
        clearInterval(refreshInterval)
    }
})
</script>
