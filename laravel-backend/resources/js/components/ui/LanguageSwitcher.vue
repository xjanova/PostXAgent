<template>
  <div class="language-switcher">
    <button
      @click="toggleDropdown"
      class="lang-button"
      :aria-expanded="isOpen"
      aria-haspopup="true"
    >
      <span class="flag">{{ currentFlag }}</span>
      <span class="lang-code">{{ currentLang.toUpperCase() }}</span>
      <svg class="arrow" :class="{ 'rotate': isOpen }" viewBox="0 0 20 20" fill="currentColor">
        <path fill-rule="evenodd" d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" clip-rule="evenodd" />
      </svg>
    </button>

    <transition name="fade">
      <div v-if="isOpen" class="dropdown" @click.stop>
        <button
          v-for="lang in languages"
          :key="lang.code"
          @click="selectLanguage(lang.code)"
          class="lang-option"
          :class="{ 'active': currentLang === lang.code }"
        >
          <span class="flag">{{ lang.flag }}</span>
          <span class="lang-name">{{ lang.name }}</span>
          <svg v-if="currentLang === lang.code" class="check" viewBox="0 0 20 20" fill="currentColor">
            <path fill-rule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clip-rule="evenodd" />
          </svg>
        </button>
      </div>
    </transition>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'

const emit = defineEmits(['change'])

const languages = [
  { code: 'th', name: 'ไทย', flag: '🇹🇭' },
  { code: 'en', name: 'English', flag: '🇺🇸' },
]

const isOpen = ref(false)
const currentLang = ref(localStorage.getItem('locale') || 'th')

const currentFlag = computed(() => {
  const lang = languages.find(l => l.code === currentLang.value)
  return lang?.flag || '🇹🇭'
})

function toggleDropdown() {
  isOpen.value = !isOpen.value
}

function closeDropdown() {
  isOpen.value = false
}

function selectLanguage(code) {
  currentLang.value = code
  localStorage.setItem('locale', code)
  isOpen.value = false

  // Update API header for subsequent requests
  window.axios?.defaults.headers.common['X-Locale'] = code

  // Emit change event
  emit('change', code)

  // Optional: Reload page to apply all translations
  // window.location.reload()
}

// Close dropdown on outside click
function handleClickOutside(event) {
  const el = document.querySelector('.language-switcher')
  if (el && !el.contains(event.target)) {
    closeDropdown()
  }
}

onMounted(() => {
  document.addEventListener('click', handleClickOutside)

  // Set initial locale header
  if (window.axios) {
    window.axios.defaults.headers.common['X-Locale'] = currentLang.value
  }
})

onUnmounted(() => {
  document.removeEventListener('click', handleClickOutside)
})
</script>

<style scoped>
.language-switcher {
  position: relative;
  display: inline-block;
}

.lang-button {
  display: flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  background: var(--button-bg, #f5f5f5);
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 6px;
  cursor: pointer;
  font-size: 14px;
  transition: all 0.2s;
}

.lang-button:hover {
  background: var(--button-hover-bg, #e8e8e8);
}

.flag {
  font-size: 18px;
}

.lang-code {
  font-weight: 500;
}

.arrow {
  width: 16px;
  height: 16px;
  transition: transform 0.2s;
}

.arrow.rotate {
  transform: rotate(180deg);
}

.dropdown {
  position: absolute;
  top: 100%;
  right: 0;
  margin-top: 4px;
  min-width: 150px;
  background: var(--dropdown-bg, #fff);
  border: 1px solid var(--border-color, #e0e0e0);
  border-radius: 8px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  overflow: hidden;
  z-index: 1000;
}

.lang-option {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 10px 14px;
  background: none;
  border: none;
  cursor: pointer;
  font-size: 14px;
  text-align: left;
  transition: background 0.2s;
}

.lang-option:hover {
  background: var(--hover-bg, #f5f5f5);
}

.lang-option.active {
  background: var(--active-bg, #e8f4fc);
}

.lang-name {
  flex: 1;
}

.check {
  width: 16px;
  height: 16px;
  color: var(--primary-color, #3498db);
}

/* Transition */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s, transform 0.2s;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
  transform: translateY(-10px);
}
</style>
