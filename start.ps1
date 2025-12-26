# PostXAgent - PowerShell Start Script
# Starts all development servers

$ErrorActionPreference = "Continue"

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "PostXAgent - Auto Start Script" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Starting all services..." -ForegroundColor Yellow
Write-Host ""

# Function to find executable
function Find-Executable {
    param([string]$Name, [string[]]$CommonPaths)

    $found = Get-Command $Name -ErrorAction SilentlyContinue
    if ($found) {
        return $found.Source
    }

    foreach ($path in $CommonPaths) {
        if (Test-Path $path) {
            return $path
        }
    }

    return $null
}

# Find executables
$phpPaths = @("C:\php\php.exe", "C:\xampp\php\php.exe")

# Search Laragon
if (Test-Path "C:\laragon\bin\php") {
    $laragonPhps = Get-ChildItem "C:\laragon\bin\php\php-*\php.exe" -ErrorAction SilentlyContinue
    if ($laragonPhps) {
        $phpPaths += $laragonPhps | Select-Object -First 1 -ExpandProperty FullName
    }
}

$php = Find-Executable -Name "php" -CommonPaths $phpPaths
$npm = Find-Executable -Name "npm" -CommonPaths @("C:\Program Files\nodejs\npm.cmd")

if (-not $php) {
    Write-Host "[ERROR] PHP not found" -ForegroundColor Red
    pause
    exit 1
}

if (-not $npm) {
    Write-Host "[ERROR] npm not found" -ForegroundColor Red
    pause
    exit 1
}

Set-Location laravel-backend

# Start Vite dev server
Write-Host "[1/2] Starting Vite dev server..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD'; & '$npm' run dev" -WindowStyle Normal

Start-Sleep -Seconds 3

# Start Laravel server
Write-Host "[2/2] Starting Laravel server..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD'; & '$php' artisan serve" -WindowStyle Normal

Start-Sleep -Seconds 2

Set-Location ..

Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "All Services Started! 🚀" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Services running:" -ForegroundColor Yellow
Write-Host "- Vite Dev Server: " -NoNewline -ForegroundColor White
Write-Host "http://localhost:5173" -ForegroundColor Cyan
Write-Host "- Laravel Server:  " -NoNewline -ForegroundColor White
Write-Host "http://localhost:8000" -ForegroundColor Cyan
Write-Host ""
Write-Host "Setup Wizard: " -NoNewline -ForegroundColor White
Write-Host "http://localhost:8000/setup" -ForegroundColor Green
Write-Host ""
Write-Host "Press Ctrl+C to stop (close the PowerShell windows manually)" -ForegroundColor Yellow
Write-Host ""

# Keep script running
try {
    while ($true) {
        Start-Sleep -Seconds 1
    }
} finally {
    Write-Host "Stopped." -ForegroundColor Yellow
}
