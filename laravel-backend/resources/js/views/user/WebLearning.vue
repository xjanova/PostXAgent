<template>
    <div class="space-y-6">
        <!-- Header -->
        <div class="flex flex-col sm:flex-row sm:items-center sm:justify-between">
            <div>
                <h1 class="text-2xl font-bold text-white">เครื่องมือเรียนรู้ Web Workflow</h1>
                <p class="text-gray-400 mt-1">บันทึกและเรียนรู้ workflow การโพสต์บนเว็บไซต์โซเชียลมีเดีย</p>
            </div>
            <button
                class="mt-4 sm:mt-0 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg transition-colors flex items-center"
                @click="startRecording"
                :disabled="isRecording"
            >
                <template v-if="!isRecording">
                    <PlayIcon class="w-5 h-5 mr-2" />
                    เริ่มบันทึก
                </template>
                <template v-else>
                    <StopIcon class="w-5 h-5 mr-2 animate-pulse" />
                    กำลังบันทึก...
                </template>
            </button>
        </div>

        <!-- Recording Status -->
        <div v-if="isRecording" class="bg-red-900/20 border border-red-500/50 rounded-xl p-6">
            <div class="flex items-center justify-between">
                <div class="flex items-center">
                    <span class="w-3 h-3 bg-red-500 rounded-full animate-pulse mr-3"></span>
                    <div>
                        <p class="text-white font-medium">กำลังบันทึก Workflow</p>
                        <p class="text-gray-400 text-sm">{{ recordingPlatform }} - {{ formatDuration(recordingDuration) }}</p>
                    </div>
                </div>
                <div class="flex items-center space-x-3">
                    <span class="text-gray-400 text-sm">{{ recordedSteps.length }} ขั้นตอน</span>
                    <button
                        class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg"
                        @click="stopRecording"
                    >
                        หยุดบันทึก
                    </button>
                </div>
            </div>

            <!-- Live Steps -->
            <div class="mt-4 max-h-48 overflow-y-auto space-y-2">
                <div
                    v-for="(step, index) in recordedSteps"
                    :key="index"
                    class="flex items-center p-2 bg-gray-800/50 rounded-lg text-sm"
                >
                    <span class="w-6 h-6 flex items-center justify-center bg-gray-700 rounded-full text-xs text-gray-300 mr-3">
                        {{ index + 1 }}
                    </span>
                    <span class="text-gray-300">{{ step.action }}</span>
                    <span class="text-gray-500 ml-auto">{{ step.element }}</span>
                </div>
            </div>
        </div>

        <!-- Platform Selection (when not recording) -->
        <div v-if="!isRecording && !showWorkflows" class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-4">เลือกแพลตฟอร์มที่ต้องการบันทึก</h2>

            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-4">
                <button
                    v-for="platform in platforms"
                    :key="platform.id"
                    :class="[
                        'p-4 rounded-lg border transition-all flex flex-col items-center',
                        selectedPlatform === platform.id
                            ? 'border-indigo-500 bg-indigo-500/10'
                            : 'border-gray-700 hover:border-gray-600'
                    ]"
                    @click="selectedPlatform = platform.id"
                >
                    <div
                        class="w-12 h-12 rounded-lg flex items-center justify-center mb-2"
                        :style="{ backgroundColor: platform.color + '20' }"
                    >
                        <component :is="platform.icon" class="w-6 h-6" :style="{ color: platform.color }" />
                    </div>
                    <span class="text-white text-sm font-medium">{{ platform.name }}</span>
                </button>
            </div>

            <div class="mt-6 p-4 bg-gray-700/50 rounded-lg">
                <h3 class="text-white font-medium mb-2">วิธีใช้งาน:</h3>
                <ol class="list-decimal list-inside text-gray-400 text-sm space-y-1">
                    <li>เลือกแพลตฟอร์มที่ต้องการบันทึก</li>
                    <li>กดปุ่ม "เริ่มบันทึก" - จะเปิดหน้าต่างเบราว์เซอร์ใหม่</li>
                    <li>ทำขั้นตอนการโพสต์ตามปกติ - ระบบจะบันทึกทุกการกระทำ</li>
                    <li>กด "หยุดบันทึก" เมื่อเสร็จสิ้น</li>
                    <li>ตรวจสอบและบันทึก Workflow เพื่อใช้งานอัตโนมัติ</li>
                </ol>
            </div>
        </div>

        <!-- Saved Workflows -->
        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <div class="flex items-center justify-between mb-4">
                <h2 class="text-lg font-semibold text-white">Workflows ที่บันทึกไว้</h2>
                <button
                    class="text-sm text-indigo-400 hover:text-indigo-300"
                    @click="showWorkflows = !showWorkflows"
                >
                    {{ showWorkflows ? 'ซ่อน' : 'แสดงทั้งหมด' }}
                </button>
            </div>

            <div v-if="workflows.length === 0" class="text-center py-8">
                <AcademicCapIcon class="w-12 h-12 text-gray-500 mx-auto mb-4" />
                <p class="text-gray-400">ยังไม่มี Workflow ที่บันทึกไว้</p>
                <p class="text-gray-500 text-sm">เริ่มบันทึก Workflow แรกของคุณ</p>
            </div>

            <div v-else class="space-y-3">
                <div
                    v-for="workflow in displayedWorkflows"
                    :key="workflow.id"
                    class="flex items-center justify-between p-4 bg-gray-700/50 rounded-lg"
                >
                    <div class="flex items-center">
                        <div
                            class="w-10 h-10 rounded-lg flex items-center justify-center mr-3"
                            :style="{ backgroundColor: getPlatformColor(workflow.platform) + '20' }"
                        >
                            <component
                                :is="getPlatformIcon(workflow.platform)"
                                class="w-5 h-5"
                                :style="{ color: getPlatformColor(workflow.platform) }"
                            />
                        </div>
                        <div>
                            <p class="text-white font-medium">{{ workflow.name }}</p>
                            <p class="text-gray-400 text-sm">
                                {{ workflow.steps_count }} ขั้นตอน ·
                                สร้างเมื่อ {{ formatDate(workflow.created_at) }}
                            </p>
                        </div>
                    </div>
                    <div class="flex items-center space-x-2">
                        <button
                            class="p-2 text-gray-400 hover:text-white rounded-lg hover:bg-gray-600"
                            title="ทดสอบ Workflow"
                            @click="testWorkflow(workflow)"
                        >
                            <PlayIcon class="w-5 h-5" />
                        </button>
                        <button
                            class="p-2 text-gray-400 hover:text-white rounded-lg hover:bg-gray-600"
                            title="แก้ไข"
                            @click="editWorkflow(workflow)"
                        >
                            <PencilIcon class="w-5 h-5" />
                        </button>
                        <button
                            class="p-2 text-gray-400 hover:text-red-400 rounded-lg hover:bg-gray-600"
                            title="ลบ"
                            @click="deleteWorkflow(workflow)"
                        >
                            <TrashIcon class="w-5 h-5" />
                        </button>
                    </div>
                </div>
            </div>
        </div>

        <!-- Workflow Editor Modal -->
        <div v-if="showEditor" class="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
            <div class="bg-gray-800 rounded-xl p-6 max-w-4xl w-full mx-4 max-h-[90vh] overflow-y-auto border border-gray-700">
                <div class="flex items-center justify-between mb-6">
                    <h3 class="text-xl font-bold text-white">
                        {{ editingWorkflow ? 'แก้ไข Workflow' : 'บันทึก Workflow ใหม่' }}
                    </h3>
                    <button
                        class="p-2 text-gray-400 hover:text-white"
                        @click="closeEditor"
                    >
                        <XMarkIcon class="w-6 h-6" />
                    </button>
                </div>

                <form @submit.prevent="saveWorkflow" class="space-y-6">
                    <!-- Workflow Name -->
                    <div>
                        <label class="block text-sm font-medium text-gray-300 mb-1">ชื่อ Workflow</label>
                        <input
                            v-model="workflowForm.name"
                            type="text"
                            class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white"
                            placeholder="เช่น โพสต์รูปภาพ Facebook"
                            required
                        />
                    </div>

                    <!-- Platform -->
                    <div>
                        <label class="block text-sm font-medium text-gray-300 mb-1">แพลตฟอร์ม</label>
                        <select
                            v-model="workflowForm.platform"
                            class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white"
                        >
                            <option v-for="p in platforms" :key="p.id" :value="p.id">{{ p.name }}</option>
                        </select>
                    </div>

                    <!-- Steps -->
                    <div>
                        <label class="block text-sm font-medium text-gray-300 mb-2">ขั้นตอน</label>
                        <div class="space-y-2 max-h-64 overflow-y-auto">
                            <div
                                v-for="(step, index) in workflowForm.steps"
                                :key="index"
                                class="flex items-center p-3 bg-gray-700/50 rounded-lg"
                            >
                                <span class="w-8 h-8 flex items-center justify-center bg-indigo-600/20 rounded-full text-indigo-400 text-sm mr-3">
                                    {{ index + 1 }}
                                </span>
                                <div class="flex-1">
                                    <p class="text-white text-sm">{{ step.action }}</p>
                                    <p class="text-gray-400 text-xs">{{ step.selector }}</p>
                                </div>
                                <div class="flex items-center space-x-2">
                                    <input
                                        type="number"
                                        v-model="step.delay"
                                        class="w-16 px-2 py-1 bg-gray-600 border border-gray-500 rounded text-white text-sm"
                                        placeholder="ms"
                                        title="Delay (ms)"
                                    />
                                    <button
                                        type="button"
                                        class="p-1 text-red-400 hover:text-red-300"
                                        @click="removeStep(index)"
                                    >
                                        <TrashIcon class="w-4 h-4" />
                                    </button>
                                </div>
                            </div>
                        </div>

                        <!-- Add Step -->
                        <button
                            type="button"
                            class="mt-3 px-4 py-2 bg-gray-700 hover:bg-gray-600 text-gray-300 rounded-lg text-sm flex items-center"
                            @click="addStep"
                        >
                            <PlusIcon class="w-4 h-4 mr-2" />
                            เพิ่มขั้นตอน
                        </button>
                    </div>

                    <!-- Actions -->
                    <div class="flex justify-end space-x-3">
                        <button
                            type="button"
                            class="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg"
                            @click="closeEditor"
                        >
                            ยกเลิก
                        </button>
                        <button
                            type="submit"
                            class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg"
                        >
                            บันทึก
                        </button>
                    </div>
                </form>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted, onUnmounted } from 'vue'
import {
    PlayIcon,
    StopIcon,
    PencilIcon,
    TrashIcon,
    PlusIcon,
    XMarkIcon,
    AcademicCapIcon,
} from '@heroicons/vue/24/outline'

// Platform icons (simplified)
const FacebookIcon = { template: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M24 12.073c0-6.627-5.373-12-12-12s-12 5.373-12 12c0 5.99 4.388 10.954 10.125 11.854v-8.385H7.078v-3.47h3.047V9.43c0-3.007 1.792-4.669 4.533-4.669 1.312 0 2.686.235 2.686.235v2.953H15.83c-1.491 0-1.956.925-1.956 1.874v2.25h3.328l-.532 3.47h-2.796v8.385C19.612 23.027 24 18.062 24 12.073z"/></svg>' }
const InstagramIcon = { template: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12 2.163c3.204 0 3.584.012 4.85.07 3.252.148 4.771 1.691 4.919 4.919.058 1.265.069 1.645.069 4.849 0 3.205-.012 3.584-.069 4.849-.149 3.225-1.664 4.771-4.919 4.919-1.266.058-1.644.07-4.85.07-3.204 0-3.584-.012-4.849-.07-3.26-.149-4.771-1.699-4.919-4.92-.058-1.265-.07-1.644-.07-4.849 0-3.204.013-3.583.07-4.849.149-3.227 1.664-4.771 4.919-4.919 1.266-.057 1.645-.069 4.849-.069zM12 0C8.741 0 8.333.014 7.053.072 2.695.272.273 2.69.073 7.052.014 8.333 0 8.741 0 12c0 3.259.014 3.668.072 4.948.2 4.358 2.618 6.78 6.98 6.98C8.333 23.986 8.741 24 12 24c3.259 0 3.668-.014 4.948-.072 4.354-.2 6.782-2.618 6.979-6.98.059-1.28.073-1.689.073-4.948 0-3.259-.014-3.667-.072-4.947-.196-4.354-2.617-6.78-6.979-6.98C15.668.014 15.259 0 12 0zm0 5.838a6.162 6.162 0 100 12.324 6.162 6.162 0 000-12.324zM12 16a4 4 0 110-8 4 4 0 010 8zm6.406-11.845a1.44 1.44 0 100 2.881 1.44 1.44 0 000-2.881z"/></svg>' }
const TikTokIcon = { template: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M12.525.02c1.31-.02 2.61-.01 3.91-.02.08 1.53.63 3.09 1.75 4.17 1.12 1.11 2.7 1.62 4.24 1.79v4.03c-1.44-.05-2.89-.35-4.2-.97-.57-.26-1.1-.59-1.62-.93-.01 2.92.01 5.84-.02 8.75-.08 1.4-.54 2.79-1.35 3.94-1.31 1.92-3.58 3.17-5.91 3.21-1.43.08-2.86-.31-4.08-1.03-2.02-1.19-3.44-3.37-3.65-5.71-.02-.5-.03-1-.01-1.49.18-1.9 1.12-3.72 2.58-4.96 1.66-1.44 3.98-2.13 6.15-1.72.02 1.48-.04 2.96-.04 4.44-.99-.32-2.15-.23-3.02.37-.63.41-1.11 1.04-1.36 1.75-.21.51-.15 1.07-.14 1.61.24 1.64 1.82 3.02 3.5 2.87 1.12-.01 2.19-.66 2.77-1.61.19-.33.4-.67.41-1.06.1-1.79.06-3.57.07-5.36.01-4.03-.01-8.05.02-12.07z"/></svg>' }
const TwitterIcon = { template: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M18.244 2.25h3.308l-7.227 8.26 8.502 11.24H16.17l-5.214-6.817L4.99 21.75H1.68l7.73-8.835L1.254 2.25H8.08l4.713 6.231zm-1.161 17.52h1.833L7.084 4.126H5.117z"/></svg>' }
const YouTubeIcon = { template: '<svg viewBox="0 0 24 24" fill="currentColor"><path d="M23.498 6.186a3.016 3.016 0 0 0-2.122-2.136C19.505 3.545 12 3.545 12 3.545s-7.505 0-9.377.505A3.017 3.017 0 0 0 .502 6.186C0 8.07 0 12 0 12s0 3.93.502 5.814a3.016 3.016 0 0 0 2.122 2.136c1.871.505 9.376.505 9.376.505s7.505 0 9.377-.505a3.015 3.015 0 0 0 2.122-2.136C24 15.93 24 12 24 12s0-3.93-.502-5.814zM9.545 15.568V8.432L15.818 12l-6.273 3.568z"/></svg>' }

const platforms = [
    { id: 'facebook', name: 'Facebook', icon: FacebookIcon, color: '#1877f2' },
    { id: 'instagram', name: 'Instagram', icon: InstagramIcon, color: '#e4405f' },
    { id: 'tiktok', name: 'TikTok', icon: TikTokIcon, color: '#000000' },
    { id: 'twitter', name: 'Twitter/X', icon: TwitterIcon, color: '#1da1f2' },
    { id: 'youtube', name: 'YouTube', icon: YouTubeIcon, color: '#ff0000' },
]

const selectedPlatform = ref('')
const isRecording = ref(false)
const recordingPlatform = ref('')
const recordingDuration = ref(0)
const recordedSteps = ref([])
const workflows = ref([])
const showWorkflows = ref(false)
const showEditor = ref(false)
const editingWorkflow = ref(null)

const workflowForm = reactive({
    name: '',
    platform: '',
    steps: [],
})

let recordingInterval = null

const displayedWorkflows = computed(() => {
    return showWorkflows.value ? workflows.value : workflows.value.slice(0, 3)
})

const formatDuration = (seconds) => {
    const mins = Math.floor(seconds / 60)
    const secs = seconds % 60
    return `${mins}:${secs.toString().padStart(2, '0')}`
}

const formatDate = (date) => {
    return new Date(date).toLocaleDateString('th-TH')
}

const getPlatformColor = (platformId) => {
    return platforms.find(p => p.id === platformId)?.color || '#6b7280'
}

const getPlatformIcon = (platformId) => {
    return platforms.find(p => p.id === platformId)?.icon || null
}

const startRecording = () => {
    if (!selectedPlatform.value) {
        alert('กรุณาเลือกแพลตฟอร์มก่อน')
        return
    }

    isRecording.value = true
    recordingPlatform.value = platforms.find(p => p.id === selectedPlatform.value)?.name || ''
    recordingDuration.value = 0
    recordedSteps.value = []

    // Start duration counter
    recordingInterval = setInterval(() => {
        recordingDuration.value++
    }, 1000)

    // Simulate step recording (in real app, this would use browser extension or WebSocket)
    simulateRecording()
}

const simulateRecording = () => {
    const sampleSteps = [
        { action: 'Navigate to', element: 'facebook.com' },
        { action: 'Click', element: 'Create Post button' },
        { action: 'Type', element: 'Post content area' },
        { action: 'Click', element: 'Add Photo button' },
        { action: 'Upload', element: 'Image file' },
        { action: 'Click', element: 'Post button' },
    ]

    let stepIndex = 0
    const stepInterval = setInterval(() => {
        if (stepIndex < sampleSteps.length && isRecording.value) {
            recordedSteps.value.push(sampleSteps[stepIndex])
            stepIndex++
        } else {
            clearInterval(stepInterval)
        }
    }, 2000)
}

const stopRecording = () => {
    isRecording.value = false
    if (recordingInterval) {
        clearInterval(recordingInterval)
    }

    if (recordedSteps.value.length > 0) {
        workflowForm.name = `${recordingPlatform.value} Workflow`
        workflowForm.platform = selectedPlatform.value
        workflowForm.steps = recordedSteps.value.map(step => ({
            ...step,
            selector: `#${step.element.toLowerCase().replace(/\s+/g, '-')}`,
            delay: 500,
        }))
        showEditor.value = true
    }
}

const testWorkflow = (workflow) => {
    alert(`Testing workflow: ${workflow.name}`)
    // In real app, this would trigger the automation
}

const editWorkflow = (workflow) => {
    editingWorkflow.value = workflow
    workflowForm.name = workflow.name
    workflowForm.platform = workflow.platform
    workflowForm.steps = [...workflow.steps]
    showEditor.value = true
}

const deleteWorkflow = (workflow) => {
    if (confirm(`ลบ Workflow "${workflow.name}" ?`)) {
        workflows.value = workflows.value.filter(w => w.id !== workflow.id)
    }
}

const addStep = () => {
    workflowForm.steps.push({
        action: 'Click',
        element: '',
        selector: '',
        delay: 500,
    })
}

const removeStep = (index) => {
    workflowForm.steps.splice(index, 1)
}

const saveWorkflow = () => {
    const workflow = {
        id: editingWorkflow.value?.id || Date.now(),
        name: workflowForm.name,
        platform: workflowForm.platform,
        steps: workflowForm.steps,
        steps_count: workflowForm.steps.length,
        created_at: editingWorkflow.value?.created_at || new Date().toISOString(),
    }

    if (editingWorkflow.value) {
        const index = workflows.value.findIndex(w => w.id === editingWorkflow.value.id)
        workflows.value[index] = workflow
    } else {
        workflows.value.unshift(workflow)
    }

    closeEditor()
}

const closeEditor = () => {
    showEditor.value = false
    editingWorkflow.value = null
    workflowForm.name = ''
    workflowForm.platform = ''
    workflowForm.steps = []
}

onMounted(() => {
    // Load saved workflows from localStorage or API
    const saved = localStorage.getItem('workflows')
    if (saved) {
        workflows.value = JSON.parse(saved)
    }
})

onUnmounted(() => {
    if (recordingInterval) {
        clearInterval(recordingInterval)
    }
    // Save workflows
    localStorage.setItem('workflows', JSON.stringify(workflows.value))
})
</script>
