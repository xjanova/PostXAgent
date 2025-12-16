import './bootstrap';
import { createApp } from 'vue';
import { createPinia } from 'pinia';
import router from './router';
import { useAuthStore } from './stores/auth';

// Import root component
import App from './App.vue';

// Create Vue app
const app = createApp(App);

// Use Pinia for state management
const pinia = createPinia();
app.use(pinia);

// Use Vue Router
app.use(router);

// Initialize auth store and check for existing session
const authStore = useAuthStore();
authStore.initializeFromStorage().then(() => {
    // Mount app after auth initialization
    app.mount('#app');
});
