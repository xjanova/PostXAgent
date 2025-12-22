<template>
  <div class="content-calendar">
    <!-- Header -->
    <div class="calendar-header">
      <div class="header-left">
        <button @click="prevMonth" class="nav-btn">
          <i class="fas fa-chevron-left"></i>
        </button>
        <h2>{{ currentMonthYear }}</h2>
        <button @click="nextMonth" class="nav-btn">
          <i class="fas fa-chevron-right"></i>
        </button>
        <button @click="goToToday" class="today-btn">วันนี้</button>
      </div>

      <div class="header-right">
        <div class="view-toggle">
          <button :class="{ active: view === 'month' }" @click="view = 'month'">เดือน</button>
          <button :class="{ active: view === 'week' }" @click="view = 'week'">สัปดาห์</button>
          <button :class="{ active: view === 'day' }" @click="view = 'day'">วัน</button>
        </div>

        <select v-model="filterBrand" class="brand-filter">
          <option value="">ทุกแบรนด์</option>
          <option v-for="brand in brands" :key="brand.id" :value="brand.id">
            {{ brand.name }}
          </option>
        </select>

        <select v-model="filterPlatform" class="platform-filter">
          <option value="">ทุกแพลตฟอร์ม</option>
          <option v-for="platform in platforms" :key="platform" :value="platform">
            {{ platform }}
          </option>
        </select>
      </div>
    </div>

    <!-- Month View -->
    <div v-if="view === 'month'" class="month-view">
      <!-- Weekday Headers -->
      <div class="weekday-headers">
        <div v-for="day in weekdayHeaders" :key="day" class="weekday-header">
          {{ day }}
        </div>
      </div>

      <!-- Calendar Grid -->
      <div class="calendar-grid">
        <div
          v-for="(day, index) in calendarDays"
          :key="index"
          class="calendar-day"
          :class="{
            'other-month': !day.isCurrentMonth,
            'today': day.isToday,
            'selected': isSelectedDate(day.date)
          }"
          @click="selectDate(day.date)"
          @drop="onDrop($event, day.date)"
          @dragover.prevent
        >
          <div class="day-header">
            <span class="day-number">{{ day.dayNumber }}</span>
            <span v-if="getPostCount(day.date) > 0" class="post-count">
              {{ getPostCount(day.date) }}
            </span>
          </div>

          <div class="day-posts">
            <div
              v-for="post in getPostsForDate(day.date).slice(0, 3)"
              :key="post.id"
              class="post-item"
              :class="getPlatformClass(post.platforms[0])"
              :draggable="post.status === 'scheduled'"
              @dragstart="onDragStart($event, post)"
              @click.stop="openPost(post)"
            >
              <span class="post-time">{{ formatTime(post.scheduled_at) }}</span>
              <span class="post-title">{{ truncate(post.content, 20) }}</span>
            </div>
            <div
              v-if="getPostsForDate(day.date).length > 3"
              class="more-posts"
              @click.stop="showDayDetail(day.date)"
            >
              +{{ getPostsForDate(day.date).length - 3 }} เพิ่มเติม
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Week View -->
    <div v-if="view === 'week'" class="week-view">
      <div class="time-column">
        <div class="time-header"></div>
        <div v-for="hour in hours" :key="hour" class="time-slot">
          {{ hour }}:00
        </div>
      </div>

      <div class="week-days">
        <div v-for="day in weekDays" :key="day.date" class="week-day">
          <div class="week-day-header" :class="{ today: day.isToday }">
            <span class="day-name">{{ day.dayName }}</span>
            <span class="day-num">{{ day.dayNumber }}</span>
          </div>

          <div class="week-day-content">
            <div
              v-for="post in getPostsForDate(day.date)"
              :key="post.id"
              class="week-post"
              :class="getPlatformClass(post.platforms[0])"
              :style="getPostStyle(post)"
              @click="openPost(post)"
            >
              <span class="post-time">{{ formatTime(post.scheduled_at) }}</span>
              <span class="post-title">{{ truncate(post.content, 30) }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Day View -->
    <div v-if="view === 'day'" class="day-view">
      <div class="day-view-header">
        <h3>{{ formatFullDate(selectedDate) }}</h3>
      </div>

      <div class="day-timeline">
        <div v-for="hour in hours" :key="hour" class="hour-row">
          <div class="hour-label">{{ hour }}:00</div>
          <div class="hour-content">
            <div
              v-for="post in getPostsForHour(selectedDate, hour)"
              :key="post.id"
              class="day-post"
              :class="getPlatformClass(post.platforms[0])"
              @click="openPost(post)"
            >
              <div class="post-platforms">
                <span v-for="p in post.platforms" :key="p" class="platform-icon">
                  {{ getPlatformIcon(p) }}
                </span>
              </div>
              <div class="post-info">
                <span class="post-time">{{ formatTime(post.scheduled_at) }}</span>
                <span class="post-content">{{ truncate(post.content, 100) }}</span>
                <span class="post-brand">{{ post.brand?.name }}</span>
              </div>
              <div class="post-status" :class="post.status">
                {{ getStatusLabel(post.status) }}
              </div>
            </div>

            <div
              v-if="getPostsForHour(selectedDate, hour).length === 0"
              class="empty-hour"
              @click="createPostAt(selectedDate, hour)"
            >
              <i class="fas fa-plus"></i>
              เพิ่มโพสต์
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Post Detail Modal -->
    <div v-if="selectedPost" class="modal-overlay" @click.self="selectedPost = null">
      <div class="modal post-modal">
        <div class="modal-header">
          <h3>รายละเอียดโพสต์</h3>
          <button @click="selectedPost = null" class="close-btn">&times;</button>
        </div>
        <div class="modal-body">
          <div class="post-detail">
            <div class="detail-row">
              <label>แบรนด์:</label>
              <span>{{ selectedPost.brand?.name }}</span>
            </div>
            <div class="detail-row">
              <label>แพลตฟอร์ม:</label>
              <div class="platforms">
                <span v-for="p in selectedPost.platforms" :key="p" class="platform-badge" :class="p">
                  {{ p }}
                </span>
              </div>
            </div>
            <div class="detail-row">
              <label>ตั้งเวลา:</label>
              <span>{{ formatFullDateTime(selectedPost.scheduled_at) }}</span>
            </div>
            <div class="detail-row">
              <label>สถานะ:</label>
              <span class="status-badge" :class="selectedPost.status">
                {{ getStatusLabel(selectedPost.status) }}
              </span>
            </div>
            <div class="detail-row full-width">
              <label>เนื้อหา:</label>
              <p class="post-content-full">{{ selectedPost.content }}</p>
            </div>
            <div v-if="selectedPost.image_url" class="detail-row full-width">
              <label>รูปภาพ:</label>
              <img :src="selectedPost.image_url" class="post-image" />
            </div>
          </div>
        </div>
        <div class="modal-footer">
          <button @click="editPost(selectedPost)" class="btn btn-secondary">
            <i class="fas fa-edit"></i> แก้ไข
          </button>
          <button
            v-if="selectedPost.status === 'scheduled'"
            @click="reschedulePost(selectedPost)"
            class="btn btn-warning"
          >
            <i class="fas fa-clock"></i> เปลี่ยนเวลา
          </button>
          <button
            v-if="selectedPost.status === 'scheduled'"
            @click="cancelPost(selectedPost)"
            class="btn btn-danger"
          >
            <i class="fas fa-times"></i> ยกเลิก
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'

const props = defineProps({
  posts: { type: Array, default: () => [] },
  brands: { type: Array, default: () => [] }
})

const emit = defineEmits(['create', 'edit', 'reschedule', 'cancel', 'refresh'])

const platforms = ['facebook', 'instagram', 'tiktok', 'twitter', 'line', 'youtube', 'threads', 'linkedin', 'pinterest']
const hours = Array.from({ length: 24 }, (_, i) => i)

const view = ref('month')
const currentDate = ref(new Date())
const selectedDate = ref(new Date().toISOString().split('T')[0])
const selectedPost = ref(null)
const filterBrand = ref('')
const filterPlatform = ref('')
const draggedPost = ref(null)

const weekdayHeaders = ['อา', 'จ', 'อ', 'พ', 'พฤ', 'ศ', 'ส']

// Computed
const currentMonthYear = computed(() => {
  return currentDate.value.toLocaleDateString('th-TH', {
    year: 'numeric',
    month: 'long'
  })
})

const calendarDays = computed(() => {
  const year = currentDate.value.getFullYear()
  const month = currentDate.value.getMonth()

  const firstDay = new Date(year, month, 1)
  const lastDay = new Date(year, month + 1, 0)

  const startDate = new Date(firstDay)
  startDate.setDate(startDate.getDate() - firstDay.getDay())

  const days = []
  const today = new Date().toISOString().split('T')[0]

  for (let i = 0; i < 42; i++) {
    const date = new Date(startDate)
    date.setDate(date.getDate() + i)

    const dateStr = date.toISOString().split('T')[0]

    days.push({
      date: dateStr,
      dayNumber: date.getDate(),
      isCurrentMonth: date.getMonth() === month,
      isToday: dateStr === today
    })
  }

  return days
})

const weekDays = computed(() => {
  const startOfWeek = new Date(selectedDate.value)
  startOfWeek.setDate(startOfWeek.getDate() - startOfWeek.getDay())

  const today = new Date().toISOString().split('T')[0]
  const days = []

  for (let i = 0; i < 7; i++) {
    const date = new Date(startOfWeek)
    date.setDate(date.getDate() + i)
    const dateStr = date.toISOString().split('T')[0]

    days.push({
      date: dateStr,
      dayName: weekdayHeaders[i],
      dayNumber: date.getDate(),
      isToday: dateStr === today
    })
  }

  return days
})

const filteredPosts = computed(() => {
  let result = [...props.posts]

  if (filterBrand.value) {
    result = result.filter(p => p.brand_id === filterBrand.value)
  }

  if (filterPlatform.value) {
    result = result.filter(p => p.platforms.includes(filterPlatform.value))
  }

  return result
})

// Methods
function prevMonth() {
  currentDate.value = new Date(currentDate.value.setMonth(currentDate.value.getMonth() - 1))
}

function nextMonth() {
  currentDate.value = new Date(currentDate.value.setMonth(currentDate.value.getMonth() + 1))
}

function goToToday() {
  currentDate.value = new Date()
  selectedDate.value = new Date().toISOString().split('T')[0]
}

function selectDate(date) {
  selectedDate.value = date
}

function isSelectedDate(date) {
  return date === selectedDate.value
}

function getPostsForDate(date) {
  return filteredPosts.value.filter(p => {
    if (!p.scheduled_at) return false
    return p.scheduled_at.split('T')[0] === date
  })
}

function getPostsForHour(date, hour) {
  return getPostsForDate(date).filter(p => {
    const postHour = new Date(p.scheduled_at).getHours()
    return postHour === hour
  })
}

function getPostCount(date) {
  return getPostsForDate(date).length
}

function getPlatformClass(platform) {
  return `platform-${platform}`
}

function getPlatformIcon(platform) {
  const icons = {
    facebook: '📘',
    instagram: '📷',
    tiktok: '🎵',
    twitter: '🐦',
    line: '💚',
    youtube: '📺',
    threads: '🧵',
    linkedin: '💼',
    pinterest: '📌'
  }
  return icons[platform] || '📱'
}

function getStatusLabel(status) {
  const labels = {
    draft: 'แบบร่าง',
    scheduled: 'ตั้งเวลาแล้ว',
    publishing: 'กำลังเผยแพร่',
    published: 'เผยแพร่แล้ว',
    failed: 'ล้มเหลว'
  }
  return labels[status] || status
}

function formatTime(datetime) {
  if (!datetime) return ''
  return new Date(datetime).toLocaleTimeString('th-TH', {
    hour: '2-digit',
    minute: '2-digit'
  })
}

function formatFullDate(date) {
  return new Date(date).toLocaleDateString('th-TH', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  })
}

function formatFullDateTime(datetime) {
  if (!datetime) return ''
  return new Date(datetime).toLocaleString('th-TH', {
    weekday: 'short',
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

function truncate(text, length) {
  if (!text) return ''
  return text.length > length ? text.slice(0, length) + '...' : text
}

function getPostStyle(post) {
  const hour = new Date(post.scheduled_at).getHours()
  const minute = new Date(post.scheduled_at).getMinutes()
  return {
    top: `${(hour * 60 + minute) / 1440 * 100}%`
  }
}

function openPost(post) {
  selectedPost.value = post
}

function editPost(post) {
  emit('edit', post)
  selectedPost.value = null
}

function reschedulePost(post) {
  emit('reschedule', post)
  selectedPost.value = null
}

function cancelPost(post) {
  if (confirm('ต้องการยกเลิกโพสต์นี้หรือไม่?')) {
    emit('cancel', post)
    selectedPost.value = null
  }
}

function createPostAt(date, hour) {
  emit('create', { date, hour })
}

function showDayDetail(date) {
  selectedDate.value = date
  view.value = 'day'
}

// Drag and Drop
function onDragStart(event, post) {
  draggedPost.value = post
  event.dataTransfer.effectAllowed = 'move'
}

function onDrop(event, date) {
  if (draggedPost.value && draggedPost.value.status === 'scheduled') {
    const oldDate = draggedPost.value.scheduled_at.split('T')[0]
    if (oldDate !== date) {
      const time = draggedPost.value.scheduled_at.split('T')[1]
      emit('reschedule', {
        ...draggedPost.value,
        newScheduledAt: `${date}T${time}`
      })
    }
  }
  draggedPost.value = null
}

onMounted(() => {
  emit('refresh')
})
</script>

<style scoped>
.content-calendar {
  background: var(--card-bg, #fff);
  border-radius: 12px;
  overflow: hidden;
}

/* Header */
.calendar-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-color, #e0e0e0);
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-left h2 {
  margin: 0;
  min-width: 160px;
}

.nav-btn {
  width: 32px;
  height: 32px;
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 6px;
  background: none;
  cursor: pointer;
}

.today-btn {
  padding: 6px 12px;
  border: 1px solid var(--primary-color, #3498db);
  border-radius: 6px;
  background: none;
  color: var(--primary-color, #3498db);
  cursor: pointer;
  font-size: 13px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.view-toggle {
  display: flex;
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 6px;
  overflow: hidden;
}

.view-toggle button {
  padding: 6px 12px;
  border: none;
  background: none;
  cursor: pointer;
  font-size: 13px;
}

.view-toggle button.active {
  background: var(--primary-color, #3498db);
  color: white;
}

.brand-filter,
.platform-filter {
  padding: 6px 12px;
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 6px;
  font-size: 13px;
}

/* Month View */
.weekday-headers {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  background: var(--muted-bg, #f5f5f5);
}

.weekday-header {
  padding: 10px;
  text-align: center;
  font-weight: 600;
  font-size: 13px;
}

.calendar-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
}

.calendar-day {
  min-height: 100px;
  border: 1px solid var(--border-color, #e0e0e0);
  padding: 8px;
  cursor: pointer;
}

.calendar-day.other-month {
  background: var(--muted-bg, #f5f5f5);
  opacity: 0.5;
}

.calendar-day.today {
  background: var(--today-bg, #e3f2fd);
}

.calendar-day.selected {
  border-color: var(--primary-color, #3498db);
  border-width: 2px;
}

.day-header {
  display: flex;
  justify-content: space-between;
  margin-bottom: 6px;
}

.day-number {
  font-weight: 600;
  font-size: 14px;
}

.post-count {
  background: var(--primary-color, #3498db);
  color: white;
  font-size: 10px;
  padding: 2px 6px;
  border-radius: 10px;
}

.day-posts {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.post-item {
  padding: 4px 6px;
  border-radius: 4px;
  font-size: 11px;
  cursor: pointer;
  display: flex;
  gap: 4px;
  overflow: hidden;
}

.post-time {
  font-weight: 600;
  flex-shrink: 0;
}

.post-title {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

/* Platform Colors */
.platform-facebook { background: #1877f2; color: white; }
.platform-instagram { background: #e4405f; color: white; }
.platform-tiktok { background: #000000; color: white; }
.platform-twitter { background: #1da1f2; color: white; }
.platform-line { background: #00b900; color: white; }
.platform-youtube { background: #ff0000; color: white; }
.platform-threads { background: #000000; color: white; }
.platform-linkedin { background: #0077b5; color: white; }
.platform-pinterest { background: #bd081c; color: white; }

.more-posts {
  font-size: 11px;
  color: var(--primary-color, #3498db);
  cursor: pointer;
}

/* Week View */
.week-view {
  display: flex;
}

.time-column {
  width: 60px;
  flex-shrink: 0;
}

.time-header {
  height: 50px;
}

.time-slot {
  height: 60px;
  font-size: 11px;
  color: var(--text-muted, #666);
  text-align: right;
  padding-right: 8px;
}

.week-days {
  flex: 1;
  display: grid;
  grid-template-columns: repeat(7, 1fr);
}

.week-day {
  border-left: 1px solid var(--border-color, #e0e0e0);
}

.week-day-header {
  height: 50px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-bottom: 1px solid var(--border-color, #e0e0e0);
}

.week-day-header.today {
  background: var(--today-bg, #e3f2fd);
}

.week-day-content {
  position: relative;
  height: calc(60px * 24);
}

.week-post {
  position: absolute;
  left: 2px;
  right: 2px;
  padding: 4px;
  border-radius: 4px;
  font-size: 11px;
  cursor: pointer;
}

/* Day View */
.day-view-header {
  padding: 16px 20px;
  border-bottom: 1px solid var(--border-color, #e0e0e0);
}

.day-timeline {
  padding: 0 20px;
}

.hour-row {
  display: flex;
  min-height: 80px;
  border-bottom: 1px solid var(--border-color, #e0e0e0);
}

.hour-label {
  width: 60px;
  font-size: 12px;
  color: var(--text-muted, #666);
  padding-top: 8px;
}

.hour-content {
  flex: 1;
  padding: 8px;
}

.day-post {
  display: flex;
  gap: 12px;
  padding: 12px;
  background: var(--card-bg, #fff);
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 8px;
  margin-bottom: 8px;
  cursor: pointer;
}

.post-platforms {
  font-size: 20px;
}

.post-info {
  flex: 1;
}

.post-content {
  display: block;
  color: var(--text-muted, #666);
  font-size: 13px;
}

.post-brand {
  display: block;
  font-size: 12px;
  color: var(--text-muted, #666);
  margin-top: 4px;
}

.post-status {
  font-size: 12px;
  padding: 4px 8px;
  border-radius: 12px;
}

.post-status.scheduled { background: #fff3cd; color: #856404; }
.post-status.published { background: #d4edda; color: #155724; }
.post-status.failed { background: #f8d7da; color: #721c24; }

.empty-hour {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  height: 60px;
  border: 2px dashed var(--border-color, #e0e0e0);
  border-radius: 8px;
  color: var(--text-muted, #666);
  cursor: pointer;
  transition: all 0.2s;
}

.empty-hour:hover {
  border-color: var(--primary-color, #3498db);
  color: var(--primary-color, #3498db);
}

/* Modal */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal {
  background: var(--modal-bg, #fff);
  border-radius: 12px;
  width: 90%;
  max-width: 500px;
  max-height: 80vh;
  overflow-y: auto;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 20px;
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
  padding: 20px;
}

.detail-row {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}

.detail-row label {
  width: 100px;
  font-weight: 500;
  color: var(--text-muted, #666);
}

.detail-row.full-width {
  flex-direction: column;
}

.detail-row.full-width label {
  width: auto;
}

.platform-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 12px;
  margin-right: 4px;
}

.status-badge {
  display: inline-block;
  padding: 4px 12px;
  border-radius: 12px;
  font-size: 12px;
}

.post-content-full {
  white-space: pre-wrap;
  background: var(--muted-bg, #f5f5f5);
  padding: 12px;
  border-radius: 8px;
  margin: 8px 0 0;
}

.post-image {
  max-width: 100%;
  border-radius: 8px;
  margin-top: 8px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 16px 20px;
  border-top: 1px solid var(--border-color, #e0e0e0);
}

/* Buttons */
.btn {
  padding: 8px 16px;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  display: flex;
  align-items: center;
  gap: 6px;
}

.btn-secondary { background: var(--muted-bg, #e0e0e0); }
.btn-warning { background: #f39c12; color: white; }
.btn-danger { background: #e74c3c; color: white; }
</style>
