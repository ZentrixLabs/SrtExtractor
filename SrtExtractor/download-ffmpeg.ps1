param(
    [string]$Configuration = "Release"
)

Write-Host "Downloading FFmpeg..." -ForegroundColor Green

# Define our application's output directory
$ourOutputDir = Join-Path (Split-Path $PSScriptRoot -Parent) "SrtExtractor\bin\$Configuration\net9.0-windows"
if (-not (Test-Path $ourOutputDir)) {
    New-Item -ItemType Directory -Path $ourOutputDir -Force | Out-Null
}

# Check if FFmpeg is already downloaded
$ffmpegExe = Join-Path $ourOutputDir "ffmpeg.exe"
$ffprobeExe = Join-Path $ourOutputDir "ffprobe.exe"

if ((Test-Path $ffmpegExe) -and (Test-Path $ffprobeExe)) {
    Write-Host "FFmpeg already exists in output directory" -ForegroundColor Yellow
    exit 0
}

# Create temp directory for download
$tempDir = Join-Path $env:TEMP "ffmpeg-download"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir -Force | Out-Null

try {
    Write-Host "Downloading FFmpeg build from GitHub releases..." -ForegroundColor Yellow
    
    # Download FFmpeg from GitHub releases (using a known working build)
    $ffmpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
    $zipFile = Join-Path $tempDir "ffmpeg.zip"
    
    # Download using PowerShell
    Invoke-WebRequest -Uri $ffmpegUrl -OutFile $zipFile -UseBasicParsing
    
    if (-not (Test-Path $zipFile)) {
        Write-Error "Failed to download FFmpeg"
        exit 1
    }
    
    Write-Host "Extracting FFmpeg..." -ForegroundColor Yellow
    
    # Extract the zip file
    Expand-Archive -Path $zipFile -DestinationPath $tempDir -Force
    
    # Find the extracted directory
    $extractedDir = Get-ChildItem $tempDir -Directory | Where-Object { $_.Name -like "ffmpeg-*" } | Select-Object -First 1
    
    if (-not $extractedDir) {
        Write-Error "Could not find extracted FFmpeg directory"
        exit 1
    }
    
    $binDir = Join-Path $extractedDir.FullName "bin"
    
    # Copy the executables
    $sourceFfmpeg = Join-Path $binDir "ffmpeg.exe"
    $sourceFfprobe = Join-Path $binDir "ffprobe.exe"
    
    if (Test-Path $sourceFfmpeg) {
        Copy-Item $sourceFfmpeg $ourOutputDir -Force
        Write-Host "Copied ffmpeg.exe" -ForegroundColor Green
    }
    
    if (Test-Path $sourceFfprobe) {
        Copy-Item $sourceFfprobe $ourOutputDir -Force
        Write-Host "Copied ffprobe.exe" -ForegroundColor Green
    }
    
    # Copy any required DLLs
    $dllFiles = Get-ChildItem $binDir -Filter "*.dll"
    foreach ($dll in $dllFiles) {
        Copy-Item $dll.FullName $ourOutputDir -Force
        Write-Host "Copied $($dll.Name)" -ForegroundColor Green
    }
    
    Write-Host "FFmpeg download completed successfully!" -ForegroundColor Green
    Write-Host "ffmpeg.exe is now available at: $ffmpegExe" -ForegroundColor Cyan
    Write-Host "ffprobe.exe is now available at: $ffprobeExe" -ForegroundColor Cyan
    
} catch {
    Write-Error "Failed to download FFmpeg: $($_.Exception.Message)"
    exit 1
} finally {
    # Clean up temp directory
    if (Test-Path $tempDir) {
        Remove-Item $tempDir -Recurse -Force
    }
}
