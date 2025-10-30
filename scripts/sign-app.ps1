param(
	[string]$ExePath = "",
	[string]$Thumbprint = $env:CODESIGN_CERT_SHA1,
	[string]$TimestampUrl = "http://timestamp.digicert.com",
	[string]$Description = "SrtExtractor",
	[string]$DescriptionUrl = "https://github.com/ZentrixLabs/SrtExtractor"
)

$ErrorActionPreference = 'Stop'

# Resolve relative path
if ([string]::IsNullOrWhiteSpace($ExePath)) {
	$scriptRoot = Split-Path -Parent $PSScriptRoot
	$ExePath = Join-Path $scriptRoot "SrtExtractor\bin\Release\net9.0-windows\SrtExtractor.exe"
}

$exePathResolved = Resolve-Path -Path $ExePath -ErrorAction SilentlyContinue
if (-not $exePathResolved) {
	Write-Host "EXE not found at: $ExePath" -ForegroundColor Red
	Write-Host "Please build the project in Visual Studio (Release configuration) first." -ForegroundColor Yellow
	throw "EXE not found. Build the project first."
}
$exePath = $exePathResolved.Path

# Find signtool.exe
function Get-LatestSigntoolPath {
	$candidates = @()
	$kitsRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
	if (Test-Path $kitsRoot) {
		Get-ChildItem -Path $kitsRoot -Directory -ErrorAction SilentlyContinue |
			Where-Object { $_.Name -match '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$' } |
			Sort-Object { [version]$_.Name } -Descending |
			ForEach-Object {
				$p = Join-Path $_.FullName "x64\signtool.exe"
				if (Test-Path $p) { $candidates += $p }
			}
	}
	$alt = Join-Path ${env:ProgramFiles} "Windows Kits\10\bin\x64\signtool.exe"
	if (Test-Path $alt) { $candidates += $alt }
	try {
		$where = (where.exe signtool 2>$null | Select-Object -First 1)
		if ($where) { $candidates += $where }
	} catch {}
	$candidates | Select-Object -Unique | Select-Object -First 1
}

$SigntoolPath = Get-LatestSigntoolPath
if (-not $SigntoolPath) {
	throw "signtool.exe not found. Please install Windows 10/11 SDK."
}

Write-Host "Using signtool at: $SigntoolPath" -ForegroundColor Cyan

if ([string]::IsNullOrWhiteSpace($Thumbprint)) {
	throw "Provide a certificate thumbprint via -Thumbprint or CODESIGN_CERT_SHA1 environment variable."
}

Write-Host "Signing: $exePath" -ForegroundColor Yellow
Write-Host "Thumbprint: $Thumbprint" -ForegroundColor DarkGray

& $SigntoolPath sign /fd SHA256 /td SHA256 /tr $TimestampUrl /sha1 $Thumbprint /d $Description /du $DescriptionUrl "$exePath"

if ($LASTEXITCODE -ne 0) {
	throw "SignTool failed with exit code $LASTEXITCODE"
}

Write-Host "Verifying signature..." -ForegroundColor Yellow
& $SigntoolPath verify /pa /all $exePath
if ($LASTEXITCODE -ne 0) {
	throw "Signature verification failed"
}

Write-Host "Signed and verified: $exePath" -ForegroundColor Green

