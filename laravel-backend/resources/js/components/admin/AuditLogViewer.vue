<template>
  <div class="audit-log-viewer">
    <div class="header">
      <h2>Audit Logs</h2>
      <button @click="exportLogs" class="btn btn-secondary">
        <i class="fas fa-download"></i> Export CSV
      </button>
    </div>

    <!-- Statistics Cards -->
    <div class="stats-cards">
      <div class="stat-card">
        <div class="stat-value">{{ stats.total_activities || 0 }}</div>
        <div class="stat-label">กิจกรรมทั้งหมด</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ Object.keys(stats.by_event || {}).length }}</div>
        <div class="stat-label">ประเภท Event</div>
      </div>
      <div class="stat-card">
        <div class="stat-value">{{ stats.top_causers?.length || 0 }}</div>
        <div class="stat-label">ผู้ใช้งานบ่อย</div>
      </div>
    </div>

    <!-- Filters -->
    <div class="filters">
      <div class="filter-group">
        <label>Log Type</label>
        <select v-model="filters.log_name" @change="fetchLogs">
          <option value="">ทั้งหมด</option>
          <option v-for="name in logNames" :key="name" :value="name">{{ name }}</option>
        </select>
      </div>
      <div class="filter-group">
        <label>Event</label>
        <select v-model="filters.event" @change="fetchLogs">
          <option value="">ทั้งหมด</option>
          <option value="created">สร้าง</option>
          <option value="updated">แก้ไข</option>
          <option value="deleted">ลบ</option>
          <option value="login">เข้าสู่ระบบ</option>
          <option value="logout">ออกจากระบบ</option>
        </select>
      </div>
      <div class="filter-group">
        <label>จากวันที่</label>
        <input type="date" v-model="filters.from" @change="fetchLogs" />
      </div>
      <div class="filter-group">
        <label>ถึงวันที่</label>
        <input type="date" v-model="filters.to" @change="fetchLogs" />
      </div>
      <button @click="resetFilters" class="btn btn-link">รีเซ็ต</button>
    </div>

    <!-- Logs Table -->
    <div class="logs-table-container">
      <table class="logs-table">
        <thead>
          <tr>
            <th>เวลา</th>
            <th>ผู้ดำเนินการ</th>
            <th>Event</th>
            <th>รายละเอียด</th>
            <th>Subject</th>
            <th></th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="log in logs" :key="log.id">
            <td class="time-cell">
              <div class="date">{{ formatDate(log.created_at) }}</div>
              <div class="time">{{ formatTime(log.created_at) }}</div>
            </td>
            <td>
              <div v-if="log.causer" class="causer">
                <span class="causer-name">{{ log.causer.name }}</span>
                <span class="causer-email">{{ log.causer.email }}</span>
              </div>
              <span v-else class="system-action">ระบบ</span>
            </td>
            <td>
              <span class="event-badge" :class="getEventClass(log.event)">
                {{ log.event || 'N/A' }}
              </span>
            </td>
            <td class="description-cell">{{ log.description }}</td>
            <td>
              <span v-if="log.subject_type" class="subject-type">
                {{ log.subject_type }} #{{ log.subject_id }}
              </span>
            </td>
            <td>
              <button @click="showDetails(log)" class="btn btn-sm btn-link">
                ดูรายละเอียด
              </button>
            </td>
          </tr>
          <tr v-if="logs.length === 0">
            <td colspan="6" class="empty-state">ไม่มีข้อมูล</td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Pagination -->
    <div class="pagination" v-if="meta.last_page > 1">
      <button
        @click="goToPage(meta.current_page - 1)"
        :disabled="meta.current_page === 1"
        class="btn btn-sm"
      >
        ก่อนหน้า
      </button>
      <span class="page-info">
        หน้า {{ meta.current_page }} จาก {{ meta.last_page }}
        ({{ meta.total }} รายการ)
      </span>
      <button
        @click="goToPage(meta.current_page + 1)"
        :disabled="meta.current_page === meta.last_page"
        class="btn btn-sm"
      >
        ถัดไป
      </button>
    </div>

    <!-- Detail Modal -->
    <div v-if="selectedLog" class="modal-overlay" @click.self="selectedLog = null">
      <div class="modal">
        <div class="modal-header">
          <h3>รายละเอียด Activity Log</h3>
          <button @click="selectedLog = null" class="close-btn">&times;</button>
        </div>
        <div class="modal-body">
          <div class="detail-grid">
            <div class="detail-item">
              <label>ID</label>
              <span>{{ selectedLog.id }}</span>
            </div>
            <div class="detail-item">
              <label>Log Name</label>
              <span>{{ selectedLog.log_name || 'N/A' }}</span>
            </div>
            <div class="detail-item">
              <label>Event</label>
              <span class="event-badge" :class="getEventClass(selectedLog.event)">
                {{ selectedLog.event || 'N/A' }}
              </span>
            </div>
            <div class="detail-item">
              <label>เวลา</label>
              <span>{{ formatDateTime(selectedLog.created_at) }}</span>
            </div>
            <div class="detail-item full-width">
              <label>คำอธิบาย</label>
              <span>{{ selectedLog.description }}</span>
            </div>
            <div class="detail-item" v-if="selectedLog.causer">
              <label>ผู้ดำเนินการ</label>
              <span>{{ selectedLog.causer.name }} ({{ selectedLog.causer.email }})</span>
            </div>
            <div class="detail-item" v-if="selectedLog.subject_type">
              <label>Subject</label>
              <span>{{ selectedLog.subject_type }} #{{ selectedLog.subject_id }}</span>
            </div>
            <div class="detail-item full-width" v-if="selectedLog.properties">
              <label>Properties</label>
              <pre class="properties-json">{{ JSON.stringify(selectedLog.properties, null, 2) }}</pre>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useToast } from '../composables/useToast'

const { showToast } = useToast()

const logs = ref([])
const stats = ref({})
const logNames = ref([])
const meta = ref({})
const selectedLog = ref(null)

const filters = ref({
  log_name: '',
  event: '',
  from: '',
  to: '',
  page: 1
})

onMounted(async () => {
  await Promise.all([fetchLogs(), fetchStats(), fetchLogNames()])
})

async function fetchLogs() {
  try {
    const params = new URLSearchParams()
    if (filters.value.log_name) params.append('log_name', filters.value.log_name)
    if (filters.value.event) params.append('event', filters.value.event)
    if (filters.value.from) params.append('from', filters.value.from)
    if (filters.value.to) params.append('to', filters.value.to)
    params.append('page', filters.value.page)

    const response = await fetch(`/api/v1/admin/audit-logs?${params}`, {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
    const data = await response.json()
    if (data.success) {
      logs.value = data.data
      meta.value = data.meta
    }
  } catch (error) {
    showToast('ไม่สามารถโหลด Logs ได้', 'error')
  }
}

async function fetchStats() {
  try {
    const response = await fetch('/api/v1/admin/audit-logs/stats', {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
    const data = await response.json()
    if (data.success) {
      stats.value = data.data
    }
  } catch (error) {
    console.error('Failed to fetch stats:', error)
  }
}

async function fetchLogNames() {
  try {
    const response = await fetch('/api/v1/admin/audit-logs/log-names', {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
    const data = await response.json()
    if (data.success) {
      logNames.value = data.data
    }
  } catch (error) {
    console.error('Failed to fetch log names:', error)
  }
}

function resetFilters() {
  filters.value = {
    log_name: '',
    event: '',
    from: '',
    to: '',
    page: 1
  }
  fetchLogs()
}

function goToPage(page) {
  filters.value.page = page
  fetchLogs()
}

function getEventClass(event) {
  const classes = {
    created: 'event-created',
    updated: 'event-updated',
    deleted: 'event-deleted',
    login: 'event-login',
    logout: 'event-logout'
  }
  return classes[event] || 'event-default'
}

function formatDate(dateStr) {
  return new Date(dateStr).toLocaleDateString('th-TH')
}

function formatTime(dateStr) {
  return new Date(dateStr).toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' })
}

function formatDateTime(dateStr) {
  return new Date(dateStr).toLocaleString('th-TH')
}

function showDetails(log) {
  selectedLog.value = log
}

async function exportLogs() {
  try {
    const params = new URLSearchParams()
    if (filters.value.from) params.append('from', filters.value.from)
    if (filters.value.to) params.append('to', filters.value.to)

    const response = await fetch(`/api/v1/admin/audit-logs/export?${params}`, {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })

    const blob = await response.blob()
    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `audit-log-${new Date().toISOString().split('T')[0]}.csv`
    document.body.appendChild(a)
    a.click()
    window.URL.revokeObjectURL(url)
    a.remove()

    showToast('ดาวน์โหลดสำเร็จ', 'success')
  } catch (error) {
    showToast('ไม่สามารถดาวน์โหลดได้', 'error')
  }
}
</script>

<style scoped>
.audit-log-viewer {
  padding: 20px;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.stats-cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 16px;
  margin-bottom: 20px;
}

.stat-card {
  background: var(--card-bg, #fff);
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 8px;
  padding: 16px;
  text-align: center;
}

.stat-value {
  font-size: 28px;
  font-weight: bold;
  color: var(--primary-color, #3498db);
}

.stat-label {
  font-size: 12px;
  color: var(--text-muted, #666);
}

.filters {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  align-items: flex-end;
  margin-bottom: 20px;
  padding: 16px;
  background: var(--card-bg, #fff);
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 8px;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.filter-group label {
  font-size: 12px;
  color: var(--text-muted, #666);
}

.filter-group select,
.filter-group input {
  padding: 8px 12px;
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 4px;
  min-width: 150px;
}

.logs-table-container {
  overflow-x: auto;
  background: var(--card-bg, #fff);
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 8px;
}

.logs-table {
  width: 100%;
  border-collapse: collapse;
}

.logs-table th,
.logs-table td {
  padding: 12px;
  text-align: left;
  border-bottom: 1px solid var(--border-color, #e0e0e0);
}

.logs-table th {
  background: var(--muted-bg, #f5f5f5);
  font-weight: 600;
  font-size: 13px;
}

.time-cell {
  white-space: nowrap;
}

.time-cell .date {
  font-size: 13px;
}

.time-cell .time {
  font-size: 11px;
  color: var(--text-muted, #666);
}

.causer {
  display: flex;
  flex-direction: column;
}

.causer-name {
  font-weight: 500;
}

.causer-email {
  font-size: 11px;
  color: var(--text-muted, #666);
}

.system-action {
  color: var(--text-muted, #666);
  font-style: italic;
}

.event-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 500;
}

.event-created { background: #d4edda; color: #155724; }
.event-updated { background: #fff3cd; color: #856404; }
.event-deleted { background: #f8d7da; color: #721c24; }
.event-login { background: #cce5ff; color: #004085; }
.event-logout { background: #e2e3e5; color: #383d41; }
.event-default { background: #f5f5f5; color: #666; }

.description-cell {
  max-width: 300px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.subject-type {
  font-size: 12px;
  color: var(--text-muted, #666);
}

.empty-state {
  text-align: center;
  color: var(--text-muted, #666);
  padding: 40px !important;
}

.pagination {
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 16px;
  margin-top: 16px;
}

.page-info {
  font-size: 13px;
  color: var(--text-muted, #666);
}

/* Modal */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0,0,0,0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal {
  background: var(--modal-bg, #fff);
  border-radius: 8px;
  width: 90%;
  max-width: 600px;
  max-height: 80vh;
  overflow-y: auto;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px;
  border-bottom: 1px solid var(--border-color, #e0e0e0);
}

.modal-header h3 {
  margin: 0;
}

.close-btn {
  background: none;
  border: none;
  font-size: 24px;
  cursor: pointer;
}

.modal-body {
  padding: 16px;
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
}

.detail-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.detail-item.full-width {
  grid-column: span 2;
}

.detail-item label {
  font-size: 12px;
  color: var(--text-muted, #666);
}

.properties-json {
  background: var(--muted-bg, #f5f5f5);
  padding: 12px;
  border-radius: 4px;
  font-size: 12px;
  overflow-x: auto;
  white-space: pre;
}

/* Button styles */
.btn {
  padding: 8px 16px;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 14px;
}

.btn-secondary {
  background: #95a5a6;
  color: white;
}

.btn-link {
  background: none;
  color: var(--primary-color, #3498db);
}

.btn-sm {
  padding: 4px 8px;
  font-size: 12px;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
