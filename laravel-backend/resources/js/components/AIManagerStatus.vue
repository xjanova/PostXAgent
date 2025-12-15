<template>
  <div class="ai-manager-status">
    <!-- Connection Status Card -->
    <div class="status-card" :class="statusClass">
      <div class="status-header">
        <div class="status-indicator" :class="indicatorClass">
          <span class="pulse" v-if="status.connected"></span>
        </div>
        <div class="status-info">
          <h3>AI Manager</h3>
          <p class="status-text">{{ status.status_text || 'Checking...' }}</p>
        </div>
        <button @click="refresh" class="refresh-btn" :disabled="loading">
          <svg class="refresh-icon" :class="{ spinning: loading }" viewBox="0 0 24 24">
            <path d="M17.65 6.35A7.958 7.958 0 0012 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08A5.99 5.99 0 0112 18c-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z"/>
          </svg>
        </button>
      </div>

      <div class="status-details" v-if="status.connected">
        <!-- Server Info -->
        <div class="detail-row">
          <span class="label">Server</span>
          <span class="value">{{ status.server_url }}</span>
        </div>

        <div class="detail-row">
          <span class="label">Latency</span>
          <span class="value" :class="latencyClass">{{ status.latency_ms }}ms</span>
        </div>

        <!-- Stats -->
        <div class="stats-grid" v-if="status.stats">
          <div class="stat-item">
            <span class="stat-value">{{ status.stats.active_workers }}</span>
            <span class="stat-label">Workers</span>
          </div>
          <div class="stat-item">
            <span class="stat-value">{{ formatNumber(status.stats.tasks_completed) }}</span>
            <span class="stat-label">Tasks</span>
          </div>
          <div class="stat-item">
            <span class="stat-value">{{ status.stats.uptime }}</span>
            <span class="stat-label">Uptime</span>
          </div>
        </div>

        <!-- System Info -->
        <div class="system-info" v-if="status.system">
          <div class="detail-row">
            <span class="label">CPU Cores</span>
            <span class="value">{{ status.system.processor_count }}</span>
          </div>
          <div class="detail-row">
            <span class="label">Memory</span>
            <span class="value">{{ status.system.memory_mb }} MB</span>
          </div>
        </div>
      </div>

      <!-- Error State -->
      <div class="error-state" v-else-if="status.error">
        <p class="error-message">{{ status.error }}</p>
        <button @click="refresh" class="retry-btn">Retry Connection</button>
      </div>

      <!-- Last Check -->
      <div class="last-check" v-if="status.last_check">
        Last check: {{ formatTime(status.last_check) }}
      </div>
    </div>

    <!-- Admin Controls -->
    <div class="admin-controls" v-if="isAdmin && status.connected">
      <button
        @click="startManager"
        class="control-btn start"
        :disabled="controlLoading"
        v-if="status.status !== 'online'">
        Start
      </button>
      <button
        @click="stopManager"
        class="control-btn stop"
        :disabled="controlLoading"
        v-else>
        Stop
      </button>
    </div>
  </div>
</template>

<script>
export default {
  name: 'AIManagerStatus',

  props: {
    isAdmin: {
      type: Boolean,
      default: false
    },
    autoRefresh: {
      type: Boolean,
      default: true
    },
    refreshInterval: {
      type: Number,
      default: 30000 // 30 seconds
    }
  },

  data() {
    return {
      status: {
        connected: false,
        status: 'checking',
        status_text: 'Checking connection...',
        status_color: 'gray',
        server_url: '',
        signalr_url: '',
        latency_ms: 0,
        last_check: null,
        error: null,
        stats: null,
        system: null
      },
      loading: false,
      controlLoading: false,
      refreshTimer: null
    }
  },

  computed: {
    statusClass() {
      return `status-${this.status.status || 'checking'}`
    },
    indicatorClass() {
      return `indicator-${this.status.status_color || 'gray'}`
    },
    latencyClass() {
      if (this.status.latency_ms < 50) return 'latency-good'
      if (this.status.latency_ms < 200) return 'latency-ok'
      return 'latency-slow'
    }
  },

  mounted() {
    this.fetchStatus()

    if (this.autoRefresh) {
      this.startAutoRefresh()
    }
  },

  beforeDestroy() {
    this.stopAutoRefresh()
  },

  methods: {
    async fetchStatus() {
      this.loading = true
      try {
        const response = await fetch('/api/v1/ai-manager/status')
        this.status = await response.json()
      } catch (error) {
        this.status = {
          connected: false,
          status: 'offline',
          status_text: 'Failed to check status',
          status_color: 'red',
          error: error.message
        }
      } finally {
        this.loading = false
      }
    },

    async refresh() {
      await this.fetchStatus()
    },

    async startManager() {
      this.controlLoading = true
      try {
        await fetch('/api/v1/ai-manager/start', { method: 'POST' })
        await this.fetchStatus()
      } catch (error) {
        console.error('Failed to start AI Manager:', error)
      } finally {
        this.controlLoading = false
      }
    },

    async stopManager() {
      this.controlLoading = true
      try {
        await fetch('/api/v1/ai-manager/stop', { method: 'POST' })
        await this.fetchStatus()
      } catch (error) {
        console.error('Failed to stop AI Manager:', error)
      } finally {
        this.controlLoading = false
      }
    },

    startAutoRefresh() {
      this.refreshTimer = setInterval(() => {
        this.fetchStatus()
      }, this.refreshInterval)
    },

    stopAutoRefresh() {
      if (this.refreshTimer) {
        clearInterval(this.refreshTimer)
        this.refreshTimer = null
      }
    },

    formatNumber(num) {
      if (!num) return '0'
      return num.toLocaleString()
    },

    formatTime(isoString) {
      if (!isoString) return ''
      const date = new Date(isoString)
      return date.toLocaleTimeString()
    }
  }
}
</script>

<style scoped>
.ai-manager-status {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
}

.status-card {
  background: #1e1e2e;
  border-radius: 16px;
  padding: 20px;
  color: white;
  transition: all 0.3s ease;
}

.status-card.status-online {
  border-left: 4px solid #4caf50;
}

.status-card.status-offline {
  border-left: 4px solid #f44336;
}

.status-card.status-idle {
  border-left: 4px solid #ff9800;
}

.status-header {
  display: flex;
  align-items: center;
  gap: 15px;
  margin-bottom: 20px;
}

.status-indicator {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
}

.indicator-green {
  background: rgba(76, 175, 80, 0.2);
}

.indicator-red {
  background: rgba(244, 67, 54, 0.2);
}

.indicator-yellow {
  background: rgba(255, 152, 0, 0.2);
}

.indicator-gray {
  background: rgba(128, 128, 128, 0.2);
}

.pulse {
  position: absolute;
  width: 12px;
  height: 12px;
  background: #4caf50;
  border-radius: 50%;
  animation: pulse 2s infinite;
}

@keyframes pulse {
  0% {
    transform: scale(1);
    opacity: 1;
  }
  50% {
    transform: scale(1.5);
    opacity: 0.5;
  }
  100% {
    transform: scale(1);
    opacity: 1;
  }
}

.status-info h3 {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
}

.status-text {
  margin: 5px 0 0;
  font-size: 14px;
  color: #a0a0a0;
}

.refresh-btn {
  margin-left: auto;
  background: transparent;
  border: none;
  cursor: pointer;
  padding: 8px;
  border-radius: 8px;
  transition: background 0.2s;
}

.refresh-btn:hover {
  background: rgba(255, 255, 255, 0.1);
}

.refresh-icon {
  width: 20px;
  height: 20px;
  fill: #a0a0a0;
}

.refresh-icon.spinning {
  animation: spin 1s linear infinite;
}

@keyframes spin {
  from { transform: rotate(0deg); }
  to { transform: rotate(360deg); }
}

.status-details {
  border-top: 1px solid #2a2a3e;
  padding-top: 15px;
}

.detail-row {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
  font-size: 14px;
}

.label {
  color: #808080;
}

.value {
  color: white;
  font-family: monospace;
}

.latency-good { color: #4caf50; }
.latency-ok { color: #ff9800; }
.latency-slow { color: #f44336; }

.stats-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 15px;
  margin: 20px 0;
  padding: 15px;
  background: #2a2a3e;
  border-radius: 12px;
}

.stat-item {
  text-align: center;
}

.stat-value {
  display: block;
  font-size: 24px;
  font-weight: 700;
  color: white;
}

.stat-label {
  display: block;
  font-size: 12px;
  color: #808080;
  margin-top: 5px;
}

.system-info {
  border-top: 1px solid #2a2a3e;
  padding-top: 15px;
  margin-top: 15px;
}

.error-state {
  text-align: center;
  padding: 20px;
}

.error-message {
  color: #f44336;
  margin-bottom: 15px;
}

.retry-btn {
  background: #7c4dff;
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 500;
}

.last-check {
  text-align: center;
  font-size: 12px;
  color: #606060;
  margin-top: 15px;
  padding-top: 15px;
  border-top: 1px solid #2a2a3e;
}

.admin-controls {
  display: flex;
  gap: 10px;
  margin-top: 15px;
}

.control-btn {
  flex: 1;
  padding: 12px;
  border: none;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.control-btn.start {
  background: #4caf50;
  color: white;
}

.control-btn.stop {
  background: #f44336;
  color: white;
}

.control-btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
