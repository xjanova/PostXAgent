<template>
    <router-view />
</template>

<script setup>
// Root component - just renders the router view
</script>

<style>
/* Global styles */
html {
    scroll-behavior: smooth;
}

body {
    font-family: 'Noto Sans Thai', 'Inter', system-ui, -apple-system, sans-serif;
}

/* Custom scrollbar */
::-webkit-scrollbar {
    width: 8px;
    height: 8px;
}

::-webkit-scrollbar-track {
    background: #1f2937;
}

::-webkit-scrollbar-thumb {
    background: #4b5563;
    border-radius: 4px;
}

::-webkit-scrollbar-thumb:hover {
    background: #6b7280;
}

/* Selection color */
::selection {
    background: #6366f1;
    color: white;
}
</style>
