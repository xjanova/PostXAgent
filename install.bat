@echo off
chcp 65001 >nul
echo ================================================
echo PostXAgent - Auto Installation Script
echo ================================================
echo.

:: Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Please run this script as Administrator
    echo Right-click install.bat and select "Run as administrator"
    pause
    exit /b 1
)

:: Set color
color 0A

echo [1/8] Checking prerequisites...
echo.

:: Check PHP
where php >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] PHP is not installed or not in PATH
    echo Please install PHP 8.2 or higher
    pause
    exit /b 1
)

php -v | findstr "PHP 8"
if %errorLevel% neq 0 (
    echo [WARNING] PHP version might not be compatible. Recommended: PHP 8.2+
)
echo [OK] PHP found

:: Check Composer
where composer >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Composer is not installed or not in PATH
    echo Please install Composer from https://getcomposer.org/
    pause
    exit /b 1
)
echo [OK] Composer found

:: Check Node.js
where node >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] Node.js is not installed or not in PATH
    echo Please install Node.js from https://nodejs.org/
    pause
    exit /b 1
)
echo [OK] Node.js found

:: Check npm
where npm >nul 2>&1
if %errorLevel% neq 0 (
    echo [ERROR] npm is not installed or not in PATH
    pause
    exit /b 1
)
echo [OK] npm found

:: Check .NET SDK
where dotnet >nul 2>&1
if %errorLevel% neq 0 (
    echo [WARNING] .NET SDK not found. C# AI Manager will not be available.
    echo You can install it later from https://dotnet.microsoft.com/download
) else (
    echo [OK] .NET SDK found
)

echo.
echo [2/8] Installing Laravel dependencies...
cd laravel-backend
call composer install --no-interaction --prefer-dist --optimize-autoloader
if %errorLevel% neq 0 (
    echo [ERROR] Composer install failed
    cd ..
    pause
    exit /b 1
)
echo [OK] Laravel dependencies installed

echo.
echo [3/8] Installing NPM dependencies...
call npm install
if %errorLevel% neq 0 (
    echo [ERROR] npm install failed
    cd ..
    pause
    exit /b 1
)
echo [OK] NPM dependencies installed

echo.
echo [4/8] Building frontend assets...
call npm run build
if %errorLevel% neq 0 (
    echo [ERROR] npm run build failed
    cd ..
    pause
    exit /b 1
)
echo [OK] Frontend assets built

echo.
echo [5/8] Setting up environment file...
if not exist .env (
    if exist .env.example (
        copy .env.example .env
        echo [OK] .env file created from .env.example
    ) else (
        echo [ERROR] .env.example not found
        cd ..
        pause
        exit /b 1
    )
) else (
    echo [OK] .env file already exists
)

echo.
echo [6/8] Generating application key...
call php artisan key:generate --force
if %errorLevel% neq 0 (
    echo [ERROR] Failed to generate application key
    cd ..
    pause
    exit /b 1
)
echo [OK] Application key generated

echo.
echo [7/8] Creating storage directories...
if not exist "storage\app\public" mkdir "storage\app\public"
if not exist "storage\framework\cache" mkdir "storage\framework\cache"
if not exist "storage\framework\sessions" mkdir "storage\framework\sessions"
if not exist "storage\framework\views" mkdir "storage\framework\views"
if not exist "storage\logs" mkdir "storage\logs"
echo [OK] Storage directories created

echo.
echo [8/8] Building C# AI Manager (optional)...
cd ..\AIManagerCore
dotnet build --configuration Release >nul 2>&1
if %errorLevel% neq 0 (
    echo [WARNING] C# build failed or .NET SDK not installed
    echo You can build it manually later with: cd AIManagerCore && dotnet build
) else (
    echo [OK] C# AI Manager built successfully
)

cd ..

echo.
echo ================================================
echo Installation Complete! 🎉
echo ================================================
echo.
echo Next steps:
echo 1. Configure your database in: laravel-backend\.env
echo 2. Start Laravel: cd laravel-backend ^&^& php artisan serve
echo 3. Visit: http://localhost:8000/setup
echo.
echo The setup wizard will guide you through the rest!
echo.
echo For AI Manager (optional):
echo - Run: cd AIManagerCore\src\AIManager.UI ^&^& dotnet run
echo.
pause
