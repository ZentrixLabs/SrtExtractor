# Build SrtExtractor Installer
# This script builds the application and creates an Inno Setup installer

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0-dev"
)

Write-Host "Building SrtExtractor Installer v$Version" -ForegroundColor Green

# Set error action preference
$ErrorActionPreference = "Stop"

try {
    # Clean previous builds
    Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
    if (Test-Path "artifacts") {
        Remove-Item "artifacts" -Recurse -Force
    }
    if (Test-Path "SrtExtractor\bin\Release") {
        Remove-Item "SrtExtractor\bin\Release" -Recurse -Force
    }
    
    # Create artifacts directory
    New-Item -ItemType Directory -Force -Path "artifacts" | Out-Null
    
    # Restore and build the application
    Write-Host "Building SrtExtractor..." -ForegroundColor Yellow
    dotnet restore "SrtExtractor.sln"
    dotnet build "SrtExtractor.sln" --configuration $Configuration --no-restore
    
    # Build SubtitleEdit CLI if submodule exists
    if (Test-Path "SubtitleEdit-CLI\src\se-cli") {
        Write-Host "Building SubtitleEdit CLI..." -ForegroundColor Yellow
        Push-Location "SubtitleEdit-CLI\src\se-cli"
        dotnet restore
        dotnet build -c $Configuration
        Pop-Location
        
        # Copy seconv.exe to output directory
        $cliExe = "SubtitleEdit-CLI\src\se-cli\bin\$Configuration\net8.0\seconv.exe"
        if (Test-Path $cliExe) {
            Copy-Item $cliExe "SrtExtractor\bin\$Configuration\net9.0-windows\" -Force
            Write-Host "Copied seconv.exe to output directory" -ForegroundColor Green
        }
    } else {
        Write-Warning "SubtitleEdit-CLI submodule not found, skipping CLI build"
    }
    
    # Download FFmpeg if submodule exists
    if (Test-Path "FFmpeg") {
        Write-Host "Downloading FFmpeg..." -ForegroundColor Yellow
        Push-Location "FFmpeg"
        .\download-ffmpeg.ps1
        Pop-Location
        
        # Copy FFmpeg tools to output directory
        if (Test-Path "FFmpeg\ffmpeg.exe") {
            Copy-Item "FFmpeg\ffmpeg.exe" "SrtExtractor\bin\$Configuration\net9.0-windows\" -Force
            Copy-Item "FFmpeg\ffprobe.exe" "SrtExtractor\bin\$Configuration\net9.0-windows\" -Force
            Write-Host "Copied FFmpeg tools to output directory" -ForegroundColor Green
        }
    } else {
        Write-Warning "FFmpeg submodule not found, skipping FFmpeg download"
    }
    
    # Check if Inno Setup is installed
    $innoSetupPath = "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $innoSetupPath)) {
        $innoSetupPath = "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    }
    
    if (-not (Test-Path $innoSetupPath)) {
        Write-Error "Inno Setup not found. Please install Inno Setup 6 from https://jrsoftware.org/isinfo.php"
        exit 1
    }
    
    # Build the installer
    Write-Host "Building installer with Inno Setup..." -ForegroundColor Yellow
    & $innoSetupPath "SrtExtractorSetup.iss" /DMyAppVersion=$Version
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installer built successfully!" -ForegroundColor Green
        Write-Host "Installer location: artifacts\SrtExtractorInstaller.exe" -ForegroundColor Cyan
    } else {
        Write-Error "Installer build failed"
        exit 1
    }
    
} catch {
    Write-Error "Build failed: $($_.Exception.Message)"
    exit 1
}
