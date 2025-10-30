param(
	[Parameter(Mandatory=$true)][string]$Version,
	[string]$ProjectPath = "${PSScriptRoot}\..\SrtExtractor\SrtExtractor.csproj",
	[string]$IssPath = "${PSScriptRoot}\..\SrtExtractorSetup.iss"
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ProjectPath)) {
	throw "Project file not found at: $ProjectPath"
}

if (-not (Test-Path -LiteralPath $IssPath)) {
	throw "ISS file not found at: $IssPath"
}

# Update csproj file
Write-Host "Updating version in $ProjectPath..." -ForegroundColor Cyan
$projectContent = Get-Content -LiteralPath $ProjectPath -Raw

# Remove git tag auto-generation target block if present
if ($projectContent -match '(?s)<Target\s+Name="SetVersionFromGit".*?</Target>') {
	$projectContent = [System.Text.RegularExpressions.Regex]::Replace(
		$projectContent,
		'(?s)<Target\s+Name="SetVersionFromGit".*?</Target>',
		''
	)
	Write-Host "  Removed SetVersionFromGit target block" -ForegroundColor Yellow
}

# Update or insert explicit Version in PropertyGroup
if ($projectContent -match '<Version>.*?</Version>') {
	# Simple replace of existing version
	$projectContent = $projectContent -replace '<Version>[^<]*</Version>', "<Version>$Version</Version>"
} else {
	# Insert after first PropertyGroup opening tag
	$projectContent = [System.Text.RegularExpressions.Regex]::Replace(
		$projectContent,
		'(?m)^(\s*<PropertyGroup>)',
		("`$1`r`n    <Version>$Version</Version>")
	)
}

Set-Content -LiteralPath $ProjectPath -Value $projectContent -Encoding UTF8 -NoNewline
Write-Host "  Updated Version to $Version in csproj" -ForegroundColor Green

# Update ISS file
Write-Host "Updating version in $IssPath..." -ForegroundColor Cyan
$issContent = Get-Content -LiteralPath $IssPath -Raw

# Update or insert the MyAppVersion define
if ($issContent -match '(?m)^#define\s+MyAppVersion\s+".*?"') {
	$issContent = [System.Text.RegularExpressions.Regex]::Replace(
		$issContent,
		'(?m)^(#define\s+MyAppVersion\s+)"[^"]*"',
		('$1"{0}"' -f $Version)
	)
} else {
	# Insert after first #define MyAppVersion or before [Setup]
	if ($issContent -match '(?s)(.*?)(\r?\n\[Setup\])') {
		$issContent = $issContent -replace '(?s)(.*?)(\r?\n\[Setup\])', "`$1`r`n`r`n; Default app version to allow GUI compile without CLI defines`r`n#ifndef MyAppVersion`r`n  #define MyAppVersion `"$Version`"`r`n#endif`r`n`$2"
	}
}

Set-Content -LiteralPath $IssPath -Value $issContent -Encoding UTF8 -NoNewline
Write-Host "  Updated MyAppVersion to $Version in ISS" -ForegroundColor Green

Write-Host "`nVersion $Version synchronized across project and installer files" -ForegroundColor Green

