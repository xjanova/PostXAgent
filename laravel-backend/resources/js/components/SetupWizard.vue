<template>
  <div class="min-h-screen flex items-center justify-center p-4">
    <div class="w-full max-w-4xl">
      <!-- Header -->
      <div class="text-center mb-8">
        <h1 class="text-4xl font-bold text-white mb-2">PostXAgent Setup Wizard</h1>
        <p class="text-gray-400">Let's configure your AI-powered social media automation system</p>
      </div>

      <!-- Progress Steps -->
      <div class="flex items-center justify-center mb-12">
        <div v-for="(step, index) in steps" :key="index" class="flex items-center">
          <!-- Step Circle -->
          <div class="flex flex-col items-center">
            <div
              :class="[
                'w-12 h-12 rounded-full flex items-center justify-center font-bold text-lg transition-all',
                currentStep === index
                  ? 'bg-gradient-to-r from-cyan-500 to-blue-500 text-white shadow-lg shadow-cyan-500/50'
                  : currentStep > index
                  ? 'bg-green-500 text-white'
                  : 'bg-gray-700 text-gray-400'
              ]"
            >
              <span v-if="currentStep > index">✓</span>
              <span v-else>{{ index + 1 }}</span>
            </div>
            <span class="text-xs text-gray-400 mt-2">{{ step.title }}</span>
          </div>

          <!-- Connector Line -->
          <div
            v-if="index < steps.length - 1"
            :class="[
              'w-24 h-1 mx-2 transition-all',
              currentStep > index ? 'bg-green-500' : 'bg-gray-700'
            ]"
          ></div>
        </div>
      </div>

      <!-- Setup Card -->
      <div class="bg-gray-800 rounded-2xl shadow-2xl p-8 border border-gray-700">
        <!-- Welcome Step -->
        <div v-if="currentStep === 0" class="text-center">
          <div class="text-6xl mb-6">🚀</div>
          <h2 class="text-3xl font-bold text-white mb-4">Welcome to PostXAgent!</h2>
          <p class="text-gray-300 mb-8 max-w-2xl mx-auto">
            PostXAgent is your comprehensive AI-powered platform for automating social media
            marketing across 9 major platforms. Let's get you set up in just a few steps.
          </p>

          <div class="grid grid-cols-1 md:grid-cols-2 gap-4 max-w-2xl mx-auto text-left">
            <div class="flex items-start space-x-3 bg-gray-700/50 p-4 rounded-lg">
              <span class="text-green-400 text-xl">✓</span>
              <div>
                <h4 class="font-semibold text-white">Multi-Platform Support</h4>
                <p class="text-sm text-gray-400">Facebook, Instagram, TikTok, Twitter, and more</p>
              </div>
            </div>
            <div class="flex items-start space-x-3 bg-gray-700/50 p-4 rounded-lg">
              <span class="text-green-400 text-xl">✓</span>
              <div>
                <h4 class="font-semibold text-white">AI Content Generation</h4>
                <p class="text-sm text-gray-400">Powered by GPT-4, Gemini, and Ollama</p>
              </div>
            </div>
            <div class="flex items-start space-x-3 bg-gray-700/50 p-4 rounded-lg">
              <span class="text-green-400 text-xl">✓</span>
              <div>
                <h4 class="font-semibold text-white">Smart Scheduling</h4>
                <p class="text-sm text-gray-400">Intelligent campaign and post management</p>
              </div>
            </div>
            <div class="flex items-start space-x-3 bg-gray-700/50 p-4 rounded-lg">
              <span class="text-green-400 text-xl">✓</span>
              <div>
                <h4 class="font-semibold text-white">Media Generation</h4>
                <p class="text-sm text-gray-400">AI video and music creation</p>
              </div>
            </div>
          </div>
        </div>

        <!-- Database Setup Step -->
        <div v-if="currentStep === 1">
          <h2 class="text-2xl font-bold text-white mb-6">Database Configuration</h2>
          <p class="text-gray-400 mb-6">Configure your database connection for storing campaigns and posts</p>

          <div class="space-y-4">
            <div>
              <label class="block text-sm font-medium text-gray-300 mb-2">Database Type</label>
              <select v-model="dbConfig.driver" class="w-full bg-gray-700 border border-gray-600 rounded-lg px-4 py-2 text-white focus:ring-2 focus:ring-cyan-500 focus:outline-none">
                <option value="mysql">MySQL</option>
                <option value="pgsql">PostgreSQL</option>
                <option value="sqlite">SQLite (Local)</option>
              </select>
            </div>

            <div v-if="dbConfig.driver !== 'sqlite'">
              <label class="block text-sm font-medium text-gray-300 mb-2">Host</label>
              <input v-model="dbConfig.host" type="text" class="w-full bg-gray-700 border border-gray-600 rounded-lg px-4 py-2 text-white focus:ring-2 focus:ring-cyan-500 focus:outline-none" placeholder="localhost">
            </div>

            <div v-if="dbConfig.driver !== 'sqlite'" class="grid grid-cols-2 gap-4">
              <div>
                <label class="block text-sm font-medium text-gray-300 mb-2">Port</label>
                <input v-model="dbConfig.port" type="number" class="w-full bg-gray-700 border border-gray-600 rounded-lg px-4 py-2 text-white focus:ring-2 focus:ring-cyan-500 focus:outline-none" placeholder="3306">
              </div>
              <div>
                <label class="block text-sm font-medium text-gray-300 mb-2">Database Name</label>
                <input v-model="dbConfig.database" type="text" class="w-full bg-gray-700 border border-gray-600 rounded-lg px-4 py-2 text-white focus:ring-2 focus:ring-cyan-500 focus:outline-none" placeholder="postxagent">
              </div>
            </div>

            <div v-if="dbConfig.driver !== 'sqlite'">
              <label class="block text-sm font-medium text-gray-300 mb-2">Username</label>
              <input v-model="dbConfig.username" type="text" class="w-full bg-gray-700 border border-gray-600 rounded-lg px-4 py-2 text-white focus:ring-2 focus:ring-cyan-500 focus:outline-none" placeholder="root">
            </div>

            <div v-if="dbConfig.driver !== 'sqlite'">
              <label class="block text-sm font-medium text-gray-300 mb-2">Password</label>
              <input v-model="dbConfig.password" type="password" class="w-full bg-gray-700 border border-gray-600 rounded-lg px-4 py-2 text-white focus:ring-2 focus:ring-cyan-500 focus:outline-none">
            </div>

            <button @click="testDatabase" :disabled="testing" class="bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-700 hover:to-blue-700 text-white px-6 py-2 rounded-lg font-medium transition-all disabled:opacity-50">
              {{ testing ? 'Testing...' : 'Test Connection' }}
            </button>

            <div v-if="dbTestResult" :class="['p-4 rounded-lg', dbTestResult.success ? 'bg-green-900/30 text-green-400' : 'bg-red-900/30 text-red-400']">
              {{ dbTestResult.message }}
            </div>
          </div>
        </div>

        <!-- AI Providers Step -->
        <div v-if="currentStep === 2">
          <h2 class="text-2xl font-bold text-white mb-6">AI Providers Configuration</h2>
          <p class="text-gray-400 mb-6">Configure AI providers for content generation (optional)</p>

          <div class="space-y-6">
            <!-- Ollama -->
            <div class="bg-gray-700/50 p-6 rounded-lg border border-gray-600">
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h3 class="text-lg font-semibold text-white">Ollama (Local AI)</h3>
                  <p class="text-sm text-gray-400">Run AI models locally - Free</p>
                  <span class="inline-block mt-1 px-2 py-1 bg-green-600 text-white text-xs rounded">Recommended</span>
                </div>
                <label class="relative inline-flex items-center cursor-pointer">
                  <input v-model="aiProviders.ollama_enabled" type="checkbox" class="sr-only peer">
                  <div class="w-11 h-6 bg-gray-600 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-cyan-800 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-cyan-600"></div>
                </label>
              </div>
              <div v-if="aiProviders.ollama_enabled">
                <label class="block text-sm font-medium text-gray-300 mb-2">Ollama URL</label>
                <input v-model="aiProviders.ollama_url" type="text" class="w-full bg-gray-700 border border-gray-600 rounded-lg px-4 py-2 text-white focus:ring-2 focus:ring-cyan-500 focus:outline-none" placeholder="http://localhost:11434">
              </div>
            </div>

            <!-- Google Gemini -->
            <div class="bg-gray-700/50 p-6 rounded-lg border border-gray-600">
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h3 class="text-lg font-semibold text-white">Google Gemini</h3>
                  <p class="text-sm text-gray-400">Google's advanced AI - Free tier available</p>
                  <span class="inline-block mt-1 px-2 py-1 bg-orange-600 text-white text-xs rounded">Optional</span>
                </div>
                <label class="relative inline-flex items-center cursor-pointer">
                  <input v-model="aiProviders.gemini_enabled" type="checkbox" class="sr-only peer">
                  <div class="w-11 h-6 bg-gray-600 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-cyan-800 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-cyan-600"></div>
                </label>
              </div>
              <div v-if="aiProviders.gemini_enabled">
                <label class="block text-sm font-medium text-gray-300 mb-2">API Key</label>
                <input v-model="aiProviders.gemini_key" type="password" class="w-full bg-gray-700 border border-gray-600 rounded-lg px-4 py-2 text-white focus:ring-2 focus:ring-cyan-500 focus:outline-none" placeholder="Enter Gemini API key">
              </div>
            </div>

            <!-- OpenAI -->
            <div class="bg-gray-700/50 p-6 rounded-lg border border-gray-600">
              <div class="flex items-center justify-between mb-4">
                <div>
                  <h3 class="text-lg font-semibold text-white">OpenAI GPT-4</h3>
                  <p class="text-sm text-gray-400">Most powerful AI model - Paid service</p>
                  <span class="inline-block mt-1 px-2 py-1 bg-orange-600 text-white text-xs rounded">Optional</span>
                </div>
                <label class="relative inline-flex items-center cursor-pointer">
                  <input v-model="aiProviders.openai_enabled" type="checkbox" class="sr-only peer">
                  <div class="w-11 h-6 bg-gray-600 peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-cyan-800 rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-cyan-600"></div>
                </label>
              </div>
              <div v-if="aiProviders.openai_enabled">
                <label class="block text-sm font-medium text-gray-300 mb-2">API Key</label>
                <input v-model="aiProviders.openai_key" type="password" class="w-full bg-gray-700 border border-gray-600 rounded-lg px-4 py-2 text-white focus:ring-2 focus:ring-cyan-500 focus:outline-none" placeholder="sk-...">
              </div>
            </div>
          </div>
        </div>

        <!-- Completion Step -->
        <div v-if="currentStep === 3" class="text-center">
          <div class="text-6xl mb-6">✅</div>
          <h2 class="text-3xl font-bold text-white mb-4">Setup Complete!</h2>
          <p class="text-gray-300 mb-8">PostXAgent is now configured and ready to use</p>

          <div class="bg-gray-700/50 p-6 rounded-lg border border-gray-600 max-w-2xl mx-auto text-left">
            <h3 class="text-lg font-semibold text-cyan-400 mb-4">Quick Tips to Get Started:</h3>
            <ul class="space-y-3 text-gray-300">
              <li class="flex items-start">
                <span class="text-cyan-400 mr-3">1.</span>
                <span>Go to Settings to configure your social media accounts</span>
              </li>
              <li class="flex items-start">
                <span class="text-cyan-400 mr-3">2.</span>
                <span>Visit AI Providers page to test your AI connections</span>
              </li>
              <li class="flex items-start">
                <span class="text-cyan-400 mr-3">3.</span>
                <span>Create your first campaign from the Dashboard</span>
              </li>
              <li class="flex items-start">
                <span class="text-cyan-400 mr-3">4.</span>
                <span>Use Content Creator to generate AI-powered posts</span>
              </li>
            </ul>
          </div>
        </div>

        <!-- Navigation -->
        <div class="flex justify-between mt-8 pt-8 border-t border-gray-700">
          <button
            v-if="currentStep > 0"
            @click="previousStep"
            class="bg-gray-700 hover:bg-gray-600 text-white px-6 py-2 rounded-lg font-medium transition-all"
          >
            Back
          </button>
          <div v-else></div>

          <button
            v-if="currentStep < steps.length - 1"
            @click="nextStep"
            :disabled="!canProceed"
            class="bg-gradient-to-r from-cyan-600 to-blue-600 hover:from-cyan-700 hover:to-blue-700 text-white px-8 py-2 rounded-lg font-medium transition-all disabled:opacity-50 disabled:cursor-not-allowed"
          >
            Next
          </button>

          <button
            v-else
            @click="completeSetup"
            :disabled="completing"
            class="bg-gradient-to-r from-green-600 to-emerald-600 hover:from-green-700 hover:to-emerald-700 text-white px-8 py-2 rounded-lg font-medium transition-all disabled:opacity-50"
          >
            {{ completing ? 'Completing...' : 'Finish' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'SetupWizard',
  data() {
    return {
      currentStep: 0,
      steps: [
        { title: 'Welcome', key: 'welcome' },
        { title: 'Database', key: 'database' },
        { title: 'AI Providers', key: 'ai' },
        { title: 'Complete', key: 'complete' }
      ],
      dbConfig: {
        driver: 'mysql',
        host: 'localhost',
        port: 3306,
        database: 'postxagent',
        username: 'root',
        password: ''
      },
      aiProviders: {
        ollama_enabled: true,
        ollama_url: 'http://localhost:11434',
        openai_enabled: false,
        openai_key: '',
        gemini_enabled: false,
        gemini_key: ''
      },
      testing: false,
      completing: false,
      dbTestResult: null
    }
  },
  computed: {
    canProceed() {
      if (this.currentStep === 0) return true
      if (this.currentStep === 1) return this.dbTestResult?.success
      if (this.currentStep === 2) return true
      return false
    }
  },
  methods: {
    nextStep() {
      if (this.currentStep < this.steps.length - 1) {
        if (this.currentStep === 1) {
          this.saveDatabase()
        } else if (this.currentStep === 2) {
          this.saveAIProviders()
        }
        this.currentStep++
      }
    },
    previousStep() {
      if (this.currentStep > 0) {
        this.currentStep--
      }
    },
    async testDatabase() {
      this.testing = true
      this.dbTestResult = null

      try {
        const response = await fetch('/setup/test-database', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-CSRF-TOKEN': document.querySelector('meta[name="csrf-token"]').content
          },
          body: JSON.stringify(this.dbConfig)
        })

        const data = await response.json()
        this.dbTestResult = data
      } catch (error) {
        this.dbTestResult = {
          success: false,
          message: 'Connection test failed: ' + error.message
        }
      } finally {
        this.testing = false
      }
    },
    async saveDatabase() {
      try {
        await fetch('/setup/save-database', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-CSRF-TOKEN': document.querySelector('meta[name="csrf-token"]').content
          },
          body: JSON.stringify(this.dbConfig)
        })
      } catch (error) {
        console.error('Failed to save database config:', error)
      }
    },
    async saveAIProviders() {
      try {
        await fetch('/setup/save-ai-providers', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-CSRF-TOKEN': document.querySelector('meta[name="csrf-token"]').content
          },
          body: JSON.stringify(this.aiProviders)
        })
      } catch (error) {
        console.error('Failed to save AI providers:', error)
      }
    },
    async completeSetup() {
      this.completing = true

      try {
        const response = await fetch('/setup/complete', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-CSRF-TOKEN': document.querySelector('meta[name="csrf-token"]').content
          }
        })

        const data = await response.json()

        if (data.success) {
          window.location.href = data.redirect || '/'
        }
      } catch (error) {
        console.error('Failed to complete setup:', error)
      } finally {
        this.completing = false
      }
    }
  }
}
</script>
