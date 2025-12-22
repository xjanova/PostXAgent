# PostXAgent Ollama CPU Optimizer
# Script: restart-ollama-cpu-optimized.ps1
# Purpose: Restart Ollama with CPU optimization settings for multi-core CPUs

Write-Host "=== Ollama CPU Optimizer ===" -ForegroundColor Cyan
Write-Host ""

# Detect CPU Information
Write-Host "Detecting CPU..." -ForegroundColor Yellow
$cpu = Get-CimInstance Win32_Processor
$physicalCores = ($cpu | Measure-Object -Property NumberOfCores -Sum).Sum
$logicalCores = ($cpu | Measure-Object -Property NumberOfLogicalProcessors -Sum).Sum
$cpuName = $cpu[0].Name

Write-Host "  CPU: $cpuName" -ForegroundColor White
Write-Host "  Physical Cores: $physicalCores" -ForegroundColor White
Write-Host "  Logical Cores: $logicalCores" -ForegroundColor White

# Detect if Server CPU
$isServerCpu = $cpuName -match "Xeon|EPYC|Opteron|Threadripper"
if ($isServerCpu) {
    Write-Host "  Type: Server CPU" -ForegroundColor Green
} else {
    Write-Host "  Type: Desktop CPU" -ForegroundColor Green
}

# Detect RAM
$ram = Get-CimInstance Win32_ComputerSystem
$totalRamGB = [math]::Round($ram.TotalPhysicalMemory / 1GB)
Write-Host "  RAM: ${totalRamGB}GB" -ForegroundColor White

Write-Host ""

# Calculate optimal settings
Write-Host "Calculating optimal settings..." -ForegroundColor Yellow

# NUM_THREADS - Use physical cores (HyperThreading doesn't help much for LLM)
if ($isServerCpu) {
    $numThreads = [math]::Max(4, $physicalCores - 2)
    if ($numThreads -gt 64) { $numThreads = 64 }
} else {
    $numThreads = [math]::Max(2, $physicalCores - 1)
}

# NUM_PARALLEL - Concurrent requests
if ($isServerCpu -and $physicalCores -ge 16) {
    $numParallel = [math]::Min(8, [math]::Floor($physicalCores / 8))
} elseif ($physicalCores -ge 8) {
    $numParallel = 2
} else {
    $numParallel = 1
}

# NUM_BATCH - Based on RAM
if ($totalRamGB -ge 64) {
    $numBatch = 1024
} elseif ($totalRamGB -ge 32) {
    $numBatch = 512
} elseif ($totalRamGB -ge 16) {
    $numBatch = 256
} else {
    $numBatch = 128
}

# NUM_CTX - Context size based on RAM
if ($totalRamGB -ge 64) {
    $numCtx = 8192
} elseif ($totalRamGB -ge 32) {
    $numCtx = 4096
} elseif ($totalRamGB -ge 16) {
    $numCtx = 2048
} else {
    $numCtx = 1024
}

Write-Host "  NUM_THREADS: $numThreads" -ForegroundColor White
Write-Host "  NUM_PARALLEL: $numParallel" -ForegroundColor White
Write-Host "  NUM_BATCH: $numBatch" -ForegroundColor White
Write-Host "  NUM_CTX: $numCtx" -ForegroundColor White
Write-Host ""

# Stop existing Ollama
Write-Host "Stopping existing Ollama processes..." -ForegroundColor Yellow
$ollamaProcesses = Get-Process -Name "ollama*" -ErrorAction SilentlyContinue
if ($ollamaProcesses) {
    $ollamaProcesses | Stop-Process -Force
    Start-Sleep -Seconds 2
    Write-Host "  Ollama stopped." -ForegroundColor Green
} else {
    Write-Host "  No Ollama process found." -ForegroundColor Gray
}

Write-Host ""

# Set Environment Variables
Write-Host "Setting environment variables..." -ForegroundColor Yellow

# Ollama settings
$env:OLLAMA_NUM_PARALLEL = $numParallel
$env:OLLAMA_MAX_LOADED_MODELS = 1

# llama.cpp / OpenBLAS / MKL / OMP settings for CPU optimization
$env:LLAMA_NUM_THREADS = $numThreads
$env:OPENBLAS_NUM_THREADS = $numThreads
$env:MKL_NUM_THREADS = $numThreads
$env:MKL_DYNAMIC = "FALSE"
$env:OMP_NUM_THREADS = $numThreads
$env:OMP_WAIT_POLICY = "ACTIVE"
$env:GOMP_CPU_AFFINITY = "0-$($numThreads - 1)"

# Disable GPU (CPU-only mode)
$env:CUDA_VISIBLE_DEVICES = ""

Write-Host "  OLLAMA_NUM_PARALLEL=$numParallel" -ForegroundColor Gray
Write-Host "  OMP_NUM_THREADS=$numThreads" -ForegroundColor Gray
Write-Host "  MKL_NUM_THREADS=$numThreads" -ForegroundColor Gray
Write-Host "  CUDA_VISIBLE_DEVICES= (CPU-only mode)" -ForegroundColor Gray
Write-Host ""

# Find Ollama executable
$ollamaPath = "$env:LOCALAPPDATA\Programs\Ollama\ollama.exe"
if (-not (Test-Path $ollamaPath)) {
    $ollamaPath = "ollama"
}

Write-Host "Starting Ollama with CPU optimization..." -ForegroundColor Yellow
Write-Host "  Using: $ollamaPath serve" -ForegroundColor Gray

# Start Ollama as background process
$process = Start-Process -FilePath $ollamaPath -ArgumentList "serve" -PassThru -WindowStyle Hidden

Start-Sleep -Seconds 5

# Verify Ollama is running
try {
    $response = Invoke-WebRequest -Uri "http://localhost:11434/api/tags" -UseBasicParsing -TimeoutSec 10
    Write-Host ""
    Write-Host "Ollama started successfully!" -ForegroundColor Green
    Write-Host "  PID: $($process.Id)" -ForegroundColor White
    Write-Host ""

    # Parse models
    $models = $response.Content | ConvertFrom-Json
    if ($models.models.Count -gt 0) {
        Write-Host "Available models:" -ForegroundColor Cyan
        foreach ($model in $models.models) {
            $sizeMB = [math]::Round($model.size / 1MB)
            Write-Host "  - $($model.name) (${sizeMB}MB)" -ForegroundColor White
        }
    }
} catch {
    Write-Host "Warning: Could not verify Ollama status" -ForegroundColor Yellow
    Write-Host "  Try running: curl http://localhost:11434/api/tags" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=== CPU Optimization Summary ===" -ForegroundColor Cyan
Write-Host "  Threads: $numThreads (of $logicalCores logical cores)" -ForegroundColor White
Write-Host "  Batch Size: $numBatch" -ForegroundColor White
Write-Host "  Context Size: $numCtx" -ForegroundColor White
Write-Host "  Mode: CPU-only (GPU disabled)" -ForegroundColor White
Write-Host ""
Write-Host "To use these settings with Ollama API, include in your request:" -ForegroundColor Yellow
Write-Host @"
{
  "options": {
    "num_thread": $numThreads,
    "num_ctx": $numCtx,
    "num_batch": $numBatch,
    "num_gpu": 0
  }
}
"@ -ForegroundColor Gray
Write-Host ""
Write-Host "Done!" -ForegroundColor Green
