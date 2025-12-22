<template>
    <div class="space-y-6">
        <!-- Header -->
        <div class="flex items-center justify-between">
            <div>
                <h2 class="text-xl font-semibold text-white">AI Page Analyzer</h2>
                <p class="text-sm text-gray-400">ให้ AI วิเคราะห์หน้าเว็บและสร้าง workflow อัตโนมัติ</p>
            </div>
        </div>

        <!-- Input Section -->
        <div class="p-6 bg-gray-800 rounded-lg border border-gray-700 space-y-4">
            <div>
                <label class="block text-sm font-medium text-gray-400 mb-2">URL ที่ต้องการวิเคราะห์</label>
                <div class="flex items-center gap-3">
                    <input
                        v-model="pageUrl"
                        type="url"
                        placeholder="https://facebook.com/login"
                        class="flex-1 px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
                    />
                    <button
                        @click="analyzeUrl"
                        :disabled="analyzing || !pageUrl"
                        class="flex items-center gap-2 px-6 py-2 bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white rounded-lg transition-colors disabled:opacity-50"
                    >
                        <SparklesIcon v-if="!analyzing" class="w-5 h-5" />
                        <div v-else class="w-5 h-5 animate-spin rounded-full border-2 border-white border-t-transparent"></div>
                        วิเคราะห์ด้วย AI
                    </button>
                </div>
            </div>

            <div class="flex items-center gap-4">
                <select
                    v-model="platform"
                    class="px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
                >
                    <option value="">เลือก Platform</option>
                    <option v-for="p in platforms" :key="p.value" :value="p.value">{{ p.label }}</option>
                </select>
                <select
                    v-model="purpose"
                    class="px-4 py-2 bg-gray-900 border border-gray-600 rounded-lg text-white focus:ring-2 focus:ring-purple-500"
                >
                    <option value="">เป้าหมาย</option>
                    <option value="login">Login</option>
                    <option value="post">Create Post</option>
                    <option value="comment">Comment</option>
                    <option value="message">Send Message</option>
                    <option value="scrape">Scrape Data</option>
                </select>
            </div>
        </div>

        <!-- Analysis Progress -->
        <div v-if="analyzing" class="p-6 bg-gray-800 rounded-lg border border-purple-500/50">
            <div class="flex items-center gap-4 mb-4">
                <div class="w-12 h-12 rounded-full bg-purple-600/20 flex items-center justify-center">
                    <CpuChipIcon class="w-6 h-6 text-purple-400 animate-pulse" />
                </div>
                <div>
                    <h3 class="text-lg font-medium text-white">AI กำลังวิเคราะห์...</h3>
                    <p class="text-sm text-gray-400">{{ analysisStep }}</p>
                </div>
            </div>
            <div class="h-2 bg-gray-700 rounded-full overflow-hidden">
                <div
                    class="h-full bg-gradient-to-r from-blue-500 to-purple-500 rounded-full transition-all"
                    :style="{ width: `${analysisProgress}%` }"
                ></div>
            </div>
        </div>

        <!-- Analysis Results -->
        <div v-if="analysisResult && !analyzing" class="space-y-6">
            <!-- Page Overview -->
            <div class="p-6 bg-gray-800 rounded-lg border border-gray-700">
                <h3 class="text-lg font-medium text-white mb-4 flex items-center gap-2">
                    <DocumentTextIcon class="w-5 h-5 text-purple-400" />
                    ภาพรวมหน้าเว็บ
                </h3>
                <div class="grid grid-cols-2 gap-4">
                    <div>
                        <p class="text-sm text-gray-400 mb-1">Page Type</p>
                        <p class="text-white">{{ analysisResult.page_type }}</p>
                    </div>
                    <div>
                        <p class="text-sm text-gray-400 mb-1">Confidence</p>
                        <div class="flex items-center gap-2">
                            <div class="flex-1 h-2 bg-gray-700 rounded-full">
                                <div
                                    class="h-full bg-green-500 rounded-full"
                                    :style="{ width: `${analysisResult.confidence * 100}%` }"
                                ></div>
                            </div>
                            <span class="text-white">{{ (analysisResult.confidence * 100).toFixed(0) }}%</span>
                        </div>
                    </div>
                </div>
                <div class="mt-4">
                    <p class="text-sm text-gray-400 mb-2">Description</p>
                    <p class="text-gray-300">{{ analysisResult.description }}</p>
                </div>
            </div>

            <!-- Detected Elements -->
            <div class="p-6 bg-gray-800 rounded-lg border border-gray-700">
                <h3 class="text-lg font-medium text-white mb-4 flex items-center gap-2">
                    <CursorArrowRaysIcon class="w-5 h-5 text-purple-400" />
                    Elements ที่ตรวจพบ ({{ analysisResult.elements?.length || 0 }})
                </h3>
                <div class="space-y-3 max-h-96 overflow-y-auto">
                    <div
                        v-for="(element, index) in analysisResult.elements || []"
                        :key="index"
                        class="p-4 bg-gray-900 rounded-lg border border-gray-700"
                    >
                        <div class="flex items-start justify-between">
                            <div class="flex items-center gap-3">
                                <div :class="['w-8 h-8 rounded flex items-center justify-center', elementTypeColor(element.type)]">
                                    <component :is="elementTypeIcon(element.type)" class="w-4 h-4 text-white" />
                                </div>
                                <div>
                                    <p class="font-medium text-white">{{ element.purpose || element.type }}</p>
                                    <p class="text-sm text-gray-400">{{ element.selector }}</p>
                                </div>
                            </div>
                            <span :class="['px-2 py-1 text-xs rounded', confidenceClass(element.confidence)]">
                                {{ (element.confidence * 100).toFixed(0) }}%
                            </span>
                        </div>
                        <div v-if="element.attributes" class="mt-2 flex flex-wrap gap-2">
                            <span
                                v-for="(value, key) in element.attributes"
                                :key="key"
                                class="px-2 py-1 text-xs bg-gray-800 text-gray-400 rounded"
                            >
                                {{ key }}: {{ value }}
                            </span>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Suggested Workflows -->
            <div class="p-6 bg-gray-800 rounded-lg border border-gray-700">
                <h3 class="text-lg font-medium text-white mb-4 flex items-center gap-2">
                    <LightBulbIcon class="w-5 h-5 text-yellow-400" />
                    Workflow ที่แนะนำ
                </h3>
                <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div
                        v-for="(workflow, index) in analysisResult.suggested_workflows || []"
                        :key="index"
                        class="p-4 bg-gray-900 rounded-lg border border-gray-700 hover:border-purple-500/50 transition-colors"
                    >
                        <div class="flex items-start justify-between mb-3">
                            <div>
                                <h4 class="font-medium text-white">{{ workflow.name }}</h4>
                                <p class="text-sm text-gray-400">{{ workflow.type }}</p>
                            </div>
                            <span :class="['px-2 py-1 text-xs rounded', confidenceClass(workflow.confidence)]">
                                {{ (workflow.confidence * 100).toFixed(0) }}%
                            </span>
                        </div>
                        <p class="text-sm text-gray-400 mb-4">{{ workflow.description }}</p>
                        <div class="flex items-center gap-2">
                            <span class="text-xs text-gray-500">{{ workflow.steps_count }} ขั้นตอน</span>
                            <span class="text-xs text-gray-500">~{{ workflow.estimated_duration }}s</span>
                        </div>
                        <div class="mt-4">
                            <button
                                @click="createWorkflow(workflow)"
                                class="w-full px-4 py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg transition-colors text-sm"
                            >
                                สร้าง Workflow นี้
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            <!-- Raw AI Response (Expandable) -->
            <details class="p-4 bg-gray-800 rounded-lg border border-gray-700">
                <summary class="text-sm text-gray-400 cursor-pointer hover:text-gray-300">
                    ดู AI Response แบบเต็ม
                </summary>
                <pre class="mt-4 p-4 bg-gray-900 rounded text-xs text-gray-300 overflow-x-auto">{{ JSON.stringify(analysisResult.raw_response, null, 2) }}</pre>
            </details>
        </div>

        <!-- Empty State -->
        <div v-if="!analysisResult && !analyzing" class="p-12 bg-gray-800 rounded-lg border border-gray-700 text-center">
            <SparklesIcon class="w-16 h-16 mx-auto text-gray-600 mb-4" />
            <h3 class="text-lg font-medium text-white mb-2">ให้ AI ช่วยวิเคราะห์</h3>
            <p class="text-gray-400 mb-6 max-w-md mx-auto">
                ใส่ URL ของหน้าเว็บที่ต้องการให้ AI วิเคราะห์ ระบบจะตรวจหา elements ที่สำคัญ
                และแนะนำ workflow ที่เหมาะสม
            </p>
        </div>
    </div>
</template>

<script setup>
import { ref } from 'vue'
import {
    SparklesIcon,
    CpuChipIcon,
    DocumentTextIcon,
    CursorArrowRaysIcon,
    LightBulbIcon,
    PencilSquareIcon,
    CursorArrowRippleIcon,
    PhotoIcon,
    LinkIcon,
    RectangleGroupIcon
} from '@heroicons/vue/24/outline'

const emit = defineEmits(['create-workflow'])

const pageUrl = ref('')
const platform = ref('')
const purpose = ref('')
const analyzing = ref(false)
const analysisStep = ref('')
const analysisProgress = ref(0)
const analysisResult = ref(null)

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

const analyzeUrl = async () => {
    analyzing.value = true
    analysisResult.value = null
    analysisProgress.value = 0

    const steps = [
        { step: 'กำลังโหลดหน้าเว็บ...', progress: 20 },
        { step: 'กำลังวิเคราะห์ DOM structure...', progress: 40 },
        { step: 'กำลังระบุ interactive elements...', progress: 60 },
        { step: 'กำลังให้ AI วิเคราะห์ patterns...', progress: 80 },
        { step: 'กำลังสร้างคำแนะนำ...', progress: 95 }
    ]

    for (const s of steps) {
        analysisStep.value = s.step
        analysisProgress.value = s.progress
        await new Promise(resolve => setTimeout(resolve, 800))
    }

    // Simulate AI analysis result
    analysisResult.value = {
        page_type: 'login_page',
        confidence: 0.94,
        description: 'This appears to be a login page for Facebook with email/phone and password fields, and a login button.',
        elements: [
            {
                type: 'input',
                purpose: 'Email/Phone Input',
                selector: 'input[name="email"]',
                confidence: 0.98,
                attributes: { type: 'text', placeholder: 'Email or phone' }
            },
            {
                type: 'input',
                purpose: 'Password Input',
                selector: 'input[name="pass"]',
                confidence: 0.97,
                attributes: { type: 'password', placeholder: 'Password' }
            },
            {
                type: 'button',
                purpose: 'Login Button',
                selector: 'button[data-testid="royal_login_button"]',
                confidence: 0.95,
                attributes: { type: 'submit', text: 'Log In' }
            },
            {
                type: 'link',
                purpose: 'Forgot Password',
                selector: 'a[href*="forgot"]',
                confidence: 0.88,
                attributes: { text: 'Forgot password?' }
            }
        ],
        suggested_workflows: [
            {
                name: 'Facebook Login',
                type: 'login',
                description: 'Login to Facebook with email/phone and password',
                confidence: 0.95,
                steps_count: 4,
                estimated_duration: 3,
                steps: [
                    { action: 'wait', params: { selector: 'input[name="email"]' } },
                    { action: 'type', params: { selector: 'input[name="email"]', source: 'credentials.email' } },
                    { action: 'type', params: { selector: 'input[name="pass"]', source: 'credentials.password' } },
                    { action: 'click', params: { selector: 'button[data-testid="royal_login_button"]' } }
                ]
            },
            {
                name: 'Facebook Login with Verification',
                type: 'login',
                description: 'Login with additional 2FA verification handling',
                confidence: 0.82,
                steps_count: 7,
                estimated_duration: 8,
                steps: []
            }
        ],
        raw_response: {
            model: 'gpt-4',
            tokens_used: 1542,
            analysis_time_ms: 3200
        }
    }

    analysisProgress.value = 100
    analysisStep.value = 'วิเคราะห์เสร็จสิ้น'
    analyzing.value = false
}

const createWorkflow = (workflow) => {
    emit('create-workflow', {
        name: workflow.name,
        platform: platform.value,
        workflow_type: workflow.type,
        source: 'ai_generated',
        steps: workflow.steps
    })
}

const elementTypeColor = (type) => {
    const colors = {
        input: 'bg-blue-600',
        button: 'bg-green-600',
        link: 'bg-purple-600',
        image: 'bg-yellow-600',
        form: 'bg-orange-600'
    }
    return colors[type] || 'bg-gray-600'
}

const elementTypeIcon = (type) => {
    const icons = {
        input: PencilSquareIcon,
        button: CursorArrowRippleIcon,
        link: LinkIcon,
        image: PhotoIcon,
        form: RectangleGroupIcon
    }
    return icons[type] || CursorArrowRaysIcon
}

const confidenceClass = (confidence) => {
    if (confidence >= 0.9) return 'bg-green-500/20 text-green-400'
    if (confidence >= 0.7) return 'bg-yellow-500/20 text-yellow-400'
    return 'bg-red-500/20 text-red-400'
}
</script>
