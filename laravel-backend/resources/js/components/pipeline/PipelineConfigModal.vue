<template>
    <Teleport to="body">
        <div class="modal-overlay" @click.self="$emit('close')">
            <div class="modal-content">
                <div class="modal-header">
                    <h2>{{ pipeline ? 'แก้ไข Pipeline' : 'สร้าง Pipeline ใหม่' }}</h2>
                    <button class="close-btn" @click="$emit('close')">&times;</button>
                </div>

                <form @submit.prevent="save" class="modal-body">
                    <!-- Step tabs -->
                    <div class="form-steps">
                        <button
                            v-for="(step, i) in formSteps"
                            :key="step.key"
                            type="button"
                            :class="['step-btn', { active: currentStep === i, completed: i < currentStep }]"
                            @click="currentStep = i"
                        >
                            <span class="step-num">{{ i + 1 }}</span>
                            {{ step.label }}
                        </button>
                    </div>

                    <!-- Step 1: Basic Info -->
                    <div v-show="currentStep === 0" class="form-section">
                        <h3 class="section-title">ข้อมูลพื้นฐาน</h3>

                        <div class="form-group">
                            <label>ชื่อ Pipeline *</label>
                            <input v-model="form.name" type="text" required placeholder="เช่น เรื่องผีไทย TikTok" />
                        </div>

                        <div class="form-group">
                            <label>รายละเอียด</label>
                            <textarea v-model="form.description" rows="2" placeholder="อธิบายสั้นๆ เกี่ยวกับ Pipeline นี้"></textarea>
                        </div>

                        <div class="form-row">
                            <div class="form-group">
                                <label>ประเภทเนื้อหา</label>
                                <select v-model="form.content_type">
                                    <option value="story">เล่าเรื่อง (Story)</option>
                                    <option value="tips">ทิปส์ / สาระ</option>
                                    <option value="news">ข่าว / เหตุการณ์</option>
                                    <option value="promotional">โปรโมท / ขาย</option>
                                    <option value="educational">ให้ความรู้</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>ภาษา</label>
                                <select v-model="form.language">
                                    <option value="th">ไทย</option>
                                    <option value="en">English</option>
                                </select>
                            </div>
                        </div>

                        <div class="form-row">
                            <div class="form-group">
                                <label>โทนเสียง</label>
                                <select v-model="form.tone">
                                    <option value="casual">สบายๆ (Casual)</option>
                                    <option value="professional">มืออาชีพ</option>
                                    <option value="dramatic">ดราม่า</option>
                                    <option value="humorous">ตลก</option>
                                    <option value="scary">น่ากลัว</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>จำนวนซีน</label>
                                <input v-model.number="form.scene_count" type="number" min="3" max="20" />
                            </div>
                        </div>

                        <div class="form-group">
                            <label>Prompt Template</label>
                            <textarea v-model="form.content_prompt_template" rows="3" placeholder="Template สำหรับสร้างเนื้อหา (ใช้ {content_type}, {language}, {tone} เป็น placeholder)"></textarea>
                        </div>
                    </div>

                    <!-- Step 2: Image & Video -->
                    <div v-show="currentStep === 1" class="form-section">
                        <h3 class="section-title">ภาพ & วิดีโอ</h3>

                        <div class="form-row">
                            <div class="form-group">
                                <label>สไตล์ภาพ</label>
                                <select v-model="form.image_style">
                                    <option value="realistic">เหมือนจริง</option>
                                    <option value="anime">อนิเมะ</option>
                                    <option value="cartoon">การ์ตูน</option>
                                    <option value="watercolor">สีน้ำ</option>
                                    <option value="oil_painting">สีน้ำมัน</option>
                                    <option value="digital_art">Digital Art</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>ผู้ให้บริการภาพ</label>
                                <select v-model="form.image_provider">
                                    <option value="diffusers">Diffusers (Local GPU)</option>
                                    <option value="huggingface">HuggingFace Inference</option>
                                </select>
                            </div>
                        </div>

                        <div class="form-row">
                            <div class="form-group">
                                <label>ขนาดภาพ (กว้าง)</label>
                                <input v-model.number="form.image_width" type="number" min="256" max="2048" step="64" />
                            </div>
                            <div class="form-group">
                                <label>ขนาดภาพ (สูง)</label>
                                <input v-model.number="form.image_height" type="number" min="256" max="2048" step="64" />
                            </div>
                        </div>

                        <hr class="section-divider" />

                        <div class="form-row">
                            <div class="form-group">
                                <label>ผู้ให้บริการวิดีโอ</label>
                                <select v-model="form.video_provider">
                                    <option value="auto">อัตโนมัติ (GPU > Freepik > Slideshow)</option>
                                    <option value="svd">SVD (Local GPU)</option>
                                    <option value="freepik">Freepik Pikaso (Browser)</option>
                                    <option value="slideshow">Slideshow (FFmpeg)</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>อัตราส่วน</label>
                                <select v-model="form.aspect_ratio">
                                    <option value="9:16">9:16 (TikTok/Shorts)</option>
                                    <option value="16:9">16:9 (YouTube)</option>
                                    <option value="1:1">1:1 (Instagram)</option>
                                </select>
                            </div>
                        </div>

                        <div class="form-row">
                            <div class="form-group">
                                <label>ความยาววิดีโอ (วินาที)</label>
                                <input v-model.number="form.video_duration_seconds" type="number" min="15" max="600" />
                            </div>
                            <div class="form-group">
                                <label>ความยาวคลิป/ซีน (วินาที)</label>
                                <input v-model.number="form.clip_duration_seconds" type="number" min="2" max="30" />
                            </div>
                        </div>
                    </div>

                    <!-- Step 3: Audio & Lip Sync -->
                    <div v-show="currentStep === 2" class="form-section">
                        <h3 class="section-title">เสียงบรรยาย & Lip Sync</h3>

                        <div class="form-row">
                            <div class="form-group">
                                <label>ผู้ให้บริการ TTS</label>
                                <select v-model="form.tts_provider">
                                    <option value="edge">Edge TTS (ฟรี)</option>
                                    <option value="google">Google Cloud TTS</option>
                                    <option value="both">ทั้งสอง (Edge หลัก, Google สำรอง)</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>เพศเสียง</label>
                                <select v-model="form.tts_voice_gender">
                                    <option value="female">หญิง</option>
                                    <option value="male">ชาย</option>
                                </select>
                            </div>
                        </div>

                        <div class="form-group">
                            <label>Voice ID</label>
                            <input v-model="form.tts_voice_id" type="text" :placeholder="form.tts_voice_gender === 'female' ? 'th-TH-PremwadeeNeural' : 'th-TH-NiwatNeural'" />
                            <small class="form-hint">ปล่อยว่างเพื่อใช้ค่าเริ่มต้นตามเพศที่เลือก</small>
                        </div>

                        <hr class="section-divider" />

                        <div class="form-group">
                            <label class="checkbox-label">
                                <input v-model="form.enable_lip_sync" type="checkbox" />
                                เปิดใช้ Lip Sync
                            </label>
                            <small class="form-hint">สร้างวิดีโอหน้าคนพูดตามเสียง (ต้องมี GPU ~4GB VRAM)</small>
                        </div>

                        <div v-if="form.enable_lip_sync" class="lip-sync-options">
                            <div class="form-row">
                                <div class="form-group">
                                    <label>ผู้ให้บริการ Lip Sync</label>
                                    <select v-model="form.lip_sync_provider">
                                        <option value="sadtalker">SadTalker (คุณภาพสูง)</option>
                                        <option value="wav2lip">Wav2Lip (เร็วกว่า)</option>
                                    </select>
                                </div>
                            </div>

                            <div class="form-group">
                                <label>เส้นทางภาพ Avatar</label>
                                <input v-model="form.lip_sync_avatar_path" type="text" placeholder="เช่น C:\Avatars\presenter.png" />
                                <small class="form-hint">ภาพหน้าคนสำหรับ lip sync (ถ้าไม่ระบุจะใช้ภาพจาก scene แรก)</small>
                            </div>
                        </div>

                        <hr class="section-divider" />

                        <div class="form-group">
                            <label class="checkbox-label">
                                <input v-model="form.enable_music" type="checkbox" />
                                เพิ่มเพลงประกอบ
                            </label>
                        </div>

                        <div v-if="form.enable_music" class="form-row">
                            <div class="form-group">
                                <label>สไตล์เพลง</label>
                                <input v-model="form.music_style" type="text" placeholder="เช่น ambient, dramatic, upbeat" />
                            </div>
                            <div class="form-group">
                                <label>ระดับเสียงเพลง (%)</label>
                                <input v-model.number="form.music_volume" type="number" min="5" max="100" />
                            </div>
                        </div>
                    </div>

                    <!-- Step 4: Publishing & Schedule -->
                    <div v-show="currentStep === 3" class="form-section">
                        <h3 class="section-title">การเผยแพร่ & ตั้งเวลา</h3>

                        <div class="form-group">
                            <label>แพลตฟอร์มเป้าหมาย</label>
                            <div class="platform-checkboxes">
                                <label v-for="p in platformOptions" :key="p.value" class="checkbox-label">
                                    <input type="checkbox" :value="p.value" v-model="form.target_platforms" />
                                    {{ p.label }}
                                </label>
                            </div>
                        </div>

                        <div class="form-group">
                            <label class="checkbox-label">
                                <input v-model="form.auto_publish" type="checkbox" />
                                โพสต์อัตโนมัติหลังสร้างเสร็จ
                            </label>
                        </div>

                        <hr class="section-divider" />

                        <div class="form-row">
                            <div class="form-group">
                                <label>ประเภทการตั้งเวลา</label>
                                <select v-model="form.schedule_type">
                                    <option value="manual">ด้วยตนเอง</option>
                                    <option value="interval">ทุก X ชั่วโมง</option>
                                    <option value="cron">Cron Expression</option>
                                </select>
                            </div>
                            <div class="form-group" v-if="form.schedule_type === 'interval'">
                                <label>ทุกกี่ชั่วโมง</label>
                                <input v-model.number="form.interval_hours" type="number" min="1" max="168" />
                            </div>
                            <div class="form-group" v-if="form.schedule_type === 'cron'">
                                <label>Cron Expression</label>
                                <input v-model="form.cron_expression" type="text" placeholder="0 */6 * * *" />
                            </div>
                        </div>

                        <div class="form-group">
                            <label>จำนวนรันสูงสุดต่อวัน</label>
                            <input v-model.number="form.max_runs_per_day" type="number" min="1" max="100" />
                        </div>

                        <hr class="section-divider" />

                        <h4 class="subsection-title">API Key Pools</h4>

                        <div class="form-row">
                            <div class="form-group">
                                <label>Text Generation Pool</label>
                                <select v-model="form.text_pool_id">
                                    <option :value="null">-- ไม่ระบุ --</option>
                                    <option v-for="pool in textPools" :key="pool.id" :value="pool.id">{{ pool.name }}</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>TTS Pool</label>
                                <select v-model="form.tts_pool_id">
                                    <option :value="null">-- ไม่ระบุ --</option>
                                    <option v-for="pool in ttsPools" :key="pool.id" :value="pool.id">{{ pool.name }}</option>
                                </select>
                            </div>
                        </div>
                    </div>

                    <!-- Actions -->
                    <div class="modal-actions">
                        <div class="step-nav">
                            <button type="button" class="btn btn-secondary" v-if="currentStep > 0" @click="currentStep--">
                                ย้อนกลับ
                            </button>
                        </div>
                        <div class="step-nav-right">
                            <button type="button" class="btn btn-secondary" @click="$emit('close')">ยกเลิก</button>
                            <button type="button" class="btn btn-primary" v-if="currentStep < formSteps.length - 1" @click="currentStep++">
                                ถัดไป
                            </button>
                            <button type="submit" class="btn btn-primary" v-else :disabled="saving">
                                {{ saving ? 'กำลังบันทึก...' : 'บันทึก' }}
                            </button>
                        </div>
                    </div>
                </form>
            </div>
        </div>
    </Teleport>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useToast } from '@/composables/useToast'
import { pipelineApi } from '@/services/api'

const props = defineProps({
    pipeline: { type: Object, default: null },
    apiKeyPools: { type: Array, default: () => [] },
})

const emit = defineEmits(['close', 'saved'])
const toast = useToast()

const currentStep = ref(0)
const saving = ref(false)

const formSteps = [
    { key: 'basic', label: 'พื้นฐาน' },
    { key: 'media', label: 'ภาพ & วิดีโอ' },
    { key: 'audio', label: 'เสียง' },
    { key: 'publish', label: 'เผยแพร่' },
]

const platformOptions = [
    { value: 'tiktok', label: 'TikTok' },
    { value: 'youtube', label: 'YouTube' },
    { value: 'facebook', label: 'Facebook' },
    { value: 'instagram', label: 'Instagram' },
]

const form = reactive({
    name: '',
    description: '',
    content_type: 'story',
    language: 'th',
    tone: 'casual',
    content_prompt_template: '',
    scene_count: 5,

    image_style: 'realistic',
    image_width: 1024,
    image_height: 1024,
    image_provider: 'diffusers',

    video_provider: 'auto',
    video_duration_seconds: 60,
    aspect_ratio: '9:16',
    clip_duration_seconds: 5,

    tts_provider: 'both',
    tts_voice_id: '',
    tts_voice_gender: 'female',

    enable_lip_sync: true,
    lip_sync_provider: 'sadtalker',
    lip_sync_avatar_path: '',

    enable_music: true,
    music_style: 'ambient',
    music_volume: 20,
    music_source: 'royalty_free',

    target_platforms: ['tiktok'],
    auto_publish: false,

    schedule_type: 'manual',
    interval_hours: 6,
    cron_expression: '',
    max_runs_per_day: 10,

    text_pool_id: null,
    tts_pool_id: null,
})

const textPools = computed(() => props.apiKeyPools.filter(p => p.service_type === 'text'))
const ttsPools = computed(() => props.apiKeyPools.filter(p => p.service_type === 'tts'))

// Populate form if editing
if (props.pipeline) {
    Object.keys(form).forEach(key => {
        if (props.pipeline[key] !== undefined) {
            form[key] = props.pipeline[key]
        }
    })
}

const save = async () => {
    if (!form.name.trim()) {
        toast.error('กรุณาระบุชื่อ Pipeline')
        currentStep.value = 0
        return
    }

    saving.value = true
    try {
        if (props.pipeline?.id) {
            await pipelineApi.update(props.pipeline.id, { ...form })
        } else {
            await pipelineApi.create({ ...form })
        }
        emit('saved')
    } catch (error) {
        const msg = error.response?.data?.message || error.response?.data?.errors
        toast.error(typeof msg === 'string' ? msg : 'ไม่สามารถบันทึกได้')
    } finally {
        saving.value = false
    }
}
</script>

<style scoped>
.modal-overlay {
    position: fixed;
    inset: 0;
    z-index: 100;
    background: rgba(0, 0, 0, 0.7);
    backdrop-filter: blur(4px);
    display: flex;
    align-items: center;
    justify-content: center;
    padding: 16px;
}

.modal-content {
    background: #1f2937;
    border-radius: 16px;
    border: 1px solid #374151;
    width: 100%;
    max-width: 720px;
    max-height: 90vh;
    display: flex;
    flex-direction: column;
}

.modal-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 16px 20px;
    border-bottom: 1px solid #374151;
}

.modal-header h2 {
    font-size: 1.1rem;
    font-weight: 600;
    color: #f3f4f6;
}

.close-btn {
    background: none;
    border: none;
    font-size: 1.5rem;
    color: #9ca3af;
    cursor: pointer;
    line-height: 1;
}

.close-btn:hover {
    color: #f3f4f6;
}

.modal-body {
    overflow-y: auto;
    flex: 1;
    padding: 0;
}

/* Form Steps */
.form-steps {
    display: flex;
    padding: 12px 20px;
    gap: 4px;
    border-bottom: 1px solid #374151;
}

.step-btn {
    display: flex;
    align-items: center;
    gap: 6px;
    padding: 6px 12px;
    font-size: 0.8rem;
    color: #6b7280;
    background: none;
    border: 1px solid #374151;
    border-radius: 6px;
    cursor: pointer;
    transition: all 0.2s;
}

.step-btn.active {
    background: #4f46e5;
    border-color: #4f46e5;
    color: white;
}

.step-btn.completed {
    border-color: #22c55e;
    color: #22c55e;
}

.step-num {
    width: 20px;
    height: 20px;
    border-radius: 50%;
    background: #374151;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 0.7rem;
    font-weight: 600;
}

.step-btn.active .step-num {
    background: rgba(255, 255, 255, 0.2);
}

.step-btn.completed .step-num {
    background: #22c55e;
    color: white;
}

/* Form */
.form-section {
    padding: 20px;
}

.section-title {
    font-size: 0.95rem;
    font-weight: 600;
    color: #e5e7eb;
    margin-bottom: 16px;
}

.subsection-title {
    font-size: 0.85rem;
    font-weight: 600;
    color: #d1d5db;
    margin-bottom: 12px;
}

.section-divider {
    border: none;
    border-top: 1px solid #374151;
    margin: 16px 0;
}

.form-group {
    margin-bottom: 14px;
}

.form-group label {
    display: block;
    font-size: 0.8rem;
    font-weight: 500;
    color: #d1d5db;
    margin-bottom: 4px;
}

.form-group input[type="text"],
.form-group input[type="number"],
.form-group select,
.form-group textarea {
    width: 100%;
    padding: 8px 12px;
    background: #111827;
    border: 1px solid #374151;
    border-radius: 6px;
    color: #f3f4f6;
    font-size: 0.85rem;
}

.form-group input:focus,
.form-group select:focus,
.form-group textarea:focus {
    outline: none;
    border-color: #4f46e5;
}

.form-hint {
    display: block;
    font-size: 0.7rem;
    color: #6b7280;
    margin-top: 2px;
}

.form-row {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: 12px;
}

.checkbox-label {
    display: flex;
    align-items: center;
    gap: 8px;
    font-size: 0.85rem;
    color: #d1d5db;
    cursor: pointer;
}

.checkbox-label input[type="checkbox"] {
    accent-color: #4f46e5;
}

.platform-checkboxes {
    display: flex;
    flex-wrap: wrap;
    gap: 12px;
    margin-top: 4px;
}

.lip-sync-options {
    margin-top: 12px;
    padding: 12px;
    background: #111827;
    border-radius: 8px;
    border: 1px solid #374151;
}

/* Actions */
.modal-actions {
    display: flex;
    justify-content: space-between;
    padding: 12px 20px;
    border-top: 1px solid #374151;
}

.step-nav-right {
    display: flex;
    gap: 8px;
}

.btn {
    display: inline-flex;
    align-items: center;
    gap: 6px;
    padding: 8px 16px;
    font-size: 0.85rem;
    font-weight: 500;
    border-radius: 8px;
    border: none;
    cursor: pointer;
    transition: all 0.2s;
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
</style>
