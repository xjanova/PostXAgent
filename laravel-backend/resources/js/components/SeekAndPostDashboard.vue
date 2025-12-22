<template>
  <div class="seek-and-post-dashboard">
    <!-- Header -->
    <div class="dashboard-header">
      <h1 class="text-2xl font-bold">Seek and Post</h1>
      <p class="text-gray-600">ระบบค้นหากลุ่มและโพสต์อัตโนมัติอัจฉริยะ</p>
    </div>

    <!-- Statistics Cards -->
    <div class="stats-grid">
      <div class="stat-card">
        <div class="stat-icon bg-blue-100 text-blue-600">
          <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
        </div>
        <div class="stat-content">
          <span class="stat-value">{{ stats.totalTasks }}</span>
          <span class="stat-label">งานทั้งหมด</span>
        </div>
      </div>

      <div class="stat-card">
        <div class="stat-icon bg-green-100 text-green-600">
          <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M17 20h5v-2a3 3 0 00-5.356-1.857M17 20H7m10 0v-2c0-.656-.126-1.283-.356-1.857M7 20H2v-2a3 3 0 015.356-1.857M7 20v-2c0-.656.126-1.283.356-1.857m0 0a5.002 5.002 0 019.288 0M15 7a3 3 0 11-6 0 3 3 0 016 0zm6 3a2 2 0 11-4 0 2 2 0 014 0zM7 10a2 2 0 11-4 0 2 2 0 014 0z" />
          </svg>
        </div>
        <div class="stat-content">
          <span class="stat-value">{{ stats.totalGroupsDiscovered }}</span>
          <span class="stat-label">กลุ่มที่ค้นพบ</span>
        </div>
      </div>

      <div class="stat-card">
        <div class="stat-icon bg-purple-100 text-purple-600">
          <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 5.882V19.24a1.76 1.76 0 01-3.417.592l-2.147-6.15M18 13a3 3 0 100-6M5.436 13.683A4.001 4.001 0 017 6h1.832c4.1 0 7.625-1.234 9.168-3v14c-1.543-1.766-5.067-3-9.168-3H7a3.988 3.988 0 01-1.564-.317z" />
          </svg>
        </div>
        <div class="stat-content">
          <span class="stat-value">{{ stats.totalPostsMade }}</span>
          <span class="stat-label">โพสต์ที่สร้าง</span>
        </div>
      </div>

      <div class="stat-card">
        <div class="stat-icon bg-yellow-100 text-yellow-600">
          <svg class="w-6 h-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 19v-6a2 2 0 00-2-2H5a2 2 0 00-2 2v6a2 2 0 002 2h2a2 2 0 002-2zm0 0V9a2 2 0 012-2h2a2 2 0 012 2v10m-6 0a2 2 0 002 2h2a2 2 0 002-2m0 0V5a2 2 0 012-2h2a2 2 0 012 2v14a2 2 0 01-2 2h-2a2 2 0 01-2-2z" />
          </svg>
        </div>
        <div class="stat-content">
          <span class="stat-value">{{ stats.overallSuccessRate }}%</span>
          <span class="stat-label">อัตราความสำเร็จ</span>
        </div>
      </div>
    </div>

    <!-- Main Content -->
    <div class="main-content">
      <!-- Active Tasks -->
      <div class="content-section">
        <div class="section-header">
          <h2 class="text-lg font-semibold">งานที่กำลังดำเนินการ</h2>
          <button @click="showCreateModal = true" class="btn-primary">
            <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
            </svg>
            สร้างงานใหม่
          </button>
        </div>

        <div v-if="loading" class="loading-state">
          <div class="spinner"></div>
          <span>กำลังโหลด...</span>
        </div>

        <div v-else-if="activeTasks.length === 0" class="empty-state">
          <svg class="w-16 h-16 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1" d="M9 5H7a2 2 0 00-2 2v12a2 2 0 002 2h10a2 2 0 002-2V7a2 2 0 00-2-2h-2M9 5a2 2 0 002 2h2a2 2 0 002-2M9 5a2 2 0 012-2h2a2 2 0 012 2" />
          </svg>
          <p>ยังไม่มีงานที่กำลังดำเนินการ</p>
          <button @click="showCreateModal = true" class="btn-primary mt-4">สร้างงานใหม่</button>
        </div>

        <div v-else class="task-list">
          <div v-for="task in activeTasks" :key="task.id" class="task-card">
            <div class="task-header">
              <div class="task-info">
                <h3 class="task-name">{{ task.name }}</h3>
                <span :class="['task-status', `status-${task.status}`]">{{ getStatusText(task.status) }}</span>
              </div>
              <div class="task-platform">
                <img :src="getPlatformIcon(task.platform)" :alt="task.platform" class="w-6 h-6" />
              </div>
            </div>

            <div class="task-progress">
              <div class="progress-bar">
                <div class="progress-fill" :style="{ width: task.progress.percentage + '%' }"></div>
              </div>
              <span class="progress-text">{{ task.progress.percentage.toFixed(1) }}%</span>
            </div>

            <div class="task-stats">
              <div class="stat-item">
                <span class="stat-label">กลุ่มที่ค้นพบ</span>
                <span class="stat-value">{{ task.progress.groups_discovered }}</span>
              </div>
              <div class="stat-item">
                <span class="stat-label">กลุ่มที่เข้าร่วม</span>
                <span class="stat-value">{{ task.progress.groups_joined }}</span>
              </div>
              <div class="stat-item">
                <span class="stat-label">โพสต์ที่สร้าง</span>
                <span class="stat-value">{{ task.progress.posts_made }}</span>
              </div>
              <div class="stat-item">
                <span class="stat-label">สำเร็จ</span>
                <span class="stat-value text-green-600">{{ task.progress.posts_successful }}</span>
              </div>
            </div>

            <div class="task-actions">
              <button v-if="task.status === 'paused'" @click="resumeTask(task.id)" class="btn-sm btn-success">
                ดำเนินการต่อ
              </button>
              <button v-else-if="['seeking', 'joining', 'posting'].includes(task.status)" @click="pauseTask(task.id)" class="btn-sm btn-warning">
                หยุดชั่วคราว
              </button>
              <button @click="viewTask(task.id)" class="btn-sm btn-outline">
                ดูรายละเอียด
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- Discovered Groups -->
      <div class="content-section">
        <div class="section-header">
          <h2 class="text-lg font-semibold">กลุ่มที่ค้นพบ</h2>
          <button @click="refreshGroups" class="btn-outline">
            <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
          </button>
        </div>

        <div class="group-filters">
          <select v-model="groupFilters.platform" class="filter-select">
            <option value="">ทุกแพลตฟอร์ม</option>
            <option value="facebook">Facebook</option>
            <option value="line">LINE</option>
            <option value="telegram">Telegram</option>
          </select>
          <input v-model="groupFilters.keyword" type="text" placeholder="ค้นหาคีย์เวิร์ด..." class="filter-input" />
          <select v-model="groupFilters.minQuality" class="filter-select">
            <option value="">ทุกคะแนน</option>
            <option value="70">70+ (คุณภาพสูง)</option>
            <option value="50">50+ (ปานกลาง)</option>
          </select>
        </div>

        <div class="groups-list">
          <div v-for="group in filteredGroups" :key="group.id" class="group-card">
            <div class="group-info">
              <h4 class="group-name">{{ group.group_name }}</h4>
              <div class="group-meta">
                <span class="platform-badge">{{ group.platform }}</span>
                <span class="member-count">{{ formatNumber(group.member_count) }} สมาชิก</span>
                <span :class="['activity-badge', `activity-${group.activity_level}`]">{{ group.activity_level }}</span>
              </div>
            </div>
            <div class="group-score">
              <div class="score-circle" :class="getScoreClass(group.quality_score)">
                {{ Math.round(group.quality_score) }}
              </div>
              <span class="score-label">คะแนน</span>
            </div>
            <div class="group-stats">
              <div class="stat">
                <span>โพสต์ของเรา</span>
                <span>{{ group.our_post_count }}</span>
              </div>
              <div class="stat">
                <span>อัตราสำเร็จ</span>
                <span>{{ group.success_rate.toFixed(1) }}%</span>
              </div>
            </div>
            <div class="group-status">
              <span v-if="group.is_joined" class="status-joined">เข้าร่วมแล้ว</span>
              <span v-else-if="group.join_requested_at" class="status-pending">รอการอนุมัติ</span>
              <span v-else class="status-not-joined">ยังไม่ได้เข้าร่วม</span>
            </div>
          </div>
        </div>

        <div v-if="groupsPagination.hasMore" class="load-more">
          <button @click="loadMoreGroups" class="btn-outline">โหลดเพิ่มเติม</button>
        </div>
      </div>
    </div>

    <!-- Create Task Modal -->
    <div v-if="showCreateModal" class="modal-overlay" @click.self="showCreateModal = false">
      <div class="modal-content">
        <div class="modal-header">
          <h3 class="text-xl font-bold">สร้างงาน Seek and Post ใหม่</h3>
          <button @click="showCreateModal = false" class="btn-close">&times;</button>
        </div>

        <form @submit.prevent="createTask" class="modal-body">
          <div class="form-group">
            <label>ชื่องาน</label>
            <input v-model="newTask.name" type="text" required class="form-input" placeholder="เช่น ค้นหากลุ่มขายของออนไลน์" />
          </div>

          <div class="form-group">
            <label>แพลตฟอร์ม</label>
            <select v-model="newTask.platform" required class="form-select">
              <option value="">เลือกแพลตฟอร์ม</option>
              <option value="facebook">Facebook</option>
              <option value="line">LINE</option>
              <option value="telegram">Telegram</option>
            </select>
          </div>

          <div class="form-group">
            <label>คีย์เวิร์ดที่ต้องการค้นหา (คั่นด้วยคอมม่า)</label>
            <input v-model="newTask.keywordsText" type="text" required class="form-input" placeholder="เช่น ขายของออนไลน์, ซื้อขาย, ตลาดนัด" />
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>สมาชิกขั้นต่ำ</label>
              <input v-model.number="newTask.min_group_members" type="number" min="1" class="form-input" />
            </div>
            <div class="form-group">
              <label>คะแนนคุณภาพขั้นต่ำ</label>
              <input v-model.number="newTask.min_quality_score" type="number" min="0" max="100" class="form-input" />
            </div>
          </div>

          <div class="form-row">
            <div class="form-group">
              <label>จำนวนกลุ่มที่ต้องการค้นหา</label>
              <input v-model.number="newTask.max_groups_to_discover" type="number" min="1" max="500" class="form-input" />
            </div>
            <div class="form-group">
              <label>ขอเข้ากลุ่มสูงสุด/วัน</label>
              <input v-model.number="newTask.max_groups_to_join_per_day" type="number" min="1" max="50" class="form-input" />
            </div>
          </div>

          <div class="form-group">
            <label>เลือก Workflow Template</label>
            <select v-model="newTask.workflow_template_id" class="form-select">
              <option value="">ไม่ใช้ Template</option>
              <option v-for="template in workflowTemplates" :key="template.id" :value="template.id">
                {{ template.localized_name }}
              </option>
            </select>
          </div>

          <div class="form-group">
            <label class="checkbox-label">
              <input v-model="newTask.auto_join" type="checkbox" />
              <span>ขอเข้ากลุ่มอัตโนมัติ</span>
            </label>
          </div>

          <div class="form-group">
            <label class="checkbox-label">
              <input v-model="newTask.smart_timing" type="checkbox" />
              <span>ใช้ Smart Timing (โพสต์ในเวลาที่เหมาะสม)</span>
            </label>
          </div>

          <div class="modal-footer">
            <button type="button" @click="showCreateModal = false" class="btn-outline">ยกเลิก</button>
            <button type="submit" class="btn-primary" :disabled="creating">
              {{ creating ? 'กำลังสร้าง...' : 'สร้างงาน' }}
            </button>
          </div>
        </form>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useToast } from '@/composables/useToast'

const toast = useToast()

// State
const loading = ref(true)
const creating = ref(false)
const showCreateModal = ref(false)

const stats = reactive({
  totalTasks: 0,
  totalGroupsDiscovered: 0,
  totalGroupsJoined: 0,
  totalPostsMade: 0,
  overallSuccessRate: 0,
})

const tasks = ref([])
const groups = ref([])
const workflowTemplates = ref([])

const groupFilters = reactive({
  platform: '',
  keyword: '',
  minQuality: '',
})

const groupsPagination = reactive({
  page: 1,
  perPage: 20,
  hasMore: true,
})

const newTask = reactive({
  name: '',
  platform: '',
  keywordsText: '',
  min_group_members: 100,
  min_quality_score: 50,
  max_groups_to_discover: 50,
  max_groups_to_join_per_day: 10,
  max_posts_per_day: 20,
  auto_join: true,
  smart_timing: true,
  workflow_template_id: null,
})

// Computed
const activeTasks = computed(() => {
  return tasks.value.filter(t => ['pending', 'seeking', 'joining', 'posting', 'paused'].includes(t.status))
})

const filteredGroups = computed(() => {
  return groups.value.filter(g => {
    if (groupFilters.platform && g.platform !== groupFilters.platform) return false
    if (groupFilters.keyword && !g.keywords?.includes(groupFilters.keyword)) return false
    if (groupFilters.minQuality && g.quality_score < parseInt(groupFilters.minQuality)) return false
    return true
  })
})

// Methods
const fetchStats = async () => {
  try {
    const response = await fetch('/api/v1/seek-and-post/statistics', {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
    const data = await response.json()
    if (data.success) {
      Object.assign(stats, {
        totalTasks: data.data.total_tasks || 0,
        totalGroupsDiscovered: data.data.total_groups_discovered || 0,
        totalGroupsJoined: data.data.total_groups_joined || 0,
        totalPostsMade: data.data.total_posts_made || 0,
        overallSuccessRate: data.data.overall_success_rate || 0,
      })
    }
  } catch (error) {
    console.error('Failed to fetch stats:', error)
  }
}

const fetchTasks = async () => {
  try {
    const response = await fetch('/api/v1/seek-and-post', {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
    const data = await response.json()
    if (data.success) {
      tasks.value = data.data.data || data.data || []
    }
  } catch (error) {
    console.error('Failed to fetch tasks:', error)
  }
}

const fetchGroups = async () => {
  try {
    const params = new URLSearchParams({
      per_page: groupsPagination.perPage.toString(),
      page: groupsPagination.page.toString(),
    })
    if (groupFilters.platform) params.append('platform', groupFilters.platform)
    if (groupFilters.minQuality) params.append('min_quality', groupFilters.minQuality)

    const response = await fetch(`/api/v1/seek-and-post/groups/list?${params}`, {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
    const data = await response.json()
    if (data.success) {
      groups.value = data.data.data || data.data || []
      groupsPagination.hasMore = data.data.next_page_url != null
    }
  } catch (error) {
    console.error('Failed to fetch groups:', error)
  }
}

const fetchTemplates = async () => {
  try {
    const response = await fetch('/api/v1/workflow-templates?category=seek_and_post')
    const data = await response.json()
    if (data.success) {
      workflowTemplates.value = data.data || []
    }
  } catch (error) {
    console.error('Failed to fetch templates:', error)
  }
}

const createTask = async () => {
  creating.value = true
  try {
    const taskData = {
      ...newTask,
      target_keywords: newTask.keywordsText.split(',').map(k => k.trim()).filter(k => k),
    }
    delete taskData.keywordsText

    const response = await fetch('/api/v1/seek-and-post', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${localStorage.getItem('token')}`
      },
      body: JSON.stringify(taskData)
    })
    const data = await response.json()
    if (data.success) {
      toast.success('สร้างงานสำเร็จ')
      showCreateModal.value = false
      resetNewTask()
      await fetchTasks()
    } else {
      toast.error(data.message || 'เกิดข้อผิดพลาด')
    }
  } catch (error) {
    toast.error('เกิดข้อผิดพลาดในการสร้างงาน')
  } finally {
    creating.value = false
  }
}

const pauseTask = async (taskId) => {
  try {
    const response = await fetch(`/api/v1/seek-and-post/${taskId}/pause`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
    const data = await response.json()
    if (data.success) {
      toast.success('หยุดงานชั่วคราวสำเร็จ')
      await fetchTasks()
    }
  } catch (error) {
    toast.error('เกิดข้อผิดพลาด')
  }
}

const resumeTask = async (taskId) => {
  try {
    const response = await fetch(`/api/v1/seek-and-post/${taskId}/resume`, {
      method: 'POST',
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
    const data = await response.json()
    if (data.success) {
      toast.success('ดำเนินการต่อสำเร็จ')
      await fetchTasks()
    }
  } catch (error) {
    toast.error('เกิดข้อผิดพลาด')
  }
}

const viewTask = (taskId) => {
  // Navigate to task detail
  window.location.href = `/seek-and-post/${taskId}`
}

const refreshGroups = () => {
  groupsPagination.page = 1
  fetchGroups()
}

const loadMoreGroups = () => {
  groupsPagination.page++
  fetchGroups()
}

const resetNewTask = () => {
  Object.assign(newTask, {
    name: '',
    platform: '',
    keywordsText: '',
    min_group_members: 100,
    min_quality_score: 50,
    max_groups_to_discover: 50,
    max_groups_to_join_per_day: 10,
    max_posts_per_day: 20,
    auto_join: true,
    smart_timing: true,
    workflow_template_id: null,
  })
}

const getStatusText = (status) => {
  const statusMap = {
    pending: 'รอดำเนินการ',
    seeking: 'กำลังค้นหากลุ่ม',
    joining: 'กำลังขอเข้ากลุ่ม',
    posting: 'กำลังโพสต์',
    completed: 'เสร็จสิ้น',
    paused: 'หยุดชั่วคราว',
    failed: 'ล้มเหลว',
  }
  return statusMap[status] || status
}

const getPlatformIcon = (platform) => {
  const icons = {
    facebook: '/images/platforms/facebook.svg',
    line: '/images/platforms/line.svg',
    telegram: '/images/platforms/telegram.svg',
    instagram: '/images/platforms/instagram.svg',
    twitter: '/images/platforms/twitter.svg',
  }
  return icons[platform] || '/images/platforms/default.svg'
}

const getScoreClass = (score) => {
  if (score >= 70) return 'score-high'
  if (score >= 50) return 'score-medium'
  return 'score-low'
}

const formatNumber = (num) => {
  if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M'
  if (num >= 1000) return (num / 1000).toFixed(1) + 'K'
  return num.toString()
}

// Lifecycle
onMounted(async () => {
  await Promise.all([
    fetchStats(),
    fetchTasks(),
    fetchGroups(),
    fetchTemplates(),
  ])
  loading.value = false
})
</script>

<style scoped>
.seek-and-post-dashboard {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.dashboard-header {
  margin-bottom: 24px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
  margin-bottom: 32px;
}

.stat-card {
  background: white;
  border-radius: 12px;
  padding: 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.stat-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.stat-content {
  display: flex;
  flex-direction: column;
}

.stat-value {
  font-size: 24px;
  font-weight: 700;
}

.stat-label {
  font-size: 14px;
  color: #6b7280;
}

.content-section {
  background: white;
  border-radius: 12px;
  padding: 24px;
  margin-bottom: 24px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
}

.section-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.task-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.task-card {
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  padding: 16px;
}

.task-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 12px;
}

.task-name {
  font-weight: 600;
  margin-bottom: 4px;
}

.task-status {
  font-size: 12px;
  padding: 2px 8px;
  border-radius: 9999px;
}

.status-seeking { background: #dbeafe; color: #1d4ed8; }
.status-joining { background: #fef3c7; color: #d97706; }
.status-posting { background: #d1fae5; color: #059669; }
.status-paused { background: #f3f4f6; color: #6b7280; }
.status-failed { background: #fee2e2; color: #dc2626; }

.task-progress {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
}

.progress-bar {
  flex: 1;
  height: 8px;
  background: #e5e7eb;
  border-radius: 4px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #3b82f6, #8b5cf6);
  transition: width 0.3s ease;
}

.task-stats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
  margin-bottom: 12px;
}

.task-stats .stat-item {
  text-align: center;
}

.task-stats .stat-label {
  display: block;
  font-size: 11px;
  color: #9ca3af;
}

.task-stats .stat-value {
  font-size: 16px;
  font-weight: 600;
}

.task-actions {
  display: flex;
  gap: 8px;
  justify-content: flex-end;
}

/* Groups */
.group-filters {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}

.filter-select, .filter-input {
  padding: 8px 12px;
  border: 1px solid #e5e7eb;
  border-radius: 6px;
  font-size: 14px;
}

.groups-list {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 16px;
}

.group-card {
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  padding: 16px;
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 12px;
}

.group-name {
  font-weight: 600;
  margin-bottom: 8px;
}

.group-meta {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.platform-badge {
  font-size: 11px;
  padding: 2px 6px;
  background: #e5e7eb;
  border-radius: 4px;
  text-transform: capitalize;
}

.score-circle {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 16px;
}

.score-high { background: #d1fae5; color: #059669; }
.score-medium { background: #fef3c7; color: #d97706; }
.score-low { background: #fee2e2; color: #dc2626; }

/* Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 50;
}

.modal-content {
  background: white;
  border-radius: 12px;
  max-width: 600px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px 24px;
  border-bottom: 1px solid #e5e7eb;
}

.modal-body {
  padding: 24px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding-top: 20px;
  border-top: 1px solid #e5e7eb;
  margin-top: 20px;
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  font-size: 14px;
  font-weight: 500;
  margin-bottom: 6px;
}

.form-input, .form-select {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  font-size: 14px;
}

.form-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
}

/* Buttons */
.btn-primary {
  background: linear-gradient(135deg, #3b82f6, #8b5cf6);
  color: white;
  padding: 10px 20px;
  border-radius: 8px;
  font-weight: 500;
  display: inline-flex;
  align-items: center;
  border: none;
  cursor: pointer;
  transition: opacity 0.2s;
}

.btn-primary:hover { opacity: 0.9; }
.btn-primary:disabled { opacity: 0.5; cursor: not-allowed; }

.btn-outline {
  background: white;
  border: 1px solid #d1d5db;
  padding: 8px 16px;
  border-radius: 6px;
  cursor: pointer;
  transition: background 0.2s;
}

.btn-outline:hover { background: #f9fafb; }

.btn-sm {
  padding: 6px 12px;
  font-size: 13px;
  border-radius: 6px;
}

.btn-success { background: #10b981; color: white; border: none; cursor: pointer; }
.btn-warning { background: #f59e0b; color: white; border: none; cursor: pointer; }

.btn-close {
  background: none;
  border: none;
  font-size: 24px;
  cursor: pointer;
  color: #6b7280;
}

/* States */
.loading-state, .empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 48px;
  color: #6b7280;
}

.spinner {
  width: 40px;
  height: 40px;
  border: 3px solid #e5e7eb;
  border-top-color: #3b82f6;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 12px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
