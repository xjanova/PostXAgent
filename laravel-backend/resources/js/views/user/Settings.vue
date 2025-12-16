<template>
    <div class="max-w-4xl mx-auto space-y-6">
        <!-- Header -->
        <div>
            <h1 class="text-2xl font-bold text-white">ตั้งค่าบัญชี</h1>
            <p class="text-gray-400 mt-1">จัดการข้อมูลส่วนตัวและการตั้งค่าต่างๆ</p>
        </div>

        <!-- Tabs -->
        <div class="border-b border-gray-700">
            <nav class="flex space-x-8">
                <button
                    v-for="tab in tabs"
                    :key="tab.id"
                    :class="[
                        'py-4 px-1 border-b-2 font-medium text-sm transition-colors',
                        activeTab === tab.id
                            ? 'border-indigo-500 text-indigo-400'
                            : 'border-transparent text-gray-400 hover:text-gray-300'
                    ]"
                    @click="activeTab = tab.id"
                >
                    {{ tab.label }}
                </button>
            </nav>
        </div>

        <!-- Profile Settings -->
        <div v-show="activeTab === 'profile'" class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-6">ข้อมูลส่วนตัว</h2>

            <form @submit.prevent="updateProfile" class="space-y-5">
                <!-- Avatar -->
                <div class="flex items-center space-x-6">
                    <div class="w-20 h-20 rounded-full bg-indigo-600 flex items-center justify-center">
                        <span class="text-2xl font-bold text-white">{{ userInitials }}</span>
                    </div>
                    <div>
                        <button
                            type="button"
                            class="px-4 py-2 bg-gray-700 hover:bg-gray-600 text-white rounded-lg transition-colors text-sm"
                        >
                            เปลี่ยนรูปโปรไฟล์
                        </button>
                        <p class="text-gray-500 text-xs mt-1">JPG, PNG ไม่เกิน 2MB</p>
                    </div>
                </div>

                <!-- Name -->
                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-1">ชื่อ-นามสกุล</label>
                    <input
                        v-model="profileForm.name"
                        type="text"
                        class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    />
                </div>

                <!-- Email (read-only) -->
                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-1">อีเมล</label>
                    <input
                        :value="authStore.user?.email"
                        type="email"
                        class="w-full px-4 py-3 bg-gray-600 border border-gray-600 rounded-lg text-gray-400 cursor-not-allowed"
                        disabled
                    />
                    <p class="text-gray-500 text-xs mt-1">ไม่สามารถเปลี่ยนอีเมลได้</p>
                </div>

                <!-- Phone -->
                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-1">เบอร์โทรศัพท์</label>
                    <input
                        v-model="profileForm.phone"
                        type="tel"
                        class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                        placeholder="08X-XXX-XXXX"
                    />
                </div>

                <!-- Company -->
                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-1">บริษัท/องค์กร (ไม่บังคับ)</label>
                    <input
                        v-model="profileForm.company_name"
                        type="text"
                        class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    />
                </div>

                <!-- Timezone -->
                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-1">เขตเวลา</label>
                    <select
                        v-model="profileForm.timezone"
                        class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    >
                        <option value="Asia/Bangkok">เวลาประเทศไทย (GMT+7)</option>
                        <option value="Asia/Singapore">เวลาสิงคโปร์ (GMT+8)</option>
                        <option value="UTC">UTC (GMT+0)</option>
                    </select>
                </div>

                <!-- Language -->
                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-1">ภาษา</label>
                    <select
                        v-model="profileForm.language"
                        class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                    >
                        <option value="th">ไทย</option>
                        <option value="en">English</option>
                    </select>
                </div>

                <!-- Save Button -->
                <div class="flex justify-end">
                    <button
                        type="submit"
                        :disabled="profileLoading"
                        class="px-6 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg transition-colors disabled:opacity-50"
                    >
                        {{ profileLoading ? 'กำลังบันทึก...' : 'บันทึกการเปลี่ยนแปลง' }}
                    </button>
                </div>
            </form>
        </div>

        <!-- Password Settings -->
        <div v-show="activeTab === 'password'" class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-6">เปลี่ยนรหัสผ่าน</h2>

            <form @submit.prevent="updatePassword" class="space-y-5 max-w-md">
                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-1">รหัสผ่านปัจจุบัน</label>
                    <input
                        v-model="passwordForm.current_password"
                        type="password"
                        class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                        required
                    />
                </div>

                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-1">รหัสผ่านใหม่</label>
                    <input
                        v-model="passwordForm.password"
                        type="password"
                        class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                        placeholder="อย่างน้อย 8 ตัวอักษร"
                        required
                    />
                </div>

                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-1">ยืนยันรหัสผ่านใหม่</label>
                    <input
                        v-model="passwordForm.password_confirmation"
                        type="password"
                        class="w-full px-4 py-3 bg-gray-700 border border-gray-600 rounded-lg text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
                        required
                    />
                </div>

                <div v-if="passwordError" class="p-3 bg-red-500/20 border border-red-500/50 rounded-lg">
                    <p class="text-sm text-red-400">{{ passwordError }}</p>
                </div>

                <div v-if="passwordSuccess" class="p-3 bg-green-500/20 border border-green-500/50 rounded-lg">
                    <p class="text-sm text-green-400">เปลี่ยนรหัสผ่านสำเร็จ</p>
                </div>

                <button
                    type="submit"
                    :disabled="passwordLoading"
                    class="px-6 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg transition-colors disabled:opacity-50"
                >
                    {{ passwordLoading ? 'กำลังบันทึก...' : 'เปลี่ยนรหัสผ่าน' }}
                </button>
            </form>
        </div>

        <!-- Notifications Settings -->
        <div v-show="activeTab === 'notifications'" class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-6">การแจ้งเตือน</h2>

            <div class="space-y-6">
                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-white font-medium">แจ้งเตือนทางอีเมล</p>
                        <p class="text-gray-400 text-sm">รับอีเมลแจ้งเตือนกิจกรรมสำคัญ</p>
                    </div>
                    <button
                        :class="[
                            'relative inline-flex h-6 w-11 items-center rounded-full transition-colors',
                            notifications.email ? 'bg-indigo-600' : 'bg-gray-600'
                        ]"
                        @click="notifications.email = !notifications.email"
                    >
                        <span
                            :class="[
                                'inline-block h-4 w-4 transform rounded-full bg-white transition-transform',
                                notifications.email ? 'translate-x-6' : 'translate-x-1'
                            ]"
                        ></span>
                    </button>
                </div>

                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-white font-medium">แจ้งเตือนโพสต์สำเร็จ</p>
                        <p class="text-gray-400 text-sm">รับการแจ้งเตือนเมื่อโพสต์สำเร็จ</p>
                    </div>
                    <button
                        :class="[
                            'relative inline-flex h-6 w-11 items-center rounded-full transition-colors',
                            notifications.postSuccess ? 'bg-indigo-600' : 'bg-gray-600'
                        ]"
                        @click="notifications.postSuccess = !notifications.postSuccess"
                    >
                        <span
                            :class="[
                                'inline-block h-4 w-4 transform rounded-full bg-white transition-transform',
                                notifications.postSuccess ? 'translate-x-6' : 'translate-x-1'
                            ]"
                        ></span>
                    </button>
                </div>

                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-white font-medium">แจ้งเตือนโพสต์ล้มเหลว</p>
                        <p class="text-gray-400 text-sm">รับการแจ้งเตือนเมื่อโพสต์ล้มเหลว</p>
                    </div>
                    <button
                        :class="[
                            'relative inline-flex h-6 w-11 items-center rounded-full transition-colors',
                            notifications.postFailed ? 'bg-indigo-600' : 'bg-gray-600'
                        ]"
                        @click="notifications.postFailed = !notifications.postFailed"
                    >
                        <span
                            :class="[
                                'inline-block h-4 w-4 transform rounded-full bg-white transition-transform',
                                notifications.postFailed ? 'translate-x-6' : 'translate-x-1'
                            ]"
                        ></span>
                    </button>
                </div>

                <div class="flex items-center justify-between">
                    <div>
                        <p class="text-white font-medium">แจ้งเตือนแพ็กเกจใกล้หมดอายุ</p>
                        <p class="text-gray-400 text-sm">รับการแจ้งเตือนก่อนแพ็กเกจหมดอายุ 3 วัน</p>
                    </div>
                    <button
                        :class="[
                            'relative inline-flex h-6 w-11 items-center rounded-full transition-colors',
                            notifications.packageExpiring ? 'bg-indigo-600' : 'bg-gray-600'
                        ]"
                        @click="notifications.packageExpiring = !notifications.packageExpiring"
                    >
                        <span
                            :class="[
                                'inline-block h-4 w-4 transform rounded-full bg-white transition-transform',
                                notifications.packageExpiring ? 'translate-x-6' : 'translate-x-1'
                            ]"
                        ></span>
                    </button>
                </div>
            </div>
        </div>

        <!-- API Settings -->
        <div v-show="activeTab === 'api'" class="bg-gray-800 rounded-xl p-6 border border-gray-700">
            <h2 class="text-lg font-semibold text-white mb-6">API Access</h2>

            <div v-if="!authStore.canAccess('api_access')" class="text-center py-8">
                <LockClosedIcon class="w-12 h-12 text-gray-500 mx-auto mb-4" />
                <p class="text-gray-400">ต้องอัพเกรดเป็นแพ็กเกจ Quarterly ขึ้นไปเพื่อใช้ API</p>
                <router-link
                    to="/subscription"
                    class="inline-block mt-4 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-lg transition-colors"
                >
                    อัพเกรดแพ็กเกจ
                </router-link>
            </div>

            <div v-else class="space-y-6">
                <div>
                    <label class="block text-sm font-medium text-gray-300 mb-2">API Key</label>
                    <div class="flex">
                        <input
                            :type="showApiKey ? 'text' : 'password'"
                            :value="apiKey"
                            class="flex-1 px-4 py-3 bg-gray-700 border border-gray-600 rounded-l-lg text-white font-mono text-sm"
                            readonly
                        />
                        <button
                            type="button"
                            class="px-4 bg-gray-600 border border-gray-600 border-l-0"
                            @click="showApiKey = !showApiKey"
                        >
                            <EyeIcon v-if="!showApiKey" class="w-5 h-5 text-gray-300" />
                            <EyeSlashIcon v-else class="w-5 h-5 text-gray-300" />
                        </button>
                        <button
                            type="button"
                            class="px-4 bg-gray-600 border border-gray-600 border-l-0 rounded-r-lg"
                            @click="copyApiKey"
                        >
                            <ClipboardIcon class="w-5 h-5 text-gray-300" />
                        </button>
                    </div>
                    <p class="text-gray-500 text-xs mt-2">อย่าเปิดเผย API Key ของคุณกับผู้อื่น</p>
                </div>

                <button
                    type="button"
                    class="px-4 py-2 bg-red-600/20 hover:bg-red-600/30 text-red-400 rounded-lg transition-colors text-sm"
                >
                    รีเจนเนอเรท API Key
                </button>
            </div>
        </div>

        <!-- Danger Zone -->
        <div v-show="activeTab === 'danger'" class="bg-red-900/20 rounded-xl p-6 border border-red-900/50">
            <h2 class="text-lg font-semibold text-red-400 mb-6">Danger Zone</h2>

            <div class="space-y-6">
                <div class="flex items-center justify-between p-4 bg-gray-800 rounded-lg">
                    <div>
                        <p class="text-white font-medium">ลบบัญชี</p>
                        <p class="text-gray-400 text-sm">การดำเนินการนี้ไม่สามารถย้อนกลับได้</p>
                    </div>
                    <button
                        class="px-4 py-2 bg-red-600 hover:bg-red-700 text-white rounded-lg transition-colors text-sm"
                        @click="confirmDeleteAccount"
                    >
                        ลบบัญชี
                    </button>
                </div>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, reactive, computed, onMounted } from 'vue'
import { useAuthStore } from '../../stores/auth'
import {
    EyeIcon,
    EyeSlashIcon,
    ClipboardIcon,
    LockClosedIcon,
} from '@heroicons/vue/24/outline'

const authStore = useAuthStore()

const tabs = [
    { id: 'profile', label: 'โปรไฟล์' },
    { id: 'password', label: 'รหัสผ่าน' },
    { id: 'notifications', label: 'การแจ้งเตือน' },
    { id: 'api', label: 'API' },
    { id: 'danger', label: 'Danger Zone' },
]

const activeTab = ref('profile')

const userInitials = computed(() => {
    const name = authStore.user?.name || ''
    return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2)
})

// Profile Form
const profileForm = reactive({
    name: '',
    phone: '',
    company_name: '',
    timezone: 'Asia/Bangkok',
    language: 'th',
})
const profileLoading = ref(false)

// Password Form
const passwordForm = reactive({
    current_password: '',
    password: '',
    password_confirmation: '',
})
const passwordLoading = ref(false)
const passwordError = ref('')
const passwordSuccess = ref(false)

// Notifications
const notifications = reactive({
    email: true,
    postSuccess: true,
    postFailed: true,
    packageExpiring: true,
})

// API
const apiKey = ref('pxa_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx')
const showApiKey = ref(false)

onMounted(() => {
    // Load user data into form
    if (authStore.user) {
        profileForm.name = authStore.user.name || ''
        profileForm.phone = authStore.user.phone || ''
        profileForm.company_name = authStore.user.company_name || ''
        profileForm.timezone = authStore.user.timezone || 'Asia/Bangkok'
        profileForm.language = authStore.user.language || 'th'
    }
})

const updateProfile = async () => {
    profileLoading.value = true
    const result = await authStore.updateProfile(profileForm)
    profileLoading.value = false

    if (result.success) {
        alert('บันทึกข้อมูลสำเร็จ')
    } else {
        alert(result.message)
    }
}

const updatePassword = async () => {
    passwordError.value = ''
    passwordSuccess.value = false

    if (passwordForm.password !== passwordForm.password_confirmation) {
        passwordError.value = 'รหัสผ่านไม่ตรงกัน'
        return
    }

    if (passwordForm.password.length < 8) {
        passwordError.value = 'รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร'
        return
    }

    passwordLoading.value = true
    const result = await authStore.updatePassword(passwordForm)
    passwordLoading.value = false

    if (result.success) {
        passwordSuccess.value = true
        passwordForm.current_password = ''
        passwordForm.password = ''
        passwordForm.password_confirmation = ''
    } else {
        passwordError.value = result.message
    }
}

const copyApiKey = () => {
    navigator.clipboard.writeText(apiKey.value)
    alert('คัดลอก API Key แล้ว')
}

const confirmDeleteAccount = () => {
    if (confirm('คุณแน่ใจหรือไม่ที่จะลบบัญชี? การดำเนินการนี้ไม่สามารถย้อนกลับได้')) {
        // Delete account logic
    }
}
</script>
