<template>
    <Modal v-model="isOpen" :title="title" size="sm" :close-on-backdrop="false">
        <div class="text-center">
            <div :class="['mx-auto w-12 h-12 rounded-full flex items-center justify-center mb-4', iconBgClass]">
                <component :is="iconComponent" :class="['w-6 h-6', iconClass]" />
            </div>
            <p class="text-gray-300">{{ message }}</p>
            <p v-if="description" class="text-gray-500 text-sm mt-2">{{ description }}</p>
        </div>

        <template #footer>
            <button
                class="px-4 py-2 text-gray-300 hover:text-white hover:bg-gray-700 rounded-lg transition-colors"
                @click="cancel"
            >
                {{ cancelText }}
            </button>
            <button
                :class="['px-4 py-2 rounded-lg font-medium transition-colors', confirmButtonClass]"
                :disabled="loading"
                @click="confirm"
            >
                <ArrowPathIcon v-if="loading" class="w-4 h-4 animate-spin inline mr-2" />
                {{ confirmText }}
            </button>
        </template>
    </Modal>
</template>

<script setup>
import { ref, computed, markRaw } from 'vue'
import Modal from './Modal.vue'
import {
    ArrowPathIcon,
    ExclamationTriangleIcon,
    TrashIcon,
    QuestionMarkCircleIcon,
    InformationCircleIcon,
    CheckCircleIcon
} from '@heroicons/vue/24/outline'

const props = defineProps({
    modelValue: {
        type: Boolean,
        default: false
    },
    type: {
        type: String,
        default: 'warning',
        validator: (v) => ['warning', 'danger', 'info', 'success'].includes(v)
    },
    title: {
        type: String,
        default: 'ยืนยันการดำเนินการ'
    },
    message: {
        type: String,
        default: 'คุณแน่ใจหรือไม่ที่จะดำเนินการนี้?'
    },
    description: {
        type: String,
        default: ''
    },
    confirmText: {
        type: String,
        default: 'ยืนยัน'
    },
    cancelText: {
        type: String,
        default: 'ยกเลิก'
    },
    loading: {
        type: Boolean,
        default: false
    }
})

const emit = defineEmits(['update:modelValue', 'confirm', 'cancel'])

const isOpen = computed({
    get: () => props.modelValue,
    set: (value) => emit('update:modelValue', value)
})

const typeConfig = {
    warning: {
        icon: ExclamationTriangleIcon,
        iconBg: 'bg-yellow-500/20',
        iconColor: 'text-yellow-500',
        button: 'bg-yellow-600 hover:bg-yellow-700 text-white'
    },
    danger: {
        icon: TrashIcon,
        iconBg: 'bg-red-500/20',
        iconColor: 'text-red-500',
        button: 'bg-red-600 hover:bg-red-700 text-white'
    },
    info: {
        icon: InformationCircleIcon,
        iconBg: 'bg-blue-500/20',
        iconColor: 'text-blue-500',
        button: 'bg-blue-600 hover:bg-blue-700 text-white'
    },
    success: {
        icon: CheckCircleIcon,
        iconBg: 'bg-green-500/20',
        iconColor: 'text-green-500',
        button: 'bg-green-600 hover:bg-green-700 text-white'
    }
}

const iconComponent = computed(() => markRaw(typeConfig[props.type].icon))
const iconBgClass = computed(() => typeConfig[props.type].iconBg)
const iconClass = computed(() => typeConfig[props.type].iconColor)
const confirmButtonClass = computed(() => typeConfig[props.type].button)

const confirm = () => {
    emit('confirm')
}

const cancel = () => {
    emit('update:modelValue', false)
    emit('cancel')
}
</script>
