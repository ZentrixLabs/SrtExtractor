# SrtExtractor Build and Release Scripts

## Overview

This directory contains scripts to manage versions, sign executables, and release SrtExtractor. The workflow is designed to be simple and match the pattern used in ZLGetCert.

## Scripts

### `update-version.ps1`

Updates the version in both the project file and installer script to keep them in sync.

**Usage:**
```powershell
pwsh ./scripts/update-version.ps1 -Version 2.5.3
```

**What it does:**
- Updates `<Version>` in `SrtExtractor/SrtExtractor.csproj`
- Updates `#define MyAppVersion` in `SrtExtractorSetup.iss`
- Single source of truth for versioning

### `sign-app.ps1`

Signs the SrtExtractor.exe executable using your YubiKey certificate.

**Prerequisites:**
- Set environment variable `CODESIGN_CERT_SHA1` with your certificate thumbprint
- OR pass `-Thumbprint` parameter
- Windows SDK with signtool.exe installed

**Usage:**
```powershell
# Using environment variable
$env:CODESIGN_CERT_SHA1 = "YOUR_THUMBPRINT"
pwsh ./scripts/sign-app.ps1

# With explicit thumbprint
pwsh ./scripts/sign-app.ps1 -Thumbprint "YOUR_THUMBPRINT"

# Custom exe path
pwsh ./scripts/sign-app.ps1 -ExePath "path\to\SrtExtractor.exe"
```

**What it does:**
- Finds signtool.exe automatically
- Signs with SHA256 digest and timestamp
- Verifies the signature
- Uses http://timestamp.digicert.com for timestamping

### `release.ps1`

Orchestrates the complete release workflow: version update, building, signing (optional), and GitHub release.

**Usage:**
```powershell
# Without signing (dev/test release)
pwsh ./scripts/release.ps1 -Version 2.5.3

# With signing (production release)
pwsh ./scripts/release.ps1 -Version 2.5.3 -Sign

# Draft release (don't publish immediately)
pwsh ./scripts/release.ps1 -Version 2.5.3 -Sign -Draft
```

**What it does:**
1. Updates version in csproj and ISS files
2. Prompts you to build in Visual Studio
3. Optionally signs the exe (if `-Sign` is provided)
4. Prompts you to compile installer in Inno Setup GUI
5. Creates checksum file
6. Uploads to GitHub release

### `upload-release.ps1`

Uploads the installer to a GitHub release. Usually called by `release.ps1`, but can be used standalone.

**Usage:**
```powershell
pwsh ./scripts/upload-release.ps1 -Version 2.5.3 -InstallerPath "artifacts\SrtExtractorInstaller.exe"
```

**What it does:**
- Creates git tag (if doesn't exist)
- Creates GitHub release
- Uploads installer and checksum

## Workflow Examples

### Daily Development (No Signing)

1. Build in Visual Studio (Release)
2. Compile `SrtExtractorSetup.iss` in Inno Setup GUI
3. Installer appears in `artifacts` folder (unsigned)

### Production Release (With Signing)

```powershell
# Set your cert thumbprint (one-time, or use environment variable)
$env:CODESIGN_CERT_SHA1 = "YOUR_THUMBPRINT_HERE"

# Run the release orchestrator
pwsh ./scripts/release.ps1 -Version 2.5.3 -Sign
```

The script will:
1. Update versions
2. Wait for you to build in VS
3. Sign the exe with your YubiKey
4. Wait for you to compile in Inno Setup (which signs the installer)
5. Upload to GitHub

### Manual Step-by-Step

If you prefer more control:

```powershell
# 1. Update version
pwsh ./scripts/update-version.ps1 -Version 2.5.3

# 2. Build in Visual Studio (Release)

# 3. Sign the exe
pwsh ./scripts/sign-app.ps1

# 4. Compile in Inno Setup GUI
# (opens SrtExtractorSetup.iss, compiler signs installer automatically)

# 5. Upload to GitHub
pwsh ./scripts/upload-release.ps1 -Version 2.5.3
```

## Inno Setup Signing

The `SrtExtractorSetup.iss` file references `SignTool=SignTool` which points to the SignTool command configured in Inno Setup's IDE (Tools → Configure Sign Tools).

**To configure:**
1. Open Inno Setup IDE
2. Go to Tools → Configure Sign Tools
3. Add a new tool named "SignTool"
4. Set the command to use your cert thumbprint:

```
signtool.exe sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /sha1 YOUR_THUMBPRINT /d "SrtExtractor Installer" $f
```

Replace `YOUR_THUMBPRINT` with your certificate thumbprint.

When you compile the installer in Inno Setup, it will automatically sign both the installer and uninstaller.

## Version Management

Versions are managed in three places:
1. `SrtExtractor/SrtExtractor.csproj` - `<Version>X.Y.Z</Version>`
2. `SrtExtractorSetup.iss` - `#define MyAppVersion "X.Y.Z"`
3. Git tag - `vX.Y.Z` (created during upload)

The `update-version.ps1` script ensures all three stay in sync. Always run it before creating a release.

## Troubleshooting

### "signtool.exe not found"
Install Windows 10/11 SDK from Microsoft. The script will auto-detect the latest version.

### "Certificate not found"
- Ensure your YubiKey is plugged in
- Verify the thumbprint in the environment variable
- Check that the certificate is installed in Windows cert store

### "Inno Setup signing failed"
- Verify SignTool is configured correctly in Inno Setup IDE
- Check that the thumbprint in the SignTool command matches your cert
- Ensure the YubiKey is plugged in when compiling

### "GitHub release failed"
- Ensure `gh` CLI is installed and authenticated (`gh auth login`)
- Check that you have push permissions to the repository

## Notes

- The old `build-installer.ps1` script (240 lines) has been replaced with this simpler, modular approach
- Each script does one thing well
- Manual steps (VS build, Inno compile) are intentional - gives you control
- Matching ZLGetCert's proven workflow

