<template>
    <Modal v-model="isOpen" :title="isEdit ? 'แก้ไขโพสต์' : 'สร้างโพสต์ใหม่'" size="xl">
        <div class="space-y-6">
            <!-- Brand & Platform Selection -->
            <div class="grid grid-cols-2 gap-4">
                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-2">แบรนด์</label>
                    <select
                        v-model="form.brand_id"
                        class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:border-indigo-500"
                    >
                        <option value="">เลือกแบรนด์</option>
                        <option v-for="brand in brands" :key="brand.id" :value="brand.id">
                            {{ brand.name }}
                        </option>
                    </select>
                </div>
                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-2">แคมเปญ (ถ้ามี)</label>
                    <select
                        v-model="form.campaign_id"
                        class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:border-indigo-500"
                    >
                        <option value="">ไม่มี</option>
                        <option v-for="campaign in campaigns" :key="campaign.id" :value="campaign.id">
                            {{ campaign.name }}
                        </option>
                    </select>
                </div>
            </div>

            <!-- Platform Selection -->
            <div>
                <label class="block text-sm font-medium text-gray-300 mb-2">แพลตฟอร์ม</label>
                <div class="flex flex-wrap gap-2">
                    <button
                        v-for="platform in platforms"
                        :key="platform.value"
                        :class="[
                            'flex items-center gap-2 px-3 py-2 rounded-lg border transition-colors',
                            form.platforms.includes(platform.value)
                                ? 'border-indigo-500 bg-indigo-500/20 text-indigo-400'
                                : 'border-gray-600 bg-gray-700 text-gray-400 hover:border-gray-500'
                        ]"
                        @click="togglePlatform(platform.value)"
                    >
                        <component :is="platform.icon" class="w-5 h-5" />
                        {{ platform.label }}
                    </button>
                </div>
            </div>

            <!-- Content -->
            <div>
                <div class="flex items-center justify-between mb-2">
                    <label class="block text-sm font-medium text-gray-300">เนื้อหา</label>
                    <button
                        class="text-sm text-indigo-400 hover:text-indigo-300 flex items-center gap-1"
                        :disabled="generatingContent"
                        @click="generateContent"
                    >
                        <SparklesIcon class="w-4 h-4" />
                        สร้างด้วย AI
                    </button>
                </div>
                <textarea
                    v-model="form.content"
                    rows="6"
                    class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white placeholder-gray-400 focus:outline-none focus:border-indigo-500 resize-none"
                    placeholder="เขียนเนื้อหาโพสต์ของคุณ..."
                ></textarea>
                <div class="flex items-center justify-between mt-1 text-sm text-gray-500">
                    <span>{{ form.content.length }} ตัวอักษร</span>
                    <span v-if="form.platforms.includes('twitter')">280 สูงสุดสำหรับ Twitter</span>
                </div>
            </div>

            <!-- Media Upload -->
            <div>
                <label class="block text-sm font-medium text-gray-300 mb-2">รูปภาพ/วิดีโอ</label>
                <div class="flex flex-wrap gap-3">
                    <div
                        v-for="(media, index) in form.media"
                        :key="index"
                        class="relative w-24 h-24 rounded-lg overflow-hidden border border-gray-600 group"
                    >
                        <img :src="media.preview" class="w-full h-full object-cover" />
                        <button
                            class="absolute inset-0 bg-black/60 opacity-0 group-hover:opacity-100 flex items-center justify-center transition-opacity"
                            @click="removeMedia(index)"
                        >
                            <TrashIcon class="w-5 h-5 text-red-400" />
                        </button>
                    </div>
                    <label class="w-24 h-24 rounded-lg border-2 border-dashed border-gray-600 flex flex-col items-center justify-center cursor-pointer hover:border-indigo-500 transition-colors">
                        <PhotoIcon class="w-6 h-6 text-gray-400" />
                        <span class="text-xs text-gray-400 mt-1">เพิ่มรูป</span>
                        <input type="file" class="hidden" accept="image/*,video/*" multiple @change="handleFileUpload" />
                    </label>
                </div>
            </div>

            <!-- Scheduling -->
            <div class="border-t border-gray-700 pt-6">
                <div class="flex items-center gap-4 mb-4">
                    <label class="flex items-center gap-2 cursor-pointer">
                        <input
                            type="radio"
                            v-model="scheduleType"
                            value="now"
                            class="text-indigo-600 bg-gray-700 border-gray-600 focus:ring-indigo-500"
                        />
                        <span class="text-gray-300">โพสต์ทันที</span>
                    </label>
                    <label class="flex items-center gap-2 cursor-pointer">
                        <input
                            type="radio"
                            v-model="scheduleType"
                            value="scheduled"
                            class="text-indigo-600 bg-gray-700 border-gray-600 focus:ring-indigo-500"
                        />
                        <span class="text-gray-300">ตั้งเวลา</span>
                    </label>
                    <label class="flex items-center gap-2 cursor-pointer">
                        <input
                            type="radio"
                            v-model="scheduleType"
                            value="draft"
                            class="text-indigo-600 bg-gray-700 border-gray-600 focus:ring-indigo-500"
                        />
                        <span class="text-gray-300">บันทึกแบบร่าง</span>
                    </label>
                </div>

                <div v-if="scheduleType === 'scheduled'" class="grid grid-cols-2 gap-4">
                    <div>
                        <label class="block text-sm font-medium text-gray-300 mb-2">วันที่</label>
                        <input
                            type="date"
                            v-model="form.scheduled_date"
                            class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:border-indigo-500"
                        />
                    </div>
                    <div>
                        <label class="block text-sm font-medium text-gray-300 mb-2">เวลา</label>
                        <input
                            type="time"
                            v-model="form.scheduled_time"
                            class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:border-indigo-500"
                        />
                    </div>
                </div>
            </div>
        </div>

        <template #footer>
            <button
                class="px-4 py-2 text-gray-300 hover:text-white hover:bg-gray-700 rounded-lg transition-colors"
                @click="close"
            >
                ยกเลิก
            </button>
            <button
                class="px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg transition-colors disabled:opacity-50"
                :disabled="!isValid || saving"
                @click="save"
            >
                <ArrowPathIcon v-if="saving" class="w-4 h-4 animate-spin inline mr-2" />
                {{ scheduleType === 'now' ? 'โพสต์เลย' : scheduleType === 'scheduled' ? 'ตั้งเวลาโพสต์' : 'บันทึกแบบร่าง' }}
            </button>
        </template>
    </Modal>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import Modal from './Modal.vue'
import { brandApi, campaignApi, postApi } from '../../services/api'
import {
    SparklesIcon,
    PhotoIcon,
    TrashIcon,
    ArrowPathIcon
} from '@heroicons/vue/24/outline'

const props = defineProps({
    modelValue: {
        type: Boolean,
        default: false
    },
    post: {
        type: Object,
        default: null
    }
})

const emit = defineEmits(['update:modelValue', 'saved'])

const isOpen = computed({
    get: () => props.modelValue,
    set: (value) => emit('update:modelValue', value)
})

const isEdit = computed(() => !!props.post?.id)

const form = ref({
    brand_id: '',
    campaign_id: '',
    platforms: [],
    content: '',
    media: [],
    scheduled_date: '',
    scheduled_time: ''
})

const scheduleType = ref('now')
const saving = ref(false)
const generatingContent = ref(false)
const brands = ref([])
const campaigns = ref([])

const platforms = [
    { value: 'facebook', label: 'Facebook', icon: 'div' },
    { value: 'instagram', label: 'Instagram', icon: 'div' },
    { value: 'tiktok', label: 'TikTok', icon: 'div' },
    { value: 'twitter', label: 'Twitter/X', icon: 'div' },
    { value: 'line', label: 'LINE', icon: 'div' },
    { value: 'threads', label: 'Threads', icon: 'div' }
]

const isValid = computed(() => {
    if (!form.value.brand_id) return false
    if (form.value.platforms.length === 0) return false
    if (!form.value.content.trim()) return false
    if (scheduleType.value === 'scheduled' && (!form.value.scheduled_date || !form.value.scheduled_time)) return false
    return true
})

const togglePlatform = (platform) => {
    const index = form.value.platforms.indexOf(platform)
    if (index > -1) {
        form.value.platforms.splice(index, 1)
    } else {
        form.value.platforms.push(platform)
    }
}

const handleFileUpload = (event) => {
    const files = event.target.files
    for (const file of files) {
        const reader = new FileReader()
        reader.onload = (e) => {
            form.value.media.push({
                file,
                preview: e.target.result
            })
        }
        reader.readAsDataURL(file)
    }
}

const removeMedia = (index) => {
    form.value.media.splice(index, 1)
}

const generateContent = async () => {
    if (!form.value.brand_id) return

    generatingContent.value = true
    try {
        // AI content generation would go here
        // This is a placeholder for actual AI integration
        await new Promise(resolve => setTimeout(resolve, 1500))
        form.value.content = 'เนื้อหาที่สร้างโดย AI จะปรากฏที่นี่ 🎉 #PostXAgent #AIContent'
    } finally {
        generatingContent.value = false
    }
}

const save = async () => {
    if (!isValid.value) return

    saving.value = true
    try {
        const data = {
            brand_id: form.value.brand_id,
            campaign_id: form.value.campaign_id || null,
            platforms: form.value.platforms,
            content: form.value.content,
            status: scheduleType.value === 'draft' ? 'draft' : scheduleType.value === 'scheduled' ? 'scheduled' : 'pending'
        }

        if (scheduleType.value === 'scheduled') {
            data.scheduled_at = `${form.value.scheduled_date}T${form.value.scheduled_time}:00`
        }

        if (isEdit.value) {
            await postApi.update(props.post.id, data)
        } else {
            await postApi.create(data)
        }

        emit('saved')
        close()
    } catch (error) {
        console.error('Failed to save post:', error)
    } finally {
        saving.value = false
    }
}

const close = () => {
    emit('update:modelValue', false)
}

const loadData = async () => {
    try {
        const [brandsRes, campaignsRes] = await Promise.all([
            brandApi.list(),
            campaignApi.list()
        ])
        brands.value = brandsRes.data.data || []
        campaigns.value = campaignsRes.data.data || []
    } catch (error) {
        console.error('Failed to load data:', error)
    }
}

// Reset form when modal opens
watch(isOpen, (open) => {
    if (open) {
        loadData()
        if (props.post) {
            form.value = {
                brand_id: props.post.brand_id,
                campaign_id: props.post.campaign_id || '',
                platforms: props.post.platforms || [],
                content: props.post.content || '',
                media: [],
                scheduled_date: props.post.scheduled_at?.split('T')[0] || '',
                scheduled_time: props.post.scheduled_at?.split('T')[1]?.substring(0, 5) || ''
            }
            scheduleType.value = props.post.status === 'draft' ? 'draft' : props.post.scheduled_at ? 'scheduled' : 'now'
        } else {
            form.value = {
                brand_id: '',
                campaign_id: '',
                platforms: [],
                content: '',
                media: [],
                scheduled_date: '',
                scheduled_time: ''
            }
            scheduleType.value = 'now'
        }
    }
})
</script>
