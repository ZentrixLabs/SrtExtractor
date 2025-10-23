#requires -version 5.1
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

Write-Host "Building SrtExtractor ($Configuration)" -ForegroundColor Green

# Ensure repo root
try {
    $repoRoot = (& git rev-parse --show-toplevel 2>$null)
    if ($repoRoot) { Set-Location -Path $repoRoot }
} catch { }

# Restore and build solution
dotnet restore "SrtExtractor.sln"
dotnet build "SrtExtractor.sln" --configuration $Configuration --no-restore

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

$outputDir = "SrtExtractor\bin\$Configuration\net9.0-windows"
Write-Host "Build completed. Output: $outputDir" -ForegroundColor Cyan


