import { defineStore } from 'pinia'
import api from '../services/api'

export const useAuthStore = defineStore('auth', {
    state: () => ({
        user: null,
        token: localStorage.getItem('auth_token'),
        rental: null,
        permissions: [],
        initialized: false,
        loading: false,
    }),

    getters: {
        isAuthenticated: (state) => !!state.token && !!state.user,
        isAdmin: (state) => state.user?.roles?.includes('admin') || false,
        currentPackage: (state) => state.rental?.package_name || 'Free',
        hasActiveRental: (state) => state.rental?.is_active || false,

        // ตรวจสอบว่าสามารถเข้าถึง feature ได้หรือไม่ตาม package
        canAccess: (state) => (feature) => {
            if (!state.rental?.is_active) return false

            const packageFeatures = {
                'Trial': ['basic_posting', 'ai_content'],
                'Daily Pass': ['all_platforms', 'ai_content', 'scheduling'],
                'Weekly': ['all_platforms', 'ai_content', 'scheduling', 'analytics', 'multiple_brands'],
                'Monthly': ['all_platforms', 'ai_content', 'scheduling', 'analytics', 'multiple_brands', 'priority_support', 'web_learning'],
                'Quarterly': ['unlimited_posts', 'all_platforms', 'ai_content', 'scheduling', 'analytics', 'multiple_brands', 'priority_support', 'api_access', 'web_learning', 'ai_tools'],
                'Yearly': ['unlimited_everything', 'all_platforms', 'ai_content', 'scheduling', 'analytics', 'team_access', 'priority_support', 'api_access', 'white_label', 'web_learning', 'ai_tools'],
            }

            const features = packageFeatures[state.rental?.package_name] || []
            return features.includes(feature) || features.includes('unlimited_everything')
        },

        // Usage limits
        usageLimits: (state) => state.rental?.limits || {
            posts: 0,
            brands: 0,
            platforms: 0,
            ai_generations: 0,
        },

        usageRemaining: (state) => state.rental?.remaining || {
            posts: 0,
            ai_generations: 0,
        },

        daysRemaining: (state) => state.rental?.days_remaining || 0,
    },

    actions: {
        async initAuth() {
            if (this.token) {
                api.defaults.headers.common['Authorization'] = `Bearer ${this.token}`
                try {
                    await this.fetchUser()
                } catch (error) {
                    this.logout()
                }
            }
            this.initialized = true
        },

        async login(credentials) {
            this.loading = true
            try {
                const response = await api.post('/auth/login', credentials)
                this.token = response.data.data.token
                this.user = response.data.data.user

                localStorage.setItem('auth_token', this.token)
                api.defaults.headers.common['Authorization'] = `Bearer ${this.token}`

                await this.fetchRentalStatus()
                return { success: true }
            } catch (error) {
                return {
                    success: false,
                    message: error.response?.data?.message || 'เกิดข้อผิดพลาด'
                }
            } finally {
                this.loading = false
            }
        },

        async register(data) {
            this.loading = true
            try {
                const response = await api.post('/auth/register', data)
                this.token = response.data.data.token
                this.user = response.data.data.user

                localStorage.setItem('auth_token', this.token)
                api.defaults.headers.common['Authorization'] = `Bearer ${this.token}`

                return { success: true }
            } catch (error) {
                return {
                    success: false,
                    message: error.response?.data?.message || 'เกิดข้อผิดพลาด',
                    errors: error.response?.data?.errors || {}
                }
            } finally {
                this.loading = false
            }
        },

        async fetchUser() {
            try {
                const response = await api.get('/auth/me')
                this.user = response.data.data
                this.permissions = response.data.data.permissions || []
                await this.fetchRentalStatus()
            } catch (error) {
                throw error
            }
        },

        async fetchRentalStatus() {
            try {
                const response = await api.get('/rentals/status')
                this.rental = response.data.data
            } catch (error) {
                this.rental = null
            }
        },

        async updateProfile(data) {
            try {
                const response = await api.put('/auth/profile', data)
                this.user = response.data.data
                return { success: true }
            } catch (error) {
                return {
                    success: false,
                    message: error.response?.data?.message || 'เกิดข้อผิดพลาด'
                }
            }
        },

        async updatePassword(data) {
            try {
                await api.put('/auth/password', data)
                return { success: true }
            } catch (error) {
                return {
                    success: false,
                    message: error.response?.data?.message || 'เกิดข้อผิดพลาด'
                }
            }
        },

        logout() {
            this.user = null
            this.token = null
            this.rental = null
            this.permissions = []
            localStorage.removeItem('auth_token')
            delete api.defaults.headers.common['Authorization']
        },
    }
})
