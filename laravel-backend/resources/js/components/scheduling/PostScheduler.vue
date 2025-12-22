<template>
  <div class="post-scheduler">
    <div class="scheduler-header">
      <h3>{{ $t('scheduling.title') }}</h3>
      <div class="timezone-selector">
        <label>{{ $t('scheduling.timezone') }}:</label>
        <select v-model="selectedTimezone" @change="onTimezoneChange">
          <option v-for="tz in popularTimezones" :key="tz.value" :value="tz.value">
            {{ tz.label }} ({{ tz.offset }})
          </option>
        </select>
      </div>
    </div>

    <!-- Schedule Type -->
    <div class="schedule-type">
      <label class="radio-card" :class="{ active: scheduleType === 'now' }">
        <input type="radio" v-model="scheduleType" value="now" />
        <div class="radio-content">
          <i class="fas fa-bolt"></i>
          <span>{{ $t('scheduling.publishNow') }}</span>
        </div>
      </label>

      <label class="radio-card" :class="{ active: scheduleType === 'later' }">
        <input type="radio" v-model="scheduleType" value="later" />
        <div class="radio-content">
          <i class="fas fa-clock"></i>
          <span>{{ $t('scheduling.scheduleLater') }}</span>
        </div>
      </label>

      <label class="radio-card" :class="{ active: scheduleType === 'recurring' }">
        <input type="radio" v-model="scheduleType" value="recurring" />
        <div class="radio-content">
          <i class="fas fa-sync"></i>
          <span>{{ $t('scheduling.recurring') }}</span>
        </div>
      </label>
    </div>

    <!-- Date Time Picker (for 'later' and 'recurring') -->
    <div v-if="scheduleType !== 'now'" class="datetime-section">
      <div class="datetime-picker">
        <div class="form-group">
          <label>{{ $t('scheduling.date') }}</label>
          <input
            type="date"
            v-model="scheduleDate"
            :min="minDate"
            class="form-control"
          />
        </div>
        <div class="form-group">
          <label>{{ $t('scheduling.time') }}</label>
          <input
            type="time"
            v-model="scheduleTime"
            class="form-control"
          />
        </div>
      </div>

      <!-- Quick Time Slots -->
      <div class="quick-slots">
        <span class="slots-label">{{ $t('scheduling.quickSlots') }}:</span>
        <button
          v-for="slot in quickTimeSlots"
          :key="slot.value"
          @click="setQuickTime(slot)"
          class="slot-btn"
          :class="{ active: isSlotActive(slot) }"
        >
          {{ slot.label }}
        </button>
      </div>

      <!-- Optimal Times -->
      <div v-if="optimalTimes.length > 0" class="optimal-times">
        <div class="optimal-header">
          <i class="fas fa-star"></i>
          <span>{{ $t('scheduling.optimalTimes') }}</span>
        </div>
        <div class="optimal-slots">
          <button
            v-for="time in optimalTimes"
            :key="time.datetime"
            @click="setOptimalTime(time)"
            class="optimal-btn"
          >
            <span class="time">{{ formatTime(time.datetime) }}</span>
            <span class="engagement">{{ time.engagement }}% engagement</span>
          </button>
        </div>
      </div>
    </div>

    <!-- Recurring Options -->
    <div v-if="scheduleType === 'recurring'" class="recurring-section">
      <div class="form-group">
        <label>{{ $t('scheduling.repeatEvery') }}</label>
        <div class="repeat-selector">
          <input type="number" v-model="repeatInterval" min="1" max="30" class="form-control interval-input" />
          <select v-model="repeatUnit" class="form-control">
            <option value="day">{{ $t('scheduling.days') }}</option>
            <option value="week">{{ $t('scheduling.weeks') }}</option>
            <option value="month">{{ $t('scheduling.months') }}</option>
          </select>
        </div>
      </div>

      <!-- Days of Week (for weekly) -->
      <div v-if="repeatUnit === 'week'" class="weekdays-selector">
        <label>{{ $t('scheduling.onDays') }}:</label>
        <div class="weekdays">
          <label
            v-for="day in weekdays"
            :key="day.value"
            class="weekday-btn"
            :class="{ active: selectedDays.includes(day.value) }"
          >
            <input type="checkbox" :value="day.value" v-model="selectedDays" />
            <span>{{ day.short }}</span>
          </label>
        </div>
      </div>

      <!-- End Date -->
      <div class="form-group">
        <label>{{ $t('scheduling.endDate') }}</label>
        <div class="end-options">
          <label class="radio-inline">
            <input type="radio" v-model="endType" value="never" />
            {{ $t('scheduling.never') }}
          </label>
          <label class="radio-inline">
            <input type="radio" v-model="endType" value="date" />
            {{ $t('scheduling.onDate') }}
          </label>
          <label class="radio-inline">
            <input type="radio" v-model="endType" value="occurrences" />
            {{ $t('scheduling.afterOccurrences') }}
          </label>
        </div>
        <input
          v-if="endType === 'date'"
          type="date"
          v-model="endDate"
          :min="scheduleDate"
          class="form-control mt-10"
        />
        <input
          v-if="endType === 'occurrences'"
          type="number"
          v-model="occurrences"
          min="1"
          max="100"
          class="form-control mt-10"
          style="width: 100px;"
        />
      </div>
    </div>

    <!-- Preview -->
    <div class="schedule-preview">
      <div class="preview-header">
        <i class="fas fa-calendar-check"></i>
        <span>{{ $t('scheduling.preview') }}</span>
      </div>
      <div class="preview-content">
        <p v-if="scheduleType === 'now'">
          {{ $t('scheduling.willPublishNow') }}
        </p>
        <p v-else-if="scheduleType === 'later'">
          {{ $t('scheduling.willPublishAt') }} <strong>{{ formattedScheduleTime }}</strong>
          <span class="local-time">({{ localTimeLabel }})</span>
        </p>
        <div v-else-if="scheduleType === 'recurring'">
          <p>{{ recurringDescription }}</p>
          <div v-if="nextOccurrences.length > 0" class="next-occurrences">
            <span class="label">{{ $t('scheduling.nextPosts') }}:</span>
            <ul>
              <li v-for="(occ, idx) in nextOccurrences.slice(0, 5)" :key="idx">
                {{ formatDateTime(occ) }}
              </li>
            </ul>
          </div>
        </div>
      </div>
    </div>

    <!-- Actions -->
    <div class="scheduler-actions">
      <button @click="$emit('cancel')" class="btn btn-secondary">
        {{ $t('common.cancel') }}
      </button>
      <button @click="confirm" class="btn btn-primary" :disabled="!isValid">
        <i class="fas fa-check"></i>
        {{ scheduleType === 'now' ? $t('scheduling.publishNow') : $t('scheduling.schedule') }}
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'

const props = defineProps({
  initialDate: { type: String, default: null },
  platforms: { type: Array, default: () => [] }
})

const emit = defineEmits(['confirm', 'cancel'])

// Timezone data
const popularTimezones = [
  { value: 'Asia/Bangkok', label: 'Bangkok', offset: 'UTC+7' },
  { value: 'Asia/Tokyo', label: 'Tokyo', offset: 'UTC+9' },
  { value: 'Asia/Singapore', label: 'Singapore', offset: 'UTC+8' },
  { value: 'Asia/Hong_Kong', label: 'Hong Kong', offset: 'UTC+8' },
  { value: 'Asia/Seoul', label: 'Seoul', offset: 'UTC+9' },
  { value: 'Europe/London', label: 'London', offset: 'UTC+0' },
  { value: 'Europe/Paris', label: 'Paris', offset: 'UTC+1' },
  { value: 'America/New_York', label: 'New York', offset: 'UTC-5' },
  { value: 'America/Los_Angeles', label: 'Los Angeles', offset: 'UTC-8' },
  { value: 'Australia/Sydney', label: 'Sydney', offset: 'UTC+11' },
]

const weekdays = [
  { value: 0, short: 'อา', full: 'อาทิตย์' },
  { value: 1, short: 'จ', full: 'จันทร์' },
  { value: 2, short: 'อ', full: 'อังคาร' },
  { value: 3, short: 'พ', full: 'พุธ' },
  { value: 4, short: 'พฤ', full: 'พฤหัส' },
  { value: 5, short: 'ศ', full: 'ศุกร์' },
  { value: 6, short: 'ส', full: 'เสาร์' },
]

const quickTimeSlots = [
  { label: '09:00', value: '09:00' },
  { label: '12:00', value: '12:00' },
  { label: '18:00', value: '18:00' },
  { label: '20:00', value: '20:00' },
  { label: '21:00', value: '21:00' },
]

// State
const selectedTimezone = ref('Asia/Bangkok')
const scheduleType = ref('now')
const scheduleDate = ref('')
const scheduleTime = ref('09:00')
const repeatInterval = ref(1)
const repeatUnit = ref('day')
const selectedDays = ref([1, 3, 5]) // Mon, Wed, Fri
const endType = ref('never')
const endDate = ref('')
const occurrences = ref(10)
const optimalTimes = ref([])

// Computed
const minDate = computed(() => {
  return new Date().toISOString().split('T')[0]
})

const formattedScheduleTime = computed(() => {
  if (!scheduleDate.value || !scheduleTime.value) return ''
  const date = new Date(`${scheduleDate.value}T${scheduleTime.value}`)
  return date.toLocaleString('th-TH', {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
})

const localTimeLabel = computed(() => {
  const tz = popularTimezones.find(t => t.value === selectedTimezone.value)
  return tz ? `${tz.label} ${tz.offset}` : ''
})

const recurringDescription = computed(() => {
  let desc = `โพสต์ทุก ${repeatInterval.value} `

  if (repeatUnit.value === 'day') {
    desc += repeatInterval.value === 1 ? 'วัน' : 'วัน'
  } else if (repeatUnit.value === 'week') {
    desc += repeatInterval.value === 1 ? 'สัปดาห์' : 'สัปดาห์'
    if (selectedDays.value.length > 0) {
      const dayNames = selectedDays.value
        .sort()
        .map(d => weekdays.find(w => w.value === d)?.full)
        .join(', ')
      desc += ` วัน${dayNames}`
    }
  } else if (repeatUnit.value === 'month') {
    desc += repeatInterval.value === 1 ? 'เดือน' : 'เดือน'
  }

  desc += ` เวลา ${scheduleTime.value}`

  if (endType.value === 'date' && endDate.value) {
    desc += ` จนถึงวันที่ ${formatDate(endDate.value)}`
  } else if (endType.value === 'occurrences') {
    desc += ` จำนวน ${occurrences.value} ครั้ง`
  }

  return desc
})

const nextOccurrences = computed(() => {
  if (scheduleType.value !== 'recurring') return []

  const occList = []
  let current = new Date(`${scheduleDate.value}T${scheduleTime.value}`)
  const maxOccurrences = endType.value === 'occurrences' ? occurrences.value : 10
  const endDateObj = endType.value === 'date' ? new Date(endDate.value) : null

  for (let i = 0; i < maxOccurrences && occList.length < 10; i++) {
    if (endDateObj && current > endDateObj) break

    if (repeatUnit.value === 'week') {
      // For weekly, check if day matches
      if (selectedDays.value.includes(current.getDay())) {
        occList.push(new Date(current))
      }
    } else {
      occList.push(new Date(current))
    }

    // Advance to next occurrence
    if (repeatUnit.value === 'day') {
      current.setDate(current.getDate() + repeatInterval.value)
    } else if (repeatUnit.value === 'week') {
      current.setDate(current.getDate() + 1)
    } else if (repeatUnit.value === 'month') {
      current.setMonth(current.getMonth() + repeatInterval.value)
    }
  }

  return occList
})

const isValid = computed(() => {
  if (scheduleType.value === 'now') return true
  if (!scheduleDate.value || !scheduleTime.value) return false

  const scheduledDateTime = new Date(`${scheduleDate.value}T${scheduleTime.value}`)
  if (scheduledDateTime <= new Date()) return false

  if (scheduleType.value === 'recurring') {
    if (repeatUnit.value === 'week' && selectedDays.value.length === 0) return false
    if (endType.value === 'date' && !endDate.value) return false
  }

  return true
})

// Methods
function onTimezoneChange() {
  // Could convert times when timezone changes
  fetchOptimalTimes()
}

function setQuickTime(slot) {
  scheduleTime.value = slot.value
  if (!scheduleDate.value) {
    scheduleDate.value = minDate.value
  }
}

function isSlotActive(slot) {
  return scheduleTime.value === slot.value
}

function setOptimalTime(time) {
  const date = new Date(time.datetime)
  scheduleDate.value = date.toISOString().split('T')[0]
  scheduleTime.value = date.toTimeString().slice(0, 5)
}

function formatTime(datetime) {
  return new Date(datetime).toLocaleTimeString('th-TH', {
    hour: '2-digit',
    minute: '2-digit'
  })
}

function formatDate(dateStr) {
  return new Date(dateStr).toLocaleDateString('th-TH', {
    year: 'numeric',
    month: 'long',
    day: 'numeric'
  })
}

function formatDateTime(date) {
  return date.toLocaleString('th-TH', {
    weekday: 'short',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit'
  })
}

async function fetchOptimalTimes() {
  // Fetch AI-recommended optimal posting times based on platforms
  try {
    const response = await fetch('/api/v1/analytics/optimal-times', {
      method: 'POST',
      headers: {
        'Authorization': `Bearer ${localStorage.getItem('token')}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({
        platforms: props.platforms,
        timezone: selectedTimezone.value
      })
    })
    const data = await response.json()
    if (data.success) {
      optimalTimes.value = data.data
    }
  } catch (error) {
    console.error('Failed to fetch optimal times:', error)
  }
}

function confirm() {
  const result = {
    type: scheduleType.value,
    timezone: selectedTimezone.value,
  }

  if (scheduleType.value === 'now') {
    result.scheduled_at = null
  } else {
    result.scheduled_at = `${scheduleDate.value}T${scheduleTime.value}:00`

    if (scheduleType.value === 'recurring') {
      result.recurring = {
        interval: repeatInterval.value,
        unit: repeatUnit.value,
        days: repeatUnit.value === 'week' ? selectedDays.value : null,
        end_type: endType.value,
        end_date: endType.value === 'date' ? endDate.value : null,
        occurrences: endType.value === 'occurrences' ? occurrences.value : null
      }
    }
  }

  emit('confirm', result)
}

// i18n helper (simplified)
function $t(key) {
  const locale = localStorage.getItem('locale') || 'th'
  const translations = {
    th: {
      'scheduling.title': 'ตั้งเวลาโพสต์',
      'scheduling.timezone': 'เขตเวลา',
      'scheduling.publishNow': 'โพสต์ทันที',
      'scheduling.scheduleLater': 'ตั้งเวลา',
      'scheduling.recurring': 'โพสต์ซ้ำ',
      'scheduling.date': 'วันที่',
      'scheduling.time': 'เวลา',
      'scheduling.quickSlots': 'เวลาแนะนำ',
      'scheduling.optimalTimes': 'เวลาที่ดีที่สุด',
      'scheduling.repeatEvery': 'โพสต์ซ้ำทุก',
      'scheduling.days': 'วัน',
      'scheduling.weeks': 'สัปดาห์',
      'scheduling.months': 'เดือน',
      'scheduling.onDays': 'ในวัน',
      'scheduling.endDate': 'สิ้นสุด',
      'scheduling.never': 'ไม่มีกำหนด',
      'scheduling.onDate': 'วันที่กำหนด',
      'scheduling.afterOccurrences': 'หลังจากโพสต์',
      'scheduling.preview': 'ตัวอย่าง',
      'scheduling.willPublishNow': 'จะเผยแพร่ทันทีเมื่อกดยืนยัน',
      'scheduling.willPublishAt': 'จะเผยแพร่ในวันที่',
      'scheduling.nextPosts': 'โพสต์ถัดไป',
      'scheduling.schedule': 'ตั้งเวลา',
      'common.cancel': 'ยกเลิก',
    },
    en: {
      'scheduling.title': 'Schedule Post',
      'scheduling.timezone': 'Timezone',
      'scheduling.publishNow': 'Publish Now',
      'scheduling.scheduleLater': 'Schedule',
      'scheduling.recurring': 'Recurring',
      'scheduling.date': 'Date',
      'scheduling.time': 'Time',
      'scheduling.quickSlots': 'Quick slots',
      'scheduling.optimalTimes': 'Best times',
      'scheduling.repeatEvery': 'Repeat every',
      'scheduling.days': 'days',
      'scheduling.weeks': 'weeks',
      'scheduling.months': 'months',
      'scheduling.onDays': 'On days',
      'scheduling.endDate': 'End',
      'scheduling.never': 'Never',
      'scheduling.onDate': 'On date',
      'scheduling.afterOccurrences': 'After occurrences',
      'scheduling.preview': 'Preview',
      'scheduling.willPublishNow': 'Will publish immediately when confirmed',
      'scheduling.willPublishAt': 'Will publish on',
      'scheduling.nextPosts': 'Next posts',
      'scheduling.schedule': 'Schedule',
      'common.cancel': 'Cancel',
    }
  }
  const keys = key.split('.')
  let result = translations[locale]
  for (const k of keys) {
    result = result?.[k]
  }
  return result || key
}

// Initialize
onMounted(() => {
  if (props.initialDate) {
    const date = new Date(props.initialDate)
    scheduleDate.value = date.toISOString().split('T')[0]
    scheduleTime.value = date.toTimeString().slice(0, 5)
    scheduleType.value = 'later'
  } else {
    scheduleDate.value = minDate.value
  }

  // Detect user timezone
  const userTimezone = Intl.DateTimeFormat().resolvedOptions().timeZone
  if (popularTimezones.find(tz => tz.value === userTimezone)) {
    selectedTimezone.value = userTimezone
  }

  fetchOptimalTimes()
})
</script>

<style scoped>
.post-scheduler {
  padding: 20px;
  background: var(--card-bg, #fff);
  border-radius: 12px;
}

.scheduler-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.scheduler-header h3 {
  margin: 0;
}

.timezone-selector {
  display: flex;
  align-items: center;
  gap: 8px;
}

.timezone-selector label {
  font-size: 13px;
  color: var(--text-muted, #666);
}

.timezone-selector select {
  padding: 6px 10px;
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 6px;
  font-size: 13px;
}

/* Schedule Type */
.schedule-type {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
  margin-bottom: 24px;
}

.radio-card {
  cursor: pointer;
}

.radio-card input {
  display: none;
}

.radio-content {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 16px;
  border: 2px solid var(--border-color, #e0e0e0);
  border-radius: 8px;
  transition: all 0.2s;
}

.radio-card.active .radio-content {
  border-color: var(--primary-color, #3498db);
  background: var(--primary-bg, #e8f4fc);
}

.radio-content i {
  font-size: 24px;
  color: var(--text-muted, #666);
}

.radio-card.active .radio-content i {
  color: var(--primary-color, #3498db);
}

/* DateTime Picker */
.datetime-section {
  margin-bottom: 24px;
}

.datetime-picker {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
  margin-bottom: 16px;
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  margin-bottom: 6px;
  font-weight: 500;
  font-size: 14px;
}

.form-control {
  width: 100%;
  padding: 10px 12px;
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 6px;
  font-size: 14px;
}

/* Quick Slots */
.quick-slots {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
  margin-bottom: 16px;
}

.slots-label {
  font-size: 13px;
  color: var(--text-muted, #666);
}

.slot-btn {
  padding: 6px 12px;
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 16px;
  background: none;
  cursor: pointer;
  font-size: 13px;
  transition: all 0.2s;
}

.slot-btn:hover {
  border-color: var(--primary-color, #3498db);
}

.slot-btn.active {
  background: var(--primary-color, #3498db);
  color: white;
  border-color: var(--primary-color, #3498db);
}

/* Optimal Times */
.optimal-times {
  background: var(--success-bg, #e8f5e9);
  border-radius: 8px;
  padding: 12px;
}

.optimal-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 10px;
  font-weight: 500;
  color: var(--success-color, #27ae60);
}

.optimal-slots {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.optimal-btn {
  display: flex;
  flex-direction: column;
  padding: 8px 12px;
  border: 1px solid var(--success-color, #27ae60);
  border-radius: 6px;
  background: white;
  cursor: pointer;
  transition: all 0.2s;
}

.optimal-btn:hover {
  background: var(--success-color, #27ae60);
  color: white;
}

.optimal-btn .time {
  font-weight: 600;
}

.optimal-btn .engagement {
  font-size: 11px;
  color: var(--text-muted, #666);
}

.optimal-btn:hover .engagement {
  color: rgba(255, 255, 255, 0.8);
}

/* Recurring Section */
.recurring-section {
  background: var(--muted-bg, #f5f5f5);
  border-radius: 8px;
  padding: 16px;
  margin-bottom: 24px;
}

.repeat-selector {
  display: flex;
  gap: 8px;
}

.interval-input {
  width: 80px;
}

.weekdays-selector {
  margin-top: 16px;
}

.weekdays {
  display: flex;
  gap: 8px;
  margin-top: 8px;
}

.weekday-btn {
  cursor: pointer;
}

.weekday-btn input {
  display: none;
}

.weekday-btn span {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 36px;
  height: 36px;
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 50%;
  font-size: 12px;
  transition: all 0.2s;
}

.weekday-btn.active span {
  background: var(--primary-color, #3498db);
  color: white;
  border-color: var(--primary-color, #3498db);
}

.end-options {
  display: flex;
  gap: 16px;
  margin-bottom: 8px;
}

.radio-inline {
  display: flex;
  align-items: center;
  gap: 4px;
  cursor: pointer;
  font-size: 14px;
}

.mt-10 {
  margin-top: 10px;
}

/* Preview */
.schedule-preview {
  background: var(--info-bg, #e3f2fd);
  border-radius: 8px;
  padding: 16px;
  margin-bottom: 24px;
}

.preview-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 10px;
  font-weight: 500;
  color: var(--info-color, #1976d2);
}

.preview-content p {
  margin: 0;
}

.local-time {
  font-size: 12px;
  color: var(--text-muted, #666);
}

.next-occurrences {
  margin-top: 12px;
}

.next-occurrences .label {
  font-size: 12px;
  color: var(--text-muted, #666);
}

.next-occurrences ul {
  margin: 8px 0 0;
  padding-left: 20px;
}

.next-occurrences li {
  font-size: 13px;
  margin-bottom: 4px;
}

/* Actions */
.scheduler-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}

.btn {
  padding: 10px 20px;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  transition: all 0.2s;
}

.btn-primary {
  background: var(--primary-color, #3498db);
  color: white;
}

.btn-primary:hover {
  background: var(--primary-hover, #2980b9);
}

.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.btn-secondary {
  background: var(--muted-bg, #f5f5f5);
  color: var(--text-color, #333);
}

.btn-secondary:hover {
  background: var(--muted-hover, #e8e8e8);
}

/* Responsive */
@media (max-width: 600px) {
  .schedule-type {
    grid-template-columns: 1fr;
  }

  .datetime-picker {
    grid-template-columns: 1fr;
  }

  .scheduler-header {
    flex-direction: column;
    gap: 12px;
    align-items: flex-start;
  }
}
</style>
