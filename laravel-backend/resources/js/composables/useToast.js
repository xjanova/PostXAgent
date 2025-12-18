import { ref } from 'vue'

// Global toast state
const toasts = ref([])
let toastId = 0

const toastStyles = {
    success: 'bg-green-600 border-green-500 text-white',
    error: 'bg-red-600 border-red-500 text-white',
    warning: 'bg-yellow-600 border-yellow-500 text-white',
    info: 'bg-blue-600 border-blue-500 text-white'
}

const addToast = ({ type = 'info', title = '', message, duration = 4000 }) => {
    const id = ++toastId
    toasts.value.push({ id, type, title, message })

    if (duration > 0) {
        setTimeout(() => removeToast(id), duration)
    }

    return id
}

const removeToast = (id) => {
    const index = toasts.value.findIndex(t => t.id === id)
    if (index > -1) {
        toasts.value.splice(index, 1)
    }
}

export function useToast() {
    return {
        toasts,
        toastStyles,
        success: (message, title) => addToast({ type: 'success', message, title }),
        error: (message, title) => addToast({ type: 'error', message, title }),
        warning: (message, title) => addToast({ type: 'warning', message, title }),
        info: (message, title) => addToast({ type: 'info', message, title }),
        remove: removeToast
    }
}
