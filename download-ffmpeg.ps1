# Download FFmpeg for SrtExtractor
# This script downloads pre-built FFmpeg binaries for Windows

param(
    [string]$Version = "6.1.1",
    [string]$Architecture = "win64",
    [string]$OutputDir = "tools\ffmpeg"
)

Write-Host "Downloading FFmpeg $Version for $Architecture..." -ForegroundColor Green

# Create output directory
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# FFmpeg download URL (using a stable release)
$baseUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2023-12-17-12-50"
$zipFile = "ffmpeg-master-latest-$Architecture-gpl.zip"
$downloadUrl = "$baseUrl/$zipFile"

try {
    # Download FFmpeg
    Write-Host "Downloading from: $downloadUrl" -ForegroundColor Yellow
    Invoke-WebRequest -Uri $downloadUrl -OutFile $zipFile -UseBasicParsing
    
    # Extract the zip file
    Write-Host "Extracting FFmpeg..." -ForegroundColor Yellow
    Expand-Archive -Path $zipFile -DestinationPath "temp" -Force
    
    # Find the extracted directory (it has a random name)
    $extractedDir = Get-ChildItem -Path "temp" -Directory | Select-Object -First 1
    
    if ($extractedDir) {
        # Copy ffmpeg.exe and ffprobe.exe to the output directory
        $binDir = Join-Path $extractedDir.FullName "bin"
        if (Test-Path $binDir) {
            Copy-Item (Join-Path $binDir "ffmpeg.exe") $OutputDir -Force
            Copy-Item (Join-Path $binDir "ffprobe.exe") $OutputDir -Force
            Write-Host "FFmpeg downloaded and extracted successfully!" -ForegroundColor Green
        } else {
            Write-Error "Could not find bin directory in extracted FFmpeg"
            exit 1
        }
    } else {
        Write-Error "Could not find extracted FFmpeg directory"
        exit 1
    }
    
    # Clean up
    Remove-Item $zipFile -Force
    Remove-Item "temp" -Recurse -Force
    
    # Verify the files exist
    if ((Test-Path (Join-Path $OutputDir "ffmpeg.exe")) -and (Test-Path (Join-Path $OutputDir "ffprobe.exe"))) {
        Write-Host "FFmpeg setup completed successfully!" -ForegroundColor Green
        Write-Host "Files: $OutputDir\ffmpeg.exe, $OutputDir\ffprobe.exe" -ForegroundColor Cyan
    } else {
        Write-Error "FFmpeg files not found after extraction"
        exit 1
    }
    
} catch {
    Write-Error "Failed to download FFmpeg: $($_.Exception.Message)"
    exit 1
}
