<template>
    <div class="fixed inset-0 z-50 bg-gray-900">
        <!-- Top Bar -->
        <div class="absolute top-0 left-0 right-0 h-16 bg-gray-800 border-b border-gray-700 flex items-center justify-between px-6 z-10">
            <div class="flex items-center gap-4">
                <div class="flex items-center gap-2">
                    <div class="w-3 h-3 rounded-full animate-pulse" :class="isRecording ? 'bg-red-500' : 'bg-gray-500'"></div>
                    <span class="text-white font-medium">
                        {{ isRecording ? 'กำลังบันทึก...' : 'พร้อมบันทึก' }}
                    </span>
                </div>
                <div class="h-6 w-px bg-gray-600"></div>
                <div class="text-gray-400">
                    <span class="text-white">{{ recordedSteps.length }}</span> ขั้นตอน
                </div>
            </div>

            <div class="flex items-center gap-4">
                <div class="flex items-center gap-2 text-gray-400">
                    <ClockIcon class="w-5 h-5" />
                    <span>{{ formatDuration(elapsedTime) }}</span>
                </div>
                <div class="h-6 w-px bg-gray-600"></div>
                <button
                    v-if="!isRecording"
                    @click="startRecording"
                    class="flex items-center gap-2 px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors"
                >
                    <span class="w-3 h-3 rounded-full bg-white"></span>
                    เริ่มบันทึก
                </button>
                <button
                    v-else
                    @click="stopRecording"
                    class="flex items-center gap-2 px-4 py-2 bg-gray-600 hover:bg-gray-700 text-white rounded-lg transition-colors"
                >
                    <StopIcon class="w-5 h-5" />
                    หยุดบันทึก
                </button>
                <button
                    @click="cancelTeaching"
                    class="p-2 text-gray-400 hover:text-white hover:bg-gray-700 rounded-lg transition-colors"
                >
                    <XMarkIcon class="w-5 h-5" />
                </button>
            </div>
        </div>

        <!-- Main Content -->
        <div class="absolute top-16 bottom-0 left-0 right-0 flex">
            <!-- Left Panel - Browser Frame -->
            <div class="flex-1 p-4">
                <div class="h-full bg-gray-800 rounded-lg border border-gray-700 overflow-hidden flex flex-col">
                    <!-- Browser Header -->
                    <div class="flex items-center gap-3 px-4 py-3 bg-gray-900 border-b border-gray-700">
                        <div class="flex items-center gap-1.5">
                            <div class="w-3 h-3 rounded-full bg-red-500"></div>
                            <div class="w-3 h-3 rounded-full bg-yellow-500"></div>
                            <div class="w-3 h-3 rounded-full bg-green-500"></div>
                        </div>
                        <div class="flex-1 flex items-center gap-2">
                            <input
                                v-model="currentUrl"
                                type="text"
                                placeholder="Enter URL..."
                                class="flex-1 px-4 py-1.5 bg-gray-800 border border-gray-600 rounded text-white text-sm focus:ring-2 focus:ring-purple-500"
                                @keydown.enter="navigateToUrl"
                            />
                            <button
                                @click="navigateToUrl"
                                class="p-2 text-gray-400 hover:text-white hover:bg-gray-700 rounded transition-colors"
                            >
                                <ArrowPathIcon class="w-4 h-4" />
                            </button>
                        </div>
                    </div>

                    <!-- Browser Content -->
                    <div class="flex-1 relative bg-white">
                        <iframe
                            ref="browserFrame"
                            :src="frameUrl"
                            class="w-full h-full border-0"
                            @load="onFrameLoad"
                        ></iframe>

                        <!-- Overlay for capturing clicks -->
                        <div
                            v-if="isRecording && captureMode === 'click'"
                            class="absolute inset-0 cursor-crosshair"
                            @click="captureClick"
                            @mousemove="highlightElement"
                        >
                            <!-- Highlight box -->
                            <div
                                v-if="highlightBox"
                                class="absolute border-2 border-purple-500 bg-purple-500/20 pointer-events-none transition-all"
                                :style="{
                                    left: highlightBox.left + 'px',
                                    top: highlightBox.top + 'px',
                                    width: highlightBox.width + 'px',
                                    height: highlightBox.height + 'px'
                                }"
                            ></div>
                        </div>

                        <!-- Loading overlay -->
                        <div v-if="frameLoading" class="absolute inset-0 bg-gray-900/80 flex items-center justify-center">
                            <div class="animate-spin rounded-full h-12 w-12 border-b-2 border-purple-500"></div>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Right Panel - Steps & Controls -->
            <div class="w-96 border-l border-gray-700 bg-gray-800 flex flex-col">
                <!-- Workflow Info -->
                <div class="p-4 border-b border-gray-700">
                    <label class="block text-sm font-medium text-gray-400 mb-2">ชื่อ Workflow</label>
                    <input
                        v-model="workflowName"
                        type="text"
                        placeholder="เช่น: Login Facebook"
                        class="w-full px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
                    />
                    <div class="flex items-center gap-3 mt-3">
                        <select
                            v-model="workflowPlatform"
                            class="flex-1 px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
                        >
                            <option value="">เลือก Platform</option>
                            <option v-for="p in platforms" :key="p.value" :value="p.value">{{ p.label }}</option>
                        </select>
                        <select
                            v-model="workflowType"
                            class="flex-1 px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
                        >
                            <option value="">ประเภท</option>
                            <option v-for="t in workflowTypes" :key="t.value" :value="t.value">{{ t.label }}</option>
                        </select>
                    </div>
                </div>

                <!-- Action Palette -->
                <div class="p-4 border-b border-gray-700">
                    <label class="block text-sm font-medium text-gray-400 mb-2">เพิ่มขั้นตอน</label>
                    <div class="grid grid-cols-4 gap-2">
                        <button
                            v-for="action in actions"
                            :key="action.type"
                            @click="addManualStep(action.type)"
                            :class="[
                                'p-3 rounded-lg flex flex-col items-center gap-1 transition-colors',
                                captureMode === action.type
                                    ? 'bg-purple-600 text-white'
                                    : 'bg-gray-900 text-gray-400 hover:bg-gray-700 hover:text-white'
                            ]"
                            :title="action.label"
                        >
                            <component :is="action.icon" class="w-5 h-5" />
                            <span class="text-xs">{{ action.label }}</span>
                        </button>
                    </div>
                </div>

                <!-- Recorded Steps -->
                <div class="flex-1 overflow-y-auto p-4">
                    <div class="space-y-2">
                        <div
                            v-for="(step, index) in recordedSteps"
                            :key="index"
                            class="bg-gray-900 rounded-lg p-3 border border-gray-700"
                        >
                            <div class="flex items-start justify-between">
                                <div class="flex items-center gap-3">
                                    <div class="w-8 h-8 rounded-lg bg-purple-600/20 flex items-center justify-center text-purple-400 text-sm font-medium">
                                        {{ index + 1 }}
                                    </div>
                                    <div>
                                        <div class="text-white text-sm font-medium">{{ step.name || getActionLabel(step.action_type) }}</div>
                                        <div class="text-xs text-gray-400 truncate max-w-48">
                                            {{ getStepDescription(step) }}
                                        </div>
                                    </div>
                                </div>
                                <div class="flex items-center gap-1">
                                    <button
                                        @click="editStep(index)"
                                        class="p-1.5 text-gray-400 hover:text-white hover:bg-gray-700 rounded transition-colors"
                                    >
                                        <PencilIcon class="w-4 h-4" />
                                    </button>
                                    <button
                                        @click="removeStep(index)"
                                        class="p-1.5 text-gray-400 hover:text-red-400 hover:bg-gray-700 rounded transition-colors"
                                    >
                                        <TrashIcon class="w-4 h-4" />
                                    </button>
                                </div>
                            </div>
                        </div>

                        <!-- Empty state -->
                        <div v-if="recordedSteps.length === 0" class="text-center py-8">
                            <CursorArrowRaysIcon class="w-12 h-12 mx-auto text-gray-600 mb-3" />
                            <p class="text-gray-400 text-sm">คลิกปุ่ม "เริ่มบันทึก" แล้วใช้งานเว็บไซต์ตามปกติ</p>
                            <p class="text-gray-500 text-xs mt-1">ระบบจะบันทึกทุกการกระทำของคุณ</p>
                        </div>
                    </div>
                </div>

                <!-- Footer Actions -->
                <div class="p-4 border-t border-gray-700 bg-gray-900/50">
                    <div class="flex items-center gap-3">
                        <button
                            @click="cancelTeaching"
                            class="flex-1 px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors"
                        >
                            ยกเลิก
                        </button>
                        <button
                            @click="saveWorkflow"
                            :disabled="!canSave"
                            :class="[
                                'flex-1 px-4 py-2 rounded-lg transition-colors flex items-center justify-center gap-2',
                                canSave
                                    ? 'bg-purple-600 hover:bg-purple-700 text-white'
                                    : 'bg-gray-700 text-gray-500 cursor-not-allowed'
                            ]"
                        >
                            <CheckIcon class="w-5 h-5" />
                            บันทึก Workflow
                        </button>
                    </div>
                </div>
            </div>
        </div>

        <!-- Step Editor Modal -->
        <Modal v-model="showStepEditor" title="แก้ไขขั้นตอน" size="lg">
            <div v-if="editingStep" class="space-y-4">
                <div>
                    <label class="block text-sm font-medium text-gray-400 mb-2">ชื่อขั้นตอน</label>
                    <input
                        v-model="editingStep.name"
                        type="text"
                        class="w-full px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
                    />
                </div>
                <div>
                    <label class="block text-sm font-medium text-gray-400 mb-2">Selector</label>
                    <textarea
                        v-model="editingStep.selectors[0].value"
                        rows="3"
                        class="w-full px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white font-mono text-sm focus:ring-2 focus:ring-purple-500"
                    ></textarea>
                </div>
                <div class="grid grid-cols-2 gap-4">
                    <div>
                        <label class="block text-sm font-medium text-gray-400 mb-2">Wait Before (ms)</label>
                        <input
                            v-model.number="editingStep.wait_before_ms"
                            type="number"
                            class="w-full px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
                        />
                    </div>
                    <div>
                        <label class="block text-sm font-medium text-gray-400 mb-2">Wait After (ms)</label>
                        <input
                            v-model.number="editingStep.wait_after_ms"
                            type="number"
                            class="w-full px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
                        />
                    </div>
                </div>
            </div>
            <template #footer>
                <button
                    @click="showStepEditor = false"
                    class="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors"
                >
                    ยกเลิก
                </button>
                <button
                    @click="saveStepEdit"
                    class="px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg transition-colors"
                >
                    บันทึก
                </button>
            </template>
        </Modal>
    </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import Modal from '../ui/Modal.vue'
import {
    ClockIcon,
    StopIcon,
    XMarkIcon,
    ArrowPathIcon,
    PencilIcon,
    TrashIcon,
    CheckIcon,
    CursorArrowRaysIcon,
    CursorArrowRippleIcon,
    PencilSquareIcon,
    ArrowDownTrayIcon,
    ClockIcon as WaitIcon,
    CameraIcon,
    ArrowsPointingOutIcon,
    DocumentArrowUpIcon
} from '@heroicons/vue/24/outline'

const emit = defineEmits(['cancel', 'save'])

// Workflow info
const workflowName = ref('')
const workflowPlatform = ref('')
const workflowType = ref('')

// Recording state
const isRecording = ref(false)
const captureMode = ref('click')
const recordedSteps = ref([])
const elapsedTime = ref(0)
let timerInterval = null

// Browser state
const browserFrame = ref(null)
const currentUrl = ref('')
const frameUrl = ref('about:blank')
const frameLoading = ref(false)
const highlightBox = ref(null)

// Step editor
const showStepEditor = ref(false)
const editingStep = ref(null)
const editingStepIndex = ref(-1)

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

const workflowTypes = [
    { value: 'login', label: 'Login' },
    { value: 'post_text', label: 'Post Text' },
    { value: 'post_image', label: 'Post Image' },
    { value: 'post_video', label: 'Post Video' },
    { value: 'comment', label: 'Comment' },
    { value: 'like', label: 'Like' },
    { value: 'share', label: 'Share' },
    { value: 'follow', label: 'Follow' },
    { value: 'message', label: 'Send Message' },
    { value: 'scrape', label: 'Scrape Data' }
]

const actions = [
    { type: 'click', label: 'Click', icon: CursorArrowRippleIcon },
    { type: 'type', label: 'Type', icon: PencilSquareIcon },
    { type: 'scroll', label: 'Scroll', icon: ArrowsPointingOutIcon },
    { type: 'wait', label: 'Wait', icon: WaitIcon },
    { type: 'screenshot', label: 'Screenshot', icon: CameraIcon },
    { type: 'upload', label: 'Upload', icon: DocumentArrowUpIcon },
    { type: 'select', label: 'Select', icon: ArrowDownTrayIcon }
]

const canSave = computed(() => {
    return workflowName.value && workflowPlatform.value && workflowType.value && recordedSteps.value.length > 0
})

const startRecording = () => {
    isRecording.value = true
    elapsedTime.value = 0
    timerInterval = setInterval(() => {
        elapsedTime.value++
    }, 1000)
}

const stopRecording = () => {
    isRecording.value = false
    if (timerInterval) {
        clearInterval(timerInterval)
        timerInterval = null
    }
}

const cancelTeaching = () => {
    stopRecording()
    emit('cancel')
}

const navigateToUrl = () => {
    if (currentUrl.value) {
        frameLoading.value = true
        let url = currentUrl.value
        if (!url.startsWith('http://') && !url.startsWith('https://')) {
            url = 'https://' + url
        }
        frameUrl.value = url
    }
}

const onFrameLoad = () => {
    frameLoading.value = false
}

const highlightElement = (event) => {
    // In real implementation, this would communicate with the iframe
    // For demo, we just show a highlight box at cursor position
    const rect = event.target.getBoundingClientRect()
    highlightBox.value = {
        left: event.clientX - rect.left - 25,
        top: event.clientY - rect.top - 25,
        width: 50,
        height: 50
    }
}

const captureClick = (event) => {
    const step = {
        action_type: 'click',
        name: 'Click element',
        selectors: [
            { type: 'xpath', value: '//element', confidence: 0.8 }
        ],
        action_params: {
            x: event.clientX,
            y: event.clientY
        },
        wait_before_ms: 0,
        wait_after_ms: 500,
        timeout_ms: 30000
    }
    recordedSteps.value.push(step)
}

const addManualStep = (actionType) => {
    captureMode.value = actionType

    if (actionType === 'wait') {
        recordedSteps.value.push({
            action_type: 'wait',
            name: 'Wait',
            selectors: [],
            action_params: { duration_ms: 2000 },
            wait_before_ms: 0,
            wait_after_ms: 0,
            timeout_ms: 30000
        })
    } else if (actionType === 'screenshot') {
        recordedSteps.value.push({
            action_type: 'screenshot',
            name: 'Take screenshot',
            selectors: [],
            action_params: { filename: 'screenshot_{timestamp}.png' },
            wait_before_ms: 0,
            wait_after_ms: 0,
            timeout_ms: 30000
        })
    }
}

const editStep = (index) => {
    editingStepIndex.value = index
    editingStep.value = { ...recordedSteps.value[index] }
    showStepEditor.value = true
}

const saveStepEdit = () => {
    if (editingStepIndex.value >= 0 && editingStep.value) {
        recordedSteps.value[editingStepIndex.value] = { ...editingStep.value }
    }
    showStepEditor.value = false
    editingStep.value = null
    editingStepIndex.value = -1
}

const removeStep = (index) => {
    recordedSteps.value.splice(index, 1)
}

const getActionLabel = (type) => {
    const action = actions.find(a => a.type === type)
    return action ? action.label : type
}

const getStepDescription = (step) => {
    if (step.action_type === 'type' && step.action_params?.text) {
        return `"${step.action_params.text.substring(0, 30)}..."`
    }
    if (step.selectors?.length > 0) {
        return step.selectors[0].value.substring(0, 40)
    }
    if (step.action_type === 'wait') {
        return `${step.action_params?.duration_ms || 0}ms`
    }
    return ''
}

const formatDuration = (seconds) => {
    const mins = Math.floor(seconds / 60)
    const secs = seconds % 60
    return `${mins.toString().padStart(2, '0')}:${secs.toString().padStart(2, '0')}`
}

const saveWorkflow = async () => {
    stopRecording()

    const workflow = {
        name: workflowName.value,
        platform: workflowPlatform.value,
        workflow_type: workflowType.value,
        steps: recordedSteps.value.map((step, index) => ({
            ...step,
            step_order: index + 1
        }))
    }

    emit('save', workflow)
}

onUnmounted(() => {
    if (timerInterval) {
        clearInterval(timerInterval)
    }
})
</script>
