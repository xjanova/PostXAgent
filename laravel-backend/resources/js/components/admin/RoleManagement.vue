<template>
  <div class="role-management">
    <div class="header">
      <h2>จัดการ Roles & Permissions</h2>
      <button @click="showCreateModal = true" class="btn btn-primary">
        <i class="fas fa-plus"></i> สร้าง Role ใหม่
      </button>
    </div>

    <!-- Roles List -->
    <div class="roles-grid">
      <div v-for="role in roles" :key="role.id" class="role-card" :class="getRoleClass(role)">
        <div class="role-header">
          <h3>{{ role.name }}</h3>
          <span class="user-count">{{ role.users_count }} ผู้ใช้</span>
        </div>
        <div class="role-description">
          {{ role.description || 'ไม่มีคำอธิบาย' }}
        </div>
        <div class="role-permissions">
          <span v-for="perm in role.permissions.slice(0, 5)" :key="perm" class="permission-badge">
            {{ perm }}
          </span>
          <span v-if="role.permissions.length > 5" class="more-badge">
            +{{ role.permissions.length - 5 }} เพิ่มเติม
          </span>
        </div>
        <div class="role-actions">
          <button @click="editRole(role)" class="btn btn-sm btn-outline">
            <i class="fas fa-edit"></i> แก้ไข
          </button>
          <button
            @click="deleteRole(role)"
            class="btn btn-sm btn-danger"
            :disabled="isProtectedRole(role.name)"
          >
            <i class="fas fa-trash"></i> ลบ
          </button>
        </div>
      </div>
    </div>

    <!-- Permissions by Category -->
    <div class="permissions-section">
      <h3>รายการ Permissions ทั้งหมด</h3>
      <div class="permissions-grid">
        <div v-for="(perms, category) in permissionsByCategory" :key="category" class="permission-category">
          <h4>{{ formatCategory(category) }}</h4>
          <div class="permission-list">
            <span v-for="perm in perms" :key="perm" class="permission-item">
              {{ perm }}
            </span>
          </div>
        </div>
      </div>
    </div>

    <!-- Create/Edit Modal -->
    <div v-if="showCreateModal || editingRole" class="modal-overlay" @click.self="closeModal">
      <div class="modal">
        <div class="modal-header">
          <h3>{{ editingRole ? 'แก้ไข Role' : 'สร้าง Role ใหม่' }}</h3>
          <button @click="closeModal" class="close-btn">&times;</button>
        </div>
        <div class="modal-body">
          <div class="form-group">
            <label>ชื่อ Role</label>
            <input
              v-model="roleForm.name"
              type="text"
              class="form-control"
              :disabled="editingRole && isProtectedRole(editingRole.name)"
            />
          </div>
          <div class="form-group">
            <label>คำอธิบาย</label>
            <textarea v-model="roleForm.description" class="form-control"></textarea>
          </div>
          <div class="form-group">
            <label>Permissions</label>
            <div class="permissions-selector">
              <div v-for="(perms, category) in permissionsByCategory" :key="category" class="category-group">
                <div class="category-header">
                  <label class="checkbox-label">
                    <input
                      type="checkbox"
                      :checked="isCategorySelected(perms)"
                      @change="toggleCategory(perms)"
                    />
                    {{ formatCategory(category) }}
                  </label>
                </div>
                <div class="category-permissions">
                  <label v-for="perm in perms" :key="perm" class="checkbox-label">
                    <input
                      type="checkbox"
                      :value="perm"
                      v-model="roleForm.permissions"
                    />
                    {{ perm }}
                  </label>
                </div>
              </div>
            </div>
          </div>
        </div>
        <div class="modal-footer">
          <button @click="closeModal" class="btn btn-secondary">ยกเลิก</button>
          <button @click="saveRole" class="btn btn-primary" :disabled="saving">
            {{ saving ? 'กำลังบันทึก...' : 'บันทึก' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useToast } from '../composables/useToast'

const { showToast } = useToast()

const roles = ref([])
const permissions = ref({})
const showCreateModal = ref(false)
const editingRole = ref(null)
const saving = ref(false)

const roleForm = ref({
  name: '',
  description: '',
  permissions: []
})

const protectedRoles = ['super-admin', 'admin', 'user', 'premium', 'trial', 'moderator']

const permissionsByCategory = computed(() => permissions.value)

onMounted(async () => {
  await Promise.all([fetchRoles(), fetchPermissions()])
})

async function fetchRoles() {
  try {
    const response = await fetch('/api/v1/admin/roles', {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
    const data = await response.json()
    if (data.success) {
      roles.value = data.data
    }
  } catch (error) {
    showToast('ไม่สามารถโหลด Roles ได้', 'error')
  }
}

async function fetchPermissions() {
  try {
    const response = await fetch('/api/v1/admin/roles/permissions', {
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })
    const data = await response.json()
    if (data.success) {
      permissions.value = data.data
    }
  } catch (error) {
    showToast('ไม่สามารถโหลด Permissions ได้', 'error')
  }
}

function getRoleClass(role) {
  const classes = {
    'super-admin': 'role-superadmin',
    'admin': 'role-admin',
    'moderator': 'role-moderator',
    'premium': 'role-premium',
    'user': 'role-user',
    'trial': 'role-trial'
  }
  return classes[role.name] || ''
}

function isProtectedRole(name) {
  return protectedRoles.includes(name)
}

function formatCategory(category) {
  const labels = {
    brands: 'แบรนด์',
    posts: 'โพสต์',
    campaigns: 'แคมเปญ',
    accounts: 'บัญชี Social',
    analytics: 'วิเคราะห์',
    ai: 'AI',
    automation: 'ระบบอัตโนมัติ',
    comments: 'คอมเมนต์',
    pools: 'Account Pools',
    users: 'ผู้ใช้',
    payments: 'การชำระเงิน',
    system: 'ระบบ',
    roles: 'Roles'
  }
  return labels[category] || category
}

function editRole(role) {
  editingRole.value = role
  roleForm.value = {
    name: role.name,
    description: role.description || '',
    permissions: [...role.permissions]
  }
}

function closeModal() {
  showCreateModal.value = false
  editingRole.value = null
  roleForm.value = {
    name: '',
    description: '',
    permissions: []
  }
}

function isCategorySelected(perms) {
  return perms.every(p => roleForm.value.permissions.includes(p))
}

function toggleCategory(perms) {
  if (isCategorySelected(perms)) {
    roleForm.value.permissions = roleForm.value.permissions.filter(p => !perms.includes(p))
  } else {
    const newPerms = perms.filter(p => !roleForm.value.permissions.includes(p))
    roleForm.value.permissions.push(...newPerms)
  }
}

async function saveRole() {
  saving.value = true
  try {
    const url = editingRole.value
      ? `/api/v1/admin/roles/${editingRole.value.id}`
      : '/api/v1/admin/roles'

    const response = await fetch(url, {
      method: editingRole.value ? 'PUT' : 'POST',
      headers: {
        'Authorization': `Bearer ${localStorage.getItem('token')}`,
        'Content-Type': 'application/json'
      },
      body: JSON.stringify(roleForm.value)
    })

    const data = await response.json()
    if (data.success) {
      showToast(data.message, 'success')
      closeModal()
      await fetchRoles()
    } else {
      showToast(data.error || 'เกิดข้อผิดพลาด', 'error')
    }
  } catch (error) {
    showToast('ไม่สามารถบันทึกได้', 'error')
  } finally {
    saving.value = false
  }
}

async function deleteRole(role) {
  if (!confirm(`ต้องการลบ Role "${role.name}" หรือไม่?`)) return

  try {
    const response = await fetch(`/api/v1/admin/roles/${role.id}`, {
      method: 'DELETE',
      headers: { 'Authorization': `Bearer ${localStorage.getItem('token')}` }
    })

    const data = await response.json()
    if (data.success) {
      showToast('ลบ Role สำเร็จ', 'success')
      await fetchRoles()
    } else {
      showToast(data.error || 'ไม่สามารถลบได้', 'error')
    }
  } catch (error) {
    showToast('เกิดข้อผิดพลาด', 'error')
  }
}
</script>

<style scoped>
.role-management {
  padding: 20px;
}

.header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.roles-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 20px;
  margin-bottom: 40px;
}

.role-card {
  background: var(--card-bg, #fff);
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 8px;
  padding: 16px;
}

.role-superadmin { border-left: 4px solid #e74c3c; }
.role-admin { border-left: 4px solid #3498db; }
.role-moderator { border-left: 4px solid #9b59b6; }
.role-premium { border-left: 4px solid #f39c12; }
.role-user { border-left: 4px solid #2ecc71; }
.role-trial { border-left: 4px solid #95a5a6; }

.role-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}

.role-header h3 {
  margin: 0;
  font-size: 18px;
}

.user-count {
  font-size: 12px;
  color: var(--text-muted, #666);
}

.role-description {
  font-size: 14px;
  color: var(--text-muted, #666);
  margin-bottom: 12px;
}

.role-permissions {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin-bottom: 12px;
}

.permission-badge {
  background: var(--badge-bg, #e8f4fc);
  color: var(--badge-color, #2980b9);
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 11px;
}

.more-badge {
  background: var(--muted-bg, #f5f5f5);
  color: var(--text-muted, #666);
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 11px;
}

.role-actions {
  display: flex;
  gap: 8px;
}

.permissions-section h3 {
  margin-bottom: 16px;
}

.permissions-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
  gap: 16px;
}

.permission-category {
  background: var(--card-bg, #fff);
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 8px;
  padding: 12px;
}

.permission-category h4 {
  margin: 0 0 8px 0;
  font-size: 14px;
  color: var(--text-muted, #666);
}

.permission-list {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.permission-item {
  font-size: 13px;
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

.modal-footer {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  padding: 16px;
  border-top: 1px solid var(--border-color, #e0e0e0);
}

.form-group {
  margin-bottom: 16px;
}

.form-group label {
  display: block;
  margin-bottom: 4px;
  font-weight: 500;
}

.form-control {
  width: 100%;
  padding: 8px 12px;
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 4px;
}

textarea.form-control {
  min-height: 80px;
}

.permissions-selector {
  max-height: 300px;
  overflow-y: auto;
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 4px;
}

.category-group {
  border-bottom: 1px solid var(--border-color, #e0e0e0);
}

.category-group:last-child {
  border-bottom: none;
}

.category-header {
  background: var(--muted-bg, #f5f5f5);
  padding: 8px 12px;
}

.category-permissions {
  padding: 8px 12px;
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 4px;
}

.checkbox-label {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  font-size: 13px;
}

/* Button styles */
.btn {
  padding: 8px 16px;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  font-size: 14px;
}

.btn-primary {
  background: #3498db;
  color: white;
}

.btn-secondary {
  background: #95a5a6;
  color: white;
}

.btn-danger {
  background: #e74c3c;
  color: white;
}

.btn-outline {
  background: transparent;
  border: 1px solid currentColor;
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
