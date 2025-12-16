<template>
    <div class="space-y-6">
        <div class="flex items-center justify-between">
            <h1 class="text-2xl font-bold text-white">จัดการผู้ใช้</h1>
            <div class="flex space-x-3">
                <input v-model="search" type="text" placeholder="ค้นหาผู้ใช้..."
                    class="px-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white" />
            </div>
        </div>

        <div class="bg-gray-800 rounded-xl border border-gray-700 overflow-hidden">
            <table class="w-full">
                <thead class="bg-gray-700/50">
                    <tr>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">ผู้ใช้</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">อีเมล</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">แพ็กเกจ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">สถานะ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">สมัครเมื่อ</th>
                        <th class="px-4 py-3 text-left text-sm font-medium text-gray-300">จัดการ</th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-gray-700">
                    <tr v-for="user in users" :key="user.id" class="hover:bg-gray-700/50">
                        <td class="px-4 py-3">
                            <div class="flex items-center">
                                <div class="w-8 h-8 rounded-full bg-indigo-600 flex items-center justify-center mr-3">
                                    <span class="text-white text-sm">{{ user.name[0] }}</span>
                                </div>
                                <span class="text-white">{{ user.name }}</span>
                            </div>
                        </td>
                        <td class="px-4 py-3 text-gray-300">{{ user.email }}</td>
                        <td class="px-4 py-3">
                            <span v-if="user.active_rental" class="px-2 py-1 bg-indigo-500/20 text-indigo-400 text-xs rounded-full">
                                {{ user.active_rental }}
                            </span>
                            <span v-else class="text-gray-500 text-sm">ไม่มี</span>
                        </td>
                        <td class="px-4 py-3">
                            <span :class="['px-2 py-1 text-xs rounded-full', user.is_active ? 'bg-green-500/20 text-green-400' : 'bg-red-500/20 text-red-400']">
                                {{ user.is_active ? 'Active' : 'Inactive' }}
                            </span>
                        </td>
                        <td class="px-4 py-3 text-gray-400 text-sm">{{ formatDate(user.created_at) }}</td>
                        <td class="px-4 py-3">
                            <div class="flex space-x-2">
                                <button class="text-gray-400 hover:text-white" @click="viewUser(user)">ดู</button>
                                <button class="text-gray-400 hover:text-white" @click="editUser(user)">แก้ไข</button>
                                <button v-if="user.is_active" class="text-red-400 hover:text-red-300" @click="suspendUser(user)">ระงับ</button>
                                <button v-else class="text-green-400 hover:text-green-300" @click="activateUser(user)">เปิดใช้</button>
                            </div>
                        </td>
                    </tr>
                </tbody>
            </table>

            <div v-if="users.length === 0" class="p-12 text-center">
                <p class="text-gray-400">ไม่พบผู้ใช้</p>
            </div>
        </div>

        <!-- Pagination -->
        <div class="flex items-center justify-between">
            <p class="text-gray-400 text-sm">แสดง {{ users.length }} จาก {{ totalUsers }} รายการ</p>
            <div class="flex space-x-2">
                <button :disabled="currentPage === 1" class="px-3 py-1 bg-gray-700 text-gray-300 rounded disabled:opacity-50" @click="prevPage">&larr; ก่อนหน้า</button>
                <button :disabled="currentPage >= totalPages" class="px-3 py-1 bg-gray-700 text-gray-300 rounded disabled:opacity-50" @click="nextPage">ถัดไป &rarr;</button>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue'
import { adminApi } from '../../services/api'

const users = ref([])
const search = ref('')
const currentPage = ref(1)
const totalUsers = ref(0)
const totalPages = ref(1)

const formatDate = (date) => new Date(date).toLocaleDateString('th-TH')

const fetchUsers = async () => {
    try {
        const response = await adminApi.users({ search: search.value, page: currentPage.value, per_page: 20 })
        if (response.data.success) {
            users.value = response.data.data
            totalUsers.value = response.data.meta?.total || 0
            totalPages.value = response.data.meta?.last_page || 1
        }
    } catch (error) {
        console.error('Failed to fetch users:', error)
    }
}

const viewUser = (user) => console.log('View user:', user)
const editUser = (user) => console.log('Edit user:', user)
const suspendUser = async (user) => {
    if (confirm(`ระงับผู้ใช้ ${user.name}?`)) {
        await adminApi.updateUser(user.id, { is_active: false })
        fetchUsers()
    }
}
const activateUser = async (user) => {
    await adminApi.updateUser(user.id, { is_active: true })
    fetchUsers()
}

const prevPage = () => { if (currentPage.value > 1) { currentPage.value--; fetchUsers() } }
const nextPage = () => { if (currentPage.value < totalPages.value) { currentPage.value++; fetchUsers() } }

watch(search, () => { currentPage.value = 1; fetchUsers() })
onMounted(fetchUsers)
</script>
