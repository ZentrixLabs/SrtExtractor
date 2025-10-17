#requires -version 5.1
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$Configuration = "Release",
    [Parameter(Mandatory=$true)][string]$Thumbprint,
    [string]$TimestampUrl = "https://timestamp.digicert.com",
    [switch]$NoGit,
    [switch]$NoRelease
)

$ErrorActionPreference = "Stop"

Write-Host "Starting local signed release v$Version" -ForegroundColor Green

try {
    # Build and sign
    Write-Host "Building and signing via build-installer.ps1..." -ForegroundColor Yellow
    pwsh -NoProfile -ExecutionPolicy Bypass -File "build-installer.ps1" -Configuration $Configuration -Version $Version -Thumbprint $Thumbprint -TimestampUrl $TimestampUrl

    $installer = Join-Path $PSScriptRoot "artifacts\SrtExtractorInstaller.exe"
    if (-not (Test-Path $installer)) { throw "Installer not found at $installer" }

    # Verify signatures
    Write-Host "Verifying installer signature..." -ForegroundColor Yellow
    & signtool verify /pa /all $installer

    if (-not $NoGit) {
        # Create git tag and push
        $tag = "v$Version"
        Write-Host "Tagging repo as $tag..." -ForegroundColor Yellow
        git tag -a $tag -m "SrtExtractor $Version"
        git push origin $tag
    } else {
        Write-Host "Skipping git tagging/push as requested" -ForegroundColor Yellow
    }

    if (-not $NoRelease) {
        # Publish GitHub release via gh if available
        $gh = Get-Command gh -ErrorAction SilentlyContinue
        if ($gh) {
            $tag = "v$Version"
            Write-Host "Creating GitHub release $tag..." -ForegroundColor Yellow
            & gh release create $tag $installer --title "SrtExtractor $Version" --notes "Signed release $Version"
        } else {
            Write-Warning "GitHub CLI not found. Please upload artifacts\\SrtExtractorInstaller.exe to the $tag release manually."
        }
    } else {
        Write-Host "Skipping GitHub release as requested" -ForegroundColor Yellow
    }

    Write-Host "Local signed release completed." -ForegroundColor Green
}
catch {
    Write-Error $_
    exit 1
}


