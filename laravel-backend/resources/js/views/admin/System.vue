<template>
    <div class="space-y-6">
        <h1 class="text-2xl font-bold text-white">ตั้งค่าระบบ</h1>

        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6">
            <!-- AI Manager Status -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <div class="flex items-center justify-between mb-4">
                    <h2 class="text-lg font-semibold text-white flex items-center">
                        <ServerIcon class="w-5 h-5 mr-2 text-blue-400" />
                        AI Manager Status
                    </h2>
                    <span :class="aiManagerStatus.connected ? 'bg-green-500/20 text-green-400' : 'bg-red-500/20 text-red-400'" class="px-2 py-1 text-xs rounded-full">
                        {{ aiManagerStatus.connected ? 'เชื่อมต่อแล้ว' : 'ไม่ได้เชื่อมต่อ' }}
                    </span>
                </div>

                <div class="space-y-3">
                    <div class="flex justify-between">
                        <span class="text-gray-400">Host</span>
                        <span class="text-white font-mono text-sm">{{ aiManagerStatus.host }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Version</span>
                        <span class="text-white">{{ aiManagerStatus.version || '-' }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Active Workers</span>
                        <span class="text-white">{{ aiManagerStatus.activeWorkers || 0 }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Queue Size</span>
                        <span class="text-white">{{ aiManagerStatus.queueSize || 0 }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Uptime</span>
                        <span class="text-white">{{ aiManagerStatus.uptime || '-' }}</span>
                    </div>
                </div>

                <div class="mt-4 flex space-x-2">
                    <button @click="refreshAIManagerStatus" :disabled="loading.aiManager" class="px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition-colors disabled:opacity-50">
                        {{ loading.aiManager ? 'กำลังโหลด...' : 'รีเฟรช' }}
                    </button>
                    <button @click="restartAIManager" :disabled="loading.restart" class="px-4 py-2 bg-orange-600 hover:bg-orange-700 text-white rounded-lg transition-colors disabled:opacity-50">
                        {{ loading.restart ? 'กำลังรีสตาร์ท...' : 'รีสตาร์ท' }}
                    </button>
                </div>
            </div>

            <!-- Database Status -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <div class="flex items-center justify-between mb-4">
                    <h2 class="text-lg font-semibold text-white flex items-center">
                        <CircleStackIcon class="w-5 h-5 mr-2 text-green-400" />
                        Database Status
                    </h2>
                    <span class="bg-green-500/20 text-green-400 px-2 py-1 text-xs rounded-full">
                        เชื่อมต่อแล้ว
                    </span>
                </div>

                <div class="space-y-3">
                    <div class="flex justify-between">
                        <span class="text-gray-400">Driver</span>
                        <span class="text-white">{{ dbStatus.driver }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Database</span>
                        <span class="text-white">{{ dbStatus.database }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Users</span>
                        <span class="text-white">{{ dbStatus.users || 0 }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Active Rentals</span>
                        <span class="text-white">{{ dbStatus.activeRentals || 0 }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Total Posts</span>
                        <span class="text-white">{{ dbStatus.totalPosts || 0 }}</span>
                    </div>
                </div>
            </div>

            <!-- Queue Status -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <div class="flex items-center justify-between mb-4">
                    <h2 class="text-lg font-semibold text-white flex items-center">
                        <QueueListIcon class="w-5 h-5 mr-2 text-purple-400" />
                        Queue Status
                    </h2>
                </div>

                <div class="space-y-3">
                    <div class="flex justify-between">
                        <span class="text-gray-400">Pending Jobs</span>
                        <span class="text-white">{{ queueStatus.pending || 0 }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Processing</span>
                        <span class="text-white">{{ queueStatus.processing || 0 }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Failed Jobs</span>
                        <span :class="queueStatus.failed > 0 ? 'text-red-400' : 'text-white'">{{ queueStatus.failed || 0 }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Completed (24h)</span>
                        <span class="text-white">{{ queueStatus.completed || 0 }}</span>
                    </div>
                </div>

                <div class="mt-4 flex space-x-2">
                    <button @click="retryFailedJobs" :disabled="loading.queue || queueStatus.failed === 0" class="px-4 py-2 bg-yellow-600 hover:bg-yellow-700 text-white rounded-lg transition-colors disabled:opacity-50">
                        ลองใหม่ ({{ queueStatus.failed }})
                    </button>
                    <button @click="clearFailedJobs" :disabled="loading.queue || queueStatus.failed === 0" class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors disabled:opacity-50">
                        ล้าง Failed Jobs
                    </button>
                </div>
            </div>

            <!-- Cache Status -->
            <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
                <div class="flex items-center justify-between mb-4">
                    <h2 class="text-lg font-semibold text-white flex items-center">
                        <BoltIcon class="w-5 h-5 mr-2 text-yellow-400" />
                        Cache Status
                    </h2>
                </div>

                <div class="space-y-3">
                    <div class="flex justify-between">
                        <span class="text-gray-400">Driver</span>
                        <span class="text-white">{{ cacheStatus.driver }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Hit Rate</span>
                        <span class="text-white">{{ cacheStatus.hitRate }}%</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Memory Usage</span>
                        <span class="text-white">{{ cacheStatus.memoryUsage }}</span>
                    </div>
                    <div class="flex justify-between">
                        <span class="text-gray-400">Keys</span>
                        <span class="text-white">{{ cacheStatus.keys || 0 }}</span>
                    </div>
                </div>

                <div class="mt-4">
                    <button @click="clearCache" :disabled="loading.cache" class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors disabled:opacity-50">
                        {{ loading.cache ? 'กำลังล้าง...' : 'ล้าง Cache ทั้งหมด' }}
                    </button>
                </div>
            </div>
        </div>

        <!-- Platform Settings -->
        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-4">การตั้งค่าแพลตฟอร์ม</h2>

            <div class="grid grid-cols-1 md:grid-cols-3 gap-4">
                <div v-for="platform in platforms" :key="platform.id" class="bg-gray-700/50 rounded-lg p-4">
                    <div class="flex items-center justify-between mb-2">
                        <span class="text-white font-medium">{{ platform.name }}</span>
                        <label class="relative inline-flex items-center cursor-pointer">
                            <input type="checkbox" v-model="platform.enabled" @change="updatePlatform(platform)" class="sr-only peer">
                            <div class="w-9 h-5 bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-4 after:w-4 after:transition-all peer-checked:bg-green-600"></div>
                        </label>
                    </div>
                    <p class="text-gray-400 text-sm">
                        <span :class="platform.api_configured ? 'text-green-400' : 'text-red-400'">
                            {{ platform.api_configured ? '✓ API configured' : '✗ API not configured' }}
                        </span>
                    </p>
                </div>
            </div>
        </div>

        <!-- Maintenance Mode -->
        <div class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <div class="flex items-center justify-between">
                <div>
                    <h2 class="text-lg font-semibold text-white">โหมดซ่อมบำรุง</h2>
                    <p class="text-gray-400 text-sm mt-1">เมื่อเปิดใช้งาน ผู้ใช้จะไม่สามารถเข้าถึงระบบได้</p>
                </div>
                <label class="relative inline-flex items-center cursor-pointer">
                    <input type="checkbox" v-model="maintenanceMode" @change="toggleMaintenance" class="sr-only peer">
                    <div class="w-11 h-6 bg-gray-600 peer-focus:outline-none rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-red-600"></div>
                </label>
            </div>

            <div v-if="maintenanceMode" class="mt-4">
                <label class="block text-sm text-gray-400 mb-1">ข้อความแจ้งผู้ใช้</label>
                <textarea v-model="maintenanceMessage" rows="2"
                    class="w-full px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white"
                    placeholder="ระบบกำลังปรับปรุง กรุณารอสักครู่..."></textarea>
            </div>
        </div>

        <!-- Danger Zone -->
        <div class="bg-gray-800 rounded-xl p-6 border border-red-900">
            <h2 class="text-lg font-semibold text-red-400 mb-4">Danger Zone</h2>

            <div class="space-y-4">
                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-white">ล้างข้อมูลทดสอบ</p>
                        <p class="text-gray-400 text-sm">ลบข้อมูล test users และ test data ทั้งหมด</p>
                    </div>
                    <button @click="clearTestData" class="px-4 py-2 bg-red-600/20 text-red-400 hover:bg-red-600 hover:text-white rounded-lg transition-colors">
                        ล้างข้อมูลทดสอบ
                    </button>
                </div>

                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-white">รีเซ็ตสถิติ</p>
                        <p class="text-gray-400 text-sm">รีเซ็ตสถิติการใช้งานทั้งหมด</p>
                    </div>
                    <button @click="resetStats" class="px-4 py-2 bg-red-600/20 text-red-400 hover:bg-red-600 hover:text-white rounded-lg transition-colors">
                        รีเซ็ตสถิติ
                    </button>
                </div>

                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-white">ล้าง Logs</p>
                        <p class="text-gray-400 text-sm">ลบ activity logs และ error logs ทั้งหมด</p>
                    </div>
                    <button @click="clearLogs" class="px-4 py-2 bg-red-600/20 text-red-400 hover:bg-red-600 hover:text-white rounded-lg transition-colors">
                        ล้าง Logs
                    </button>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { adminApi, aiManagerApi } from '../../services/api'
import {
    ServerIcon,
    CircleStackIcon,
    QueueListIcon,
    BoltIcon,
} from '@heroicons/vue/24/outline'

const loading = reactive({
    aiManager: false,
    restart: false,
    queue: false,
    cache: false,
})

const aiManagerStatus = ref({
    connected: false,
    host: 'localhost:5000',
    version: null,
    activeWorkers: 0,
    queueSize: 0,
    uptime: null,
})

const dbStatus = ref({
    driver: 'MySQL',
    database: 'postxagent',
    users: 0,
    activeRentals: 0,
    totalPosts: 0,
})

const queueStatus = ref({
    pending: 0,
    processing: 0,
    failed: 0,
    completed: 0,
})

const cacheStatus = ref({
    driver: 'Redis',
    hitRate: 0,
    memoryUsage: '0 MB',
    keys: 0,
})

const platforms = ref([
    { id: 'facebook', name: 'Facebook', enabled: true, api_configured: true },
    { id: 'instagram', name: 'Instagram', enabled: true, api_configured: true },
    { id: 'tiktok', name: 'TikTok', enabled: true, api_configured: false },
    { id: 'twitter', name: 'Twitter/X', enabled: true, api_configured: true },
    { id: 'line', name: 'LINE', enabled: true, api_configured: false },
    { id: 'youtube', name: 'YouTube', enabled: false, api_configured: false },
    { id: 'threads', name: 'Threads', enabled: false, api_configured: false },
    { id: 'linkedin', name: 'LinkedIn', enabled: false, api_configured: false },
    { id: 'pinterest', name: 'Pinterest', enabled: false, api_configured: false },
])

const maintenanceMode = ref(false)
const maintenanceMessage = ref('')

const refreshAIManagerStatus = async () => {
    loading.aiManager = true
    try {
        const response = await aiManagerApi.status()
        if (response.data.success) {
            const data = response.data.data
            aiManagerStatus.value = {
                connected: true,
                host: data.host || 'localhost:5000',
                version: data.version,
                activeWorkers: data.active_workers || 0,
                queueSize: data.queue_size || 0,
                uptime: data.uptime,
            }
        }
    } catch (error) {
        aiManagerStatus.value.connected = false
        console.error('Failed to get AI Manager status:', error)
    } finally {
        loading.aiManager = false
    }
}

const restartAIManager = async () => {
    if (!confirm('รีสตาร์ท AI Manager? การดำเนินการที่กำลังทำอยู่จะถูกยกเลิก')) return

    loading.restart = true
    try {
        await aiManagerApi.restart()
        alert('กำลังรีสตาร์ท AI Manager...')
        setTimeout(refreshAIManagerStatus, 5000)
    } catch (error) {
        console.error('Failed to restart AI Manager:', error)
        alert('ไม่สามารถรีสตาร์ท AI Manager ได้')
    } finally {
        loading.restart = false
    }
}

const fetchSystemStatus = async () => {
    try {
        const response = await adminApi.systemStatus()
        if (response.data.success) {
            const data = response.data.data
            dbStatus.value = data.database || dbStatus.value
            queueStatus.value = data.queue || queueStatus.value
            cacheStatus.value = data.cache || cacheStatus.value
            maintenanceMode.value = data.maintenance_mode || false
            maintenanceMessage.value = data.maintenance_message || ''
        }
    } catch (error) {
        console.error('Failed to fetch system status:', error)
    }
}

const retryFailedJobs = async () => {
    loading.queue = true
    try {
        await adminApi.retryFailedJobs()
        fetchSystemStatus()
    } catch (error) {
        console.error('Failed to retry jobs:', error)
    } finally {
        loading.queue = false
    }
}

const clearFailedJobs = async () => {
    if (!confirm('ล้าง failed jobs ทั้งหมด?')) return

    loading.queue = true
    try {
        await adminApi.clearFailedJobs()
        fetchSystemStatus()
    } catch (error) {
        console.error('Failed to clear jobs:', error)
    } finally {
        loading.queue = false
    }
}

const clearCache = async () => {
    if (!confirm('ล้าง cache ทั้งหมด? อาจทำให้ระบบช้าลงชั่วคราว')) return

    loading.cache = true
    try {
        await adminApi.clearCache()
        alert('ล้าง cache เรียบร้อยแล้ว')
        fetchSystemStatus()
    } catch (error) {
        console.error('Failed to clear cache:', error)
    } finally {
        loading.cache = false
    }
}

const updatePlatform = async (platform) => {
    try {
        await adminApi.updatePlatformSetting(platform.id, { enabled: platform.enabled })
    } catch (error) {
        console.error('Failed to update platform:', error)
        platform.enabled = !platform.enabled
    }
}

const toggleMaintenance = async () => {
    try {
        await adminApi.toggleMaintenance({
            enabled: maintenanceMode.value,
            message: maintenanceMessage.value,
        })
    } catch (error) {
        console.error('Failed to toggle maintenance:', error)
        maintenanceMode.value = !maintenanceMode.value
    }
}

const clearTestData = async () => {
    if (!confirm('ลบข้อมูลทดสอบทั้งหมด? การดำเนินการนี้ไม่สามารถย้อนกลับได้')) return
    if (!confirm('คุณแน่ใจหรือไม่?')) return

    try {
        await adminApi.clearTestData()
        alert('ลบข้อมูลทดสอบเรียบร้อยแล้ว')
        fetchSystemStatus()
    } catch (error) {
        console.error('Failed to clear test data:', error)
    }
}

const resetStats = async () => {
    if (!confirm('รีเซ็ตสถิติทั้งหมด? การดำเนินการนี้ไม่สามารถย้อนกลับได้')) return

    try {
        await adminApi.resetStats()
        alert('รีเซ็ตสถิติเรียบร้อยแล้ว')
        fetchSystemStatus()
    } catch (error) {
        console.error('Failed to reset stats:', error)
    }
}

const clearLogs = async () => {
    if (!confirm('ล้าง logs ทั้งหมด? การดำเนินการนี้ไม่สามารถย้อนกลับได้')) return

    try {
        await adminApi.clearLogs()
        alert('ล้าง logs เรียบร้อยแล้ว')
    } catch (error) {
        console.error('Failed to clear logs:', error)
    }
}

onMounted(() => {
    refreshAIManagerStatus()
    fetchSystemStatus()
})
</script>
