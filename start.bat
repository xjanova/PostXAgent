@echo off
chcp 65001 >nul
title PostXAgent - Starting Services
color 0B

echo ================================================
echo PostXAgent - Auto Start Script
echo ================================================
echo.
echo Starting all services...
echo.

:: Kill existing processes (optional - uncomment if needed)
:: taskkill /F /IM php.exe >nul 2>&1
:: taskkill /F /IM node.exe >nul 2>&1

:: Start Vite dev server in new window
echo [1/3] Starting Vite dev server...
cd laravel-backend
start "PostXAgent - Vite Dev Server" cmd /k "npm run dev"
timeout /t 3 /nobreak >nul

:: Start Laravel server in new window
echo [2/3] Starting Laravel server...
start "PostXAgent - Laravel Server" cmd /k "php artisan serve"
timeout /t 2 /nobreak >nul

:: Start AI Manager (optional - uncomment if needed)
:: echo [3/3] Starting AI Manager...
:: cd ..\AIManagerCore\src\AIManager.UI
:: start "PostXAgent - AI Manager" cmd /k "dotnet run"
:: cd ..\..\..

cd ..

echo.
echo ================================================
echo All Services Started! 🚀
echo ================================================
echo.
echo Services running:
echo - Vite Dev Server: http://localhost:5173
echo - Laravel Server:  http://localhost:8000
echo.
echo Setup Wizard: http://localhost:8000/setup
echo.
echo Press any key to stop all services...
pause >nul

:: Stop all services
echo.
echo Stopping services...
taskkill /F /FI "WindowTitle eq PostXAgent*" >nul 2>&1
echo Services stopped.
timeout /t 2 /nobreak >nul
