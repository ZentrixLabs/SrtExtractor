param(
    [string]$Configuration = "Release"
)

Write-Host "Building all dependencies..." -ForegroundColor Green

# Build SubtitleEdit-CLI
Write-Host "`n=== Building SubtitleEdit-CLI ===" -ForegroundColor Cyan
& "$PSScriptRoot\build-cli.ps1" -Configuration $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build SubtitleEdit-CLI"
    exit 1
}

# Download FFmpeg
Write-Host "`n=== Downloading FFmpeg ===" -ForegroundColor Cyan
& "$PSScriptRoot\download-ffmpeg.ps1" -Configuration $Configuration

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to download FFmpeg"
    exit 1
}

Write-Host "`n=== All dependencies built successfully! ===" -ForegroundColor Green
Write-Host "SubtitleEdit-CLI: Built from source" -ForegroundColor Yellow
Write-Host "FFmpeg: Downloaded pre-built binaries" -ForegroundColor Yellow
