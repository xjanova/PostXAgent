import axios from 'axios'

const api = axios.create({
    baseURL: '/api/v1',
    headers: {
        'Content-Type': 'application/json',
        'Accept': 'application/json',
        'X-Requested-With': 'XMLHttpRequest',
    },
})

// Request interceptor
api.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem('auth_token')
        if (token) {
            config.headers.Authorization = `Bearer ${token}`
        }
        return config
    },
    (error) => Promise.reject(error)
)

// Response interceptor
api.interceptors.response.use(
    (response) => response,
    (error) => {
        if (error.response?.status === 401) {
            localStorage.removeItem('auth_token')
            window.location.href = '/auth/login'
        }
        return Promise.reject(error)
    }
)

export default api

// API helper functions
export const authApi = {
    login: (data) => api.post('/auth/login', data),
    register: (data) => api.post('/auth/register', data),
    logout: () => api.post('/auth/logout'),
    me: () => api.get('/auth/me'),
    updateProfile: (data) => api.put('/auth/profile', data),
    updatePassword: (data) => api.put('/auth/password', data),
    forgotPassword: (email) => api.post('/auth/forgot-password', { email }),
    resetPassword: (data) => api.post('/auth/reset-password', data),
}

export const rentalApi = {
    packages: () => api.get('/rentals/packages'),
    packageDetail: (id) => api.get(`/rentals/packages/${id}`),
    paymentMethods: () => api.get('/rentals/payment-methods'),
    status: () => api.get('/rentals/status'),
    history: (limit = 10) => api.get('/rentals/history', { params: { limit } }),
    checkout: (data) => api.post('/rentals/checkout', data),
    validatePromo: (code, packageId) => api.post('/rentals/validate-promo', { code, package_id: packageId }),
    uploadSlip: (uuid, file) => {
        const formData = new FormData()
        formData.append('slip', file)
        return api.post(`/rentals/payments/${uuid}/upload-slip`, formData, {
            headers: { 'Content-Type': 'multipart/form-data' }
        })
    },
    paymentStatus: (uuid) => api.get(`/rentals/payments/${uuid}/status`),
    cancel: (id, reason) => api.post(`/rentals/${id}/cancel`, { reason }),
    invoices: (limit = 20) => api.get('/rentals/invoices', { params: { limit } }),
    requestTaxInvoice: (id, data) => api.post(`/rentals/invoices/${id}/request-tax`, data),
}

export const brandApi = {
    list: () => api.get('/brands'),
    get: (id) => api.get(`/brands/${id}`),
    create: (data) => api.post('/brands', data),
    update: (id, data) => api.put(`/brands/${id}`, data),
    delete: (id) => api.delete(`/brands/${id}`),
}

export const campaignApi = {
    list: () => api.get('/campaigns'),
    get: (id) => api.get(`/campaigns/${id}`),
    create: (data) => api.post('/campaigns', data),
    update: (id, data) => api.put(`/campaigns/${id}`, data),
    delete: (id) => api.delete(`/campaigns/${id}`),
    start: (id) => api.post(`/campaigns/${id}/start`),
    pause: (id) => api.post(`/campaigns/${id}/pause`),
    stop: (id) => api.post(`/campaigns/${id}/stop`),
}

export const postApi = {
    list: () => api.get('/posts'),
    get: (id) => api.get(`/posts/${id}`),
    create: (data) => api.post('/posts', data),
    update: (id, data) => api.put(`/posts/${id}`, data),
    delete: (id) => api.delete(`/posts/${id}`),
    publish: (id) => api.post(`/posts/${id}/publish`),
    metrics: (id) => api.get(`/posts/${id}/metrics`),
    generateContent: (data) => api.post('/posts/generate-content', data),
    generateImage: (data) => api.post('/posts/generate-image', data),
}

export const socialAccountApi = {
    list: () => api.get('/social-accounts'),
    connect: (platform) => api.get(`/social-accounts/${platform}/connect`),
    disconnect: (id) => api.delete(`/social-accounts/${id}`),
    refresh: (id) => api.post(`/social-accounts/${id}/refresh`),
}

export const analyticsApi = {
    overview: () => api.get('/analytics/overview'),
    posts: () => api.get('/analytics/posts'),
    engagement: () => api.get('/analytics/engagement'),
    platforms: () => api.get('/analytics/platforms'),
    brand: (id) => api.get(`/analytics/brands/${id}`),
}

export const aiManagerApi = {
    status: () => api.get('/ai-manager/status'),
    fullStatus: () => api.get('/ai-manager/status/full'),
    stats: () => api.get('/ai-manager/stats'),
    workers: () => api.get('/ai-manager/workers'),
    system: () => api.get('/ai-manager/system'),
    start: () => api.post('/ai-manager/start'),
    stop: () => api.post('/ai-manager/stop'),
}

// Admin APIs
export const adminApi = {
    // Users
    users: (params) => api.get('/admin/users', { params }),
    userDetail: (id) => api.get(`/admin/users/${id}`),
    updateUser: (id, data) => api.put(`/admin/users/${id}`, data),
    deleteUser: (id) => api.delete(`/admin/users/${id}`),

    // Payments
    payments: (params) => api.get('/admin/rentals/payments', { params }),
    verifyPayment: (uuid, notes) => api.post(`/admin/rentals/payments/${uuid}/verify`, { notes }),
    rejectPayment: (uuid, reason) => api.post(`/admin/rentals/payments/${uuid}/reject`, { reason }),

    // Stats
    stats: () => api.get('/admin/rentals/stats'),
    dashboard: () => api.get('/admin/dashboard'),

    // Promo Codes
    promoCodes: () => api.get('/admin/promo-codes'),
    createPromoCode: (data) => api.post('/admin/promo-codes', data),
    updatePromoCode: (id, data) => api.put(`/admin/promo-codes/${id}`, data),
    deletePromoCode: (id) => api.delete(`/admin/promo-codes/${id}`),
}
