<template>
  <div class="workflow-gallery">
    <!-- Header -->
    <div class="gallery-header">
      <div>
        <h1 class="text-2xl font-bold">Workflow Templates</h1>
        <p class="text-gray-600">เลือก Template สำเร็จรูปเพื่อเริ่มต้นใช้งานได้ทันที</p>
      </div>
      <button @click="$emit('create-custom')" class="btn-primary">
        <svg class="w-5 h-5 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
        </svg>
        สร้าง Workflow ใหม่
      </button>
    </div>

    <!-- Category Tabs -->
    <div class="category-tabs">
      <button
        v-for="cat in categories"
        :key="cat.key"
        :class="['tab-btn', { active: selectedCategory === cat.key }]"
        @click="selectedCategory = cat.key"
      >
        <component :is="cat.icon" class="w-5 h-5 mr-2" />
        {{ cat.name_th }}
      </button>
    </div>

    <!-- Templates Grid -->
    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <span>กำลังโหลด Templates...</span>
    </div>

    <div v-else-if="filteredTemplates.length === 0" class="empty-state">
      <svg class="w-16 h-16 text-gray-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="1" d="M9 12h6m-6 4h6m2 5H7a2 2 0 01-2-2V5a2 2 0 012-2h5.586a1 1 0 01.707.293l5.414 5.414a1 1 0 01.293.707V19a2 2 0 01-2 2z" />
      </svg>
      <p>ไม่พบ Template ในหมวดนี้</p>
    </div>

    <div v-else class="templates-grid">
      <div
        v-for="template in filteredTemplates"
        :key="template.id"
        class="template-card"
        @click="selectTemplate(template)"
      >
        <div class="template-icon" :style="{ backgroundColor: getCategoryColor(template.category) + '20' }">
          <span :style="{ color: getCategoryColor(template.category) }">{{ template.icon }}</span>
        </div>
        <div class="template-info">
          <h3 class="template-name">{{ template.localized_name }}</h3>
          <p class="template-description">{{ template.localized_description }}</p>
          <div class="template-meta">
            <div class="platforms">
              <img
                v-for="platform in template.supported_platforms.slice(0, 5)"
                :key="platform"
                :src="getPlatformIcon(platform)"
                :alt="platform"
                class="platform-icon"
              />
              <span v-if="template.supported_platforms.length > 5" class="more-platforms">
                +{{ template.supported_platforms.length - 5 }}
              </span>
            </div>
            <div class="template-stats">
              <span class="stat">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
                </svg>
                {{ formatNumber(template.use_count) }}
              </span>
              <span class="stat" :class="{ 'text-green-600': template.avg_success_rate >= 70 }">
                <svg class="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
                </svg>
                {{ template.avg_success_rate.toFixed(0) }}%
              </span>
            </div>
          </div>
        </div>
        <div class="template-action">
          <button class="btn-use" @click.stop="useTemplate(template)">
            ใช้งาน
          </button>
        </div>
      </div>
    </div>

    <!-- Template Detail Modal -->
    <div v-if="selectedTemplate" class="modal-overlay" @click.self="selectedTemplate = null">
      <div class="modal-content modal-large">
        <div class="modal-header">
          <div class="modal-title-row">
            <div class="template-icon-lg" :style="{ backgroundColor: getCategoryColor(selectedTemplate.category) + '20' }">
              <span :style="{ color: getCategoryColor(selectedTemplate.category) }">{{ selectedTemplate.icon }}</span>
            </div>
            <div>
              <h2 class="text-xl font-bold">{{ selectedTemplate.localized_name }}</h2>
              <p class="text-gray-600">{{ getCategoryName(selectedTemplate.category) }}</p>
            </div>
          </div>
          <button @click="selectedTemplate = null" class="btn-close">&times;</button>
        </div>

        <div class="modal-body">
          <p class="mb-6">{{ selectedTemplate.localized_description }}</p>

          <!-- Supported Platforms -->
          <div class="detail-section">
            <h4 class="section-title">แพลตฟอร์มที่รองรับ</h4>
            <div class="platform-list">
              <div v-for="platform in selectedTemplate.supported_platforms" :key="platform" class="platform-item">
                <img :src="getPlatformIcon(platform)" :alt="platform" class="w-6 h-6" />
                <span>{{ getPlatformName(platform) }}</span>
              </div>
            </div>
          </div>

          <!-- Variables -->
          <div v-if="selectedTemplate.variables && Object.keys(selectedTemplate.variables).length > 0" class="detail-section">
            <h4 class="section-title">ตัวแปรที่ต้องกำหนด</h4>
            <div class="variables-list">
              <div v-for="(variable, key) in selectedTemplate.variables" :key="key" class="variable-item">
                <div class="variable-info">
                  <span class="variable-key">{{ key }}</span>
                  <span class="variable-label">{{ variable.label_th || variable.label }}</span>
                </div>
                <span :class="['variable-type', `type-${variable.type}`]">{{ variable.type }}</span>
              </div>
            </div>
          </div>

          <!-- Statistics -->
          <div class="detail-section">
            <h4 class="section-title">สถิติการใช้งาน</h4>
            <div class="stats-row">
              <div class="stat-box">
                <span class="stat-value">{{ formatNumber(selectedTemplate.use_count) }}</span>
                <span class="stat-label">ครั้งที่ใช้</span>
              </div>
              <div class="stat-box">
                <span :class="['stat-value', { 'text-green-600': selectedTemplate.avg_success_rate >= 70 }]">
                  {{ selectedTemplate.avg_success_rate.toFixed(1) }}%
                </span>
                <span class="stat-label">อัตราสำเร็จ</span>
              </div>
            </div>
          </div>
        </div>

        <div class="modal-footer">
          <button @click="selectedTemplate = null" class="btn-outline">ปิด</button>
          <button @click="previewWorkflow(selectedTemplate)" class="btn-outline">
            <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M15 12a3 3 0 11-6 0 3 3 0 016 0z" />
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M2.458 12C3.732 7.943 7.523 5 12 5c4.478 0 8.268 2.943 9.542 7-1.274 4.057-5.064 7-9.542 7-4.477 0-8.268-2.943-9.542-7z" />
            </svg>
            ดู Workflow
          </button>
          <button @click="useTemplate(selectedTemplate)" class="btn-primary">
            <svg class="w-4 h-4 mr-2" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 6v6m0 0v6m0-6h6m-6 0H6" />
            </svg>
            ใช้งาน Template นี้
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'

const emit = defineEmits(['select-template', 'create-custom', 'use-template', 'preview-workflow'])

// State
const loading = ref(true)
const templates = ref([])
const selectedCategory = ref('all')
const selectedTemplate = ref(null)

// Categories
const categories = [
  { key: 'all', name: 'All', name_th: 'ทั้งหมด', icon: 'GridIcon' },
  { key: 'marketing', name: 'Marketing', name_th: 'การตลาด', icon: 'CampaignIcon' },
  { key: 'content', name: 'Content', name_th: 'เนื้อหา', icon: 'ArticleIcon' },
  { key: 'engagement', name: 'Engagement', name_th: 'การมีส่วนร่วม', icon: 'ForumIcon' },
  { key: 'seek_and_post', name: 'Seek and Post', name_th: 'ค้นหาและโพสต์', icon: 'ExploreIcon' },
  { key: 'platform_specific', name: 'Platform', name_th: 'เฉพาะแพลตฟอร์ม', icon: 'DevicesIcon' },
  { key: 'special', name: 'Special', name_th: 'พิเศษ', icon: 'StarIcon' },
]

const categoryColors = {
  marketing: '#3b82f6',
  content: '#10b981',
  engagement: '#f59e0b',
  seek_and_post: '#8b5cf6',
  platform_specific: '#ec4899',
  special: '#ef4444',
}

// Computed
const filteredTemplates = computed(() => {
  if (selectedCategory.value === 'all') {
    return templates.value
  }
  return templates.value.filter(t => t.category === selectedCategory.value)
})

// Methods
const fetchTemplates = async () => {
  try {
    const response = await fetch('/api/v1/workflow-templates')
    const data = await response.json()
    if (data.success) {
      templates.value = data.data || []
    }
  } catch (error) {
    console.error('Failed to fetch templates:', error)
  } finally {
    loading.value = false
  }
}

const selectTemplate = (template) => {
  selectedTemplate.value = template
  emit('select-template', template)
}

const useTemplate = (template) => {
  emit('use-template', template)
}

const previewWorkflow = (template) => {
  emit('preview-workflow', template)
}

const getCategoryColor = (category) => {
  return categoryColors[category] || '#6b7280'
}

const getCategoryName = (category) => {
  const cat = categories.find(c => c.key === category)
  return cat?.name_th || category
}

const getPlatformIcon = (platform) => {
  const icons = {
    facebook: '/images/platforms/facebook.svg',
    instagram: '/images/platforms/instagram.svg',
    tiktok: '/images/platforms/tiktok.svg',
    twitter: '/images/platforms/twitter.svg',
    line: '/images/platforms/line.svg',
    youtube: '/images/platforms/youtube.svg',
    threads: '/images/platforms/threads.svg',
    linkedin: '/images/platforms/linkedin.svg',
    pinterest: '/images/platforms/pinterest.svg',
  }
  return icons[platform] || '/images/platforms/default.svg'
}

const getPlatformName = (platform) => {
  const names = {
    facebook: 'Facebook',
    instagram: 'Instagram',
    tiktok: 'TikTok',
    twitter: 'Twitter/X',
    line: 'LINE',
    youtube: 'YouTube',
    threads: 'Threads',
    linkedin: 'LinkedIn',
    pinterest: 'Pinterest',
  }
  return names[platform] || platform
}

const formatNumber = (num) => {
  if (num >= 1000000) return (num / 1000000).toFixed(1) + 'M'
  if (num >= 1000) return (num / 1000).toFixed(1) + 'K'
  return num.toString()
}

// Lifecycle
onMounted(() => {
  fetchTemplates()
})
</script>

<style scoped>
.workflow-gallery {
  padding: 24px;
  max-width: 1400px;
  margin: 0 auto;
}

.gallery-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.category-tabs {
  display: flex;
  gap: 8px;
  margin-bottom: 24px;
  flex-wrap: wrap;
}

.tab-btn {
  display: flex;
  align-items: center;
  padding: 10px 16px;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
  background: white;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.2s;
}

.tab-btn:hover {
  border-color: #3b82f6;
}

.tab-btn.active {
  background: linear-gradient(135deg, #3b82f6, #8b5cf6);
  color: white;
  border-color: transparent;
}

.templates-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
  gap: 20px;
}

.template-card {
  background: white;
  border-radius: 12px;
  border: 1px solid #e5e7eb;
  padding: 20px;
  cursor: pointer;
  transition: all 0.2s;
  display: flex;
  gap: 16px;
}

.template-card:hover {
  border-color: #3b82f6;
  box-shadow: 0 4px 12px rgba(59, 130, 246, 0.15);
}

.template-icon {
  width: 56px;
  height: 56px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
  flex-shrink: 0;
}

.template-info {
  flex: 1;
  min-width: 0;
}

.template-name {
  font-weight: 600;
  font-size: 16px;
  margin-bottom: 4px;
}

.template-description {
  font-size: 13px;
  color: #6b7280;
  margin-bottom: 12px;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.template-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.platforms {
  display: flex;
  align-items: center;
  gap: 4px;
}

.platform-icon {
  width: 20px;
  height: 20px;
  border-radius: 4px;
}

.more-platforms {
  font-size: 11px;
  color: #6b7280;
}

.template-stats {
  display: flex;
  gap: 12px;
}

.template-stats .stat {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: #6b7280;
}

.template-action {
  display: flex;
  align-items: center;
}

.btn-use {
  padding: 8px 16px;
  background: linear-gradient(135deg, #3b82f6, #8b5cf6);
  color: white;
  border: none;
  border-radius: 8px;
  font-weight: 500;
  cursor: pointer;
  white-space: nowrap;
}

.btn-use:hover {
  opacity: 0.9;
}

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
  border-radius: 16px;
  max-width: 600px;
  width: 90%;
  max-height: 90vh;
  overflow-y: auto;
}

.modal-large {
  max-width: 700px;
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 24px;
  border-bottom: 1px solid #e5e7eb;
}

.modal-title-row {
  display: flex;
  align-items: center;
  gap: 16px;
}

.template-icon-lg {
  width: 64px;
  height: 64px;
  border-radius: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 28px;
}

.modal-body {
  padding: 24px;
}

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
  padding: 16px 24px;
  border-top: 1px solid #e5e7eb;
}

.detail-section {
  margin-bottom: 24px;
}

.section-title {
  font-weight: 600;
  margin-bottom: 12px;
  color: #374151;
}

.platform-list {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}

.platform-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 12px;
  background: #f9fafb;
  border-radius: 8px;
  font-size: 13px;
}

.variables-list {
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  overflow: hidden;
}

.variable-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 16px;
  border-bottom: 1px solid #e5e7eb;
}

.variable-item:last-child {
  border-bottom: none;
}

.variable-key {
  font-family: monospace;
  background: #f3f4f6;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 12px;
  margin-right: 8px;
}

.variable-label {
  color: #6b7280;
  font-size: 13px;
}

.variable-type {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 4px;
  text-transform: uppercase;
}

.type-text { background: #dbeafe; color: #1d4ed8; }
.type-textarea { background: #dbeafe; color: #1d4ed8; }
.type-number { background: #d1fae5; color: #059669; }
.type-select { background: #fef3c7; color: #d97706; }
.type-multiselect { background: #fce7f3; color: #db2777; }
.type-boolean { background: #e0e7ff; color: #4f46e5; }

.stats-row {
  display: flex;
  gap: 24px;
}

.stat-box {
  flex: 1;
  text-align: center;
  padding: 16px;
  background: #f9fafb;
  border-radius: 8px;
}

.stat-box .stat-value {
  display: block;
  font-size: 28px;
  font-weight: 700;
}

.stat-box .stat-label {
  font-size: 13px;
  color: #6b7280;
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
}

.btn-outline {
  background: white;
  border: 1px solid #d1d5db;
  padding: 10px 20px;
  border-radius: 8px;
  cursor: pointer;
  display: inline-flex;
  align-items: center;
}

.btn-close {
  background: none;
  border: none;
  font-size: 28px;
  cursor: pointer;
  color: #6b7280;
  line-height: 1;
}

/* States */
.loading-state, .empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 64px;
  color: #6b7280;
}

.spinner {
  width: 48px;
  height: 48px;
  border: 3px solid #e5e7eb;
  border-top-color: #3b82f6;
  border-radius: 50%;
  animation: spin 1s linear infinite;
  margin-bottom: 16px;
}

@keyframes spin {
  to { transform: rotate(360deg); }
}
</style>
