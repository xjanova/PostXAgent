<template>
    <Teleport to="body">
        <div class="fixed top-4 right-4 z-[100] space-y-2">
            <TransitionGroup
                enter-active-class="transition duration-300 ease-out"
                enter-from-class="opacity-0 translate-x-full"
                enter-to-class="opacity-100 translate-x-0"
                leave-active-class="transition duration-200 ease-in"
                leave-from-class="opacity-100 translate-x-0"
                leave-to-class="opacity-0 translate-x-full"
            >
                <div
                    v-for="toast in toasts"
                    :key="toast.id"
                    :class="[
                        'flex items-start gap-3 px-4 py-3 rounded-lg shadow-lg border min-w-[300px] max-w-md',
                        toastStyles[toast.type]
                    ]"
                >
                    <component :is="toastIcons[toast.type]" class="w-5 h-5 flex-shrink-0 mt-0.5" />
                    <div class="flex-1 min-w-0">
                        <p v-if="toast.title" class="font-medium">{{ toast.title }}</p>
                        <p :class="['text-sm', toast.title ? 'opacity-90' : '']">{{ toast.message }}</p>
                    </div>
                    <button
                        class="flex-shrink-0 p-1 hover:bg-white/10 rounded transition-colors"
                        @click="removeToast(toast.id)"
                    >
                        <XMarkIcon class="w-4 h-4" />
                    </button>
                </div>
            </TransitionGroup>
        </div>
    </Teleport>
</template>

<script setup>
import { ref, markRaw } from 'vue'
import {
    XMarkIcon,
    CheckCircleIcon,
    ExclamationCircleIcon,
    InformationCircleIcon,
    ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'

const toasts = ref([])
let toastId = 0

const toastStyles = {
    success: 'bg-green-600 border-green-500 text-white',
    error: 'bg-red-600 border-red-500 text-white',
    warning: 'bg-yellow-600 border-yellow-500 text-white',
    info: 'bg-blue-600 border-blue-500 text-white'
}

const toastIcons = {
    success: markRaw(CheckCircleIcon),
    error: markRaw(ExclamationCircleIcon),
    warning: markRaw(ExclamationTriangleIcon),
    info: markRaw(InformationCircleIcon)
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

// Expose methods for use via ref or provide/inject
defineExpose({
    success: (message, title) => addToast({ type: 'success', message, title }),
    error: (message, title) => addToast({ type: 'error', message, title }),
    warning: (message, title) => addToast({ type: 'warning', message, title }),
    info: (message, title) => addToast({ type: 'info', message, title }),
    remove: removeToast
})
</script>
