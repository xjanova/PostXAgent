$files = Get-ChildItem -Path 'D:\Code\PostXAgent\postxagent_app\lib' -Recurse -Filter '*.dart'
foreach ($f in $files) {
    $content = Get-Content $f.FullName -Raw
    if ($content -match 'withOpacity') {
        $newContent = $content -replace '\.withOpacity\(', '.withValues(alpha: '
        Set-Content $f.FullName -Value $newContent -NoNewline
        Write-Host $f.Name
    }
}
