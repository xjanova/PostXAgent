<template>
    <Teleport to="body">
        <div class="fixed top-4 right-4 z-[100] space-y-2 pointer-events-none">
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
                        'flex items-start gap-3 px-4 py-3 rounded-lg shadow-lg border min-w-[300px] max-w-md pointer-events-auto',
                        toastStyles[toast.type]
                    ]"
                >
                    <component :is="icons[toast.type]" class="w-5 h-5 flex-shrink-0 mt-0.5" />
                    <div class="flex-1 min-w-0">
                        <p v-if="toast.title" class="font-medium">{{ toast.title }}</p>
                        <p :class="['text-sm', toast.title ? 'opacity-90' : '']">{{ toast.message }}</p>
                    </div>
                    <button
                        class="flex-shrink-0 p-1 hover:bg-white/10 rounded transition-colors"
                        @click="remove(toast.id)"
                    >
                        <XMarkIcon class="w-4 h-4" />
                    </button>
                </div>
            </TransitionGroup>
        </div>
    </Teleport>
</template>

<script setup>
import { markRaw } from 'vue'
import { useToast } from '../../composables/useToast'
import {
    XMarkIcon,
    CheckCircleIcon,
    ExclamationCircleIcon,
    InformationCircleIcon,
    ExclamationTriangleIcon
} from '@heroicons/vue/24/outline'

const { toasts, toastStyles, remove } = useToast()

const icons = {
    success: markRaw(CheckCircleIcon),
    error: markRaw(ExclamationCircleIcon),
    warning: markRaw(ExclamationTriangleIcon),
    info: markRaw(InformationCircleIcon)
}
</script>
