import { ref, computed } from 'vue'
import axios from 'axios'

const API_BASE = '/api/v1/web-learning'

// Shared state
const workflows = ref([])
const executions = ref([])
const loading = ref(false)
const error = ref(null)

export function useWebLearning() {
    // Workflows
    const fetchWorkflows = async (params = {}) => {
        loading.value = true
        error.value = null
        try {
            const response = await axios.get(`${API_BASE}/workflows`, { params })
            workflows.value = response.data.data
            return response.data
        } catch (e) {
            error.value = e.response?.data?.message || 'Failed to fetch workflows'
            throw e
        } finally {
            loading.value = false
        }
    }

    const getWorkflow = async (id) => {
        const response = await axios.get(`${API_BASE}/workflows/${id}`)
        return response.data.data
    }

    const updateWorkflow = async (id, data) => {
        const response = await axios.put(`${API_BASE}/workflows/${id}`, data)
        const index = workflows.value.findIndex(w => w.id === id)
        if (index !== -1) {
            workflows.value[index] = response.data.data
        }
        return response.data.data
    }

    const deleteWorkflow = async (id) => {
        await axios.delete(`${API_BASE}/workflows/${id}`)
        workflows.value = workflows.value.filter(w => w.id !== id)
    }

    const cloneWorkflow = async (id) => {
        const response = await axios.post(`${API_BASE}/workflows/${id}/clone`)
        workflows.value.push(response.data.data)
        return response.data.data
    }

    const optimizeWorkflow = async (id) => {
        const response = await axios.post(`${API_BASE}/workflows/${id}/optimize`)
        const index = workflows.value.findIndex(w => w.id === id)
        if (index !== -1) {
            workflows.value[index] = response.data.data
        }
        return response.data.data
    }

    // Teaching Sessions
    const startTeachingSession = async (data) => {
        const response = await axios.post(`${API_BASE}/teaching/start`, data)
        return response.data.data
    }

    const recordTeachingStep = async (workflowId, step) => {
        const response = await axios.post(`${API_BASE}/teaching/${workflowId}/record-step`, step)
        return response.data.data
    }

    const completeTeachingSession = async (workflowId) => {
        const response = await axios.post(`${API_BASE}/teaching/${workflowId}/complete`)
        workflows.value.push(response.data.data)
        return response.data.data
    }

    const cancelTeachingSession = async (workflowId) => {
        await axios.post(`${API_BASE}/teaching/${workflowId}/cancel`)
    }

    // Workflow Execution
    const executeWorkflow = async (workflowId, data = {}) => {
        const response = await axios.post(`${API_BASE}/workflows/${workflowId}/execute`, data)
        executions.value.unshift(response.data.data)
        return response.data.data
    }

    const testWorkflow = async (workflowId, data = {}) => {
        const response = await axios.post(`${API_BASE}/workflows/${workflowId}/test`, data)
        return response.data.data
    }

    const fetchExecutions = async (params = {}) => {
        const response = await axios.get(`${API_BASE}/executions`, { params })
        executions.value = response.data.data
        return response.data
    }

    const getExecution = async (id) => {
        const response = await axios.get(`${API_BASE}/executions/${id}`)
        return response.data.data
    }

    const cancelExecution = async (id) => {
        const response = await axios.post(`${API_BASE}/executions/${id}/cancel`)
        const index = executions.value.findIndex(e => e.id === id)
        if (index !== -1) {
            executions.value[index] = response.data.data
        }
        return response.data.data
    }

    // AI Analysis
    const analyzePageWithAI = async (data) => {
        const response = await axios.post(`${API_BASE}/ai/analyze-page`, data)
        return response.data.data
    }

    const generateWorkflowFromAI = async (data) => {
        const response = await axios.post(`${API_BASE}/ai/generate-workflow`, data)
        workflows.value.push(response.data.data)
        return response.data.data
    }

    const suggestSelectors = async (data) => {
        const response = await axios.post(`${API_BASE}/ai/suggest-selectors`, data)
        return response.data.data
    }

    // Statistics
    const fetchStatistics = async () => {
        const response = await axios.get(`${API_BASE}/statistics`)
        return response.data.data
    }

    const fetchStatisticsByPlatform = async () => {
        const response = await axios.get(`${API_BASE}/statistics/by-platform`)
        return response.data.data
    }

    // Computed
    const activeWorkflows = computed(() => {
        return workflows.value.filter(w => w.status === 'active')
    })

    const learningWorkflows = computed(() => {
        return workflows.value.filter(w => w.status === 'learning')
    })

    const runningExecutions = computed(() => {
        return executions.value.filter(e => e.status === 'running')
    })

    const workflowsByPlatform = computed(() => {
        const grouped = {}
        workflows.value.forEach(w => {
            if (!grouped[w.platform]) {
                grouped[w.platform] = []
            }
            grouped[w.platform].push(w)
        })
        return grouped
    })

    return {
        // State
        workflows,
        executions,
        loading,
        error,

        // Computed
        activeWorkflows,
        learningWorkflows,
        runningExecutions,
        workflowsByPlatform,

        // Workflows
        fetchWorkflows,
        getWorkflow,
        updateWorkflow,
        deleteWorkflow,
        cloneWorkflow,
        optimizeWorkflow,

        // Teaching
        startTeachingSession,
        recordTeachingStep,
        completeTeachingSession,
        cancelTeachingSession,

        // Execution
        executeWorkflow,
        testWorkflow,
        fetchExecutions,
        getExecution,
        cancelExecution,

        // AI
        analyzePageWithAI,
        generateWorkflowFromAI,
        suggestSelectors,

        // Statistics
        fetchStatistics,
        fetchStatisticsByPlatform
    }
}

export default useWebLearning
