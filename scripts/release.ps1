param(
	[Parameter(Mandatory=$true)][string]$Version,
	[switch]$Sign,
	[switch]$Draft
)

$ErrorActionPreference = 'Stop'

# Ensure we operate from repository root
$root = Resolve-Path "$PSScriptRoot\.."
Set-Location $root

$artifacts = Join-Path $root 'artifacts'
if (-not (Test-Path $artifacts)) {
	New-Item -ItemType Directory -Path $artifacts | Out-Null
}

Write-Host "=== SrtExtractor Release Workflow ===" -ForegroundColor Green

# Step 1: Update version in project files
Write-Host "`nStep 1: Updating version to $Version..." -ForegroundColor Cyan
& "$PSScriptRoot\update-version.ps1" -Version $Version

if ($Sign) {
	# Step 2: Build and sign the app
	Write-Host "`nStep 2: Please build in Visual Studio (Release), then we'll sign the EXE..." -ForegroundColor Yellow
	Read-Host "Press Enter after building in Visual Studio"
	
	Write-Host "Signing SrtExtractor.exe..." -ForegroundColor Yellow
	& "$PSScriptRoot\sign-app.ps1"
} else {
	# Step 2: Just build
	Write-Host "`nStep 2: Please build in Visual Studio (Release)..." -ForegroundColor Yellow
	Read-Host "Press Enter after building in Visual Studio"
}

# Step 3: Compile installer in Inno Setup GUI
Write-Host "`nStep 3: Please compile the installer in Inno Setup GUI..." -ForegroundColor Yellow
Write-Host "  Open SrtExtractorSetup.iss and compile it" -ForegroundColor DarkGray
Write-Host "  The installer will be signed automatically via SignTool (already configured in IDE)" -ForegroundColor DarkGray
Read-Host "Press Enter after the installer has been generated in 'artifacts'"

# Step 4: Find the installer
$installer = Get-ChildItem -LiteralPath $artifacts -Filter 'SrtExtractorInstaller*.exe' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $installer) { 
	throw "No installer found in $artifacts. Ensure Inno Setup output is configured to 'artifacts'." 
}

Write-Host "Found installer: $($installer.Name)" -ForegroundColor Green

# Step 5: Create checksum
Write-Host "`nStep 4: Creating checksum..." -ForegroundColor Cyan
$shaFile = "$($installer.FullName).sha256"
$sha = (Get-FileHash -Algorithm SHA256 $installer.FullName).Hash
Set-Content -Path $shaFile -NoNewline -Value $sha
Write-Host "Checksum: $shaFile" -ForegroundColor Green

# Step 6: Upload to GitHub
Write-Host "`nStep 5: Uploading to GitHub..." -ForegroundColor Cyan
$tag = "v$Version"
$title = "SrtExtractor $Version"
$notes = "Release $Version"

& "$PSScriptRoot\upload-release.ps1" -Version $Version -InstallerPath $installer.FullName -Notes $notes

Write-Host "`n=== Release complete ===" -ForegroundColor Green
Write-Host "Release: $tag" -ForegroundColor Cyan
Write-Host "Installer: $($installer.FullName)" -ForegroundColor Cyan
if ($Draft) {
	Write-Host "Published as draft release" -ForegroundColor Yellow
}

