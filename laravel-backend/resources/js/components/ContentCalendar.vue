<template>
  <div class="content-calendar">
    <!-- Calendar Header -->
    <div class="calendar-header">
      <div class="header-left">
        <button @click="previousMonth" class="nav-btn">
          <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
            <path d="M15.41 7.41L14 6l-6 6 6 6 1.41-1.41L10.83 12z"/>
          </svg>
        </button>
        <h2 class="month-year">{{ monthYearText }}</h2>
        <button @click="nextMonth" class="nav-btn">
          <svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor">
            <path d="M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z"/>
          </svg>
        </button>
      </div>
      <div class="header-right">
        <button @click="goToToday" class="today-btn">วันนี้</button>
        <div class="view-toggle">
          <button
            :class="{ active: view === 'month' }"
            @click="view = 'month'">
            เดือน
          </button>
          <button
            :class="{ active: view === 'week' }"
            @click="view = 'week'">
            สัปดาห์
          </button>
        </div>
        <select v-model="selectedPlatform" class="platform-filter">
          <option value="">ทุกแพลตฟอร์ม</option>
          <option v-for="platform in platforms" :key="platform" :value="platform">
            {{ formatPlatformName(platform) }}
          </option>
        </select>
      </div>
    </div>

    <!-- Calendar Grid -->
    <div class="calendar-grid" v-if="view === 'month'">
      <!-- Day Headers -->
      <div class="day-headers">
        <div v-for="day in dayNames" :key="day" class="day-header">{{ day }}</div>
      </div>

      <!-- Calendar Days -->
      <div class="days-grid">
        <div
          v-for="day in calendarDays"
          :key="day.date"
          :class="['calendar-day', {
            'other-month': !day.isCurrentMonth,
            'today': day.isToday,
            'selected': day.date === selectedDate
          }]"
          @click="selectDate(day.date)">
          <span class="day-number">{{ day.dayNumber }}</span>

          <!-- Posts for this day -->
          <div class="day-posts" v-if="day.posts.length > 0">
            <div
              v-for="post in day.posts.slice(0, 3)"
              :key="post.id"
              :class="['post-indicator', `status-${post.status}`, `platform-${post.platform}`]"
              @click.stop="openPost(post)">
              <span class="post-time">{{ formatTime(post.scheduled_at) }}</span>
              <span class="post-platform-icon">{{ getPlatformIcon(post.platform) }}</span>
            </div>
            <div v-if="day.posts.length > 3" class="more-posts">
              +{{ day.posts.length - 3 }} โพสต์
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Week View -->
    <div class="week-view" v-else>
      <div class="time-labels">
        <div v-for="hour in hours" :key="hour" class="time-label">
          {{ hour }}:00
        </div>
      </div>
      <div class="week-days">
        <div
          v-for="day in weekDays"
          :key="day.date"
          :class="['week-day', { today: day.isToday }]">
          <div class="week-day-header">
            <span class="day-name">{{ day.dayName }}</span>
            <span class="day-num">{{ day.dayNumber }}</span>
          </div>
          <div class="day-timeline">
            <div
              v-for="post in day.posts"
              :key="post.id"
              :class="['timeline-post', `status-${post.status}`, `platform-${post.platform}`]"
              :style="getPostStyle(post)"
              @click="openPost(post)">
              <span class="post-title">{{ truncate(post.content_text, 30) }}</span>
              <span class="post-platform">{{ getPlatformIcon(post.platform) }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Post Detail Modal -->
    <div class="modal-overlay" v-if="selectedPost" @click="selectedPost = null">
      <div class="modal-content" @click.stop>
        <div class="modal-header">
          <h3>รายละเอียดโพสต์</h3>
          <button @click="selectedPost = null" class="close-btn">&times;</button>
        </div>
        <div class="modal-body">
          <div class="post-detail">
            <div class="detail-row">
              <span class="label">แพลตฟอร์ม</span>
              <span class="value platform-badge" :class="`platform-${selectedPost.platform}`">
                {{ getPlatformIcon(selectedPost.platform) }} {{ formatPlatformName(selectedPost.platform) }}
              </span>
            </div>
            <div class="detail-row">
              <span class="label">สถานะ</span>
              <span class="value status-badge" :class="`status-${selectedPost.status}`">
                {{ translateStatus(selectedPost.status) }}
              </span>
            </div>
            <div class="detail-row">
              <span class="label">กำหนดโพสต์</span>
              <span class="value">{{ formatDateTime(selectedPost.scheduled_at) }}</span>
            </div>
            <div class="detail-row" v-if="selectedPost.brand">
              <span class="label">แบรนด์</span>
              <span class="value">{{ selectedPost.brand.name }}</span>
            </div>
            <div class="content-preview">
              <span class="label">เนื้อหา</span>
              <p class="content-text">{{ selectedPost.content_text }}</p>
            </div>
            <div class="detail-actions">
              <button @click="editPost(selectedPost)" class="btn btn-primary">แก้ไข</button>
              <button @click="duplicatePost(selectedPost)" class="btn btn-secondary">ทำซ้ำ</button>
              <button @click="deletePost(selectedPost)" class="btn btn-danger">ลบ</button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Quick Add Button -->
    <button class="fab-add" @click="$emit('create-post', selectedDate)" title="สร้างโพสต์ใหม่">
      <svg viewBox="0 0 24 24" width="24" height="24" fill="currentColor">
        <path d="M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z"/>
      </svg>
    </button>
  </div>
</template>

<script>
export default {
  name: 'ContentCalendar',

  props: {
    posts: {
      type: Array,
      default: () => []
    },
    loading: {
      type: Boolean,
      default: false
    }
  },

  data() {
    return {
      currentDate: new Date(),
      selectedDate: null,
      selectedPost: null,
      view: 'month',
      selectedPlatform: '',
      dayNames: ['อา.', 'จ.', 'อ.', 'พ.', 'พฤ.', 'ศ.', 'ส.'],
      platforms: ['facebook', 'instagram', 'tiktok', 'twitter', 'line', 'youtube', 'threads', 'linkedin', 'pinterest'],
      hours: Array.from({ length: 24 }, (_, i) => i)
    }
  },

  computed: {
    monthYearText() {
      const months = [
        'มกราคม', 'กุมภาพันธ์', 'มีนาคม', 'เมษายน', 'พฤษภาคม', 'มิถุนายน',
        'กรกฎาคม', 'สิงหาคม', 'กันยายน', 'ตุลาคม', 'พฤศจิกายน', 'ธันวาคม'
      ]
      return `${months[this.currentDate.getMonth()]} ${this.currentDate.getFullYear() + 543}`
    },

    filteredPosts() {
      if (!this.selectedPlatform) return this.posts
      return this.posts.filter(p => p.platform === this.selectedPlatform)
    },

    calendarDays() {
      const days = []
      const year = this.currentDate.getFullYear()
      const month = this.currentDate.getMonth()

      // First day of the month
      const firstDay = new Date(year, month, 1)
      // Start from Sunday of the first week
      const startDate = new Date(firstDay)
      startDate.setDate(startDate.getDate() - firstDay.getDay())

      // Generate 42 days (6 weeks)
      for (let i = 0; i < 42; i++) {
        const date = new Date(startDate)
        date.setDate(date.getDate() + i)

        const dateStr = this.formatDateStr(date)
        const today = new Date()

        days.push({
          date: dateStr,
          dayNumber: date.getDate(),
          isCurrentMonth: date.getMonth() === month,
          isToday: dateStr === this.formatDateStr(today),
          posts: this.getPostsForDate(dateStr)
        })
      }

      return days
    },

    weekDays() {
      const days = []
      const dayNames = ['อาทิตย์', 'จันทร์', 'อังคาร', 'พุธ', 'พฤหัสบดี', 'ศุกร์', 'เสาร์']

      // Get start of week (Sunday)
      const startOfWeek = new Date(this.currentDate)
      startOfWeek.setDate(startOfWeek.getDate() - startOfWeek.getDay())

      for (let i = 0; i < 7; i++) {
        const date = new Date(startOfWeek)
        date.setDate(date.getDate() + i)

        const dateStr = this.formatDateStr(date)
        const today = new Date()

        days.push({
          date: dateStr,
          dayNumber: date.getDate(),
          dayName: dayNames[i],
          isToday: dateStr === this.formatDateStr(today),
          posts: this.getPostsForDate(dateStr)
        })
      }

      return days
    }
  },

  methods: {
    previousMonth() {
      const newDate = new Date(this.currentDate)
      newDate.setMonth(newDate.getMonth() - 1)
      this.currentDate = newDate
      this.$emit('month-change', this.currentDate)
    },

    nextMonth() {
      const newDate = new Date(this.currentDate)
      newDate.setMonth(newDate.getMonth() + 1)
      this.currentDate = newDate
      this.$emit('month-change', this.currentDate)
    },

    goToToday() {
      this.currentDate = new Date()
      this.selectedDate = this.formatDateStr(new Date())
      this.$emit('month-change', this.currentDate)
    },

    selectDate(date) {
      this.selectedDate = date
      this.$emit('date-select', date)
    },

    openPost(post) {
      this.selectedPost = post
    },

    editPost(post) {
      this.selectedPost = null
      this.$emit('edit-post', post)
    },

    duplicatePost(post) {
      this.selectedPost = null
      this.$emit('duplicate-post', post)
    },

    deletePost(post) {
      if (confirm('คุณต้องการลบโพสต์นี้หรือไม่?')) {
        this.selectedPost = null
        this.$emit('delete-post', post)
      }
    },

    getPostsForDate(dateStr) {
      return this.filteredPosts.filter(post => {
        if (!post.scheduled_at) return false
        const postDate = new Date(post.scheduled_at)
        return this.formatDateStr(postDate) === dateStr
      }).sort((a, b) => new Date(a.scheduled_at) - new Date(b.scheduled_at))
    },

    getPostStyle(post) {
      if (!post.scheduled_at) return {}
      const date = new Date(post.scheduled_at)
      const hour = date.getHours()
      const minutes = date.getMinutes()
      const top = (hour * 60 + minutes) * (50 / 60) // 50px per hour
      return {
        top: `${top}px`,
        height: '45px'
      }
    },

    formatDateStr(date) {
      return date.toISOString().split('T')[0]
    },

    formatTime(dateStr) {
      if (!dateStr) return ''
      const date = new Date(dateStr)
      return date.toLocaleTimeString('th-TH', { hour: '2-digit', minute: '2-digit' })
    },

    formatDateTime(dateStr) {
      if (!dateStr) return ''
      const date = new Date(dateStr)
      return date.toLocaleString('th-TH', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
      })
    },

    formatPlatformName(platform) {
      const names = {
        facebook: 'Facebook',
        instagram: 'Instagram',
        tiktok: 'TikTok',
        twitter: 'X (Twitter)',
        line: 'LINE',
        youtube: 'YouTube',
        threads: 'Threads',
        linkedin: 'LinkedIn',
        pinterest: 'Pinterest'
      }
      return names[platform] || platform
    },

    getPlatformIcon(platform) {
      const icons = {
        facebook: '📘',
        instagram: '📷',
        tiktok: '🎵',
        twitter: '🐦',
        line: '💚',
        youtube: '▶️',
        threads: '🧵',
        linkedin: '💼',
        pinterest: '📌'
      }
      return icons[platform] || '📱'
    },

    translateStatus(status) {
      const statuses = {
        draft: 'แบบร่าง',
        pending: 'รอดำเนินการ',
        scheduled: 'กำหนดเวลา',
        publishing: 'กำลังโพสต์',
        published: 'โพสต์แล้ว',
        failed: 'ล้มเหลว'
      }
      return statuses[status] || status
    },

    truncate(text, length) {
      if (!text) return ''
      return text.length > length ? text.substring(0, length) + '...' : text
    }
  }
}
</script>

<style scoped>
.content-calendar {
  background: #1e1e2e;
  border-radius: 16px;
  padding: 24px;
  color: white;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  position: relative;
}

/* Header */
.calendar-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
  flex-wrap: wrap;
  gap: 16px;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.nav-btn {
  background: rgba(255, 255, 255, 0.1);
  border: none;
  color: white;
  padding: 8px;
  border-radius: 8px;
  cursor: pointer;
  transition: background 0.2s;
}

.nav-btn:hover {
  background: rgba(255, 255, 255, 0.2);
}

.month-year {
  font-size: 20px;
  font-weight: 600;
  margin: 0;
  min-width: 200px;
  text-align: center;
}

.today-btn {
  background: #7c4dff;
  border: none;
  color: white;
  padding: 8px 16px;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 500;
}

.view-toggle {
  display: flex;
  background: rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  overflow: hidden;
}

.view-toggle button {
  background: none;
  border: none;
  color: #a0a0a0;
  padding: 8px 16px;
  cursor: pointer;
  transition: all 0.2s;
}

.view-toggle button.active {
  background: #7c4dff;
  color: white;
}

.platform-filter {
  background: rgba(255, 255, 255, 0.1);
  border: none;
  color: white;
  padding: 8px 12px;
  border-radius: 8px;
  cursor: pointer;
}

/* Calendar Grid */
.day-headers {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  margin-bottom: 8px;
}

.day-header {
  text-align: center;
  padding: 12px;
  color: #a0a0a0;
  font-size: 14px;
  font-weight: 500;
}

.days-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 4px;
}

.calendar-day {
  min-height: 100px;
  background: rgba(255, 255, 255, 0.03);
  border-radius: 8px;
  padding: 8px;
  cursor: pointer;
  transition: background 0.2s;
}

.calendar-day:hover {
  background: rgba(255, 255, 255, 0.08);
}

.calendar-day.other-month {
  opacity: 0.4;
}

.calendar-day.today {
  border: 2px solid #7c4dff;
}

.calendar-day.selected {
  background: rgba(124, 77, 255, 0.2);
}

.day-number {
  font-size: 14px;
  font-weight: 500;
  color: #e0e0e0;
}

.day-posts {
  margin-top: 8px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.post-indicator {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 4px 6px;
  border-radius: 4px;
  font-size: 11px;
  background: rgba(124, 77, 255, 0.3);
  cursor: pointer;
  transition: transform 0.1s;
}

.post-indicator:hover {
  transform: scale(1.02);
}

.post-indicator.status-published {
  background: rgba(76, 175, 80, 0.3);
}

.post-indicator.status-scheduled {
  background: rgba(33, 150, 243, 0.3);
}

.post-indicator.status-failed {
  background: rgba(244, 67, 54, 0.3);
}

.post-time {
  flex: 1;
}

.post-platform-icon {
  font-size: 12px;
}

.more-posts {
  font-size: 11px;
  color: #808080;
  text-align: center;
  padding: 4px;
}

/* Week View */
.week-view {
  display: flex;
  overflow-x: auto;
}

.time-labels {
  width: 60px;
  flex-shrink: 0;
  padding-top: 60px;
}

.time-label {
  height: 50px;
  font-size: 12px;
  color: #808080;
  text-align: right;
  padding-right: 8px;
}

.week-days {
  flex: 1;
  display: flex;
  gap: 4px;
  min-width: 0;
}

.week-day {
  flex: 1;
  min-width: 120px;
  background: rgba(255, 255, 255, 0.03);
  border-radius: 8px;
}

.week-day.today {
  border: 2px solid #7c4dff;
}

.week-day-header {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 12px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.day-name {
  font-size: 12px;
  color: #a0a0a0;
}

.day-num {
  font-size: 18px;
  font-weight: 600;
}

.day-timeline {
  position: relative;
  height: 1200px; /* 24 hours * 50px */
}

.timeline-post {
  position: absolute;
  left: 4px;
  right: 4px;
  padding: 4px 6px;
  border-radius: 4px;
  background: rgba(124, 77, 255, 0.4);
  font-size: 11px;
  overflow: hidden;
  cursor: pointer;
}

/* Modal */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal-content {
  background: #2a2a3e;
  border-radius: 16px;
  width: 90%;
  max-width: 500px;
  max-height: 80vh;
  overflow-y: auto;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 20px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.1);
}

.modal-header h3 {
  margin: 0;
}

.close-btn {
  background: none;
  border: none;
  color: white;
  font-size: 24px;
  cursor: pointer;
}

.modal-body {
  padding: 20px;
}

.post-detail .detail-row {
  display: flex;
  justify-content: space-between;
  padding: 12px 0;
  border-bottom: 1px solid rgba(255, 255, 255, 0.05);
}

.post-detail .label {
  color: #808080;
}

.post-detail .value {
  font-weight: 500;
}

.status-badge {
  padding: 4px 12px;
  border-radius: 20px;
  font-size: 12px;
}

.status-badge.status-published {
  background: rgba(76, 175, 80, 0.2);
  color: #4caf50;
}

.status-badge.status-scheduled {
  background: rgba(33, 150, 243, 0.2);
  color: #2196f3;
}

.status-badge.status-failed {
  background: rgba(244, 67, 54, 0.2);
  color: #f44336;
}

.content-preview {
  margin-top: 16px;
}

.content-preview .label {
  display: block;
  color: #808080;
  margin-bottom: 8px;
}

.content-text {
  background: rgba(255, 255, 255, 0.05);
  padding: 12px;
  border-radius: 8px;
  white-space: pre-wrap;
  line-height: 1.5;
}

.detail-actions {
  display: flex;
  gap: 12px;
  margin-top: 24px;
}

.btn {
  flex: 1;
  padding: 12px;
  border: none;
  border-radius: 8px;
  font-weight: 500;
  cursor: pointer;
  transition: opacity 0.2s;
}

.btn:hover {
  opacity: 0.9;
}

.btn-primary {
  background: #7c4dff;
  color: white;
}

.btn-secondary {
  background: rgba(255, 255, 255, 0.1);
  color: white;
}

.btn-danger {
  background: #f44336;
  color: white;
}

/* FAB */
.fab-add {
  position: absolute;
  bottom: 24px;
  right: 24px;
  width: 56px;
  height: 56px;
  border-radius: 28px;
  background: #7c4dff;
  border: none;
  color: white;
  cursor: pointer;
  box-shadow: 0 4px 12px rgba(124, 77, 255, 0.4);
  transition: transform 0.2s, box-shadow 0.2s;
}

.fab-add:hover {
  transform: scale(1.1);
  box-shadow: 0 6px 16px rgba(124, 77, 255, 0.5);
}

/* Responsive */
@media (max-width: 768px) {
  .calendar-header {
    flex-direction: column;
    align-items: stretch;
  }

  .header-left, .header-right {
    justify-content: center;
  }

  .calendar-day {
    min-height: 60px;
  }

  .post-indicator {
    font-size: 10px;
    padding: 2px 4px;
  }
}
</style>
