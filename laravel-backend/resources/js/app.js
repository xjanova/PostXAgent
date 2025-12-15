import './bootstrap';
import { createApp } from 'vue';
import AIManagerStatus from './components/AIManagerStatus.vue';

// Mount Vue components
const app = createApp({});
app.component('ai-manager-status', AIManagerStatus);
app.mount('#app');
