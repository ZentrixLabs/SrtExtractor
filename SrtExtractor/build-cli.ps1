# Build script for SubtitleEdit CLI
# This script compiles the CLI and copies it to our output directory

param(
    [string]$Configuration = "Release",
    [string]$TargetFramework = "net8.0"
)

Write-Host "Building SubtitleEdit CLI..." -ForegroundColor Green

# Navigate to the CLI directory
$cliPath = Join-Path (Split-Path $PSScriptRoot -Parent) "SubtitleEdit-CLI\src\se-cli"
if (-not (Test-Path $cliPath)) {
    Write-Error "SubtitleEdit-CLI not found at $cliPath"
    exit 1
}

    # Build the CLI
    Push-Location $cliPath
    try {
        Write-Host "Restoring packages for SubtitleEdit CLI..." -ForegroundColor Yellow
        dotnet restore
        
        Write-Host "Compiling seconv..." -ForegroundColor Yellow
        dotnet build -c $Configuration
        
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to build SubtitleEdit CLI"
            exit 1
        }
    
    # Find the output directory
    $outputDir = Join-Path $cliPath "bin\$Configuration\$TargetFramework"
    $exePath = Join-Path $outputDir "seconv.exe"
    
    if (-not (Test-Path $exePath)) {
        Write-Error "seconv.exe not found at $exePath"
        exit 1
    }
    
    # Copy to our output directory
    $ourOutputDir = Join-Path $PSScriptRoot "bin\Debug\net9.0-windows"
    if (-not (Test-Path $ourOutputDir)) {
        New-Item -ItemType Directory -Path $ourOutputDir -Force | Out-Null
    }
    
    Write-Host "Copying seconv.exe to output directory..." -ForegroundColor Yellow
    Copy-Item $exePath $ourOutputDir -Force
    
    # Copy all dependencies (DLLs, runtime files, etc.)
    Write-Host "Copying dependencies..." -ForegroundColor Yellow
    $allFiles = Get-ChildItem $outputDir -File
    foreach ($file in $allFiles) {
        Copy-Item $file.FullName $ourOutputDir -Force
    }
    
    # Also copy any subdirectories (like runtimes)
    $subDirs = Get-ChildItem $outputDir -Directory
    foreach ($subDir in $subDirs) {
        $destDir = Join-Path $ourOutputDir $subDir.Name
        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        Copy-Item $subDir.FullName $ourOutputDir -Recurse -Force
    }
    
    Write-Host "SubtitleEdit CLI built successfully!" -ForegroundColor Green
    Write-Host "seconv.exe is now available at: $ourOutputDir\seconv.exe" -ForegroundColor Cyan
    
} finally {
    Pop-Location
}
