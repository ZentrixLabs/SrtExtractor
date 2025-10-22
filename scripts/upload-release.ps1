#requires -version 5.1
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [string]$InstallerPath = "artifacts\SrtExtractorInstaller.exe",
    [string]$Notes = "Signed release $Version"
)

$ErrorActionPreference = "Stop"

Write-Host "Preparing release v$Version" -ForegroundColor Green

# Ensure we're operating at the repository root so relative paths resolve correctly
try {
    $repoRoot = (& git rev-parse --show-toplevel 2>$null)
    if ($repoRoot) { Set-Location -Path $repoRoot }
} catch { }

# Resolve absolute installer path relative to repo root
$installer = Resolve-Path -Path $InstallerPath -ErrorAction SilentlyContinue
if (-not $installer) {
    throw "Installer not found at $InstallerPath"
}
$installer = $installer.Path

# Validate size (< 2 GB per GitHub limit)
$fileInfo = Get-Item $installer
if ($fileInfo.Length -ge 2GB) {
    throw "Installer exceeds GitHub asset limit (>= 2 GB): $($fileInfo.Length) bytes"
}

# Ensure GitHub CLI is available
$gh = Get-Command gh -ErrorAction SilentlyContinue
if (-not $gh) {
    throw "GitHub CLI (gh) not found. Install via winget install --id GitHub.cli -e"
}

$tag = "v$Version"

# Compute SHA256 and write alongside installer
$shaFile = "$installer.sha256"
$sha = (Get-FileHash -Algorithm SHA256 $installer).Hash
Set-Content -Path $shaFile -NoNewline -Value $sha

# Ensure we're in repo root for git/gh to pick the correct remote (already attempted above)
try {
    $repoRoot = (& git rev-parse --show-toplevel 2>$null)
    if ($repoRoot) { Set-Location -Path $repoRoot }
} catch { }

# Check if tag exists locally or remotely
$tagExists = $false
try {
    git rev-parse "$tag" 2>$null | Out-Null
    $tagExists = $true
} catch { }

if (-not $tagExists) {
    try {
        git ls-remote --tags origin "$tag" | Select-String "$tag" | Out-Null
        $tagExists = $true
    } catch { }
}

if ($tagExists) {
    Write-Host "Tag $tag exists. Replacing it (force)." -ForegroundColor Yellow
    # Move local tag to current HEAD and force-push
    git tag -a $tag -f -m "SrtExtractor $Version"
    git push origin :refs/tags/$tag
    git push origin $tag
} else {
    Write-Host "Creating tag $tag and pushing to origin." -ForegroundColor Yellow
    git tag -a $tag -m "SrtExtractor $Version"
    git push origin $tag
}

# Determine if a release already exists for the tag
$releaseExists = $false
try {
    gh release view $tag 1>$null 2>$null
    $releaseExists = $true
} catch { }

if ($releaseExists) {
    Write-Host "Release $tag exists. Uploading assets (clobber)." -ForegroundColor Yellow
    gh release upload $tag "$installer" "$shaFile" --clobber
} else {
    Write-Host "Creating release $tag and uploading assets." -ForegroundColor Yellow
    gh release create $tag "$installer" "$shaFile" --title "SrtExtractor $tag" --notes "$Notes"
}

Write-Host "Release $tag published successfully." -ForegroundColor Green


