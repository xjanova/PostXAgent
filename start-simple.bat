@echo off
title PostXAgent
color 0A

cd laravel-backend

echo ================================================
echo PostXAgent - Starting Services
echo ================================================
echo.

echo [1/2] Starting Vite dev server...
start "PostXAgent-Vite" cmd /k "npm run dev"

echo Waiting for Vite to start...
timeout /t 5 /nobreak >nul

echo [2/2] Starting Laravel server...
start "PostXAgent-Laravel" cmd /k "php artisan serve"

timeout /t 2 /nobreak >nul

echo.
echo ================================================
echo Services Started!
echo ================================================
echo.
echo Open in browser:
echo   http://localhost:8000/setup
echo.
echo Running services:
echo   - Vite Dev Server: http://localhost:5173
echo   - Laravel Server:  http://localhost:8000
echo.
echo Press any key to STOP all services...
pause >nul

echo.
echo Stopping services...
taskkill /FI "WindowTitle eq PostXAgent-*" /F >nul 2>&1
echo Services stopped.
timeout /t 1 /nobreak >nul
