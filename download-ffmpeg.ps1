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
$baseUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest"
$zipFile = "ffmpeg-master-latest-$Architecture-gpl.zip"
$downloadUrl = "$baseUrl/$zipFile"

# Alternative URLs to try if the first one fails
$alternativeUrls = @(
    "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2024-01-14-12-50/ffmpeg-master-latest-$Architecture-gpl.zip",
    "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2023-12-17-12-50/ffmpeg-master-latest-$Architecture-gpl.zip",
    "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2023-11-19-12-50/ffmpeg-master-latest-$Architecture-gpl.zip",
    "https://github.com/GyanD/codexffmpeg/releases/download/6.1.1/ffmpeg-6.1.1-$Architecture-gpl.zip"
)

try {
    # Try to download FFmpeg from multiple URLs
    $downloadSuccess = $false
    $urlsToTry = @($downloadUrl) + $alternativeUrls
    
    foreach ($url in $urlsToTry) {
        try {
            Write-Host "Trying to download from: $url" -ForegroundColor Yellow
            Invoke-WebRequest -Uri $url -OutFile $zipFile -UseBasicParsing
            $downloadSuccess = $true
            Write-Host "Successfully downloaded from: $url" -ForegroundColor Green
            break
        }
        catch {
            Write-Warning "Failed to download from $url : $($_.Exception.Message)"
            if (Test-Path $zipFile) {
                Remove-Item $zipFile -Force
            }
        }
    }
    
    if (-not $downloadSuccess) {
        throw "Failed to download FFmpeg from any of the provided URLs"
    }
    
    # Extract the zip file
    Write-Host "Extracting FFmpeg..." -ForegroundColor Yellow
    Expand-Archive -Path $zipFile -DestinationPath "temp" -Force
    
    # Find the extracted directory (it has a random name or specific name)
    $extractedDir = Get-ChildItem -Path "temp" -Directory | Select-Object -First 1
    
    if ($extractedDir) {
        # Look for ffmpeg.exe and ffprobe.exe in different possible locations
        $ffmpegExe = $null
        $ffprobeExe = $null
        
        # Try bin directory first (BtbN builds)
        $binDir = Join-Path $extractedDir.FullName "bin"
        if (Test-Path $binDir) {
            $ffmpegExe = Join-Path $binDir "ffmpeg.exe"
            $ffprobeExe = Join-Path $binDir "ffprobe.exe"
        } else {
            # Try root directory (GyanD builds)
            $ffmpegExe = Join-Path $extractedDir.FullName "ffmpeg.exe"
            $ffprobeExe = Join-Path $extractedDir.FullName "ffprobe.exe"
        }
        
        # Copy the files if found
        if ((Test-Path $ffmpegExe) -and (Test-Path $ffprobeExe)) {
            Copy-Item $ffmpegExe $OutputDir -Force
            Copy-Item $ffprobeExe $OutputDir -Force
            Write-Host "FFmpeg downloaded and extracted successfully!" -ForegroundColor Green
        } else {
            Write-Error "Could not find ffmpeg.exe and ffprobe.exe in extracted archive"
            Write-Host "Contents of extracted directory:" -ForegroundColor Yellow
            Get-ChildItem -Path $extractedDir.FullName -Recurse | Select-Object Name, FullName
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
