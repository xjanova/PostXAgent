<template>
    <div class="space-y-6">
        <h1 class="text-2xl font-bold text-white">AI Tools</h1>

        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- Content Generator -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <h2 class="text-lg font-semibold text-white mb-4 flex items-center">
                    <SparklesIcon class="w-5 h-5 mr-2 text-purple-400" />
                    สร้างเนื้อหา AI
                </h2>
                <form @submit.prevent="generateContent" class="space-y-4">
                    <div>
                        <label class="block text-sm text-gray-400 mb-1">หัวข้อ/คีย์เวิร์ด</label>
                        <input v-model="contentForm.topic" type="text" placeholder="เช่น โปรโมชั่นสินค้าใหม่"
                            class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white" />
                    </div>
                    <div>
                        <label class="block text-sm text-gray-400 mb-1">โทนเสียง</label>
                        <select v-model="contentForm.tone" class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white">
                            <option value="professional">มืออาชีพ</option>
                            <option value="casual">ลำลอง</option>
                            <option value="friendly">เป็นมิตร</option>
                            <option value="humorous">ตลก</option>
                        </select>
                    </div>
                    <div>
                        <label class="block text-sm text-gray-400 mb-1">แพลตฟอร์ม</label>
                        <select v-model="contentForm.platform" class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white">
                            <option value="facebook">Facebook</option>
                            <option value="instagram">Instagram</option>
                            <option value="tiktok">TikTok</option>
                            <option value="twitter">Twitter/X</option>
                        </select>
                    </div>
                    <button type="submit" :disabled="generating"
                        class="w-full py-2 bg-purple-600 hover:bg-purple-700 text-white rounded-lg transition-colors disabled:opacity-50">
                        {{ generating ? 'กำลังสร้าง...' : 'สร้างเนื้อหา' }}
                    </button>
                </form>

                <div v-if="generatedContent" class="mt-4 p-4 bg-gray-700/50 rounded-lg">
                    <p class="text-white whitespace-pre-line">{{ generatedContent }}</p>
                    <div class="flex justify-end mt-3 space-x-2">
                        <button class="px-3 py-1 text-gray-400 hover:text-white text-sm">คัดลอก</button>
                        <button class="px-3 py-1 bg-indigo-600 hover:bg-indigo-700 text-white text-sm rounded-lg">ใช้เนื้อหานี้</button>
                    </div>
                </div>
            </div>

            <!-- Image Generator -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <h2 class="text-lg font-semibold text-white mb-4 flex items-center">
                    <PhotoIcon class="w-5 h-5 mr-2 text-blue-400" />
                    สร้างรูปภาพ AI
                </h2>
                <form @submit.prevent="generateImage" class="space-y-4">
                    <div>
                        <label class="block text-sm text-gray-400 mb-1">คำอธิบายรูปภาพ (Prompt)</label>
                        <textarea v-model="imageForm.prompt" rows="3" placeholder="เช่น รูปสินค้าบนพื้นหลังสีพาสเทล, สไตล์มินิมอล"
                            class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white"></textarea>
                    </div>
                    <div class="grid grid-cols-2 gap-4">
                        <div>
                            <label class="block text-sm text-gray-400 mb-1">สไตล์</label>
                            <select v-model="imageForm.style" class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white">
                                <option value="realistic">สมจริง</option>
                                <option value="cartoon">การ์ตูน</option>
                                <option value="anime">อนิเมะ</option>
                                <option value="minimal">มินิมอล</option>
                            </select>
                        </div>
                        <div>
                            <label class="block text-sm text-gray-400 mb-1">ขนาด</label>
                            <select v-model="imageForm.size" class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white">
                                <option value="square">1:1 (Square)</option>
                                <option value="portrait">9:16 (Story)</option>
                                <option value="landscape">16:9 (Wide)</option>
                            </select>
                        </div>
                    </div>
                    <button type="submit" :disabled="generatingImage"
                        class="w-full py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors disabled:opacity-50">
                        {{ generatingImage ? 'กำลังสร้าง...' : 'สร้างรูปภาพ' }}
                    </button>
                </form>

                <div v-if="generatedImage" class="mt-4">
                    <img :src="generatedImage" alt="Generated" class="w-full rounded-lg" />
                    <div class="flex justify-end mt-3 space-x-2">
                        <button class="px-3 py-1 text-gray-400 hover:text-white text-sm">ดาวน์โหลด</button>
                        <button class="px-3 py-1 bg-indigo-600 hover:bg-indigo-700 text-white text-sm rounded-lg">ใช้รูปนี้</button>
                    </div>
                </div>
            </div>
        </div>

        <!-- Usage Stats -->
        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-4">การใช้งาน AI</h2>
            <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
                <div class="text-center">
                    <p class="text-3xl font-bold text-white">{{ usage.contentGenerated }}</p>
                    <p class="text-gray-400 text-sm">เนื้อหาที่สร้าง</p>
                </div>
                <div class="text-center">
                    <p class="text-3xl font-bold text-white">{{ usage.imagesGenerated }}</p>
                    <p class="text-gray-400 text-sm">รูปภาพที่สร้าง</p>
                </div>
                <div class="text-center">
                    <p class="text-3xl font-bold text-white">{{ usage.remaining }}</p>
                    <p class="text-gray-400 text-sm">เหลือใช้งาน</p>
                </div>
                <div class="text-center">
                    <p class="text-3xl font-bold text-indigo-400">{{ usage.limit === -1 ? 'Unlimited' : usage.limit }}</p>
                    <p class="text-gray-400 text-sm">โควต้าทั้งหมด</p>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useAuthStore } from '../../stores/auth'
import { SparklesIcon, PhotoIcon } from '@heroicons/vue/24/outline'

const authStore = useAuthStore()

const contentForm = reactive({ topic: '', tone: 'professional', platform: 'facebook' })
const imageForm = reactive({ prompt: '', style: 'realistic', size: 'square' })
const generating = ref(false)
const generatingImage = ref(false)
const generatedContent = ref('')
const generatedImage = ref('')
const usage = reactive({
    contentGenerated: 24,
    imagesGenerated: 12,
    remaining: authStore.usageRemaining?.ai_generations || 0,
    limit: authStore.usageLimits?.ai_generations || 0,
})

const generateContent = async () => {
    generating.value = true
    await new Promise(r => setTimeout(r, 2000))
    generatedContent.value = `🎉 ${contentForm.topic}\n\nเนื้อหาตัวอย่างที่สร้างโดย AI สำหรับ ${contentForm.platform}\nโทนเสียง: ${contentForm.tone}\n\n#PostXAgent #AI #Marketing`
    generating.value = false
}

const generateImage = async () => {
    generatingImage.value = true
    await new Promise(r => setTimeout(r, 3000))
    generatedImage.value = 'https://picsum.photos/512/512?random=' + Date.now()
    generatingImage.value = false
}
</script>
