<template>
    <div class="h-full flex flex-col bg-gray-900">
        <!-- Header -->
        <div class="flex items-center justify-between px-6 py-4 border-b border-gray-700 bg-gray-800">
            <div class="flex items-center gap-4">
                <button
                    @click="$emit('back')"
                    class="p-2 text-gray-400 hover:text-white hover:bg-gray-700 rounded-lg transition-colors"
                >
                    <ArrowLeftIcon class="w-5 h-5" />
                </button>
                <div>
                    <div class="flex items-center gap-2">
                        <input
                            v-model="workflow.name"
                            type="text"
                            class="text-xl font-semibold text-white bg-transparent border-0 focus:ring-0 p-0"
                            placeholder="Workflow Name"
                        />
                        <span :class="['px-2 py-1 text-xs rounded', statusClass]">
                            {{ workflow.status }}
                        </span>
                    </div>
                    <div class="flex items-center gap-3 mt-1 text-sm text-gray-400">
                        <span>{{ platformLabel }}</span>
                        <span class="w-1 h-1 rounded-full bg-gray-600"></span>
                        <span>{{ workflowTypeLabel }}</span>
                        <span class="w-1 h-1 rounded-full bg-gray-600"></span>
                        <span>{{ workflow.steps?.length || 0 }} ขั้นตอน</span>
                    </div>
                </div>
            </div>
            <div class="flex items-center gap-3">
                <button
                    @click="testWorkflow"
                    :disabled="testing"
                    class="flex items-center gap-2 px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors disabled:opacity-50"
                >
                    <PlayIcon v-if="!testing" class="w-5 h-5" />
                    <div v-else class="w-5 h-5 animate-spin rounded-full border-2 border-white border-t-transparent"></div>
                    ทดสอบ
                </button>
                <button
                    @click="saveWorkflow"
                    :disabled="saving"
                    class="flex items-center gap-2 px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg transition-colors disabled:opacity-50"
                >
                    <CheckIcon v-if="!saving" class="w-5 h-5" />
                    <div v-else class="w-5 h-5 animate-spin rounded-full border-2 border-white border-t-transparent"></div>
                    บันทึก
                </button>
            </div>
        </div>

        <!-- Main Content -->
        <div class="flex-1 flex overflow-hidden">
            <!-- Steps Panel -->
            <div class="w-80 border-r border-gray-700 flex flex-col bg-gray-800">
                <div class="p-4 border-b border-gray-700">
                    <div class="flex items-center justify-between mb-3">
                        <h3 class="font-medium text-white">ขั้นตอน</h3>
                        <button
                            @click="addStep"
                            class="p-1.5 text-gray-400 hover:text-white hover:bg-gray-700 rounded transition-colors"
                        >
                            <PlusIcon class="w-5 h-5" />
                        </button>
                    </div>
                </div>

                <!-- Steps List -->
                <div class="flex-1 overflow-y-auto p-4">
                    <draggable
                        v-model="workflow.steps"
                        item-key="id"
                        handle=".drag-handle"
                        class="space-y-2"
                    >
                        <template #item="{ element, index }">
                            <div
                                :class="[
                                    'bg-gray-900 rounded-lg border transition-colors cursor-pointer',
                                    selectedStepIndex === index
                                        ? 'border-purple-500 ring-1 ring-purple-500/50'
                                        : 'border-gray-700 hover:border-gray-600'
                                ]"
                                @click="selectStep(index)"
                            >
                                <div class="flex items-center gap-3 p-3">
                                    <div class="drag-handle cursor-move text-gray-500 hover:text-gray-300">
                                        <Bars3Icon class="w-4 h-4" />
                                    </div>
                                    <div class="w-7 h-7 rounded bg-purple-600/20 flex items-center justify-center text-purple-400 text-sm font-medium">
                                        {{ index + 1 }}
                                    </div>
                                    <div class="flex-1 min-w-0">
                                        <div class="text-sm font-medium text-white truncate">
                                            {{ element.name || getActionLabel(element.action_type) }}
                                        </div>
                                        <div class="text-xs text-gray-400 truncate">
                                            {{ element.action_type }}
                                        </div>
                                    </div>
                                    <button
                                        @click.stop="removeStep(index)"
                                        class="p-1 text-gray-500 hover:text-red-400 transition-colors"
                                    >
                                        <XMarkIcon class="w-4 h-4" />
                                    </button>
                                </div>
                            </div>
                        </template>
                    </draggable>

                    <!-- Empty State -->
                    <div v-if="!workflow.steps?.length" class="text-center py-8">
                        <PlusCircleIcon class="w-12 h-12 mx-auto text-gray-600 mb-3" />
                        <p class="text-gray-400 text-sm">ยังไม่มีขั้นตอน</p>
                        <button
                            @click="addStep"
                            class="mt-3 text-sm text-purple-400 hover:text-purple-300"
                        >
                            + เพิ่มขั้นตอนแรก
                        </button>
                    </div>
                </div>
            </div>

            <!-- Step Editor Panel -->
            <div class="flex-1 flex flex-col">
                <template v-if="selectedStep">
                    <!-- Step Header -->
                    <div class="p-4 border-b border-gray-700">
                        <div class="flex items-center justify-between">
                            <div class="flex items-center gap-3">
                                <div class="w-10 h-10 rounded-lg bg-purple-600/20 flex items-center justify-center text-purple-400 font-medium">
                                    {{ selectedStepIndex + 1 }}
                                </div>
                                <input
                                    v-model="selectedStep.name"
                                    type="text"
                                    class="text-lg font-medium text-white bg-transparent border-0 focus:ring-0 p-0"
                                    :placeholder="getActionLabel(selectedStep.action_type)"
                                />
                            </div>
                        </div>
                    </div>

                    <!-- Step Form -->
                    <div class="flex-1 overflow-y-auto p-6 space-y-6">
                        <!-- Action Type -->
                        <div>
                            <label class="block text-sm font-medium text-gray-400 mb-2">Action Type</label>
                            <select
                                v-model="selectedStep.action_type"
                                class="w-full px-4 py-2 bg-gray-800 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
                            >
                                <option v-for="action in actionTypes" :key="action.value" :value="action.value">
                                    {{ action.label }}
                                </option>
                            </select>
                        </div>

                        <!-- Selectors -->
                        <div>
                            <div class="flex items-center justify-between mb-2">
                                <label class="text-sm font-medium text-gray-400">Selectors</label>
                                <button
                                    @click="addSelector"
                                    class="text-sm text-purple-400 hover:text-purple-300"
                                >
                                    + เพิ่ม Selector
                                </button>
                            </div>
                            <div class="space-y-3">
                                <div
                                    v-for="(selector, sIndex) in selectedStep.selectors"
                                    :key="sIndex"
                                    class="p-4 bg-gray-800 rounded-lg border border-gray-700"
                                >
                                    <div class="flex items-start gap-3">
                                        <select
                                            v-model="selector.type"
                                            class="w-32 px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white text-sm"
                                        >
                                            <option value="css">CSS</option>
                                            <option value="xpath">XPath</option>
                                            <option value="id">ID</option>
                                            <option value="name">Name</option>
                                            <option value="text">Text</option>
                                            <option value="smart">Smart</option>
                                        </select>
                                        <textarea
                                            v-model="selector.value"
                                            rows="2"
                                            class="flex-1 px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white text-sm font-mono"
                                            placeholder="Selector value..."
                                        ></textarea>
                                        <button
                                            v-if="selectedStep.selectors.length > 1"
                                            @click="removeSelector(sIndex)"
                                            class="p-2 text-gray-400 hover:text-red-400 transition-colors"
                                        >
                                            <TrashIcon class="w-4 h-4" />
                                        </button>
                                    </div>
                                    <div class="flex items-center gap-4 mt-3">
                                        <div class="flex items-center gap-2">
                                            <label class="text-xs text-gray-400">Confidence</label>
                                            <input
                                                v-model.number="selector.confidence"
                                                type="number"
                                                min="0"
                                                max="1"
                                                step="0.1"
                                                class="w-20 px-2 py-1 bg-gray-900 border border-gray-600 rounded text-white text-sm"
                                            />
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <!-- Action Parameters -->
                        <div v-if="showActionParams">
                            <label class="block text-sm font-medium text-gray-400 mb-2">Action Parameters</label>
                            <div class="p-4 bg-gray-800 rounded-lg border border-gray-700 space-y-4">
                                <!-- Type action -->
                                <template v-if="selectedStep.action_type === 'type'">
                                    <div>
                                        <label class="block text-xs text-gray-400 mb-1">Text to type</label>
                                        <input
                                            v-model="selectedStep.action_params.text"
                                            type="text"
                                            class="w-full px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white"
                                            placeholder="Enter text..."
                                        />
                                    </div>
                                    <div>
                                        <label class="block text-xs text-gray-400 mb-1">หรือใช้ค่าจาก Content</label>
                                        <select
                                            v-model="selectedStep.input_source"
                                            class="w-full px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white"
                                        >
                                            <option value="">-- ใช้ค่า static --</option>
                                            <option value="content.title">content.title</option>
                                            <option value="content.body">content.body</option>
                                            <option value="content.hashtags">content.hashtags</option>
                                            <option value="content.image_url">content.image_url</option>
                                        </select>
                                    </div>
                                </template>

                                <!-- Wait action -->
                                <template v-if="selectedStep.action_type === 'wait'">
                                    <div>
                                        <label class="block text-xs text-gray-400 mb-1">Duration (ms)</label>
                                        <input
                                            v-model.number="selectedStep.action_params.duration_ms"
                                            type="number"
                                            class="w-full px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white"
                                        />
                                    </div>
                                </template>

                                <!-- Scroll action -->
                                <template v-if="selectedStep.action_type === 'scroll'">
                                    <div class="grid grid-cols-2 gap-4">
                                        <div>
                                            <label class="block text-xs text-gray-400 mb-1">X offset</label>
                                            <input
                                                v-model.number="selectedStep.action_params.x"
                                                type="number"
                                                class="w-full px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white"
                                            />
                                        </div>
                                        <div>
                                            <label class="block text-xs text-gray-400 mb-1">Y offset</label>
                                            <input
                                                v-model.number="selectedStep.action_params.y"
                                                type="number"
                                                class="w-full px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white"
                                            />
                                        </div>
                                    </div>
                                </template>

                                <!-- Key press action -->
                                <template v-if="selectedStep.action_type === 'key_press'">
                                    <div>
                                        <label class="block text-xs text-gray-400 mb-1">Key</label>
                                        <input
                                            v-model="selectedStep.action_params.key"
                                            type="text"
                                            class="w-full px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white"
                                            placeholder="e.g., Enter, Tab, Escape"
                                        />
                                    </div>
                                </template>
                            </div>
                        </div>

                        <!-- Timing -->
                        <div>
                            <label class="block text-sm font-medium text-gray-400 mb-2">Timing (ms)</label>
                            <div class="grid grid-cols-3 gap-4">
                                <div>
                                    <label class="block text-xs text-gray-400 mb-1">Wait Before</label>
                                    <input
                                        v-model.number="selectedStep.wait_before_ms"
                                        type="number"
                                        class="w-full px-3 py-2 bg-gray-800 border border-gray-600 rounded text-white"
                                    />
                                </div>
                                <div>
                                    <label class="block text-xs text-gray-400 mb-1">Wait After</label>
                                    <input
                                        v-model.number="selectedStep.wait_after_ms"
                                        type="number"
                                        class="w-full px-3 py-2 bg-gray-800 border border-gray-600 rounded text-white"
                                    />
                                </div>
                                <div>
                                    <label class="block text-xs text-gray-400 mb-1">Timeout</label>
                                    <input
                                        v-model.number="selectedStep.timeout_ms"
                                        type="number"
                                        class="w-full px-3 py-2 bg-gray-800 border border-gray-600 rounded text-white"
                                    />
                                </div>
                            </div>
                        </div>

                        <!-- Retry Configuration -->
                        <div>
                            <label class="block text-sm font-medium text-gray-400 mb-2">Retry Configuration</label>
                            <div class="p-4 bg-gray-800 rounded-lg border border-gray-700">
                                <div class="flex items-center gap-4">
                                    <div>
                                        <label class="block text-xs text-gray-400 mb-1">Max Retries</label>
                                        <input
                                            v-model.number="selectedStep.max_retries"
                                            type="number"
                                            min="0"
                                            class="w-24 px-3 py-2 bg-gray-900 border border-gray-600 rounded text-white"
                                        />
                                    </div>
                                    <div class="flex items-center gap-2">
                                        <input
                                            v-model="selectedStep.is_optional"
                                            type="checkbox"
                                            id="is_optional"
                                            class="w-4 h-4 rounded bg-gray-900 border-gray-600 text-purple-600 focus:ring-purple-500"
                                        />
                                        <label for="is_optional" class="text-sm text-gray-300">Optional step</label>
                                    </div>
                                    <div class="flex items-center gap-2">
                                        <input
                                            v-model="selectedStep.skip_on_failure"
                                            type="checkbox"
                                            id="skip_on_failure"
                                            class="w-4 h-4 rounded bg-gray-900 border-gray-600 text-purple-600 focus:ring-purple-500"
                                        />
                                        <label for="skip_on_failure" class="text-sm text-gray-300">Skip on failure</label>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </template>

                <!-- No Step Selected -->
                <div v-else class="flex-1 flex items-center justify-center">
                    <div class="text-center">
                        <CursorArrowRaysIcon class="w-16 h-16 mx-auto text-gray-600 mb-4" />
                        <p class="text-gray-400">เลือกขั้นตอนจากรายการทางซ้าย</p>
                        <p class="text-gray-500 text-sm mt-1">หรือเพิ่มขั้นตอนใหม่</p>
                    </div>
                </div>
            </div>

            <!-- Preview Panel -->
            <div class="w-80 border-l border-gray-700 bg-gray-800 flex flex-col">
                <div class="p-4 border-b border-gray-700">
                    <h3 class="font-medium text-white">Test Results</h3>
                </div>
                <div class="flex-1 overflow-y-auto p-4">
                    <div v-if="testResults.length > 0" class="space-y-3">
                        <div
                            v-for="(result, index) in testResults"
                            :key="index"
                            :class="[
                                'p-3 rounded-lg border',
                                result.success
                                    ? 'bg-green-500/10 border-green-500/30'
                                    : 'bg-red-500/10 border-red-500/30'
                            ]"
                        >
                            <div class="flex items-center gap-2 mb-1">
                                <CheckCircleIcon v-if="result.success" class="w-4 h-4 text-green-400" />
                                <XCircleIcon v-else class="w-4 h-4 text-red-400" />
                                <span class="text-sm font-medium" :class="result.success ? 'text-green-400' : 'text-red-400'">
                                    Step {{ result.step }}
                                </span>
                            </div>
                            <p class="text-xs text-gray-400">{{ result.message }}</p>
                            <p v-if="result.duration" class="text-xs text-gray-500 mt-1">{{ result.duration }}ms</p>
                        </div>
                    </div>
                    <div v-else class="text-center py-8">
                        <BeakerIcon class="w-12 h-12 mx-auto text-gray-600 mb-3" />
                        <p class="text-gray-400 text-sm">ยังไม่มีผลการทดสอบ</p>
                        <p class="text-gray-500 text-xs mt-1">คลิก "ทดสอบ" เพื่อรัน workflow</p>
                    </div>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import draggable from 'vuedraggable'
import {
    ArrowLeftIcon,
    PlayIcon,
    CheckIcon,
    PlusIcon,
    XMarkIcon,
    Bars3Icon,
    PlusCircleIcon,
    CursorArrowRaysIcon,
    TrashIcon,
    CheckCircleIcon,
    XCircleIcon,
    BeakerIcon
} from '@heroicons/vue/24/outline'

const props = defineProps({
    workflow: {
        type: Object,
        required: true
    }
})

const emit = defineEmits(['back', 'save', 'test'])

const selectedStepIndex = ref(-1)
const testing = ref(false)
const saving = ref(false)
const testResults = ref([])

const platforms = {
    facebook: 'Facebook',
    instagram: 'Instagram',
    tiktok: 'TikTok',
    twitter: 'Twitter/X',
    line: 'LINE',
    youtube: 'YouTube',
    threads: 'Threads',
    linkedin: 'LinkedIn',
    pinterest: 'Pinterest'
}

const workflowTypes = {
    login: 'Login',
    post_text: 'Post Text',
    post_image: 'Post Image',
    post_video: 'Post Video',
    comment: 'Comment',
    like: 'Like',
    share: 'Share',
    follow: 'Follow',
    message: 'Send Message'
}

const actionTypes = [
    { value: 'click', label: 'Click' },
    { value: 'type', label: 'Type Text' },
    { value: 'select', label: 'Select Option' },
    { value: 'scroll', label: 'Scroll' },
    { value: 'wait', label: 'Wait' },
    { value: 'screenshot', label: 'Screenshot' },
    { value: 'upload', label: 'Upload File' },
    { value: 'drag', label: 'Drag & Drop' },
    { value: 'hover', label: 'Hover' },
    { value: 'key_press', label: 'Key Press' },
    { value: 'execute_script', label: 'Execute Script' },
    { value: 'condition', label: 'Condition' }
]

const platformLabel = computed(() => platforms[props.workflow.platform] || props.workflow.platform)
const workflowTypeLabel = computed(() => workflowTypes[props.workflow.workflow_type] || props.workflow.workflow_type)

const statusClass = computed(() => {
    const classes = {
        active: 'bg-green-500/20 text-green-400',
        learning: 'bg-yellow-500/20 text-yellow-400',
        disabled: 'bg-gray-500/20 text-gray-400'
    }
    return classes[props.workflow.status] || classes.disabled
})

const selectedStep = computed(() => {
    if (selectedStepIndex.value >= 0 && props.workflow.steps?.[selectedStepIndex.value]) {
        return props.workflow.steps[selectedStepIndex.value]
    }
    return null
})

const showActionParams = computed(() => {
    return selectedStep.value && ['type', 'wait', 'scroll', 'key_press', 'upload'].includes(selectedStep.value.action_type)
})

const selectStep = (index) => {
    selectedStepIndex.value = index
}

const addStep = () => {
    if (!props.workflow.steps) {
        props.workflow.steps = []
    }
    const newStep = {
        id: Date.now(),
        step_order: props.workflow.steps.length + 1,
        name: '',
        action_type: 'click',
        selectors: [{ type: 'css', value: '', confidence: 0.8 }],
        action_params: {},
        wait_before_ms: 0,
        wait_after_ms: 500,
        timeout_ms: 30000,
        max_retries: 3,
        is_optional: false,
        skip_on_failure: false
    }
    props.workflow.steps.push(newStep)
    selectedStepIndex.value = props.workflow.steps.length - 1
}

const removeStep = (index) => {
    props.workflow.steps.splice(index, 1)
    if (selectedStepIndex.value >= props.workflow.steps.length) {
        selectedStepIndex.value = props.workflow.steps.length - 1
    }
}

const addSelector = () => {
    if (selectedStep.value) {
        selectedStep.value.selectors.push({ type: 'css', value: '', confidence: 0.5 })
    }
}

const removeSelector = (index) => {
    if (selectedStep.value && selectedStep.value.selectors.length > 1) {
        selectedStep.value.selectors.splice(index, 1)
    }
}

const getActionLabel = (type) => {
    const action = actionTypes.find(a => a.value === type)
    return action ? action.label : type
}

const testWorkflow = async () => {
    testing.value = true
    testResults.value = []

    try {
        emit('test', props.workflow)
        // Simulate test results
        for (let i = 0; i < props.workflow.steps.length; i++) {
            await new Promise(resolve => setTimeout(resolve, 500))
            testResults.value.push({
                step: i + 1,
                success: Math.random() > 0.2,
                message: Math.random() > 0.2 ? 'Element found and clicked' : 'Element not found',
                duration: Math.floor(Math.random() * 1000) + 100
            })
        }
    } finally {
        testing.value = false
    }
}

const saveWorkflow = async () => {
    saving.value = true
    try {
        emit('save', props.workflow)
    } finally {
        saving.value = false
    }
}

// Update step order on drag
watch(() => props.workflow.steps, (steps) => {
    if (steps) {
        steps.forEach((step, index) => {
            step.step_order = index + 1
        })
    }
}, { deep: true })
</script>
