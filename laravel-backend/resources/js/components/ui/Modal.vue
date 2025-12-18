<template>
    <Teleport to="body">
        <Transition
            enter-active-class="transition duration-200 ease-out"
            enter-from-class="opacity-0"
            enter-to-class="opacity-100"
            leave-active-class="transition duration-150 ease-in"
            leave-from-class="opacity-100"
            leave-to-class="opacity-0"
        >
            <div v-if="modelValue" class="fixed inset-0 z-50 overflow-y-auto" @keydown.esc="close">
                <!-- Backdrop -->
                <div class="fixed inset-0 bg-black/60 backdrop-blur-sm" @click="closeOnBackdrop && close()"></div>

                <!-- Modal Container -->
                <div class="flex min-h-full items-center justify-center p-4">
                    <Transition
                        enter-active-class="transition duration-200 ease-out"
                        enter-from-class="opacity-0 scale-95"
                        enter-to-class="opacity-100 scale-100"
                        leave-active-class="transition duration-150 ease-in"
                        leave-from-class="opacity-100 scale-100"
                        leave-to-class="opacity-0 scale-95"
                    >
                        <div v-if="modelValue"
                            :class="[
                                'relative w-full bg-gray-800 rounded-xl shadow-2xl border border-gray-700',
                                sizeClass
                            ]"
                        >
                            <!-- Header -->
                            <div v-if="title || $slots.header" class="flex items-center justify-between px-6 py-4 border-b border-gray-700">
                                <slot name="header">
                                    <h3 class="text-lg font-semibold text-white">{{ title }}</h3>
                                </slot>
                                <button
                                    v-if="showClose"
                                    class="p-1 text-gray-400 hover:text-white rounded-lg hover:bg-gray-700 transition-colors"
                                    @click="close"
                                >
                                    <XMarkIcon class="w-5 h-5" />
                                </button>
                            </div>

                            <!-- Body -->
                            <div :class="['px-6 py-4', bodyClass]">
                                <slot></slot>
                            </div>

                            <!-- Footer -->
                            <div v-if="$slots.footer" class="flex items-center justify-end gap-3 px-6 py-4 border-t border-gray-700 bg-gray-800/50 rounded-b-xl">
                                <slot name="footer"></slot>
                            </div>
                        </div>
                    </Transition>
                </div>
            </div>
        </Transition>
    </Teleport>
</template>

<script setup>
import { computed, watch, onMounted, onUnmounted } from 'vue'
import { XMarkIcon } from '@heroicons/vue/24/outline'

const props = defineProps({
    modelValue: {
        type: Boolean,
        default: false
    },
    title: {
        type: String,
        default: ''
    },
    size: {
        type: String,
        default: 'md',
        validator: (v) => ['sm', 'md', 'lg', 'xl', 'full'].includes(v)
    },
    closeOnBackdrop: {
        type: Boolean,
        default: true
    },
    showClose: {
        type: Boolean,
        default: true
    },
    bodyClass: {
        type: String,
        default: ''
    }
})

const emit = defineEmits(['update:modelValue', 'close'])

const sizeClass = computed(() => {
    const sizes = {
        sm: 'max-w-sm',
        md: 'max-w-md',
        lg: 'max-w-lg',
        xl: 'max-w-xl',
        full: 'max-w-4xl'
    }
    return sizes[props.size] || sizes.md
})

const close = () => {
    emit('update:modelValue', false)
    emit('close')
}

// Lock body scroll when modal is open
watch(() => props.modelValue, (isOpen) => {
    if (isOpen) {
        document.body.style.overflow = 'hidden'
    } else {
        document.body.style.overflow = ''
    }
})

onUnmounted(() => {
    document.body.style.overflow = ''
})
</script>
