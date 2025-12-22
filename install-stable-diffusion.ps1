# PostXAgent - Stable Diffusion WebUI Installer
# This script installs AUTOMATIC1111's Stable Diffusion WebUI for FREE image generation

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     PostXAgent - Stable Diffusion WebUI Installer            ║" -ForegroundColor Cyan
Write-Host "║                    FREE Image Generation                      ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Check system requirements
$gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1
$gpuName = $gpu.Name
$vram = [math]::Round($gpu.AdapterRAM / 1GB, 1)
$isNvidia = $gpuName -match "NVIDIA"

Write-Host "System Check:" -ForegroundColor Yellow
Write-Host "  GPU: $gpuName" -ForegroundColor White
Write-Host "  VRAM: ~$vram GB" -ForegroundColor White
Write-Host ""

if (-not $isNvidia) {
    Write-Host "⚠ Warning: NVIDIA GPU recommended for best performance" -ForegroundColor Yellow
    Write-Host "  AMD/Intel GPUs may work but slower" -ForegroundColor Yellow
    Write-Host ""
}

# Check for Python
$pythonVersion = python --version 2>$null
if (-not $pythonVersion) {
    Write-Host "✗ Python not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Python 3.10.x from:" -ForegroundColor Yellow
    Write-Host "  https://www.python.org/downloads/" -ForegroundColor White
    Write-Host ""
    Write-Host "Make sure to check 'Add Python to PATH' during installation" -ForegroundColor Yellow
    exit 1
}
Write-Host "✓ Python found: $pythonVersion" -ForegroundColor Green

# Check for Git
$gitVersion = git --version 2>$null
if (-not $gitVersion) {
    Write-Host "✗ Git not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install Git from:" -ForegroundColor Yellow
    Write-Host "  https://git-scm.com/download/win" -ForegroundColor White
    exit 1
}
Write-Host "✓ Git found: $gitVersion" -ForegroundColor Green
Write-Host ""

# Installation directory
$installDir = "$env:LOCALAPPDATA\PostXAgent\stable-diffusion-webui"

if (Test-Path $installDir) {
    Write-Host "Stable Diffusion WebUI already exists at:" -ForegroundColor Yellow
    Write-Host "  $installDir" -ForegroundColor White
    Write-Host ""
    $response = Read-Host "Do you want to update it? (y/n)"
    if ($response -eq "y") {
        Set-Location $installDir
        git pull
        Write-Host "✓ Updated successfully!" -ForegroundColor Green
    }
} else {
    Write-Host "Installing Stable Diffusion WebUI..." -ForegroundColor Cyan
    Write-Host "  Location: $installDir" -ForegroundColor White
    Write-Host ""
    Write-Host "This may take 10-30 minutes depending on your internet speed." -ForegroundColor Yellow
    Write-Host ""

    # Create parent directory
    $parentDir = Split-Path $installDir -Parent
    if (-not (Test-Path $parentDir)) {
        New-Item -ItemType Directory -Path $parentDir -Force | Out-Null
    }

    # Clone repository
    git clone https://github.com/AUTOMATIC1111/stable-diffusion-webui.git $installDir

    if ($LASTEXITCODE -ne 0) {
        Write-Host "✗ Failed to clone repository" -ForegroundColor Red
        exit 1
    }

    Write-Host "✓ Repository cloned" -ForegroundColor Green
}

# Create startup script with API enabled
$startupScript = @"
@echo off
cd /d "$installDir"
set COMMANDLINE_ARGS=--api --listen --xformers
call webui-user.bat
"@

$startupPath = "$installDir\start-with-api.bat"
$startupScript | Out-File -FilePath $startupPath -Encoding ASCII
Write-Host "✓ Created startup script: $startupPath" -ForegroundColor Green

# Create environment variable setter
Write-Host ""
Write-Host "Setting environment variable SD_API_URL..." -ForegroundColor Cyan
[Environment]::SetEnvironmentVariable("SD_API_URL", "http://localhost:7860", "User")
Write-Host "✓ SD_API_URL set to http://localhost:7860" -ForegroundColor Green

# Download recommended model
Write-Host ""
Write-Host "Would you like to download a recommended model?" -ForegroundColor Yellow
Write-Host ""
Write-Host "Available models:" -ForegroundColor Cyan
Write-Host "  1. Stable Diffusion XL Base 1.0 (6.5GB) - Best quality" -ForegroundColor White
Write-Host "  2. Stable Diffusion 1.5 (4GB) - Faster, good quality" -ForegroundColor White
Write-Host "  3. Skip - I'll download models myself" -ForegroundColor White
Write-Host ""

$modelChoice = Read-Host "Enter choice (1/2/3)"

$modelsDir = "$installDir\models\Stable-diffusion"
if (-not (Test-Path $modelsDir)) {
    New-Item -ItemType Directory -Path $modelsDir -Force | Out-Null
}

switch ($modelChoice) {
    "1" {
        Write-Host ""
        Write-Host "Downloading Stable Diffusion XL Base 1.0..." -ForegroundColor Cyan
        Write-Host "This is a large file (6.5GB), please wait..." -ForegroundColor Yellow

        $modelUrl = "https://huggingface.co/stabilityai/stable-diffusion-xl-base-1.0/resolve/main/sd_xl_base_1.0.safetensors"
        $modelPath = "$modelsDir\sd_xl_base_1.0.safetensors"

        try {
            Invoke-WebRequest -Uri $modelUrl -OutFile $modelPath -UseBasicParsing
            Write-Host "✓ Model downloaded!" -ForegroundColor Green
        }
        catch {
            Write-Host "✗ Download failed: $_" -ForegroundColor Red
            Write-Host "  You can download manually from:" -ForegroundColor Yellow
            Write-Host "  $modelUrl" -ForegroundColor White
        }
    }
    "2" {
        Write-Host ""
        Write-Host "Downloading Stable Diffusion 1.5..." -ForegroundColor Cyan

        $modelUrl = "https://huggingface.co/runwayml/stable-diffusion-v1-5/resolve/main/v1-5-pruned-emaonly.safetensors"
        $modelPath = "$modelsDir\v1-5-pruned-emaonly.safetensors"

        try {
            Invoke-WebRequest -Uri $modelUrl -OutFile $modelPath -UseBasicParsing
            Write-Host "✓ Model downloaded!" -ForegroundColor Green
        }
        catch {
            Write-Host "✗ Download failed: $_" -ForegroundColor Red
            Write-Host "  You can download manually from:" -ForegroundColor Yellow
            Write-Host "  $modelUrl" -ForegroundColor White
        }
    }
    default {
        Write-Host "Skipping model download." -ForegroundColor Yellow
        Write-Host "You can download models from https://civitai.com/" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                    Installation Complete!                     ║" -ForegroundColor Green
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "To start Stable Diffusion WebUI:" -ForegroundColor Yellow
Write-Host "  1. Run: $startupPath" -ForegroundColor White
Write-Host "  2. Wait for 'Running on local URL: http://127.0.0.1:7860'" -ForegroundColor White
Write-Host "  3. PostXAgent will automatically use it for image generation!" -ForegroundColor White
Write-Host ""
Write-Host "First startup will download additional files (~5GB)" -ForegroundColor Yellow
Write-Host ""
Write-Host "Tips:" -ForegroundColor Cyan
Write-Host "  - More models: https://civitai.com/" -ForegroundColor White
Write-Host "  - Put .safetensors files in: $modelsDir" -ForegroundColor White
Write-Host ""

$startNow = Read-Host "Start Stable Diffusion WebUI now? (y/n)"
if ($startNow -eq "y") {
    Write-Host "Starting Stable Diffusion WebUI..." -ForegroundColor Cyan
    Write-Host "This window will stay open. Press Ctrl+C to stop." -ForegroundColor Yellow
    Start-Process -FilePath $startupPath -WorkingDirectory $installDir
}
