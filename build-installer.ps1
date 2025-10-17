# Build SrtExtractor Installer
# This script builds the application and creates an Inno Setup installer

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0-dev",
    [string]$Thumbprint = "",
    [string]$TimestampUrl = "https://timestamp.digicert.com",
    [switch]$SkipSign
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
    
    # Download FFmpeg
    Write-Host "Downloading FFmpeg..." -ForegroundColor Yellow
    .\download-ffmpeg.ps1
    
    # Copy FFmpeg tools to output directory
    if (Test-Path "tools\ffmpeg\ffmpeg.exe") {
        Copy-Item "tools\ffmpeg\ffmpeg.exe" "SrtExtractor\bin\$Configuration\net9.0-windows\" -Force
        Copy-Item "tools\ffmpeg\ffprobe.exe" "SrtExtractor\bin\$Configuration\net9.0-windows\" -Force
        Write-Host "Copied FFmpeg tools to output directory" -ForegroundColor Green
    }

    # Sign SrtExtractor.exe (if requested)
    $exePath = "SrtExtractor\bin\$Configuration\net9.0-windows\SrtExtractor.exe"
    if (-not $SkipSign.IsPresent) {
        if (-not [string]::IsNullOrWhiteSpace($Thumbprint)) {
            if (Test-Path $exePath) {
                Write-Host "Signing SrtExtractor.exe..." -ForegroundColor Yellow
                & signtool sign /sha1 $Thumbprint /fd SHA256 /td SHA256 /tr $TimestampUrl $exePath
                if ($LASTEXITCODE -ne 0) {
                    Write-Error "Code signing SrtExtractor.exe failed"
                    exit 1
                }

                Write-Host "Verifying signature for SrtExtractor.exe..." -ForegroundColor Yellow
                & signtool verify /pa /all $exePath
                if ($LASTEXITCODE -ne 0) {
                    Write-Error "Signature verification failed for SrtExtractor.exe"
                    exit 1
                }
                Write-Host "SrtExtractor.exe signed and verified" -ForegroundColor Green
            } else {
                Write-Error "Executable not found at $exePath"
                exit 1
            }
        } else {
            Write-Warning "Thumbprint not provided; skipping executable signing. Use -Thumbprint to enable signing."
        }
    } else {
        Write-Host "SkipSign specified; not signing SrtExtractor.exe" -ForegroundColor Yellow
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
    $innoArgs = @("SrtExtractorSetup.iss", "/DMyAppVersion=$Version")
    if (-not $SkipSign.IsPresent -and -not [string]::IsNullOrWhiteSpace($Thumbprint)) {
        $innoArgs += "/DEnableSigning=1"
        $innoArgs += "/DMyCertThumbprint=$Thumbprint"
        $innoArgs += "/DMyTimestampUrl=$TimestampUrl"
    }
    & $innoSetupPath @innoArgs
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installer built successfully!" -ForegroundColor Green
        Write-Host "Installer location: artifacts\SrtExtractorInstaller.exe" -ForegroundColor Cyan
        $installerPath = "artifacts\SrtExtractorInstaller.exe"
        if (Test-Path $installerPath) {
            if (-not $SkipSign.IsPresent -and -not [string]::IsNullOrWhiteSpace($Thumbprint)) {
                Write-Host "Verifying installer signature..." -ForegroundColor Yellow
                & signtool verify /pa /all $installerPath
                if ($LASTEXITCODE -ne 0) {
                    Write-Error "Signature verification failed for installer"
                    exit 1
                }
                Write-Host "Installer signature verified" -ForegroundColor Green
            } else {
                Write-Host "Installer built without signing (SkipSign or no Thumbprint)" -ForegroundColor Yellow
            }
        } else {
            Write-Error "Expected installer not found at $installerPath"
            exit 1
        }
    } else {
        Write-Error "Installer build failed"
        exit 1
    }
    
} catch {
    Write-Error "Build failed: $($_.Exception.Message)"
    exit 1
}
