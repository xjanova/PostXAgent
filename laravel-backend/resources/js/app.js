import './bootstrap';
import { createApp } from 'vue';
import { createPinia } from 'pinia';
import router from './router';
import { useAuthStore } from './stores/auth';

// Import root component
import App from './App.vue';

// Import Setup Wizard component
import SetupWizard from './components/SetupWizard.vue';

// Create Vue app
const app = createApp(App);

// Register global components
app.component('setup-wizard', SetupWizard);

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
