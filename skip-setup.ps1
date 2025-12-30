$path = Join-Path $env:LOCALAPPDATA "PostXAgent"
New-Item -ItemType Directory -Force -Path $path | Out-Null

$content = @"
{
  "SetupCompleted": true,
  "CompletedAt": "2025-12-30T00:00:00Z",
  "Version": "1.0.0"
}
"@

$filePath = Join-Path $path "setup-state.json"
Set-Content -Path $filePath -Value $content -Encoding UTF8
Write-Host "Created: $filePath"
Get-Content $filePath
