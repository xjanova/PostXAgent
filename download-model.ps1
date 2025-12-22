# PostXAgent - Download AI Model from Hugging Face
# This script downloads the best AI model for your system

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║           PostXAgent - AI Model Downloader                   ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Check system resources
$ram = [math]::Round((Get-CimInstance Win32_ComputerSystem).TotalPhysicalMemory / 1GB, 1)
Write-Host "System RAM: $ram GB" -ForegroundColor Yellow

# Get GPU info
$gpu = Get-CimInstance Win32_VideoController | Select-Object -First 1
$gpuName = $gpu.Name
$vram = [math]::Round($gpu.AdapterRAM / 1GB, 1)
Write-Host "GPU: $gpuName ($vram GB VRAM)" -ForegroundColor Yellow
Write-Host ""

# Select model based on RAM
$modelRepo = ""
$modelFile = ""
$modelName = ""
$modelSize = 0

if ($ram -ge 16) {
    $modelRepo = "lmstudio-community/Meta-Llama-3.1-8B-Instruct-GGUF"
    $modelFile = "Meta-Llama-3.1-8B-Instruct-Q4_K_M.gguf"
    $modelName = "Llama 3.1 8B"
    $modelSize = 4.9
} elseif ($ram -ge 8) {
    $modelRepo = "lmstudio-community/Llama-3.2-3B-Instruct-GGUF"
    $modelFile = "Llama-3.2-3B-Instruct-Q4_K_M.gguf"
    $modelName = "Llama 3.2 3B"
    $modelSize = 2.0
} else {
    $modelRepo = "microsoft/Phi-3-mini-4k-instruct-gguf"
    $modelFile = "Phi-3-mini-4k-instruct-q4.gguf"
    $modelName = "Phi-3 Mini"
    $modelSize = 2.2
}

Write-Host "Selected Model: $modelName" -ForegroundColor Green
Write-Host "Size: ~$modelSize GB" -ForegroundColor Green
Write-Host ""

# Create models directory
$modelsDir = "$env:LOCALAPPDATA\PostXAgent\models"
if (-not (Test-Path $modelsDir)) {
    New-Item -ItemType Directory -Path $modelsDir -Force | Out-Null
}

$modelPath = "$modelsDir\$modelFile"

# Check if already downloaded
if (Test-Path $modelPath) {
    Write-Host "✓ Model already downloaded at: $modelPath" -ForegroundColor Green
    Write-Host ""
    Write-Host "To use with Ollama, run:" -ForegroundColor Yellow
    Write-Host "  ollama pull llama3.1:8b" -ForegroundColor White
    exit 0
}

# Download URL
$downloadUrl = "https://huggingface.co/$modelRepo/resolve/main/$modelFile"

Write-Host "Downloading from Hugging Face..." -ForegroundColor Cyan
Write-Host "URL: $downloadUrl" -ForegroundColor Gray
Write-Host ""

try {
    # Use BITS for reliable download with progress
    $tempPath = "$modelPath.tmp"

    # Try with Invoke-WebRequest (shows progress)
    $ProgressPreference = 'Continue'
    Invoke-WebRequest -Uri $downloadUrl -OutFile $tempPath -UseBasicParsing

    # Rename to final path
    Move-Item -Path $tempPath -Destination $modelPath -Force

    Write-Host ""
    Write-Host "✓ Download completed!" -ForegroundColor Green
    Write-Host "  Path: $modelPath" -ForegroundColor White
    Write-Host ""
    Write-Host "Next steps:" -ForegroundColor Yellow
    Write-Host "  1. Install Ollama from https://ollama.ai" -ForegroundColor White
    Write-Host "  2. Run: ollama pull llama3.1:8b" -ForegroundColor White
    Write-Host "  3. Or import this GGUF file directly" -ForegroundColor White
}
catch {
    Write-Host ""
    Write-Host "✗ Download failed: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Alternative: Download manually from:" -ForegroundColor Yellow
    Write-Host "  $downloadUrl" -ForegroundColor White

    # Cleanup temp file
    if (Test-Path $tempPath) {
        Remove-Item $tempPath -Force
    }
    exit 1
}
