<template>
    <div class="run-progress">
        <div v-if="loading" class="loading-state">
            <div class="spinner"></div>
            <p>กำลังโหลด...</p>
        </div>

        <div v-else-if="runs.length === 0" class="empty-state">
            <PlayCircleIcon class="w-12 h-12 text-gray-500" />
            <p class="text-gray-400 mt-2">ไม่มีรันที่กำลังทำงาน</p>
        </div>

        <div v-else class="runs-list">
            <div v-for="run in runs" :key="run.id" class="run-card">
                <div class="run-header">
                    <div class="run-info">
                        <h3 class="run-title">{{ run.pipeline?.name || 'Pipeline #' + run.content_pipeline_id }}</h3>
                        <span :class="['status-badge', `status-${run.status}`]">
                            {{ getStatusLabel(run.status) }}
                        </span>
                    </div>
                    <div class="run-actions">
                        <button
                            v-if="run.status === 'failed'"
                            class="btn btn-sm btn-primary"
                            @click="$emit('retry', run)"
                        >
                            <ArrowPathIcon class="w-3.5 h-3.5" />
                            ลองใหม่
                        </button>
                        <button
                            v-if="!['completed', 'failed', 'cancelled'].includes(run.status)"
                            class="btn btn-sm btn-danger"
                            @click="$emit('cancel', run)"
                        >
                            <XMarkIcon class="w-3.5 h-3.5" />
                            ยกเลิก
                        </button>
                    </div>
                </div>

                <!-- Step Progress -->
                <div class="steps-track">
                    <div
                        v-for="(step, index) in steps"
                        :key="step.key"
                        :class="['step-item', getStepState(run, step.key, index)]"
                    >
                        <div class="step-dot">
                            <CheckIcon v-if="getStepState(run, step.key, index) === 'completed'" class="w-3 h-3" />
                            <XMarkIcon v-else-if="getStepState(run, step.key, index) === 'failed'" class="w-3 h-3" />
                            <div v-else-if="getStepState(run, step.key, index) === 'active'" class="step-pulse"></div>
                            <span v-else class="step-number">{{ index + 1 }}</span>
                        </div>
                        <span class="step-label">{{ step.label }}</span>
                        <div v-if="index < steps.length - 1" class="step-connector" :class="getStepState(run, step.key, index)"></div>
                    </div>
                </div>

                <!-- Progress Bar -->
                <div class="progress-bar-container">
                    <div class="progress-bar">
                        <div
                            class="progress-fill"
                            :class="{ 'progress-error': run.status === 'failed' }"
                            :style="{ width: (run.progress_percentage || 0) + '%' }"
                        ></div>
                    </div>
                    <span class="progress-text">{{ run.progress_percentage || 0 }}%</span>
                </div>

                <!-- Step Message -->
                <p v-if="run.step_message" class="step-message">{{ run.step_message }}</p>

                <!-- Error Message -->
                <div v-if="run.status === 'failed' && run.error_message" class="error-box">
                    <ExclamationTriangleIcon class="w-4 h-4 text-red-400 flex-shrink-0" />
                    <div>
                        <p class="error-step">ขั้นตอน: {{ getStepLabel(run.error_step) }}</p>
                        <p class="error-msg">{{ run.error_message }}</p>
                    </div>
                </div>

                <!-- Run Meta -->
                <div class="run-meta">
                    <span v-if="run.started_at">เริ่ม: {{ formatTime(run.started_at) }}</span>
                    <span v-if="run.completed_at">เสร็จ: {{ formatTime(run.completed_at) }}</span>
                    <span v-if="run.retry_count > 0">ลองใหม่: {{ run.retry_count }} ครั้ง</span>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import {
    PlayCircleIcon,
    ArrowPathIcon,
    XMarkIcon,
    CheckIcon,
    ExclamationTriangleIcon,
} from '@heroicons/vue/24/outline'

defineProps({
    runs: { type: Array, default: () => [] },
    loading: { type: Boolean, default: false },
})

defineEmits(['cancel', 'retry', 'refresh'])

const steps = [
    { key: 'story', label: 'เรื่อง' },
    { key: 'images', label: 'ภาพ' },
    { key: 'video', label: 'วิดีโอ' },
    { key: 'voiceover', label: 'เสียง' },
    { key: 'lipsync', label: 'LipSync' },
    { key: 'assembly', label: 'ประกอบ' },
    { key: 'publishing', label: 'โพสต์' },
]

const stepLabels = {
    pending: 'รอดำเนินการ',
    story: 'สร้างเรื่อง',
    images: 'สร้างภาพ',
    video: 'สร้างวิดีโอ',
    voiceover: 'สร้างเสียง',
    lipsync: 'Lip Sync',
    assembly: 'ประกอบวิดีโอ',
    publishing: 'เผยแพร่',
    completed: 'เสร็จสมบูรณ์',
    failed: 'ล้มเหลว',
    cancelled: 'ยกเลิกแล้ว',
}

const getStatusLabel = (status) => stepLabels[status] || status
const getStepLabel = (step) => stepLabels[step] || step

const getStepState = (run, stepKey, stepIndex) => {
    const currentStepIndex = steps.findIndex(s => s.key === run.status)

    if (run.status === 'completed') return 'completed'
    if (run.status === 'failed' && run.error_step === stepKey) return 'failed'
    if (run.status === 'cancelled') {
        return stepIndex <= currentStepIndex ? 'completed' : 'pending'
    }

    if (stepIndex < currentStepIndex) return 'completed'
    if (stepIndex === currentStepIndex) return 'active'
    return 'pending'
}

const formatTime = (dateStr) => {
    if (!dateStr) return '-'
    return new Date(dateStr).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' })
}
</script>

<style scoped>
.runs-list {
    display: grid;
    gap: 16px;
}

.run-card {
    background: #111827;
    border-radius: 10px;
    padding: 16px;
    border: 1px solid #374151;
}

.run-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 16px;
}

.run-info {
    display: flex;
    align-items: center;
    gap: 10px;
}

.run-title {
    font-size: 0.95rem;
    font-weight: 600;
    color: #f3f4f6;
}

.run-actions {
    display: flex;
    gap: 6px;
}

/* Status Badges */
.status-badge {
    padding: 2px 8px;
    font-size: 0.7rem;
    font-weight: 600;
    border-radius: 4px;
    text-transform: uppercase;
}

.status-pending { background: #374151; color: #9ca3af; }
.status-story, .status-images, .status-video,
.status-voiceover, .status-lipsync, .status-assembly,
.status-publishing { background: #312e81; color: #a5b4fc; }
.status-completed { background: #14532d; color: #86efac; }
.status-failed { background: #7f1d1d; color: #fca5a5; }
.status-cancelled { background: #451a03; color: #fbbf24; }

/* Steps Track */
.steps-track {
    display: flex;
    align-items: flex-start;
    margin-bottom: 16px;
    overflow-x: auto;
    padding: 4px 0;
}

.step-item {
    display: flex;
    flex-direction: column;
    align-items: center;
    position: relative;
    min-width: 60px;
    flex: 1;
}

.step-dot {
    width: 24px;
    height: 24px;
    border-radius: 50%;
    background: #374151;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.65rem;
    color: #9ca3af;
    position: relative;
    z-index: 1;
}

.step-item.completed .step-dot {
    background: #22c55e;
    color: white;
}

.step-item.active .step-dot {
    background: #4f46e5;
    color: white;
    box-shadow: 0 0 0 4px rgba(79, 70, 229, 0.3);
}

.step-item.failed .step-dot {
    background: #dc2626;
    color: white;
}

.step-pulse {
    width: 8px;
    height: 8px;
    border-radius: 50%;
    background: white;
    animation: pulse 1.5s ease-in-out infinite;
}

@keyframes pulse {
    0%, 100% { opacity: 1; }
    50% { opacity: 0.4; }
}

.step-number {
    font-size: 0.65rem;
    font-weight: 600;
}

.step-label {
    font-size: 0.65rem;
    color: #6b7280;
    margin-top: 4px;
    text-align: center;
    white-space: nowrap;
}

.step-item.active .step-label,
.step-item.completed .step-label {
    color: #d1d5db;
}

.step-connector {
    position: absolute;
    top: 12px;
    left: 50%;
    width: 100%;
    height: 2px;
    background: #374151;
    z-index: 0;
}

.step-connector.completed {
    background: #22c55e;
}

.step-connector.active {
    background: linear-gradient(to right, #22c55e, #4f46e5);
}

/* Progress Bar */
.progress-bar-container {
    display: flex;
    align-items: center;
    gap: 8px;
    margin-bottom: 8px;
}

.progress-bar {
    flex: 1;
    height: 6px;
    background: #374151;
    border-radius: 3px;
    overflow: hidden;
}

.progress-fill {
    height: 100%;
    background: linear-gradient(90deg, #4f46e5, #818cf8);
    border-radius: 3px;
    transition: width 0.5s ease;
}

.progress-fill.progress-error {
    background: #dc2626;
}

.progress-text {
    font-size: 0.75rem;
    color: #9ca3af;
    min-width: 36px;
    text-align: right;
}

.step-message {
    font-size: 0.8rem;
    color: #9ca3af;
    margin-bottom: 8px;
}

/* Error Box */
.error-box {
    display: flex;
    gap: 8px;
    padding: 10px;
    background: #450a0a;
    border: 1px solid #7f1d1d;
    border-radius: 6px;
    margin-bottom: 8px;
}

.error-step {
    font-size: 0.75rem;
    color: #fca5a5;
    font-weight: 600;
}

.error-msg {
    font-size: 0.75rem;
    color: #f87171;
    margin-top: 2px;
}

/* Run Meta */
.run-meta {
    display: flex;
    gap: 16px;
    font-size: 0.7rem;
    color: #6b7280;
}

/* Shared */
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

.btn {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 6px 10px;
    font-size: 0.75rem;
    font-weight: 500;
    border-radius: 6px;
    border: none;
    cursor: pointer;
    transition: all 0.2s;
}

.btn-sm { padding: 4px 8px; }
.btn-primary { background: #4f46e5; color: white; }
.btn-primary:hover { background: #4338ca; }
.btn-danger { background: #991b1b; color: #fca5a5; }
.btn-danger:hover { background: #7f1d1d; }
</style>
