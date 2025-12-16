<template>
    <div class="min-h-screen flex items-center justify-center bg-gradient-to-br from-gray-900 via-indigo-900 to-gray-900">
        <div class="w-full max-w-md p-8">
            <!-- Logo -->
            <div class="text-center mb-8">
                <h1 class="text-3xl font-bold text-white">PostXAgent</h1>
                <p class="text-gray-400 mt-2">AI-Powered Social Media Manager</p>
            </div>

            <!-- Card -->
            <div class="bg-gray-800 rounded-2xl shadow-xl p-8 border border-gray-700">
                <router-view />
            </div>

            <!-- Footer -->
            <div class="text-center mt-6 text-gray-500 text-sm">
                &copy; {{ new Date().getFullYear() }} PostXAgent. All rights reserved.
            </div>
        </div>
    </div>
</template>
