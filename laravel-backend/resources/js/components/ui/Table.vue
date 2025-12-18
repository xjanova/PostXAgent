<template>
    <div class="bg-gray-800 rounded-xl border border-gray-700 overflow-hidden">
        <!-- Header Actions -->
        <div v-if="$slots.header || searchable" class="px-4 py-3 border-b border-gray-700 flex items-center justify-between gap-4">
            <slot name="header">
                <div v-if="searchable" class="relative flex-1 max-w-xs">
                    <MagnifyingGlassIcon class="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-gray-400" />
                    <input
                        v-model="searchQuery"
                        type="text"
                        :placeholder="searchPlaceholder"
                        class="w-full pl-9 pr-4 py-2 bg-gray-700 border border-gray-600 rounded-lg text-white text-sm placeholder-gray-400 focus:outline-none focus:border-indigo-500"
                    />
                </div>
            </slot>
            <slot name="actions"></slot>
        </div>

        <!-- Table -->
        <div class="overflow-x-auto">
            <table class="w-full">
                <thead class="bg-gray-700/50">
                    <tr>
                        <th v-if="selectable" class="px-4 py-3 text-left">
                            <input
                                type="checkbox"
                                :checked="allSelected"
                                :indeterminate="someSelected"
                                class="rounded border-gray-600 bg-gray-700 text-indigo-600 focus:ring-indigo-500"
                                @change="toggleAll"
                            />
                        </th>
                        <th
                            v-for="column in columns"
                            :key="column.key"
                            :class="[
                                'px-4 py-3 text-xs font-medium text-gray-400 uppercase tracking-wider text-left',
                                column.sortable ? 'cursor-pointer hover:text-white select-none' : '',
                                column.class || ''
                            ]"
                            @click="column.sortable && toggleSort(column.key)"
                        >
                            <div class="flex items-center gap-2">
                                {{ column.label }}
                                <template v-if="column.sortable">
                                    <ChevronUpIcon v-if="sortKey === column.key && sortOrder === 'asc'" class="w-4 h-4" />
                                    <ChevronDownIcon v-else-if="sortKey === column.key && sortOrder === 'desc'" class="w-4 h-4" />
                                    <ChevronUpDownIcon v-else class="w-4 h-4 opacity-50" />
                                </template>
                            </div>
                        </th>
                        <th v-if="$slots.rowActions" class="px-4 py-3 text-right">
                            <span class="sr-only">Actions</span>
                        </th>
                    </tr>
                </thead>
                <tbody class="divide-y divide-gray-700">
                    <tr v-if="loading">
                        <td :colspan="totalColumns" class="px-4 py-12 text-center">
                            <div class="flex items-center justify-center gap-2 text-gray-400">
                                <ArrowPathIcon class="w-5 h-5 animate-spin" />
                                <span>กำลังโหลด...</span>
                            </div>
                        </td>
                    </tr>
                    <tr v-else-if="filteredData.length === 0">
                        <td :colspan="totalColumns" class="px-4 py-12 text-center">
                            <slot name="empty">
                                <InboxIcon class="w-12 h-12 text-gray-500 mx-auto mb-3" />
                                <p class="text-gray-400">{{ emptyText }}</p>
                            </slot>
                        </td>
                    </tr>
                    <tr
                        v-else
                        v-for="(row, index) in paginatedData"
                        :key="row[rowKey] || index"
                        class="hover:bg-gray-700/50 transition-colors"
                    >
                        <td v-if="selectable" class="px-4 py-3">
                            <input
                                type="checkbox"
                                :checked="isSelected(row)"
                                class="rounded border-gray-600 bg-gray-700 text-indigo-600 focus:ring-indigo-500"
                                @change="toggleSelect(row)"
                            />
                        </td>
                        <td
                            v-for="column in columns"
                            :key="column.key"
                            :class="['px-4 py-3 text-sm', column.class || 'text-gray-300']"
                        >
                            <slot :name="`cell-${column.key}`" :row="row" :value="getNestedValue(row, column.key)">
                                {{ formatValue(row, column) }}
                            </slot>
                        </td>
                        <td v-if="$slots.rowActions" class="px-4 py-3 text-right">
                            <slot name="rowActions" :row="row" :index="index"></slot>
                        </td>
                    </tr>
                </tbody>
            </table>
        </div>

        <!-- Pagination -->
        <div v-if="pagination && filteredData.length > 0" class="px-4 py-3 border-t border-gray-700 flex items-center justify-between">
            <div class="text-sm text-gray-400">
                แสดง {{ startIndex + 1 }}-{{ endIndex }} จาก {{ filteredData.length }} รายการ
            </div>
            <div class="flex items-center gap-2">
                <button
                    :disabled="currentPage === 1"
                    class="px-3 py-1 rounded-lg text-sm bg-gray-700 text-gray-300 hover:bg-gray-600 disabled:opacity-50 disabled:cursor-not-allowed"
                    @click="currentPage--"
                >
                    ก่อนหน้า
                </button>
                <span class="px-3 py-1 text-sm text-gray-400">
                    หน้า {{ currentPage }} / {{ totalPages }}
                </span>
                <button
                    :disabled="currentPage >= totalPages"
                    class="px-3 py-1 rounded-lg text-sm bg-gray-700 text-gray-300 hover:bg-gray-600 disabled:opacity-50 disabled:cursor-not-allowed"
                    @click="currentPage++"
                >
                    ถัดไป
                </button>
            </div>
        </div>
    </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import {
    MagnifyingGlassIcon,
    ChevronUpIcon,
    ChevronDownIcon,
    ChevronUpDownIcon,
    ArrowPathIcon,
    InboxIcon
} from '@heroicons/vue/24/outline'

const props = defineProps({
    data: {
        type: Array,
        default: () => []
    },
    columns: {
        type: Array,
        required: true
    },
    rowKey: {
        type: String,
        default: 'id'
    },
    loading: {
        type: Boolean,
        default: false
    },
    selectable: {
        type: Boolean,
        default: false
    },
    searchable: {
        type: Boolean,
        default: false
    },
    searchPlaceholder: {
        type: String,
        default: 'ค้นหา...'
    },
    searchKeys: {
        type: Array,
        default: () => []
    },
    pagination: {
        type: Boolean,
        default: true
    },
    perPage: {
        type: Number,
        default: 10
    },
    emptyText: {
        type: String,
        default: 'ไม่พบข้อมูล'
    }
})

const emit = defineEmits(['select', 'sort'])

const searchQuery = ref('')
const sortKey = ref('')
const sortOrder = ref('asc')
const currentPage = ref(1)
const selected = ref([])

const totalColumns = computed(() => {
    let count = props.columns.length
    if (props.selectable) count++
    return count + 1 // +1 for actions
})

// Filter data by search query
const filteredData = computed(() => {
    let result = [...props.data]

    // Apply search filter
    if (searchQuery.value && props.searchKeys.length > 0) {
        const query = searchQuery.value.toLowerCase()
        result = result.filter(row =>
            props.searchKeys.some(key => {
                const value = getNestedValue(row, key)
                return value && String(value).toLowerCase().includes(query)
            })
        )
    }

    // Apply sorting
    if (sortKey.value) {
        result.sort((a, b) => {
            const aVal = getNestedValue(a, sortKey.value)
            const bVal = getNestedValue(b, sortKey.value)

            if (aVal < bVal) return sortOrder.value === 'asc' ? -1 : 1
            if (aVal > bVal) return sortOrder.value === 'asc' ? 1 : -1
            return 0
        })
    }

    return result
})

// Pagination
const totalPages = computed(() => Math.ceil(filteredData.value.length / props.perPage))
const startIndex = computed(() => (currentPage.value - 1) * props.perPage)
const endIndex = computed(() => Math.min(startIndex.value + props.perPage, filteredData.value.length))
const paginatedData = computed(() => {
    if (!props.pagination) return filteredData.value
    return filteredData.value.slice(startIndex.value, endIndex.value)
})

// Selection
const allSelected = computed(() =>
    filteredData.value.length > 0 && selected.value.length === filteredData.value.length
)
const someSelected = computed(() =>
    selected.value.length > 0 && selected.value.length < filteredData.value.length
)

const isSelected = (row) => selected.value.some(s => s[props.rowKey] === row[props.rowKey])

const toggleSelect = (row) => {
    const index = selected.value.findIndex(s => s[props.rowKey] === row[props.rowKey])
    if (index > -1) {
        selected.value.splice(index, 1)
    } else {
        selected.value.push(row)
    }
    emit('select', selected.value)
}

const toggleAll = () => {
    if (allSelected.value) {
        selected.value = []
    } else {
        selected.value = [...filteredData.value]
    }
    emit('select', selected.value)
}

const toggleSort = (key) => {
    if (sortKey.value === key) {
        sortOrder.value = sortOrder.value === 'asc' ? 'desc' : 'asc'
    } else {
        sortKey.value = key
        sortOrder.value = 'asc'
    }
    emit('sort', { key: sortKey.value, order: sortOrder.value })
}

const getNestedValue = (obj, path) => {
    return path.split('.').reduce((o, p) => o?.[p], obj)
}

const formatValue = (row, column) => {
    const value = getNestedValue(row, column.key)
    if (column.format) return column.format(value, row)
    return value ?? '-'
}

// Reset page when search changes
watch(searchQuery, () => {
    currentPage.value = 1
})

// Expose methods
defineExpose({
    getSelected: () => selected.value,
    clearSelection: () => { selected.value = [] }
})
</script>
