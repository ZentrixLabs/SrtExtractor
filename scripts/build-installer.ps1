# Build SrtExtractor Installer
# This script builds the application and creates an Inno Setup installer

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0-dev",
    [string]$Thumbprint = "",
    [string]$TimestampUrl = "http://timestamp.digicert.com",
    [string]$KeyStorageProvider = "",
    [string]$KeyContainer = "",
    [string]$CertSubject = "",
    [switch]$SkipSign
)

Write-Host "Building SrtExtractor Installer v$Version" -ForegroundColor Green

# Set error action preference
$ErrorActionPreference = "Stop"

# Ensure we operate from repository root regardless of invocation location
Set-Location -Path (Split-Path $PSScriptRoot -Parent)

try {
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
    & "$PSScriptRoot\download-ffmpeg.ps1"
    
    # Copy FFmpeg tools to output directory
    if (Test-Path "tools\ffmpeg\ffmpeg.exe") {
        Copy-Item "tools\ffmpeg\ffmpeg.exe" "SrtExtractor\bin\$Configuration\net9.0-windows\" -Force
        Copy-Item "tools\ffmpeg\ffprobe.exe" "SrtExtractor\bin\$Configuration\net9.0-windows\" -Force
        Write-Host "Copied FFmpeg tools to output directory" -ForegroundColor Green
    }

    # Sign SrtExtractor.exe (if requested)
    $signtoolPath = Get-LatestSigntoolPath
    if (-not $signtoolPath) { Write-Warning "signtool.exe not found automatically. Install Windows 10/11 SDK." }
    $exePath = "SrtExtractor\bin\$Configuration\net9.0-windows\SrtExtractor.exe"
    if (-not $SkipSign.IsPresent) {
        $hasKsp = (-not [string]::IsNullOrWhiteSpace($KeyStorageProvider)) -and (-not [string]::IsNullOrWhiteSpace($KeyContainer))
        $hasThumb = -not [string]::IsNullOrWhiteSpace($Thumbprint)
        if (-not $hasKsp -and -not $hasThumb) {
            try {
                $now = Get-Date
                $stores = @('Cert:\CurrentUser\My','Cert:\LocalMachine\My')
                $cs = foreach ($s in $stores) {
                    if (Test-Path $s) { Get-ChildItem -Path $s -ErrorAction SilentlyContinue }
                }
                $candidates = $cs | Where-Object {
                    $_.NotAfter -gt $now -and $_.HasPrivateKey -and (
                        ($_.EnhancedKeyUsageList | Where-Object { $_.Oid.Value -eq '1.3.6.1.5.5.7.3.3' }).Count -gt 0 -or
                        ($_.EnhancedKeyUsageList | Where-Object { $_.FriendlyName -eq 'Code Signing' }).Count -gt 0)
                }
                if ($candidates) {
                    $best = $candidates | Sort-Object NotAfter -Descending | Select-Object -First 1
                    $Thumbprint = $best.Thumbprint
                    $hasThumb = $true
                    Write-Host ("Auto-detected code signing cert: {0} (thumbprint {1}), expires {2}" -f $best.Subject, $Thumbprint, $best.NotAfter) -ForegroundColor Yellow
                }
            } catch {}
        }
        if ($hasKsp -or $hasThumb) {
            if (Test-Path $exePath) {
                Write-Host "Signing SrtExtractor.exe..." -ForegroundColor Yellow
                $signArgs = @("sign", "/fd", "SHA256", "/td", "SHA256", "/tr", $TimestampUrl)
                if ($hasKsp) {
                    $signArgs += @("/csp", $KeyStorageProvider, "/kc", $KeyContainer, "/a")
                    if (-not [string]::IsNullOrWhiteSpace($CertSubject)) { $signArgs += @("/n", $CertSubject) }
                }
                if ($hasThumb) { $signArgs += @("/sha1", $Thumbprint) }
                $signArgs += $exePath
                if ($signtoolPath) { & "$signtoolPath" @signArgs } else { throw "signtool.exe not found" }
                if ($LASTEXITCODE -ne 0) {
                    if ($hasKsp) {
                        Write-Error "Code signing SrtExtractor.exe failed using KSP/container. Ensure token is unlocked and provider/container are correct."
                        exit 1
                    } else {
                        Write-Warning "SignTool failed; trying Set-AuthenticodeSignature fallback..."
                        try {
                            $cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -ieq $Thumbprint }
                            if (-not $cert) { throw "Certificate not found by thumbprint $Thumbprint" }
                            Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert -TimestampServer $TimestampUrl -HashAlgorithm SHA256 | Out-Null
                        } catch {
                            Write-Error ("Code signing SrtExtractor.exe failed: {0}" -f $_.Exception.Message)
                            exit 1
                        }
                    }
                }

                Write-Host "Verifying signature for SrtExtractor.exe..." -ForegroundColor Yellow
                & "$signtoolPath" verify /pa /all $exePath
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
            Write-Warning "No signing parameters provided; skipping executable signing. Provide -KeyStorageProvider and -KeyContainer or -Thumbprint."
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
    # Inno Setup AppVersion should be digits and periods only
    $innoVersion = ($Version -replace "^[vV]", "") -replace "[^0-9.]", ""
    if ([string]::IsNullOrWhiteSpace($innoVersion)) { $innoVersion = "1.0.0" }
    $innoArgs = @("SrtExtractorSetup.iss", "/DMyAppVersion=$innoVersion")
    if (-not $SkipSign.IsPresent) {
        $hasKsp = (-not [string]::IsNullOrWhiteSpace($KeyStorageProvider)) -and (-not [string]::IsNullOrWhiteSpace($KeyContainer))
        $hasThumb = -not [string]::IsNullOrWhiteSpace($Thumbprint)
        if ($hasKsp -or $hasThumb) {
            $innoArgs += "/DEnableSigning=1"
            $innoArgs += "/DMyTimestampUrl=$TimestampUrl"
            # Build sign tool definitions
            $quotedTs = '"' + $TimestampUrl + '"'
            if ($hasKsp) {
                $innoArgs += "/DUseKspSigning=1"
                $innoArgs += "/DMyKsp=$KeyStorageProvider"
                $innoArgs += "/DMyKeyContainer=$KeyContainer"
            } else {
                $innoArgs += "/DMyCertThumbprint=$Thumbprint"
            }

            # Define named tools as base executable + operation; params go in .iss SignTool line
            $innoArgs += '/S"ZLSignKsp=' + ('"' + $signtoolPath + '" sign') + '"'
            $innoArgs += '/S"ZLSignThumb=' + ('"' + $signtoolPath + '" sign') + '"'
        }
    }
    # Capture ISCC output for diagnostics
    $isccLog = "artifacts\\iscc.log"
    Write-Host "ISCC args: $($innoArgs -join ' ')" -ForegroundColor DarkGray
    & $innoSetupPath @innoArgs 2>&1 | Tee-Object -FilePath $isccLog
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Installer built successfully!" -ForegroundColor Green
        Write-Host "Installer location: artifacts\SrtExtractorInstaller.exe" -ForegroundColor Cyan
        $installerPath = "artifacts\SrtExtractorInstaller.exe"
        if (Test-Path $installerPath) {
            if (-not $SkipSign.IsPresent -and -not [string]::IsNullOrWhiteSpace($Thumbprint)) {
                Write-Host "Verifying installer signature..." -ForegroundColor Yellow
                & "$signtoolPath" verify /pa /all $installerPath
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
        if (Test-Path $isccLog) {
            Write-Host "--- ISCC Output (last 100 lines) ---" -ForegroundColor Yellow
            Get-Content $isccLog -Tail 100 | ForEach-Object { Write-Host $_ }
            Write-Host "--- End ISCC Output ---" -ForegroundColor Yellow
        } else {
            Write-Warning "ISCC log not found at $isccLog"
        }
        exit 1
    }
    
} catch {
    Write-Error "Build failed: $($_.Exception.Message)"
    exit 1
}
