<template>
    <div class="api-key-manager">
        <div class="manager-header">
            <h3 class="manager-title">API Key Pools</h3>
            <button class="btn btn-primary" @click="showCreatePool = true">
                <PlusIcon class="w-4 h-4" />
                สร้าง Pool ใหม่
            </button>
        </div>

        <div v-if="loading" class="loading-state">
            <div class="spinner"></div>
            <p>กำลังโหลด...</p>
        </div>

        <div v-else-if="pools.length === 0" class="empty-state">
            <KeyIcon class="w-12 h-12 text-gray-500" />
            <p class="text-gray-400 mt-2">ยังไม่มี API Key Pool</p>
            <p class="text-gray-500 text-sm">สร้าง pool เพื่อจัดการ API keys สำหรับ Groq, Gemini, Mistral และอื่นๆ</p>
        </div>

        <div v-else class="pools-grid">
            <div v-for="pool in pools" :key="pool.id" class="pool-card">
                <div class="pool-header">
                    <div>
                        <h4 class="pool-name">{{ pool.name }}</h4>
                        <div class="pool-tags">
                            <span class="pool-tag">{{ pool.provider }}</span>
                            <span class="pool-tag">{{ pool.service_type }}</span>
                            <span class="pool-tag">{{ pool.rotation_strategy }}</span>
                        </div>
                    </div>
                    <div :class="['pool-status', pool.is_active ? 'active' : 'inactive']">
                        {{ pool.is_active ? 'Active' : 'Inactive' }}
                    </div>
                </div>

                <!-- Keys List -->
                <div class="keys-section">
                    <div class="keys-header">
                        <span class="keys-count">{{ pool.members?.length || 0 }} keys</span>
                        <button class="btn btn-sm btn-secondary" @click="openAddKey(pool)">
                            <PlusIcon class="w-3.5 h-3.5" />
                            เพิ่ม Key
                        </button>
                    </div>

                    <div v-if="pool.members && pool.members.length > 0" class="keys-list">
                        <div v-for="member in pool.members" :key="member.id" class="key-item">
                            <div class="key-info">
                                <span class="key-label">{{ member.label || 'Key #' + member.id }}</span>
                                <span :class="['key-status', `key-${member.status}`]">{{ member.status }}</span>
                            </div>

                            <div class="key-usage">
                                <div class="usage-bar">
                                    <div
                                        class="usage-fill"
                                        :style="{ width: getUsagePercent(member) + '%' }"
                                        :class="{ 'usage-high': getUsagePercent(member) > 80 }"
                                    ></div>
                                </div>
                                <span class="usage-text">
                                    {{ member.requests_today }}/{{ member.daily_limit || '&infin;' }}
                                </span>
                            </div>

                            <div class="key-stats">
                                <span title="Success rate">{{ getSuccessRate(member) }}%</span>
                                <span title="Total requests">{{ member.total_requests }} total</span>
                            </div>

                            <button
                                class="btn-icon-only btn-danger-subtle"
                                @click="removeKey(pool, member)"
                                title="ลบ key"
                            >
                                <TrashIcon class="w-3.5 h-3.5" />
                            </button>
                        </div>
                    </div>
                    <p v-else class="no-keys">ยังไม่มี keys ใน pool นี้</p>
                </div>

                <!-- Pool Stats -->
                <div class="pool-footer">
                    <span>Cooldown: {{ pool.cooldown_seconds }}s</span>
                    <span>Max/day: {{ pool.max_requests_per_day || 'Unlimited' }}</span>
                    <span v-if="pool.auto_failover" class="failover-badge">Auto Failover</span>
                </div>
            </div>
        </div>

        <!-- Create Pool Modal -->
        <Teleport to="body">
            <div v-if="showCreatePool" class="modal-overlay" @click.self="showCreatePool = false">
                <div class="modal-content modal-sm">
                    <div class="modal-header">
                        <h2>สร้าง API Key Pool</h2>
                        <button class="close-btn" @click="showCreatePool = false">&times;</button>
                    </div>
                    <form @submit.prevent="createPool" class="modal-body-form">
                        <div class="form-group">
                            <label>ชื่อ Pool *</label>
                            <input v-model="newPool.name" type="text" required placeholder="เช่น Groq Free Keys" />
                        </div>
                        <div class="form-row">
                            <div class="form-group">
                                <label>Provider *</label>
                                <select v-model="newPool.provider" required>
                                    <option value="groq">Groq</option>
                                    <option value="gemini">Gemini</option>
                                    <option value="mistral">Mistral</option>
                                    <option value="google_tts">Google TTS</option>
                                    <option value="edge_tts">Edge TTS</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>Service Type *</label>
                                <select v-model="newPool.service_type" required>
                                    <option value="text">Text Generation</option>
                                    <option value="tts">TTS</option>
                                    <option value="image">Image</option>
                                    <option value="video">Video</option>
                                </select>
                            </div>
                        </div>
                        <div class="form-row">
                            <div class="form-group">
                                <label>Rotation Strategy</label>
                                <select v-model="newPool.rotation_strategy">
                                    <option value="round_robin">Round Robin</option>
                                    <option value="random">Random</option>
                                    <option value="least_used">Least Used</option>
                                    <option value="priority">Priority</option>
                                </select>
                            </div>
                            <div class="form-group">
                                <label>Cooldown (วินาที)</label>
                                <input v-model.number="newPool.cooldown_seconds" type="number" min="0" />
                            </div>
                        </div>
                        <div class="form-group">
                            <label class="checkbox-label">
                                <input v-model="newPool.auto_failover" type="checkbox" />
                                Auto Failover (สลับ key อัตโนมัติเมื่อ error)
                            </label>
                        </div>
                        <div class="modal-actions">
                            <button type="button" class="btn btn-secondary" @click="showCreatePool = false">ยกเลิก</button>
                            <button type="submit" class="btn btn-primary" :disabled="creatingPool">
                                {{ creatingPool ? 'กำลังสร้าง...' : 'สร้าง Pool' }}
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        </Teleport>

        <!-- Add Key Modal -->
        <Teleport to="body">
            <div v-if="addKeyPool" class="modal-overlay" @click.self="addKeyPool = null">
                <div class="modal-content modal-sm">
                    <div class="modal-header">
                        <h2>เพิ่ม API Key ใน "{{ addKeyPool.name }}"</h2>
                        <button class="close-btn" @click="addKeyPool = null">&times;</button>
                    </div>
                    <form @submit.prevent="addKey" class="modal-body-form">
                        <div class="form-group">
                            <label>ป้ายกำกับ</label>
                            <input v-model="newKey.label" type="text" placeholder="เช่น Groq Key #1" />
                        </div>
                        <div class="form-group">
                            <label>API Key *</label>
                            <input v-model="newKey.api_key" type="password" required placeholder="กรอก API key" />
                        </div>
                        <div class="form-group">
                            <label>Base URL (ถ้ามี)</label>
                            <input v-model="newKey.base_url" type="text" placeholder="เช่น https://api.groq.com/openai/v1" />
                        </div>
                        <div class="form-group">
                            <label>Model ID (ถ้ามี)</label>
                            <input v-model="newKey.model_id" type="text" placeholder="เช่น llama-3.3-70b-versatile" />
                        </div>
                        <div class="form-row">
                            <div class="form-group">
                                <label>Daily Limit</label>
                                <input v-model.number="newKey.daily_limit" type="number" min="0" placeholder="0 = ไม่จำกัด" />
                            </div>
                            <div class="form-group">
                                <label>Priority</label>
                                <input v-model.number="newKey.priority" type="number" min="0" max="100" />
                            </div>
                        </div>
                        <div class="modal-actions">
                            <button type="button" class="btn btn-secondary" @click="addKeyPool = null">ยกเลิก</button>
                            <button type="submit" class="btn btn-primary" :disabled="addingKey">
                                {{ addingKey ? 'กำลังเพิ่ม...' : 'เพิ่ม Key' }}
                            </button>
                        </div>
                    </form>
                </div>
            </div>
        </Teleport>
    </div>
</template>

<script setup>
import { ref, reactive } from 'vue'
import { useToast } from '@/composables/useToast'
import { pipelineApi } from '@/services/api'
import {
    PlusIcon,
    KeyIcon,
    TrashIcon,
} from '@heroicons/vue/24/outline'

const props = defineProps({
    pools: { type: Array, default: () => [] },
    loading: { type: Boolean, default: false },
})

const emit = defineEmits(['refresh'])
const toast = useToast()

// Create Pool
const showCreatePool = ref(false)
const creatingPool = ref(false)
const newPool = reactive({
    name: '',
    provider: 'groq',
    service_type: 'text',
    rotation_strategy: 'round_robin',
    cooldown_seconds: 60,
    auto_failover: true,
})

const createPool = async () => {
    creatingPool.value = true
    try {
        await pipelineApi.createApiKeyPool({ ...newPool })
        toast.success('สร้าง Pool สำเร็จ')
        showCreatePool.value = false
        // Reset form
        newPool.name = ''
        newPool.provider = 'groq'
        newPool.service_type = 'text'
        emit('refresh')
    } catch (error) {
        toast.error(error.response?.data?.message || 'ไม่สามารถสร้าง Pool ได้')
    } finally {
        creatingPool.value = false
    }
}

// Add Key
const addKeyPool = ref(null)
const addingKey = ref(false)
const newKey = reactive({
    label: '',
    api_key: '',
    base_url: '',
    model_id: '',
    daily_limit: 0,
    priority: 0,
})

const openAddKey = (pool) => {
    addKeyPool.value = pool
    newKey.label = ''
    newKey.api_key = ''
    newKey.base_url = ''
    newKey.model_id = ''
    newKey.daily_limit = 0
    newKey.priority = 0
}

const addKey = async () => {
    addingKey.value = true
    try {
        await pipelineApi.addApiKey(addKeyPool.value.id, { ...newKey })
        toast.success('เพิ่ม Key สำเร็จ')
        addKeyPool.value = null
        emit('refresh')
    } catch (error) {
        toast.error(error.response?.data?.message || 'ไม่สามารถเพิ่ม Key ได้')
    } finally {
        addingKey.value = false
    }
}

// Remove Key
const removeKey = async (pool, member) => {
    if (!confirm(`ต้องการลบ key "${member.label || 'Key #' + member.id}" หรือไม่?`)) return
    try {
        await pipelineApi.removeApiKey(pool.id, member.id)
        toast.success('ลบ Key สำเร็จ')
        emit('refresh')
    } catch (error) {
        toast.error('ไม่สามารถลบ Key ได้')
    }
}

// Helpers
const getUsagePercent = (member) => {
    if (!member.daily_limit || member.daily_limit === 0) return 0
    return Math.min(100, Math.round((member.requests_today / member.daily_limit) * 100))
}

const getSuccessRate = (member) => {
    const total = (member.success_count || 0) + (member.failure_count || 0)
    if (total === 0) return 100
    return Math.round((member.success_count / total) * 100)
}
</script>

<style scoped>
.manager-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 16px;
}

.manager-title {
    font-size: 1rem;
    font-weight: 600;
    color: #e5e7eb;
}

/* Pool Cards */
.pools-grid {
    display: grid;
    gap: 16px;
    grid-template-columns: repeat(auto-fill, minmax(400px, 1fr));
}

.pool-card {
    background: #111827;
    border-radius: 10px;
    padding: 16px;
    border: 1px solid #374151;
}

.pool-header {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    margin-bottom: 12px;
}

.pool-name {
    font-size: 0.95rem;
    font-weight: 600;
    color: #f3f4f6;
}

.pool-tags {
    display: flex;
    gap: 4px;
    margin-top: 4px;
}

.pool-tag {
    padding: 1px 6px;
    font-size: 0.65rem;
    background: #374151;
    color: #9ca3af;
    border-radius: 3px;
    text-transform: uppercase;
}

.pool-status {
    padding: 2px 8px;
    font-size: 0.7rem;
    font-weight: 600;
    border-radius: 4px;
}

.pool-status.active {
    background: #14532d;
    color: #86efac;
}

.pool-status.inactive {
    background: #374151;
    color: #6b7280;
}

/* Keys */
.keys-section {
    margin-bottom: 12px;
}

.keys-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 8px;
}

.keys-count {
    font-size: 0.75rem;
    color: #6b7280;
}

.keys-list {
    display: grid;
    gap: 6px;
}

.key-item {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px;
    background: #1f2937;
    border-radius: 6px;
    border: 1px solid #374151;
}

.key-info {
    display: flex;
    flex-direction: column;
    min-width: 80px;
}

.key-label {
    font-size: 0.75rem;
    color: #d1d5db;
    font-weight: 500;
}

.key-status {
    font-size: 0.6rem;
    text-transform: uppercase;
    font-weight: 600;
}

.key-active { color: #22c55e; }
.key-cooldown { color: #f59e0b; }
.key-exhausted { color: #ef4444; }
.key-error { color: #dc2626; }

.key-usage {
    flex: 1;
    display: flex;
    align-items: center;
    gap: 6px;
}

.usage-bar {
    flex: 1;
    height: 4px;
    background: #374151;
    border-radius: 2px;
    overflow: hidden;
}

.usage-fill {
    height: 100%;
    background: #4f46e5;
    border-radius: 2px;
    transition: width 0.3s;
}

.usage-fill.usage-high {
    background: #ef4444;
}

.usage-text {
    font-size: 0.65rem;
    color: #6b7280;
    white-space: nowrap;
}

.key-stats {
    display: flex;
    gap: 8px;
    font-size: 0.65rem;
    color: #6b7280;
}

.no-keys {
    font-size: 0.8rem;
    color: #4b5563;
    text-align: center;
    padding: 12px;
}

/* Pool Footer */
.pool-footer {
    display: flex;
    gap: 12px;
    font-size: 0.7rem;
    color: #6b7280;
    padding-top: 8px;
    border-top: 1px solid #374151;
}

.failover-badge {
    color: #22c55e;
    font-weight: 600;
}

/* Buttons */
.btn {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    padding: 6px 12px;
    font-size: 0.8rem;
    font-weight: 500;
    border-radius: 6px;
    border: none;
    cursor: pointer;
    transition: all 0.2s;
}

.btn-sm { padding: 4px 8px; font-size: 0.75rem; }
.btn-primary { background: #4f46e5; color: white; }
.btn-primary:hover { background: #4338ca; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }
.btn-secondary { background: #374151; color: #d1d5db; }
.btn-secondary:hover { background: #4b5563; }

.btn-icon-only {
    display: flex;
    align-items: center;
    justify-content: center;
    width: 28px;
    height: 28px;
    border-radius: 4px;
    border: none;
    cursor: pointer;
    background: transparent;
    transition: all 0.2s;
}

.btn-danger-subtle {
    color: #6b7280;
}

.btn-danger-subtle:hover {
    color: #ef4444;
    background: #7f1d1d;
}

/* Modals */
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
    max-height: 90vh;
    display: flex;
    flex-direction: column;
}

.modal-sm { max-width: 520px; }

.modal-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    padding: 14px 20px;
    border-bottom: 1px solid #374151;
}

.modal-header h2 {
    font-size: 1rem;
    font-weight: 600;
    color: #f3f4f6;
}

.close-btn {
    background: none;
    border: none;
    font-size: 1.5rem;
    color: #9ca3af;
    cursor: pointer;
}

.close-btn:hover { color: #f3f4f6; }

.modal-body-form {
    padding: 20px;
    overflow-y: auto;
}

.modal-actions {
    display: flex;
    justify-content: flex-end;
    gap: 8px;
    margin-top: 16px;
}

/* Form */
.form-group {
    margin-bottom: 12px;
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
.form-group input[type="password"],
.form-group select {
    width: 100%;
    padding: 8px 12px;
    background: #111827;
    border: 1px solid #374151;
    border-radius: 6px;
    color: #f3f4f6;
    font-size: 0.85rem;
}

.form-group input:focus,
.form-group select:focus {
    outline: none;
    border-color: #4f46e5;
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
</style>
