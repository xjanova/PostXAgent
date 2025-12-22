<template>
    <div class="min-h-screen bg-gray-900">
        <!-- Teaching Mode (Full Screen) -->
        <TeachingMode
            v-if="showTeachingMode"
            @cancel="showTeachingMode = false"
            @save="handleTeachingSave"
        />

        <!-- Workflow Builder (Full Screen) -->
        <WorkflowBuilder
            v-else-if="showBuilder && selectedWorkflow"
            :workflow="selectedWorkflow"
            @back="closeBuilder"
            @save="handleWorkflowSave"
            @test="handleWorkflowTest"
        />

        <!-- Main Content -->
        <div v-else class="container mx-auto px-6 py-8">
            <!-- Page Header -->
            <div class="flex items-center justify-between mb-8">
                <div>
                    <h1 class="text-2xl font-bold text-white">Web Learning</h1>
                    <p class="text-gray-400 mt-1">ระบบเรียนรู้และจดจำ workflow อัตโนมัติด้วย AI</p>
                </div>
                <div class="flex items-center gap-4">
                    <!-- Stats -->
                    <div class="flex items-center gap-6 px-6 py-3 bg-gray-800 rounded-lg border border-gray-700">
                        <div class="text-center">
                            <div class="text-2xl font-bold text-white">{{ stats.total_workflows }}</div>
                            <div class="text-xs text-gray-400">Workflows</div>
                        </div>
                        <div class="w-px h-8 bg-gray-700"></div>
                        <div class="text-center">
                            <div class="text-2xl font-bold text-green-400">{{ stats.success_rate }}%</div>
                            <div class="text-xs text-gray-400">Success Rate</div>
                        </div>
                        <div class="w-px h-8 bg-gray-700"></div>
                        <div class="text-center">
                            <div class="text-2xl font-bold text-blue-400">{{ stats.executions_today }}</div>
                            <div class="text-xs text-gray-400">Today</div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Tabs -->
            <div class="flex items-center gap-1 p-1 bg-gray-800 rounded-lg mb-6 w-fit">
                <button
                    v-for="tab in tabs"
                    :key="tab.id"
                    @click="activeTab = tab.id"
                    :class="[
                        'flex items-center gap-2 px-4 py-2 rounded-lg transition-colors',
                        activeTab === tab.id
                            ? 'bg-purple-600 text-white'
                            : 'text-gray-400 hover:text-white hover:bg-gray-700'
                    ]"
                >
                    <component :is="tab.icon" class="w-5 h-5" />
                    {{ tab.label }}
                </button>
            </div>

            <!-- Tab Content -->
            <div class="space-y-6">
                <!-- Workflows Tab -->
                <WorkflowList
                    v-if="activeTab === 'workflows'"
                    :workflows="workflows"
                    :loading="loading"
                    @teach-new="startTeaching"
                    @ai-generate="activeTab = 'ai-analyzer'"
                    @test="testWorkflow"
                    @edit="editWorkflow"
                    @optimize="optimizeWorkflow"
                    @clone="cloneWorkflow"
                    @delete="deleteWorkflow"
                />

                <!-- Executions Tab -->
                <ExecutionMonitor
                    v-else-if="activeTab === 'executions'"
                    :executions="executions"
                    :workflows="workflows"
                    @refresh="loadExecutions"
                    @cancel="cancelExecution"
                    @retry="retryExecution"
                />

                <!-- AI Analyzer Tab -->
                <AIAnalyzer
                    v-else-if="activeTab === 'ai-analyzer'"
                    @create-workflow="handleAIWorkflow"
                />

                <!-- Statistics Tab -->
                <div v-else-if="activeTab === 'statistics'" class="space-y-6">
                    <!-- Overview Cards -->
                    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                        <div class="p-6 bg-gray-800 rounded-lg border border-gray-700">
                            <div class="flex items-center justify-between">
                                <div>
                                    <p class="text-sm text-gray-400">Total Workflows</p>
                                    <p class="text-3xl font-bold text-white mt-1">{{ stats.total_workflows }}</p>
                                </div>
                                <div class="w-12 h-12 rounded-lg bg-purple-600/20 flex items-center justify-center">
                                    <CpuChipIcon class="w-6 h-6 text-purple-400" />
                                </div>
                            </div>
                            <p class="text-sm text-green-400 mt-2">+{{ stats.new_this_week }} this week</p>
                        </div>

                        <div class="p-6 bg-gray-800 rounded-lg border border-gray-700">
                            <div class="flex items-center justify-between">
                                <div>
                                    <p class="text-sm text-gray-400">Total Executions</p>
                                    <p class="text-3xl font-bold text-white mt-1">{{ stats.total_executions }}</p>
                                </div>
                                <div class="w-12 h-12 rounded-lg bg-blue-600/20 flex items-center justify-center">
                                    <PlayIcon class="w-6 h-6 text-blue-400" />
                                </div>
                            </div>
                            <p class="text-sm text-gray-400 mt-2">{{ stats.executions_today }} today</p>
                        </div>

                        <div class="p-6 bg-gray-800 rounded-lg border border-gray-700">
                            <div class="flex items-center justify-between">
                                <div>
                                    <p class="text-sm text-gray-400">Success Rate</p>
                                    <p class="text-3xl font-bold text-green-400 mt-1">{{ stats.success_rate }}%</p>
                                </div>
                                <div class="w-12 h-12 rounded-lg bg-green-600/20 flex items-center justify-center">
                                    <CheckCircleIcon class="w-6 h-6 text-green-400" />
                                </div>
                            </div>
                            <p class="text-sm text-gray-400 mt-2">{{ stats.failed_today }} failed today</p>
                        </div>

                        <div class="p-6 bg-gray-800 rounded-lg border border-gray-700">
                            <div class="flex items-center justify-between">
                                <div>
                                    <p class="text-sm text-gray-400">Avg Execution Time</p>
                                    <p class="text-3xl font-bold text-white mt-1">{{ stats.avg_execution_time }}s</p>
                                </div>
                                <div class="w-12 h-12 rounded-lg bg-yellow-600/20 flex items-center justify-center">
                                    <ClockIcon class="w-6 h-6 text-yellow-400" />
                                </div>
                            </div>
                            <p class="text-sm text-gray-400 mt-2">-{{ stats.time_improvement }}% from last week</p>
                        </div>
                    </div>

                    <!-- Platform Stats -->
                    <div class="p-6 bg-gray-800 rounded-lg border border-gray-700">
                        <h3 class="text-lg font-medium text-white mb-6">สถิติแยกตาม Platform</h3>
                        <div class="space-y-4">
                            <div
                                v-for="platform in platformStats"
                                :key="platform.name"
                                class="flex items-center gap-4"
                            >
                                <div :class="['w-10 h-10 rounded-lg flex items-center justify-center', platform.color]">
                                    <GlobeAltIcon class="w-5 h-5 text-white" />
                                </div>
                                <div class="flex-1">
                                    <div class="flex items-center justify-between mb-1">
                                        <span class="text-white">{{ platform.name }}</span>
                                        <span class="text-gray-400">{{ platform.workflows }} workflows</span>
                                    </div>
                                    <div class="h-2 bg-gray-700 rounded-full overflow-hidden">
                                        <div
                                            :class="['h-full rounded-full', platform.barColor]"
                                            :style="{ width: `${platform.success_rate}%` }"
                                        ></div>
                                    </div>
                                </div>
                                <span :class="['text-sm font-medium', platform.success_rate >= 80 ? 'text-green-400' : 'text-yellow-400']">
                                    {{ platform.success_rate }}%
                                </span>
                            </div>
                        </div>
                    </div>

                    <!-- Recent Activity -->
                    <div class="p-6 bg-gray-800 rounded-lg border border-gray-700">
                        <h3 class="text-lg font-medium text-white mb-6">กิจกรรมล่าสุด</h3>
                        <div class="space-y-4">
                            <div
                                v-for="activity in recentActivity"
                                :key="activity.id"
                                class="flex items-center gap-4 p-3 bg-gray-900 rounded-lg"
                            >
                                <div :class="['w-8 h-8 rounded-full flex items-center justify-center', activityColor(activity.type)]">
                                    <component :is="activityIcon(activity.type)" class="w-4 h-4 text-white" />
                                </div>
                                <div class="flex-1">
                                    <p class="text-white text-sm">{{ activity.message }}</p>
                                    <p class="text-gray-500 text-xs">{{ activity.time }}</p>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <!-- Confirm Dialog -->
        <ConfirmDialog
            v-model="showConfirm"
            :title="confirmTitle"
            :message="confirmMessage"
            :confirm-text="confirmButtonText"
            :type="confirmType"
            @confirm="handleConfirm"
        />

        <!-- Toast Container -->
        <ToastContainer />
    </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import WorkflowList from './WorkflowList.vue'
import WorkflowBuilder from './WorkflowBuilder.vue'
import TeachingMode from './TeachingMode.vue'
import ExecutionMonitor from './ExecutionMonitor.vue'
import AIAnalyzer from './AIAnalyzer.vue'
import ConfirmDialog from '../ui/ConfirmDialog.vue'
import ToastContainer from '../ui/ToastContainer.vue'
import {
    CpuChipIcon,
    PlayIcon,
    SparklesIcon,
    ChartBarIcon,
    CheckCircleIcon,
    ClockIcon,
    GlobeAltIcon,
    AcademicCapIcon,
    ArrowPathIcon,
    XCircleIcon,
    PlusIcon
} from '@heroicons/vue/24/outline'

const loading = ref(false)
const activeTab = ref('workflows')
const showTeachingMode = ref(false)
const showBuilder = ref(false)
const selectedWorkflow = ref(null)

// Data
const workflows = ref([])
const executions = ref([])

// Confirm dialog
const showConfirm = ref(false)
const confirmTitle = ref('')
const confirmMessage = ref('')
const confirmButtonText = ref('Confirm')
const confirmType = ref('danger')
const confirmAction = ref(null)

// Stats
const stats = ref({
    total_workflows: 12,
    success_rate: 94.5,
    executions_today: 156,
    total_executions: 3842,
    new_this_week: 3,
    failed_today: 8,
    avg_execution_time: 4.2,
    time_improvement: 12
})

const platformStats = ref([
    { name: 'Facebook', workflows: 5, success_rate: 96, color: 'bg-blue-600', barColor: 'bg-blue-500' },
    { name: 'Instagram', workflows: 3, success_rate: 92, color: 'bg-gradient-to-br from-purple-600 to-pink-500', barColor: 'bg-purple-500' },
    { name: 'TikTok', workflows: 2, success_rate: 88, color: 'bg-black', barColor: 'bg-gray-500' },
    { name: 'Twitter', workflows: 2, success_rate: 94, color: 'bg-sky-500', barColor: 'bg-sky-500' }
])

const recentActivity = ref([
    { id: 1, type: 'success', message: 'Facebook Login workflow completed successfully', time: '2 minutes ago' },
    { id: 2, type: 'learning', message: 'New workflow "Instagram Post" learned from teaching session', time: '15 minutes ago' },
    { id: 3, type: 'failed', message: 'TikTok Upload workflow failed - element not found', time: '32 minutes ago' },
    { id: 4, type: 'optimized', message: 'AI optimized Facebook Post workflow - 15% faster', time: '1 hour ago' }
])

const tabs = [
    { id: 'workflows', label: 'Workflows', icon: CpuChipIcon },
    { id: 'executions', label: 'Executions', icon: PlayIcon },
    { id: 'ai-analyzer', label: 'AI Analyzer', icon: SparklesIcon },
    { id: 'statistics', label: 'Statistics', icon: ChartBarIcon }
]

// Methods
const loadWorkflows = async () => {
    loading.value = true
    try {
        // Simulate API call
        await new Promise(resolve => setTimeout(resolve, 500))
        workflows.value = [
            {
                id: 1,
                name: 'Facebook Login',
                platform: 'facebook',
                workflow_type: 'login',
                status: 'active',
                source: 'manual',
                version: 3,
                success_count: 245,
                failure_count: 12,
                success_rate: 95.3,
                avg_execution_time: 3.2,
                steps_count: 4
            },
            {
                id: 2,
                name: 'Instagram Post Image',
                platform: 'instagram',
                workflow_type: 'post_image',
                status: 'active',
                source: 'ai_generated',
                version: 2,
                success_count: 128,
                failure_count: 8,
                success_rate: 94.1,
                avg_execution_time: 8.5,
                steps_count: 7
            },
            {
                id: 3,
                name: 'TikTok Upload Video',
                platform: 'tiktok',
                workflow_type: 'post_video',
                status: 'learning',
                source: 'manual',
                version: 1,
                success_count: 15,
                failure_count: 5,
                success_rate: 75.0,
                avg_execution_time: 12.3,
                steps_count: 9
            }
        ]
    } finally {
        loading.value = false
    }
}

const loadExecutions = async () => {
    // Simulate API call
    await new Promise(resolve => setTimeout(resolve, 300))
    executions.value = [
        {
            id: 1,
            workflow_id: 1,
            workflow: { name: 'Facebook Login', platform: 'facebook', workflow_type: 'login' },
            status: 'completed',
            success: true,
            started_at: new Date().toISOString(),
            duration_ms: 3200,
            social_account: { username: '@testuser' }
        },
        {
            id: 2,
            workflow_id: 2,
            workflow: { name: 'Instagram Post Image', platform: 'instagram', workflow_type: 'post_image' },
            status: 'running',
            current_step: 4,
            total_steps: 7,
            current_step_name: 'Uploading image...',
            started_at: new Date().toISOString()
        }
    ]
}

const startTeaching = () => {
    showTeachingMode.value = true
}

const handleTeachingSave = async (workflow) => {
    // Save the taught workflow
    console.log('Saving taught workflow:', workflow)
    showTeachingMode.value = false
    await loadWorkflows()
}

const editWorkflow = (workflow) => {
    selectedWorkflow.value = {
        ...workflow,
        steps: workflow.steps || []
    }
    showBuilder.value = true
}

const closeBuilder = () => {
    showBuilder.value = false
    selectedWorkflow.value = null
}

const handleWorkflowSave = async (workflow) => {
    console.log('Saving workflow:', workflow)
    closeBuilder()
    await loadWorkflows()
}

const handleWorkflowTest = async (workflow) => {
    console.log('Testing workflow:', workflow)
}

const testWorkflow = (workflow) => {
    editWorkflow(workflow)
}

const optimizeWorkflow = async (workflow) => {
    console.log('Optimizing workflow:', workflow)
}

const cloneWorkflow = async (workflow) => {
    const cloned = {
        ...workflow,
        id: Date.now(),
        name: `${workflow.name} (Copy)`,
        version: 1,
        success_count: 0,
        failure_count: 0
    }
    workflows.value.push(cloned)
}

const deleteWorkflow = (workflow) => {
    confirmTitle.value = 'ลบ Workflow'
    confirmMessage.value = `คุณต้องการลบ "${workflow.name}" หรือไม่? การกระทำนี้ไม่สามารถย้อนกลับได้`
    confirmButtonText.value = 'ลบ'
    confirmType.value = 'danger'
    confirmAction.value = async () => {
        workflows.value = workflows.value.filter(w => w.id !== workflow.id)
    }
    showConfirm.value = true
}

const handleConfirm = async () => {
    if (confirmAction.value) {
        await confirmAction.value()
    }
    showConfirm.value = false
}

const handleAIWorkflow = (workflow) => {
    selectedWorkflow.value = workflow
    showBuilder.value = true
}

const cancelExecution = async (id) => {
    console.log('Cancelling execution:', id)
}

const retryExecution = async (execution) => {
    console.log('Retrying execution:', execution)
}

const activityColor = (type) => {
    const colors = {
        success: 'bg-green-600',
        failed: 'bg-red-600',
        learning: 'bg-purple-600',
        optimized: 'bg-blue-600'
    }
    return colors[type] || 'bg-gray-600'
}

const activityIcon = (type) => {
    const icons = {
        success: CheckCircleIcon,
        failed: XCircleIcon,
        learning: AcademicCapIcon,
        optimized: ArrowPathIcon
    }
    return icons[type] || PlayIcon
}

onMounted(() => {
    loadWorkflows()
    loadExecutions()
})
</script>
