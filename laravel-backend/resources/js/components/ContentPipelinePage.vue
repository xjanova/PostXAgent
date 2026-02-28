<template>
    <div class="content-pipeline-page">
        <div class="page-header">
            <div>
                <h1 class="page-title">Content Pipeline</h1>
                <p class="page-subtitle">ระบบสร้างเนื้อหาวิดีโออัตโนมัติ</p>
            </div>
            <div class="header-actions">
                <button class="btn btn-secondary" @click="refreshAll">
                    <ArrowPathIcon class="btn-icon" :class="{ 'animate-spin': refreshing }" />
                    รีเฟรช
                </button>
                <button class="btn btn-primary" @click="showCreateModal = true">
                    <PlusIcon class="btn-icon" />
                    สร้าง Pipeline ใหม่
                </button>
            </div>
        </div>

        <!-- Stats Cards -->
        <div class="stats-grid">
            <div class="stat-card">
                <div class="stat-icon bg-indigo-500/20 text-indigo-400">
                    <RectangleStackIcon class="w-6 h-6" />
                </div>
                <div class="stat-content">
                    <p class="stat-value">{{ stats.total_pipelines }}</p>
                    <p class="stat-label">Pipelines ทั้งหมด</p>
                </div>
            </div>
            <div class="stat-card">
                <div class="stat-icon bg-green-500/20 text-green-400">
                    <PlayCircleIcon class="w-6 h-6" />
                </div>
                <div class="stat-content">
                    <p class="stat-value">{{ stats.active_runs }}</p>
                    <p class="stat-label">กำลังทำงาน</p>
                </div>
            </div>
            <div class="stat-card">
                <div class="stat-icon bg-blue-500/20 text-blue-400">
                    <CheckCircleIcon class="w-6 h-6" />
                </div>
                <div class="stat-content">
                    <p class="stat-value">{{ stats.completed_today }}</p>
                    <p class="stat-label">เสร็จวันนี้</p>
                </div>
            </div>
            <div class="stat-card">
                <div class="stat-icon bg-yellow-500/20 text-yellow-400">
                    <KeyIcon class="w-6 h-6" />
                </div>
                <div class="stat-content">
                    <p class="stat-value">{{ stats.total_api_keys }}</p>
                    <p class="stat-label">API Keys</p>
                </div>
            </div>
            <div class="stat-card">
                <div class="stat-icon" :class="stats.success_rate >= 80 ? 'bg-green-500/20 text-green-400' : 'bg-orange-500/20 text-orange-400'">
                    <ChartBarIcon class="w-6 h-6" />
                </div>
                <div class="stat-content">
                    <p class="stat-value">{{ stats.success_rate }}%</p>
                    <p class="stat-label">อัตราสำเร็จ</p>
                </div>
            </div>
        </div>

        <!-- Tabs -->
        <div class="tabs-container">
            <div class="tabs-header">
                <button
                    v-for="tab in tabs"
                    :key="tab.key"
                    :class="['tab-btn', { active: activeTab === tab.key }]"
                    @click="activeTab = tab.key"
                >
                    <component :is="tab.icon" class="w-4 h-4 mr-2" />
                    {{ tab.label }}
                    <span v-if="tab.count !== undefined" class="tab-badge">{{ tab.count }}</span>
                </button>
            </div>

            <!-- Tab: Pipelines -->
            <div v-if="activeTab === 'pipelines'" class="tab-content">
                <div v-if="loading" class="loading-state">
                    <div class="spinner"></div>
                    <p>กำลังโหลด...</p>
                </div>
                <div v-else-if="pipelines.length === 0" class="empty-state">
                    <RectangleStackIcon class="w-12 h-12 text-gray-500" />
                    <p class="text-gray-400 mt-2">ยังไม่มี Pipeline</p>
                    <button class="btn btn-primary mt-4" @click="showCreateModal = true">สร้าง Pipeline แรก</button>
                </div>
                <div v-else class="pipeline-list">
                    <div v-for="pipeline in pipelines" :key="pipeline.id" class="pipeline-card">
                        <div class="pipeline-card-header">
                            <div class="pipeline-info">
                                <h3 class="pipeline-name">{{ pipeline.name }}</h3>
                                <p class="pipeline-desc">{{ pipeline.description || 'ไม่มีรายละเอียด' }}</p>
                            </div>
                            <div class="pipeline-toggle">
                                <button
                                    :class="['toggle-switch', { active: pipeline.is_active }]"
                                    @click="togglePipeline(pipeline)"
                                    :title="pipeline.is_active ? 'ปิดการทำงาน' : 'เปิดการทำงาน'"
                                >
                                    <span class="toggle-knob"></span>
                                </button>
                            </div>
                        </div>

                        <div class="pipeline-meta">
                            <span class="meta-tag">
                                <FilmIcon class="w-3.5 h-3.5" />
                                {{ pipeline.content_type }}
                            </span>
                            <span class="meta-tag">
                                <ClockIcon class="w-3.5 h-3.5" />
                                {{ getScheduleLabel(pipeline) }}
                            </span>
                            <span class="meta-tag">
                                <VideoCameraIcon class="w-3.5 h-3.5" />
                                {{ pipeline.video_provider }}
                            </span>
                            <span class="meta-tag">
                                <SpeakerWaveIcon class="w-3.5 h-3.5" />
                                {{ pipeline.tts_provider }}
                            </span>
                            <span v-if="pipeline.enable_lip_sync" class="meta-tag meta-tag-accent">
                                LipSync
                            </span>
                        </div>

                        <div class="pipeline-stats-row">
                            <span class="pipeline-stat">
                                รันทั้งหมด: <strong>{{ pipeline.runs_count || 0 }}</strong>
                            </span>
                            <span v-if="pipeline.next_run_at" class="pipeline-stat">
                                รันถัดไป: <strong>{{ formatDate(pipeline.next_run_at) }}</strong>
                            </span>
                            <span v-if="pipeline.last_run_at" class="pipeline-stat">
                                รันล่าสุด: <strong>{{ formatDate(pipeline.last_run_at) }}</strong>
                            </span>
                        </div>

                        <div class="pipeline-actions">
                            <button class="btn btn-sm btn-primary" @click="startPipeline(pipeline)" :disabled="pipeline.has_active_run">
                                <PlayIcon class="btn-icon" />
                                เริ่มรัน
                            </button>
                            <button class="btn btn-sm btn-secondary" @click="editPipeline(pipeline)">
                                <PencilSquareIcon class="btn-icon" />
                                แก้ไข
                            </button>
                            <button class="btn btn-sm btn-danger" @click="deletePipeline(pipeline)">
                                <TrashIcon class="btn-icon" />
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Tab: Active Runs -->
            <div v-if="activeTab === 'runs'" class="tab-content">
                <PipelineRunProgress
                    :runs="runs"
                    :loading="runsLoading"
                    @cancel="cancelRun"
                    @retry="retryRun"
                    @refresh="fetchRuns"
                />
            </div>

            <!-- Tab: API Keys -->
            <div v-if="activeTab === 'apikeys'" class="tab-content">
                <ApiKeyPoolManager
                    :pools="apiKeyPools"
                    :loading="apiKeysLoading"
                    @refresh="fetchApiKeyPools"
                />
            </div>
        </div>

        <!-- Pipeline Config Modal -->
        <PipelineConfigModal
            v-if="showCreateModal || editingPipeline"
            :pipeline="editingPipeline"
            :api-key-pools="apiKeyPools"
            @close="showCreateModal = false; editingPipeline = null"
            @saved="onPipelineSaved"
        />
    </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue'
import { useToast } from '@/composables/useToast'
import { pipelineApi } from '@/services/api'
import {
    ArrowPathIcon,
    PlusIcon,
    RectangleStackIcon,
    PlayCircleIcon,
    CheckCircleIcon,
    KeyIcon,
    ChartBarIcon,
    PlayIcon,
    PencilSquareIcon,
    TrashIcon,
    FilmIcon,
    ClockIcon,
    VideoCameraIcon,
    SpeakerWaveIcon,
} from '@heroicons/vue/24/outline'

import PipelineConfigModal from './pipeline/PipelineConfigModal.vue'
import PipelineRunProgress from './pipeline/PipelineRunProgress.vue'
import ApiKeyPoolManager from './pipeline/ApiKeyPoolManager.vue'

const toast = useToast()

// State
const loading = ref(true)
const refreshing = ref(false)
const runsLoading = ref(true)
const apiKeysLoading = ref(true)
const activeTab = ref('pipelines')
const showCreateModal = ref(false)
const editingPipeline = ref(null)

const pipelines = ref([])
const runs = ref([])
const apiKeyPools = ref([])
const stats = reactive({
    total_pipelines: 0,
    active_pipelines: 0,
    active_runs: 0,
    completed_today: 0,
    total_api_keys: 0,
    success_rate: 0,
})

// Tabs
const tabs = computed(() => [
    { key: 'pipelines', label: 'Pipelines', icon: RectangleStackIcon, count: pipelines.value.length },
    { key: 'runs', label: 'กำลังทำงาน', icon: PlayCircleIcon, count: stats.active_runs },
    { key: 'apikeys', label: 'API Keys', icon: KeyIcon, count: stats.total_api_keys },
])

// Fetch data
const fetchPipelines = async () => {
    try {
        const response = await pipelineApi.list()
        pipelines.value = response.data.data || response.data
    } catch (error) {
        toast.error('ไม่สามารถโหลด Pipelines ได้')
    }
}

const fetchRuns = async () => {
    runsLoading.value = true
    try {
        const response = await pipelineApi.listRuns({ status: 'active' })
        runs.value = response.data.data || response.data
    } catch (error) {
        toast.error('ไม่สามารถโหลดรายการรันได้')
    } finally {
        runsLoading.value = false
    }
}

const fetchApiKeyPools = async () => {
    apiKeysLoading.value = true
    try {
        const response = await pipelineApi.listApiKeyPools()
        apiKeyPools.value = response.data.data || response.data
    } catch (error) {
        toast.error('ไม่สามารถโหลด API Key Pools ได้')
    } finally {
        apiKeysLoading.value = false
    }
}

const fetchStats = async () => {
    try {
        const response = await pipelineApi.stats()
        Object.assign(stats, response.data.data || response.data)
    } catch {
        // Stats are non-critical
    }
}

const refreshAll = async () => {
    refreshing.value = true
    await Promise.all([fetchPipelines(), fetchRuns(), fetchApiKeyPools(), fetchStats()])
    refreshing.value = false
}

// Pipeline actions
const startPipeline = async (pipeline) => {
    try {
        await pipelineApi.start(pipeline.id)
        toast.success(`เริ่มรัน Pipeline "${pipeline.name}" แล้ว`)
        await Promise.all([fetchRuns(), fetchStats()])
    } catch (error) {
        toast.error(error.response?.data?.message || 'ไม่สามารถเริ่ม Pipeline ได้')
    }
}

const togglePipeline = async (pipeline) => {
    try {
        await pipelineApi.toggle(pipeline.id)
        pipeline.is_active = !pipeline.is_active
        toast.success(pipeline.is_active ? 'เปิดใช้งาน Pipeline แล้ว' : 'ปิดใช้งาน Pipeline แล้ว')
    } catch (error) {
        toast.error('ไม่สามารถเปลี่ยนสถานะได้')
    }
}

const editPipeline = (pipeline) => {
    editingPipeline.value = { ...pipeline }
}

const deletePipeline = async (pipeline) => {
    if (!confirm(`ต้องการลบ Pipeline "${pipeline.name}" หรือไม่?`)) return
    try {
        await pipelineApi.delete(pipeline.id)
        toast.success('ลบ Pipeline แล้ว')
        await fetchPipelines()
    } catch (error) {
        toast.error('ไม่สามารถลบ Pipeline ได้')
    }
}

const onPipelineSaved = async () => {
    showCreateModal.value = false
    editingPipeline.value = null
    await Promise.all([fetchPipelines(), fetchStats()])
    toast.success('บันทึก Pipeline สำเร็จ')
}

// Run actions
const cancelRun = async (run) => {
    try {
        await pipelineApi.cancelRun(run.id)
        toast.success('ยกเลิกรันแล้ว')
        await fetchRuns()
    } catch (error) {
        toast.error('ไม่สามารถยกเลิกรันได้')
    }
}

const retryRun = async (run) => {
    try {
        await pipelineApi.retryRun(run.id)
        toast.success('เริ่มรันใหม่แล้ว')
        await fetchRuns()
    } catch (error) {
        toast.error('ไม่สามารถรันใหม่ได้')
    }
}

// Helpers
const getScheduleLabel = (pipeline) => {
    if (pipeline.schedule_type === 'manual') return 'ด้วยตนเอง'
    if (pipeline.schedule_type === 'interval') return `ทุก ${pipeline.interval_hours} ชม.`
    if (pipeline.schedule_type === 'cron') return 'Cron'
    return pipeline.schedule_type
}

const formatDate = (dateStr) => {
    if (!dateStr) return '-'
    const d = new Date(dateStr)
    return d.toLocaleDateString('th-TH', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
}

// Auto-refresh runs every 10 seconds
let refreshInterval = null

onMounted(async () => {
    await Promise.all([fetchPipelines(), fetchRuns(), fetchApiKeyPools(), fetchStats()])
    loading.value = false

    refreshInterval = setInterval(async () => {
        if (activeTab.value === 'runs' && stats.active_runs > 0) {
            await fetchRuns()
        }
    }, 10000)
})

onUnmounted(() => {
    if (refreshInterval) clearInterval(refreshInterval)
})
</script>

<style scoped>
.content-pipeline-page {
    max-width: 1400px;
    margin: 0 auto;
}

.page-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    margin-bottom: 24px;
}

.page-title {
    font-size: 1.5rem;
    font-weight: 700;
    color: #f3f4f6;
}

.page-subtitle {
    color: #9ca3af;
    font-size: 0.875rem;
    margin-top: 4px;
}

.header-actions {
    display: flex;
    gap: 8px;
}

/* Stats */
.stats-grid {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
    gap: 16px;
    margin-bottom: 24px;
}

.stat-card {
    background: #1f2937;
    border-radius: 12px;
    padding: 16px;
    display: flex;
    align-items: center;
    gap: 12px;
    border: 1px solid #374151;
}

.stat-icon {
    width: 48px;
    height: 48px;
    border-radius: 10px;
    display: flex;
    align-items: center;
    justify-content: center;
    flex-shrink: 0;
}

.stat-value {
    font-size: 1.25rem;
    font-weight: 700;
    color: #f3f4f6;
}

.stat-label {
    font-size: 0.75rem;
    color: #9ca3af;
}

/* Tabs */
.tabs-container {
    background: #1f2937;
    border-radius: 12px;
    border: 1px solid #374151;
    overflow: hidden;
}

.tabs-header {
    display: flex;
    border-bottom: 1px solid #374151;
    padding: 0 16px;
}

.tab-btn {
    display: flex;
    align-items: center;
    padding: 12px 16px;
    font-size: 0.875rem;
    color: #9ca3af;
    border-bottom: 2px solid transparent;
    cursor: pointer;
    background: none;
    border-top: none;
    border-left: none;
    border-right: none;
    transition: all 0.2s;
}

.tab-btn:hover {
    color: #e5e7eb;
}

.tab-btn.active {
    color: #818cf8;
    border-bottom-color: #818cf8;
}

.tab-badge {
    margin-left: 6px;
    padding: 1px 6px;
    font-size: 0.7rem;
    background: #374151;
    border-radius: 9999px;
    color: #d1d5db;
}

.tab-content {
    padding: 20px;
    min-height: 300px;
}

/* Pipeline Cards */
.pipeline-list {
    display: grid;
    gap: 16px;
}

.pipeline-card {
    background: #111827;
    border-radius: 10px;
    padding: 16px;
    border: 1px solid #374151;
    transition: border-color 0.2s;
}

.pipeline-card:hover {
    border-color: #4f46e5;
}

.pipeline-card-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    margin-bottom: 12px;
}

.pipeline-name {
    font-size: 1rem;
    font-weight: 600;
    color: #f3f4f6;
}

.pipeline-desc {
    font-size: 0.8rem;
    color: #6b7280;
    margin-top: 2px;
}

.pipeline-meta {
    display: flex;
    flex-wrap: wrap;
    gap: 6px;
    margin-bottom: 12px;
}

.meta-tag {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 2px 8px;
    font-size: 0.7rem;
    background: #374151;
    color: #d1d5db;
    border-radius: 4px;
}

.meta-tag-accent {
    background: #4f46e5;
    color: white;
}

.pipeline-stats-row {
    display: flex;
    gap: 16px;
    margin-bottom: 12px;
    font-size: 0.8rem;
    color: #9ca3af;
}

.pipeline-stat strong {
    color: #e5e7eb;
}

.pipeline-actions {
    display: flex;
    gap: 8px;
}

/* Toggle Switch */
.toggle-switch {
    width: 44px;
    height: 24px;
    border-radius: 12px;
    background: #374151;
    border: none;
    cursor: pointer;
    position: relative;
    transition: background 0.2s;
}

.toggle-switch.active {
    background: #4f46e5;
}

.toggle-knob {
    position: absolute;
    top: 2px;
    left: 2px;
    width: 20px;
    height: 20px;
    border-radius: 10px;
    background: white;
    transition: transform 0.2s;
}

.toggle-switch.active .toggle-knob {
    transform: translateX(20px);
}

/* Buttons */
.btn {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 8px 16px;
    font-size: 0.875rem;
    font-weight: 500;
    border-radius: 8px;
    border: none;
    cursor: pointer;
    transition: all 0.2s;
}

.btn-sm {
    padding: 6px 12px;
    font-size: 0.8rem;
}

.btn-primary {
    background: #4f46e5;
    color: white;
}

.btn-primary:hover {
    background: #4338ca;
}

.btn-primary:disabled {
    opacity: 0.5;
    cursor: not-allowed;
}

.btn-secondary {
    background: #374151;
    color: #d1d5db;
}

.btn-secondary:hover {
    background: #4b5563;
}

.btn-danger {
    background: #991b1b;
    color: #fca5a5;
}

.btn-danger:hover {
    background: #7f1d1d;
}

.btn-icon {
    width: 16px;
    height: 16px;
}

/* Loading / Empty */
.loading-state,
.empty-state {
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 48px;
    color: #9ca3af;
}

.spinner {
    width: 32px;
    height: 32px;
    border: 3px solid #374151;
    border-top-color: #818cf8;
    border-radius: 50%;
    animation: spin 0.8s linear infinite;
}

@keyframes spin {
    to { transform: rotate(360deg); }
}

.animate-spin {
    animation: spin 1s linear infinite;
}
</style>
