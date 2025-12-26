<?php

use Illuminate\Support\Facades\Route;
use App\Http\Controllers\SetupController;

// Setup Wizard Routes (no middleware)
Route::prefix('setup')->group(function () {
    Route::get('/', [SetupController::class, 'index'])->name('setup.index');
    Route::get('/status', [SetupController::class, 'status'])->name('setup.status');
    Route::post('/test-database', [SetupController::class, 'testDatabase'])->name('setup.test-database');
    Route::post('/save-database', [SetupController::class, 'saveDatabase'])->name('setup.save-database');
    Route::post('/save-ai-manager', [SetupController::class, 'saveAIManager'])->name('setup.save-ai-manager');
    Route::post('/save-ai-providers', [SetupController::class, 'saveAIProviders'])->name('setup.save-ai-providers');
    Route::post('/complete', [SetupController::class, 'complete'])->name('setup.complete');
    Route::post('/reset', [SetupController::class, 'reset'])->name('setup.reset');
});

// Main application routes (with first-run check)
Route::middleware(['check.first.run'])->group(function () {
    Route::get('/', function () {
        return view('dashboard');
    })->name('home');
});
