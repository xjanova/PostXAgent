@echo off
echo Installing PostXAgent...
echo.

cd laravel-backend

echo [1/4] Installing Laravel dependencies...
call composer install
if %errorLevel% neq 0 exit /b 1

echo [2/4] Installing NPM dependencies...
call npm install
if %errorLevel% neq 0 exit /b 1

echo [3/4] Building frontend...
call npm run build
if %errorLevel% neq 0 exit /b 1

echo [4/4] Setting up environment...
if not exist .env copy .env.example .env
call php artisan key:generate --force

cd ..

echo.
echo Done! Now run: start-simple.bat
pause
