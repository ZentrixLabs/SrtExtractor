@echo off
echo Building SrtExtractor Installer...
powershell -ExecutionPolicy Bypass -File "build-installer.ps1" %*
pause
